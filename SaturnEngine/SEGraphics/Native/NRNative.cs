using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SaturnEngine.SEGraphics.Native
{
    /// <summary>
    /// NRResult 的托管封装。
    ///
    /// 原生侧把结果打包进一个 u64：
    ///   bit 56-63 severity, bit 48-55 step, bit 32-47 code, bit 0-31 systemcode。
    /// 这里保持值类型零开销包装，避免每次返回都分配对象。
    /// </summary>
    public readonly struct NRResult : IEquatable<NRResult>
    {
        public readonly ulong Value;

        public NRResult(ulong value) => Value = value;

        public NRSeverity Severity => (NRSeverity)(byte)((Value >> 56) & 0xFF);
        public byte Step => (byte)((Value >> 48) & 0xFF);
        public ushort Code => (ushort)((Value >> 32) & 0xFFFF);
        public uint SystemCode => (uint)(Value & 0xFFFFFFFF);

        /// <summary>严重级别为 Log 时视为成功，与 C 侧 NRR_SUCCESS 一致。</summary>
        public bool IsSuccess => Severity == NRSeverity.Log;

        /// <summary>警告与错误都算失败，与 C 侧 NRR_FAILED 一致。</summary>
        public bool IsFailed => Severity != NRSeverity.Log;

        /// <summary>仅错误级别，警告不算。用于区分"可继续"与"必须中止"。</summary>
        public bool IsError => Severity == NRSeverity.Error;

        /// <summary>失败时抛出 <see cref="NRException"/>，警告级别不抛。</summary>
        public NRResult ThrowIfError()
        {
            if (IsError) throw new NRException(this);
            return this;
        }

        public bool Equals(NRResult other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is NRResult r && Equals(r);
        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString()
            => $"NRResult(severity={Severity}, step={Step}, code={Code}, system={SystemCode})";

        public static implicit operator NRResult(ulong v) => new(v);
        public static implicit operator ulong(NRResult r) => r.Value;
        public static bool operator ==(NRResult a, NRResult b) => a.Value == b.Value;
        public static bool operator !=(NRResult a, NRResult b) => a.Value != b.Value;
    }

    /// <summary>原生渲染器返回错误级 NRResult 时抛出。</summary>
    public sealed class NRException : Exception
    {
        public NRResult Result { get; }

        public NRException(NRResult result)
            : base(BuildMessage(result))
            => Result = result;

        private static string BuildMessage(NRResult result)
        {
            // 原生侧保存了最后一次错误的详细文本，优先使用它。
            string detail;
            try
            {
                detail = NRNative.GetLastErrorString() ?? string.Empty;
            }
            catch
            {
                detail = string.Empty;
            }

            string head = $"原生渲染器调用失败：step={result.Step}, code={result.Code}, system={result.SystemCode}";
            return detail.Length > 0 ? $"{head}\n{detail}" : head;
        }
    }

    /// <summary>
    /// SENativeRenderer 的 P/Invoke 导入。
    ///
    /// 约定：
    ///   1. 全部使用 LibraryImport（源生成封送），配合 PublishAot。
    ///   2. 所有返回 NRResult 的函数在托管侧声明为 ulong，由调用方包装。
    ///   3. 字符串统一按 UTF-8 传递，与 C 侧 const char* 一致。
    ///   4. 该类只做原样映射，不含任何策略逻辑，策略放在上层封装。
    /// </summary>
    public static unsafe partial class NRNative
    {
        public const string Library = "SENativeRenderer";

        #region 诊断

        [LibraryImport(Library, EntryPoint = "NR_ResultToString")]
        private static partial byte* NR_ResultToString(ulong result);

        [LibraryImport(Library, EntryPoint = "NR_GetLastError")]
        private static partial byte* NR_GetLastError();

        /// <summary>把 NRResult 翻译为原生侧提供的可读描述。</summary>
        public static string? ResultToString(NRResult result)
            => Marshal.PtrToStringUTF8((nint)NR_ResultToString(result.Value));

        /// <summary>获取原生侧记录的最后一次错误详情。</summary>
        public static string? GetLastErrorString()
            => Marshal.PtrToStringUTF8((nint)NR_GetLastError());

        #endregion

        #region 生命周期

        [LibraryImport(Library, EntryPoint = "NR_Init")]
        public static partial ulong Init(uint sdlFlags);

        [LibraryImport(Library, EntryPoint = "NR_Shutdown")]
        public static partial ulong Shutdown();

        #endregion

        #region 窗口

        [LibraryImport(Library, EntryPoint = "NR_CreateWindow", StringMarshalling = StringMarshalling.Utf8)]
        public static partial ulong CreateWindow(string title, uint width, uint height, uint flags);

        [LibraryImport(Library, EntryPoint = "NR_DestroyWindow")]
        public static partial ulong DestroyWindow();

        [LibraryImport(Library, EntryPoint = "NR_ShowWindow")]
        public static partial ulong ShowWindow();

        [LibraryImport(Library, EntryPoint = "NR_HideWindow")]
        public static partial ulong HideWindow();

        [LibraryImport(Library, EntryPoint = "NR_SetWindowTitle", StringMarshalling = StringMarshalling.Utf8)]
        public static partial ulong SetWindowTitle(string title);

        [LibraryImport(Library, EntryPoint = "NR_SetWindowSize")]
        public static partial ulong SetWindowSize(uint width, uint height);

        [LibraryImport(Library, EntryPoint = "NR_GetWindowSize")]
        public static partial ulong GetWindowSize(uint* outWidth, uint* outHeight);

        [LibraryImport(Library, EntryPoint = "NR_GetWindowPixelSize")]
        public static partial ulong GetWindowPixelSize(uint* outWidth, uint* outHeight);

        [LibraryImport(Library, EntryPoint = "NR_SetWindowPosition")]
        public static partial ulong SetWindowPosition(int x, int y);

        [LibraryImport(Library, EntryPoint = "NR_GetWindowPosition")]
        public static partial ulong GetWindowPosition(int* outX, int* outY);

        [LibraryImport(Library, EntryPoint = "NR_SetWindowFullscreen")]
        public static partial ulong SetWindowFullscreen(int fullscreen);

        [LibraryImport(Library, EntryPoint = "NR_SetWindowResizable")]
        public static partial ulong SetWindowResizable(int resizable);

        [LibraryImport(Library, EntryPoint = "NR_SetWindowIcon")]
        public static partial ulong SetWindowIcon(void* rgbaPixels, uint width, uint height);

        [LibraryImport(Library, EntryPoint = "NR_GetWindowDisplayScale")]
        public static partial float GetWindowDisplayScale();

        [LibraryImport(Library, EntryPoint = "NR_GetNativeWindowHandle")]
        public static partial nint GetNativeWindowHandle();

        [LibraryImport(Library, EntryPoint = "NR_GetSDLWindow")]
        public static partial nint GetSDLWindow();

        #endregion

        #region 事件

        /// <summary>事件回调，需为 <c>[UnmanagedCallersOnly]</c> 静态方法的函数指针。</summary>
        [LibraryImport(Library, EntryPoint = "NR_SetEventCallback")]
        public static partial ulong SetEventCallback(
            delegate* unmanaged<NREvent*, void*, void> callback, void* userData);

        [LibraryImport(Library, EntryPoint = "NR_SetLogCallback")]
        public static partial ulong SetLogCallback(
            delegate* unmanaged<int, byte*, void*, void> callback, void* userData);

        [LibraryImport(Library, EntryPoint = "NR_PumpEvents")]
        public static partial ulong PumpEvents(uint* outCount);

        [LibraryImport(Library, EntryPoint = "NR_SetRelativeMouseMode")]
        public static partial ulong SetRelativeMouseMode(int enable);

        [LibraryImport(Library, EntryPoint = "NR_SetCursorVisible")]
        public static partial ulong SetCursorVisible(int visible);

        [LibraryImport(Library, EntryPoint = "NR_StartTextInput")]
        public static partial ulong StartTextInput();

        [LibraryImport(Library, EntryPoint = "NR_StopTextInput")]
        public static partial ulong StopTextInput();

        [LibraryImport(Library, EntryPoint = "NR_RumbleGamepad")]
        public static partial ulong RumbleGamepad(uint deviceId, float lowFreq, float highFreq, uint durationMs);

        #endregion

        #region 设备

        [LibraryImport(Library, EntryPoint = "NR_EnumerateDevices")]
        public static partial ulong EnumerateDevices(NRDeviceInfo* outDevices, uint* inoutCount);

        [LibraryImport(Library, EntryPoint = "NR_CreateRenderer")]
        public static partial ulong CreateRenderer(NRRendererCreateInfo info);

        [LibraryImport(Library, EntryPoint = "NR_CreateRendererOnDevice")]
        public static partial ulong CreateRendererOnDevice(NRRendererCreateInfo info, uint deviceIndex);

        [LibraryImport(Library, EntryPoint = "NR_DestroyRenderer")]
        public static partial ulong DestroyRenderer();

        [LibraryImport(Library, EntryPoint = "NR_GetDeviceInfo")]
        public static partial ulong GetDeviceInfo(NRDeviceInfo* outInfo);

        [LibraryImport(Library, EntryPoint = "NR_WaitDeviceIdle")]
        public static partial ulong WaitDeviceIdle();

        #endregion

        #region 交换链

        [LibraryImport(Library, EntryPoint = "NR_ResizeSwapchain")]
        public static partial ulong ResizeSwapchain(uint width, uint height);

        [LibraryImport(Library, EntryPoint = "NR_SetVSync")]
        public static partial ulong SetVSync(int enable);

        [LibraryImport(Library, EntryPoint = "NR_SetHDR")]
        public static partial ulong SetHDR(int enable);

        [LibraryImport(Library, EntryPoint = "NR_SetMSAA")]
        public static partial ulong SetMSAA(uint samples);

        #endregion

        #region 着色器

        [LibraryImport(Library, EntryPoint = "NR_CreateShaderFromSource", StringMarshalling = StringMarshalling.Utf8)]
        public static partial ulong CreateShaderFromSource(
            string source, uint stage, string entryPoint, ulong* outHandle);

        [LibraryImport(Library, EntryPoint = "NR_CreateShaderFromSPIRV")]
        public static partial ulong CreateShaderFromSPIRV(
            uint* spirv, ulong sizeBytes, uint stage, ulong* outHandle);

        [LibraryImport(Library, EntryPoint = "NR_DestroyShader")]
        public static partial ulong DestroyShader(ulong handle);

        #endregion

        #region 资源

        [LibraryImport(Library, EntryPoint = "NR_CreateMesh")]
        public static partial ulong CreateMesh(NRMeshCreateInfo* info, ulong* outHandle);

        [LibraryImport(Library, EntryPoint = "NR_UpdateMesh")]
        public static partial ulong UpdateMesh(
            ulong handle, NRVertex* vertices, uint vertexCount, uint* indices, uint indexCount);

        [LibraryImport(Library, EntryPoint = "NR_DestroyMesh")]
        public static partial ulong DestroyMesh(ulong handle);

        [LibraryImport(Library, EntryPoint = "NR_CreateTexture")]
        public static partial ulong CreateTexture(NRTextureCreateInfo* info, ulong* outHandle);

        [LibraryImport(Library, EntryPoint = "NR_UpdateTexture")]
        public static partial ulong UpdateTexture(
            ulong handle, void* pixels, ulong sizeBytes, uint mipLevel, uint layer);

        [LibraryImport(Library, EntryPoint = "NR_DestroyTexture")]
        public static partial ulong DestroyTexture(ulong handle);

        [LibraryImport(Library, EntryPoint = "NR_CreateMaterial")]
        public static partial ulong CreateMaterial(NRMaterialCreateInfo* info, ulong* outHandle);

        [LibraryImport(Library, EntryPoint = "NR_UpdateMaterial")]
        public static partial ulong UpdateMaterial(ulong handle, NRMaterialCreateInfo* info);

        [LibraryImport(Library, EntryPoint = "NR_DestroyMaterial")]
        public static partial ulong DestroyMaterial(ulong handle);

        #endregion

        #region 场景

        [LibraryImport(Library, EntryPoint = "NR_CreateScene")]
        public static partial ulong CreateScene(ulong* outHandle);

        [LibraryImport(Library, EntryPoint = "NR_DestroyScene")]
        public static partial ulong DestroyScene(ulong handle);

        [LibraryImport(Library, EntryPoint = "NR_SetActiveScene")]
        public static partial ulong SetActiveScene(ulong handle);

        [LibraryImport(Library, EntryPoint = "NR_SetOverlayScene")]
        public static partial ulong SetOverlayScene(ulong handle);

        [LibraryImport(Library, EntryPoint = "NR_SetSceneEnvironment")]
        public static partial ulong SetSceneEnvironment(ulong scene, NRSceneEnvDesc* desc);

        [LibraryImport(Library, EntryPoint = "NR_AddObject")]
        public static partial ulong AddObject(ulong scene, NRObjectDesc* desc, ulong* outHandle);

        [LibraryImport(Library, EntryPoint = "NR_UpdateObject")]
        public static partial ulong UpdateObject(ulong handle, NRObjectDesc* desc);

        [LibraryImport(Library, EntryPoint = "NR_SetObjectTransform")]
        public static partial ulong SetObjectTransform(ulong handle, NRMatrix4* world);

        [LibraryImport(Library, EntryPoint = "NR_SetObjectVisible")]
        public static partial ulong SetObjectVisible(ulong handle, int visible);

        [LibraryImport(Library, EntryPoint = "NR_RemoveObject")]
        public static partial ulong RemoveObject(ulong handle);

        [LibraryImport(Library, EntryPoint = "NR_AddLight")]
        public static partial ulong AddLight(ulong scene, NRLightDesc* desc, ulong* outHandle);

        [LibraryImport(Library, EntryPoint = "NR_UpdateLight")]
        public static partial ulong UpdateLight(ulong handle, NRLightDesc* desc);

        [LibraryImport(Library, EntryPoint = "NR_RemoveLight")]
        public static partial ulong RemoveLight(ulong handle);

        [LibraryImport(Library, EntryPoint = "NR_SetCamera")]
        public static partial ulong SetCamera(ulong scene, NRCameraDesc* desc);

        #endregion

        #region 特效

        [LibraryImport(Library, EntryPoint = "NR_SetPostProcess")]
        public static partial ulong SetPostProcess(NRPostProcessDesc* desc);

        [LibraryImport(Library, EntryPoint = "NR_CreateParticleEmitter")]
        public static partial ulong CreateParticleEmitter(
            ulong scene, NRParticleEmitterDesc* desc, ulong* outHandle);

        [LibraryImport(Library, EntryPoint = "NR_UpdateParticleEmitter")]
        public static partial ulong UpdateParticleEmitter(ulong handle, NRParticleEmitterDesc* desc);

        [LibraryImport(Library, EntryPoint = "NR_SetParticleEmitterEnabled")]
        public static partial ulong SetParticleEmitterEnabled(ulong handle, int enabled);

        [LibraryImport(Library, EntryPoint = "NR_DestroyParticleEmitter")]
        public static partial ulong DestroyParticleEmitter(ulong handle);

        #endregion

        #region 渲染

        [LibraryImport(Library, EntryPoint = "NR_MainUpdate")]
        public static partial ulong MainUpdate(double deltaTime);

        [LibraryImport(Library, EntryPoint = "NR_PrepareRender")]
        public static partial ulong PrepareRender(double deltaTime);

        [LibraryImport(Library, EntryPoint = "NR_Render")]
        public static partial ulong Render(double deltaTime);

        [LibraryImport(Library, EntryPoint = "NR_BeginFrame")]
        public static partial ulong BeginFrame(double deltaTime);

        [LibraryImport(Library, EntryPoint = "NR_EndFrame")]
        public static partial ulong EndFrame();

        [LibraryImport(Library, EntryPoint = "NR_GetFrameStats")]
        public static partial ulong GetFrameStats(NRFrameStats* outStats);

        #endregion

        #region 便捷包装

        /// <summary>把 int 形式的 b32 与托管 bool 互转，避免调用点散落三元表达式。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToB32(bool value) => value ? 1 : 0;

        /// <summary>把 C 侧 b32 转回托管 bool。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FromB32(int value) => value != 0;

        #endregion
    }
}
