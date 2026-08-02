using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SaturnEngine.SEGraphics.Native
{
    /// <summary>
    /// <see cref="NRRendererCreateInfo"/> 的托管构建器。
    /// <para>
    /// 该结构体按值传给 C 侧，但其中所有字符串与字符串数组都是裸指针。
    /// 原生层会保存这些指针直到渲染器销毁，因此不能使用栈上或 GC 堆上的临时缓冲，
    /// 必须由本类分配非托管内存并在 <see cref="Dispose"/> 时统一释放。
    /// </para>
    /// </summary>
    public sealed unsafe class NRRendererCreateInfoBuilder : IDisposable
    {
        // 记录所有分配，Dispose 时逐个释放，避免逐字段追踪的遗漏
        private readonly List<IntPtr> _allocations = new();
        private bool _disposed;

        public string RendererName { get; set; } = "SENativeRenderer";
        public string AppName { get; set; } = "SaturnEngine";
        public ulong AppVersion { get; set; } = 1;

        /// <summary>渲染 API，默认 Vulkan（NR_GRAPHICS_API_VULKAN = 1）。</summary>
        public int Api { get; set; } = 1;

        /// <summary>渲染类型，默认 3D（NR_GRAPHICS_TYPE_3D = 0）。</summary>
        public uint ApiType { get; set; } = 0;

        /// <summary>可接受的最低 API 版本，0 表示由原生层决定。</summary>
        public ulong ApiBaseVersion { get; set; }

        /// <summary>期望的目标 API 版本，0 表示由原生层决定。</summary>
        public ulong ApiTargetVersion { get; set; }

        public List<string> RequiredInstanceExtensions { get; } = new();
        public List<string> OptionalInstanceExtensions { get; } = new();
        public List<string> RequiredDeviceExtensions { get; } = new();
        public List<string> OptionalDeviceExtensions { get; } = new();

        /// <summary>
        /// 生成可直接传给 <see cref="NRNative.CreateRenderer"/> 的结构体。
        /// 返回值中的指针在本对象被释放前始终有效。
        /// </summary>
        public NRRendererCreateInfo Build()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            NRRendererCreateInfo info = default;
            info.RendererName = AllocUtf8(RendererName);
            info.AppName = AllocUtf8(AppName);
            info.AppVersion = AppVersion;
            info.Api = Api;
            info.ApiType = ApiType;
            info.ApiBaseVersion = ApiBaseVersion;
            info.ApiTargetVersion = ApiTargetVersion;

            info.RequiredInstanceExtensions = AllocUtf8Array(RequiredInstanceExtensions);
            info.RequiredInstanceExtensionsCount = RequiredInstanceExtensions.Count;
            info.OptionalInstanceExtensions = AllocUtf8Array(OptionalInstanceExtensions);
            info.OptionalInstanceExtensionsCount = OptionalInstanceExtensions.Count;

            info.RequiredDeviceExtensions = AllocUtf8Array(RequiredDeviceExtensions);
            info.RequiredDeviceExtensionsCount = RequiredDeviceExtensions.Count;
            info.OptionalDeviceExtensions = AllocUtf8Array(OptionalDeviceExtensions);
            info.OptionalDeviceExtensionsCount = OptionalDeviceExtensions.Count;

            // 特性链表由原生层自行填充默认值，托管层暂不下发
            info.RequiredRendererFeatures = null;
            info.OptionalRendererFeatures = null;
            info.RequiredRendererFeaturesCount = 0;
            info.OptionalRendererFeaturesCount = 0;

            return info;
        }

        private byte* AllocUtf8(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            IntPtr p = Marshal.StringToCoTaskMemUTF8(value);
            _allocations.Add(p);
            return (byte*)p;
        }

        private byte** AllocUtf8Array(List<string> values)
        {
            if (values.Count == 0)
                return null;

            IntPtr block = Marshal.AllocCoTaskMem(sizeof(byte*) * values.Count);
            _allocations.Add(block);

            byte** array = (byte**)block;
            for (int i = 0; i < values.Count; i++)
                array[i] = AllocUtf8(values[i]);

            return array;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            foreach (IntPtr p in _allocations)
                Marshal.FreeCoTaskMem(p);

            _allocations.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~NRRendererCreateInfoBuilder() => Dispose();
    }
}
