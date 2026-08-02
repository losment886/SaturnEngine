using System;
using System.Runtime.InteropServices;
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.SEGraphics.Native;
using SaturnEngine.SEMath;

namespace SaturnEngine.SEGraphics
{
    /// <summary>
    /// 基于 SENativeRenderer 原生 ABI 的窗口实现。
    /// <para>
    /// 与 <see cref="SEWindowSDL"/> 不同，本类不直接依赖任何 SDL 绑定：
    /// 窗口创建、事件轮询、输入控制全部通过 <see cref="NRNative"/> 转发到 C 侧，
    /// 事件以定长 <see cref="NREvent"/> 回传，避免变长联合体的跨语言布局问题。
    /// </para>
    /// </summary>
    public unsafe class SEWindowNative : SEWindow
    {
        // 与 SDL3 的 SDL_WindowFlags 对应，原生层直接透传给 SDL_CreateWindow
        private const uint SDL_WINDOW_FULLSCREEN = 0x0000000000000001u;
        private const uint SDL_WINDOW_VULKAN = 0x0000000010000000u;
        private const uint SDL_WINDOW_HIDDEN = 0x0000000000000008u;
        private const uint SDL_WINDOW_RESIZABLE = 0x0000000000000020u;
        private const uint SDL_WINDOW_HIGH_PIXEL_DENSITY = 0x0000000000002000u;

        // 与 SDL3 的 SDL_INIT_* 对应
        private const uint SDL_INIT_AUDIO = 0x00000010u;
        private const uint SDL_INIT_VIDEO = 0x00000020u;
        private const uint SDL_INIT_JOYSTICK = 0x00000200u;
        private const uint SDL_INIT_GAMEPAD = 0x00002000u;
        private const uint SDL_INIT_EVENTS = 0x00004000u;
        private const uint SDL_INIT_SENSOR = 0x00008000u;

        /// <summary>传给 <c>NR_Init</c> 的 SDL 子系统标志。</summary>
        public uint SDLInitFlags { get; set; }

        /// <summary>传给 <c>NR_CreateWindow</c> 的 SDL 窗口标志。</summary>
        public uint SDLWindowFlags { get; set; }

        private bool _initialized;
        private bool _windowCreated;

        // 原生回调只能接受静态函数指针，因此用 GCHandle 把实例传进 user_data 再取回。
        // 该句柄必须一直存活到注销回调之后，否则原生层会持有悬空引用。
        private GCHandle _selfHandle;

        public override void Initialize()
        {
            if (_initialized)
                return;

            uint flags = SDL_INIT_VIDEO | SDL_INIT_EVENTS | SDL_INIT_SENSOR;

            // Windows 下音频与手柄走引擎自身的后端，其他平台交给 SDL
            if (GVariables.OS != OS.Windows)
                flags |= SDL_INIT_AUDIO | SDL_INIT_JOYSTICK | SDL_INIT_GAMEPAD;

            if (SDLInitFlags != 0)
                flags = SDLInitFlags;

            new NRResult(NRNative.Init(flags)).ThrowIfError();
            _initialized = true;
        }

        public override void CreateWindow()
        {
            if (_windowCreated)
                return;

            if (!_initialized)
                Initialize();

            // 先隐藏创建，等属性全部应用完再在 OnStart 中显示，避免闪烁
            uint flags = SDLWindowFlags != 0
                ? SDLWindowFlags
                : SDL_WINDOW_VULKAN | SDL_WINDOW_HIDDEN | SDL_WINDOW_HIGH_PIXEL_DENSITY;

            if (Resizable)
                flags |= SDL_WINDOW_RESIZABLE;
            if (FullScreen)
                flags |= SDL_WINDOW_FULLSCREEN;

            new NRResult(NRNative.CreateWindow(Title, (uint)Size.X, (uint)Size.Y, flags)).ThrowIfError();
            _windowCreated = true;

            NRNative.SetWindowPosition((int)Position.X, (int)Position.Y);
            NRNative.SetCursorVisible(NRNative.ToB32(IsCursorVisible));

            // 注册事件回调：把实例指针塞进 user_data，回调里再还原
            _selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            new NRResult(NRNative.SetEventCallback(
                &OnNativeEvent, (void*)GCHandle.ToIntPtr(_selfHandle))).ThrowIfError();
        }

        public override void OnStart()
        {
            if (!_windowCreated)
                CreateWindow();

            new NRResult(NRNative.ShowWindow()).ThrowIfError();

            var renderer = new SENativeRender(this);
            renderer.CreateInfo.AppName = Title;
            renderer.CreateInfo.ApiBaseVersion = (ulong)BaseApiVersion.Major;
            renderer.CreateInfo.ApiTargetVersion = (ulong)AimApiVersion.Major;

            renderer.Initialize();
            renderer.CreateDevice(UseMonitorIndex >= 0 ? 0 : 0);

            Renderer = renderer;

            SyncUIScene(renderer);
        }

        /// <summary>
        /// 把 <see cref="SEWindow.OwnerGame"/> 的 UI 场景接到本窗口与渲染器上。
        /// UIScene 可能在运行期被替换，因此每帧校验一次引用。
        /// </summary>
        private void SyncUIScene(SENativeRender renderer)
        {
            var scene = OwnerGame?.UIScene;
            if (scene is null)
            {
                if (Controls is not null)
                {
                    Controls = null;
                    renderer.SetUIScene(false);
                }
                return;
            }

            if (!ReferenceEquals(Controls, scene.Controls))
            {
                Controls = scene.Controls;
                renderer.SetUIScene(true);
            }
        }

        // OnUpdate 无参，故自行统计帧间隔供原生层的动画与粒子推进使用
        private long _lastTicks;

        /// <summary>本窗口的 UI 控件树，非空时每帧参与布局与绘制。</summary>
        public Asset.SEControls? Controls { get; set; }

        /// <summary>UI 动画管理器，与 <see cref="Controls"/> 一同每帧推进。</summary>
        public SEUI.Render.SEUIAnimator Animator { get; } = new();

        public override void OnUpdate()
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            double delta = _lastTicks == 0
                ? 0.0
                : (now - _lastTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
            _lastTicks = now;

            // 事件必须在主线程抽干，否则窗口在部分平台上会被系统判定为无响应
            uint count = 0;
            NRNative.PumpEvents(&count);

            NRNative.MainUpdate(delta);

            Delegates.ProcessEvent();
            Delegates.InvokeAll();

            // 严格保证准备先于渲染；窗口已关闭时 Renderer 为 null，直接跳过
            if (Renderer is SENativeRender renderer)
            {
                SyncUIScene(renderer);

                // UI 必须在 PrepareFrame 之前完成布局与顶点提交，
                // 否则本帧拿到的仍是上一帧的网格数据
                if (Controls is not null)
                {
                    Controls.Update(delta);
                    Animator.Update(delta);
                    Controls.Flush(Size);
                    renderer.FlushUI(Controls);
                }

                renderer.PrepareFrame(delta);
                renderer.RenderFrame(delta);
            }
        }

        public override void OnClose()
        {
            // 严格逆序释放：UI 状态 -> 渲染器 -> 事件回调 -> 窗口 -> 原生库
            // 动画与控件引用必须先断开，避免渲染器销毁后仍被下一帧触碰
            Animator.Clear();
            Controls = null;

            if (Renderer is SENativeRender native)
            {
                native.Dispose();
                Renderer = null;
            }

            if (_windowCreated)
            {
                NRNative.SetEventCallback(null, null);
                NRNative.DestroyWindow();
                _windowCreated = false;
            }

            if (_selfHandle.IsAllocated)
                _selfHandle.Free();

            if (_initialized)
            {
                NRNative.Shutdown();
                _initialized = false;
            }
        }

        public override nint GetWindowHandle() => NRNative.GetSDLWindow();

        /// <summary>获取平台原生窗口句柄（HWND / NSWindow / ANativeWindow 等）。</summary>
        public nint GetNativeHandle() => NRNative.GetNativeWindowHandle();

        /// <summary>当前显示器的 DPI 缩放系数。</summary>
        public float DisplayScale => NRNative.GetWindowDisplayScale();

        public override bool SetAttribute(SEWindowAttribute attribute, object value)
        {
            Attributes[attribute] = value;

            // 窗口尚未创建时只记录属性，创建时会统一应用
            if (!_windowCreated)
                return true;

            switch (attribute)
            {
                case SEWindowAttribute.Window_Title:
                    NRNative.SetWindowTitle((string)value);
                    break;
                case SEWindowAttribute.Window_Size:
                    var size = (Vector2D)value;
                    NRNative.SetWindowSize((uint)size.X, (uint)size.Y);
                    break;
                case SEWindowAttribute.Window_Position:
                    var pos = (Vector2D)value;
                    NRNative.SetWindowPosition((int)pos.X, (int)pos.Y);
                    break;
                case SEWindowAttribute.Window_Resizable:
                    NRNative.SetWindowResizable(NRNative.ToB32((bool)value));
                    break;
                case SEWindowAttribute.Window_FullScreen:
                    NRNative.SetWindowFullscreen(NRNative.ToB32((bool)value));
                    break;
                case SEWindowAttribute.Cursor_Show:
                    NRNative.SetCursorVisible(NRNative.ToB32((bool)value));
                    break;
                case SEWindowAttribute.Render_Sync:
                    Renderer?.SetFeature(Render.Feature.Sync, (bool)value);
                    break;
                case SEWindowAttribute.Render_HDR:
                    Renderer?.SetFeature(Render.Feature.HDR, (bool)value);
                    break;
            }

            return true;
        }

        public override object GetAttribute(SEWindowAttribute attribute)
        {
            // 尺寸与位置以原生层的实际值为准，用户可能通过拖拽改变了它们
            if (_windowCreated)
            {
                switch (attribute)
                {
                    case SEWindowAttribute.Window_Size:
                    {
                        uint w = 0, h = 0;
                        if (new NRResult(NRNative.GetWindowSize(&w, &h)).IsSuccess)
                            Attributes[attribute] = new Vector2D(w, h);
                        break;
                    }
                    case SEWindowAttribute.Window_Position:
                    {
                        int x = 0, y = 0;
                        if (new NRResult(NRNative.GetWindowPosition(&x, &y)).IsSuccess)
                            Attributes[attribute] = new Vector2D(x, y);
                        break;
                    }
                }
            }

            return Attributes[attribute];
        }

        /// <summary>
        /// 原生事件回调入口。必须是静态且无托管状态捕获，才能取到函数指针。
        /// </summary>
        // 不指定 CallConvs，与 NRNative.SetEventCallback 声明的
        // delegate* unmanaged<NREvent*, void*, void> 保持一致（平台默认约定）
        [UnmanagedCallersOnly]
        private static void OnNativeEvent(NREvent* evt, void* userData)
        {
            if (evt == null || userData == null)
                return;

            var handle = GCHandle.FromIntPtr((nint)userData);
            if (handle.Target is not SEWindowNative window)
                return;

            try
            {
                window.HandleEvent(in *evt);
            }
            catch (Exception ex)
            {
                // 异常绝不能跨回原生栈帧，否则运行时行为未定义
                SELogger.Error($"处理原生窗口事件时发生异常: {ex}", nameof(SEWindowNative));
            }
        }

        /// <summary>处理单个已翻译的原生事件。子类可重写以扩展事件响应。</summary>
        protected virtual void HandleEvent(in NREvent evt)
        {
            switch (evt.Type)
            {
                case NREventType.Quit:
                    SELogger.Log("收到退出事件，正在关闭窗口", nameof(SEWindowNative));
                    Close();
                    break;

                case NREventType.WindowResize:
                    // 事件携带的是像素尺寸，可直接用于交换链重建
                    Size = new Vector2D(evt.I0, evt.I1);
                    Renderer?.SetSize(evt.I0, evt.I1);
                    break;

                case NREventType.WindowMove:
                    Position = new Vector2D(evt.I0, evt.I1);
                    break;
            }
        }
    }
}
