using System;
using SaturnEngine.SEGraphics.Native;
using SaturnEngine.SEUI.Render;

namespace SaturnEngine.SEGraphics
{
    /// <summary>
    /// 基于 <see cref="NRNative"/> 的渲染后端实现。
    /// <para>
    /// 本类只负责把引擎的 <see cref="Render"/> 抽象翻译成原生 ABI 调用，
    /// 所有 Vulkan 逻辑都在 C 侧的 SENativeRenderer 中，托管层不持有任何图形对象。
    /// </para>
    /// </summary>
    public sealed unsafe class SENativeRender : Render, IDisposable
    {
        /// <summary>对应 C 侧 NRR_CODE_SWAPCHAIN_OUT_OF_DATE。</summary>
        private const ushort CodeSwapchainOutOfDate = 25;

        private readonly NRRendererCreateInfoBuilder _createInfo = new();
        private bool _deviceCreated;
        private bool _uiScene;
        private ulong _activeScene;
        private bool _disposed;

        private SEUIRenderer? _uiRenderer;
        private int _surfaceWidth;
        private int _surfaceHeight;

        /// <summary>缓存的设备列表，<see cref="Initialize"/> 时枚举一次。</summary>
        private NRDeviceInfo[] _devices = Array.Empty<NRDeviceInfo>();

        public SENativeRender(SEWindow hoster, string nm = "SENativeRender", string desc = "NULL")
            : base(hoster, nm, desc)
        {
        }

        /// <summary>暴露创建信息以便宿主在 <see cref="CreateDevice"/> 之前调整扩展与版本。</summary>
        public NRRendererCreateInfoBuilder CreateInfo => _createInfo;

        /// <summary>最近一次枚举得到的物理设备信息。</summary>
        public NRDeviceInfo[] Devices => _devices;

        public override void Initialize()
        {
            // 两阶段调用：先传 null 查询数量，再按数量分配后取回数据
            uint count = 0;
            new NRResult(NRNative.EnumerateDevices(null, &count)).ThrowIfError();

            if (count == 0)
            {
                _devices = Array.Empty<NRDeviceInfo>();
                return;
            }

            var devices = new NRDeviceInfo[count];
            fixed (NRDeviceInfo* p = devices)
            {
                new NRResult(NRNative.EnumerateDevices(p, &count)).ThrowIfError();
            }

            // 原生层可能回填比请求更小的实际数量
            if (count < devices.Length)
                Array.Resize(ref devices, (int)count);

            _devices = devices;
        }

        public override string[] GetDeviceNames()
        {
            if (_devices.Length == 0)
                Initialize();

            var names = new string[_devices.Length];
            for (int i = 0; i < _devices.Length; i++)
                names[i] = _devices[i].Name;

            return names;
        }

        public override bool CreateDevice(int index = 0)
        {
            if (_deviceCreated)
                return true;

            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            NRRendererCreateInfo info = _createInfo.Build();
            var result = new NRResult(NRNative.CreateRendererOnDevice(info, (uint)index));
            result.ThrowIfError();

            _deviceCreated = true;
            return true;
        }

        public override void DestroyDevice()
        {
            if (!_deviceCreated)
                return;

            // 销毁前必须等待 GPU 空闲，否则仍在飞行中的帧会引用已释放资源
            NRNative.WaitDeviceIdle();

            // UI 资源建立在渲染器之上，必须先于 DestroyRenderer 释放
            _uiRenderer?.Dispose();
            _uiRenderer = null;

            new NRResult(NRNative.DestroyRenderer()).ThrowIfError();

            _deviceCreated = false;
            _activeScene = 0;
        }

        public override void PrepareFrame(double deltaTime)
        {
            if (!_deviceCreated)
                return;

            new NRResult(NRNative.PrepareRender(deltaTime)).ThrowIfError();
        }

        public override void RenderFrame(double deltaTime)
        {
            if (!_deviceCreated)
                return;

            var result = new NRResult(NRNative.Render(deltaTime));

            // 交换链过期不是错误：窗口尺寸变化时按新尺寸重建后下一帧即可恢复
            if (result.IsFailed && !result.IsError && result.Code == CodeSwapchainOutOfDate)
            {
                ResizeToWindow();
                return;
            }

            result.ThrowIfError();
        }

        public override void SetSize(int width, int height)
        {
            if (!_deviceCreated || width <= 0 || height <= 0)
                return;

            _surfaceWidth = width;
            _surfaceHeight = height;

            new NRResult(NRNative.ResizeSwapchain((uint)width, (uint)height)).ThrowIfError();
        }

        public override void SetPosition(int x, int y)
        {
            // 渲染器本身没有位置概念，窗口位置由 SEWindowNative 负责
        }

        public override void SetScene(int index)
        {
            if (!_deviceCreated)
                return;

            _activeScene = (ulong)index;
            new NRResult(NRNative.SetActiveScene(_activeScene)).ThrowIfError();
        }

        public override void Close()
        {
            DestroyDevice();
        }

        public override bool CheckSupport(Feature f)
        {
            if (_devices.Length == 0)
                return false;

            // 以当前选中的第一个设备的能力位为准
            NRFeature features = _devices[0].Features;
            return f switch
            {
                Feature.Sync => true, // VSync 由交换链呈现模式保证，始终可用
                // Dolby Vision 在原生层同样走 HDR 交换链路径，故共用同一能力位
                Feature.HDR or Feature.DolbyVision => (features & NRFeature.HdrSwapchain) != 0,
                _ => false,
            };
        }

        public override void SetFeature(Feature f, bool enable)
        {
            if (!_deviceCreated)
                return;

            switch (f)
            {
                case Feature.Sync:
                    new NRResult(NRNative.SetVSync(NRNative.ToB32(enable))).ThrowIfError();
                    break;
                case Feature.HDR:
                case Feature.DolbyVision:
                    // 两者在原生层共用同一条 HDR 输出路径
                    new NRResult(NRNative.SetHDR(NRNative.ToB32(enable))).ThrowIfError();
                    break;
            }
        }

        public override void SetUIScene(bool enable)
        {
            _uiScene = enable;

            if (!enable)
                return;

            if (!_deviceCreated)
                return;

            // 惰性创建：UI 场景依赖已就绪的渲染设备
            if (_uiRenderer is null)
            {
                _uiRenderer = new SEUIRenderer();
                _uiRenderer.Initialize();
            }
        }

        /// <summary>UI 渲染器，在 <see cref="SetUIScene"/> 启用后可用。</summary>
        public SEUIRenderer? UIRenderer => _uiRenderer;

        /// <summary>
        /// 把控件树转换为 UI 绘制数据并提交。应在 <see cref="PrepareFrame"/> 之前调用。
        /// </summary>
        public void FlushUI(Asset.SEControls? controls)
        {
            if (!_uiScene || _uiRenderer is null || controls is null)
                return;

            // 首帧尚未触发过 WindowResize，需要主动向原生层查询一次像素尺寸
            if (_surfaceWidth <= 0 || _surfaceHeight <= 0)
            {
                uint w = 0, h = 0;
                NRNative.GetWindowPixelSize(&w, &h);
                _surfaceWidth = (int)w;
                _surfaceHeight = (int)h;
            }

            if (_surfaceWidth <= 0 || _surfaceHeight <= 0)
                return;

            _uiRenderer.Build(controls, _surfaceWidth, _surfaceHeight);
            _uiRenderer.Flush();
        }

        /// <summary>当前是否启用 UI 场景叠加。</summary>
        public bool UISceneEnabled => _uiScene;

        /// <summary>读取上一帧的统计信息。</summary>
        public NRFrameStats GetFrameStats()
        {
            NRFrameStats stats = default;
            if (_deviceCreated)
                new NRResult(NRNative.GetFrameStats(&stats)).ThrowIfError();

            return stats;
        }

        /// <summary>按窗口当前像素尺寸重建交换链。</summary>
        private void ResizeToWindow()
        {
            uint w = 0, h = 0;
            if (new NRResult(NRNative.GetWindowPixelSize(&w, &h)).IsSuccess && w > 0 && h > 0)
                NRNative.ResizeSwapchain(w, h);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            DestroyDevice();
            _createInfo.Dispose();
            _disposed = true;
        }
    }
}
