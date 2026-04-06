using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Management.SEMemory;
using SaturnEngine.ScriptEngine;
using SaturnEngine.SEAudio;
using SaturnEngine.SEFont;
using SaturnEngine.SEGraphics;
using SixLabors.Fonts;

namespace SaturnEngine.Global
{
    public static class GVariables
    {
        public static string? StartUpValue;
        public static string? ProgramPath;//include exe
        public static string? ProgramPathEx;//no exe
        public static string? ProgramName = "SaturnEngine";
        public static string? SystemTempDir;
        public static string? SystemDownloadDir;
        public static string? ProgramDataDir;//
        public static string? ProgramJavaScriptDir;
        public static string? ProgramCSharpDir;
        public static string? ProgramUIDir;//仅Debug
        public static CpuVendor? CpuVendor;
        public static string? CpuID;
        public static string? CpuName;
        public static string? CpuVersion;
        public static CPUType? CpuType;
        public static OS? OS;
        public static Version? OSVersion;
        public static Platform? Platform;//平台形式
        public static bool HasBattery;
        public static float Power;
        public static bool Charging;
        public static bool Full;
        public static ulong BatteryPercent;
        public static ulong BatteryFullCapacity;
        public static ulong BatteryCurrentCapacity;
        public static double TotalRunningTimeInSecond;
        public static string CurrentLanguage = "zh-CN";//默认中文简体，
        public static string? LanguageResPath;//engine语言资源路径
        public static string? UserLanguageResPath;//用户语言资源路径

        public static readonly Version EngineVersion = new Version(0, 0, 0, 1);

        public static InternetMethod? InternetMethod;
        public static bool IPv6;
        public static GlobalMemory? ShareMemory;

        public static GameHost? ThisGameHost;

        public static ProgramTypes ProgramType;

        public static string Developer = "Losment";
        public static string Company = "lofusoft";
        public static string Copyright = "lofusoft © 2025";

        public static long DefaultBufferSize = 40960000;

        public static bool EngineRunning = true;

        public static SEScriptEngine? ScriptEngineGlobal;

        public static Dictionary<ulong, LRL>? GlobalResources = new Dictionary<ulong, LRL>();

        public static List<SEWindow> MainWindows = new List<SEWindow>();

        public static SEAudioManager AudioManager;

        public static SEFontRenderer FontRenderer;

        public static Font EngineDefaultFont;

        public static event Action? OnEngineClose;

        public const ulong DefaultEngineResources = 708566205041628488;
        //2025711Pass
        //5889585001912179270
        public const ulong DefaultEngineResourcesPassword = 5889585001912179270;

        public static bool AllowConsoleOutput = true;


        public static WindowHostType CurrentWindowHostType = WindowHostType.Glfw;


        public static List<SEBase> SEObjects = new List<SEBase>();

        public static bool AllowUseWinHook = false;

#if DEBUG
        public static bool DebugMode = true;
#else
        public static bool DebugMode = false;
#endif

        public static bool LogOnline = true;

        public static void OnClose()
        {
            OnEngineClose?.Invoke();
            EngineRunning = false;
            if (ScriptEngineGlobal != null)
            {
                ScriptEngineGlobal.Dispose();
                ScriptEngineGlobal = null;

            }
            if (ShareMemory != null)
            {
                ShareMemory.Dispose();
                ShareMemory = null;
            }
            if (GlobalResources != null)
            {
                foreach (var item in GlobalResources)
                {
                    item.Value.Close();
                }
            }
            GlobalResources?.Clear();
            if (AudioManager != null)
            {
                AudioManager.Dispose();
                AudioManager = null;
            }
        }
    }
    public enum WindowHostType : int
    {
        None = 0,
        SDL = 1,
        Avalonia = 2,
        Glfw = 3,
    }
    public enum RunningMode : int
    {
        /// <summary>
        /// 编辑模式
        /// </summary>
        Edition = 0,
        /// <summary>
        /// 在主编辑模式进行预览
        /// </summary>
        DebugViewing = 1,
        /// <summary>
        /// 调试型发布
        /// </summary>
        DebugRelease = 2,
        /// <summary>
        /// 发行版本
        /// </summary>
        Release = 3,
    }
    public enum ProgramTypes : int
    {
        None = 0,
        Game3D = 1,
        Game2D = 2,
        OnlyApplicatioon = 3
    }
    public enum CPUType : int//排除老产品
    {
        None = 0,

        //Intel
        Intel_Core_i3,
        Intel_Core_i5,
        Intel_Core_i7,
        Intel_Core_i9,

        Intel_CoreUltra_5,
        Intel_CoreUltra_7,
        Intel_CoreUltra_9,

        Intel_Pentium_G,

        Intel_Celeron_G,

        Intel_Xeon,

        //AMD
        AMD_APU,

        AMD_Ryzen3,
        AMD_Ryzen5,
        AMD_Ryzen7,
        AMD_Ryzen9,

        AMD_Ryzen5_X3D,
        AMD_Ryzen7_X3D,
        AMD_Ryzen9_X3D,

        AMD_Ryzen_ThreadRipper,
        AMD_Ryzen_ThreadRipperPro,

        AMD_EPYC,
    }
    public enum GraphicsAPI : int
    {
        None = 0,


        Vulkan = 1,
        DirectX = 2,
        OpenGL = 3,
        WebGPU = 4,

        Metal = 5,

        SDL2D = 6,

        Unknow = 114514
    }
    public enum OS : int
    {
        Windows = 1,
        Linux = 2,
        MacOS = 3,
        HarmonyOS = 4,
        HyperOS = 5,
        Android = 6,
        IOS = 7,
        XBox = 8,
        PlayStation = 9,

        Unknow = 114514
    }
    public enum InternetMethod : int
    {
        GSM = 1,//泛指流量上网（蜂窝）
        WiFi = 2,
        LAN = 3,//有线模式局域网
        Offline = 0//离线
    }

    public enum Platform : int
    {
        Unknow = 0,

        Phone = 1,
        Pad = 2,
        Laptop = 3,
        PC = 4,
        VR = 5,
        WebBrowser = 6//包括微信小程序（
    }
    public enum CpuVendor : int
    {
        Unknow = 0,//可能系统限制无法识别

        //mobile
        Hisilicon = 1,
        Quacomm = 2,
        MediaTek = 3,
        Unisoc = 4,//紫光

        //pc
        LoongSon = 5,
        Intel = 6,
        AMD = 7,


        //both
        Apple = 8,


        OEM = 9,

        //other
        Other = 114514
    }

    public enum GpuVendor : int
    {
        Unknow = 0,

        //mobile
        Mail = 1,
        Adreno = 2,
        Xclipse = 3,//Exynos 基于AMD

        //pc
        LoongSon = 4,//集显，同CPUVENDOR
        Intel = 5,
        Nvidia = 6,
        AMD = 7,
        MooreThreads = 8,

        //both
        Apple = 9,

        OEM = 10,

        //other
        Other = 114514

    }
}
