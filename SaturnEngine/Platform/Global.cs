using Microsoft.DotNet.PlatformAbstractions;
using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.Performance;
using SaturnEngine.ScriptEngine;
using SaturnEngine.Security;
using SaturnEngine.SEFont;
using SaturnEngine.SEGraphics;
using SaturnEngine.SEMath;
using SaturnEngine.SEUI;
using System.Globalization;
using System.Net.Sockets;

namespace SaturnEngine.Platform
{
    public class Global
    {
        public static void SetGPU(string vd)
        {
            var cmp = vd.ToLower().Trim();
            if (cmp.IndexOf("intel") >= 0)
            {
                GVariables.GpuVendor = GpuVendor.Intel;

            }
            else if (cmp.IndexOf("amd") >= 0 || cmp.IndexOf("ati") >= 0)
            {
                GVariables.GpuVendor = GpuVendor.AMD;
            }
            else if (cmp.IndexOf("nvidia") >= 0)
            {
                GVariables.GpuVendor = GpuVendor.Nvidia;
            }
            else if (cmp.IndexOf("moorethreads") >= 0)
            {
                GVariables.GpuVendor = GpuVendor.MooreThreads;
            }
            else if (cmp.IndexOf("mail") >= 0)
            {
                GVariables.GpuVendor = GpuVendor.Mail;
            }
            else if (cmp.IndexOf("adreno") >= 0)
            {
                GVariables.GpuVendor = GpuVendor.Adreno;
            }
            else
            {
                GVariables.GpuVendor = GpuVendor.Unknow;
            }
        }
        public static void LoadGlobalResource(ulong nmstc)
        {
            //SEResource ser = new SEResource($"./Res/{nmstc}.spk");
            LRL l = new LRL();
            l.LoadFromFile($"./Res/{nmstc}.lrl");
            GVariables.GlobalResources.Add(nmstc, l);
        }
        public static void LoadResource(string ppath)
        {
            //SEResource ser = new SEResource(ppath);
            LRL l = new LRL();
            l.LoadFromFile(ppath);
            GVariables.GlobalResources.Add(STCCode.GetSTC(Path.GetFileNameWithoutExtension(ppath)), l);
        }
        public static void EngineInit()
        {
            GVariables.OSVersion = Environment.OSVersion.Version;
            GVariables.ProgramType = ProgramTypes.p3D;
            if (OperatingSystem.IsWindows())
            {
                GVariables.OS = OS.Windows;
                if (GVariables.OSVersion.Major <= 7)
                {
                    throw new PlatformNotSupportedException();
                }
                //DX default
                GVariables.GraphicsAPI = GraphicsAPI.DirectX;
                GVariables.GraphicsBaseLevel = new Version(12, 0);
                GVariables.GraphicsAimLevel = new Version(12, 2);
            }
            else if (OperatingSystem.IsLinux())
            {
                GVariables.OS = OS.Linux;

                GVariables.GraphicsAPI = GraphicsAPI.Vulkan;
                GVariables.GraphicsBaseLevel = new Version(1, 1);
                GVariables.GraphicsAimLevel = new Version(1, 4);
            }
            else if (OperatingSystem.IsAndroid())
            {
                GVariables.OS = OS.Android;

                GVariables.GraphicsAPI = GraphicsAPI.Vulkan;
                GVariables.GraphicsBaseLevel = new Version(1, 1);
                GVariables.GraphicsAimLevel = new Version(1, 3);
            }
            else if (OperatingSystem.IsIOS())
            {
                GVariables.OS = OS.IOS;

                GVariables.GraphicsAPI = GraphicsAPI.Vulkan;
                GVariables.GraphicsBaseLevel = new Version(1, 1);
                GVariables.GraphicsAimLevel = new Version(1, 3);
            }
            else if (OperatingSystem.IsMacOS())
            {
                GVariables.OS = OS.MacOS;

                GVariables.GraphicsAPI = GraphicsAPI.Vulkan;
                GVariables.GraphicsBaseLevel = new Version(1, 1);
                GVariables.GraphicsAimLevel = new Version(1, 3);
            }
            else
            {
#if HARMONYOS
                GVariables.OS = OS.HarmonyOS;
#elif HYPEROS
                GVariables.OS = OS.HyperOS;
#elif XBOX
                GVariables.OS = OS.XBox;
#elif PS
                GVariables.OS = OS.PlayStation; 
#else
                GVariables.OS = OS.Unknow;
#endif
                GVariables.GraphicsAPI = GraphicsAPI.Vulkan;
                GVariables.GraphicsBaseLevel = new Version(1, 1);
                GVariables.GraphicsAimLevel = new Version(1, 3);
            }



            GVariables.ProgramDataDir = (Environment.CurrentDirectory + $"/SEC/{GVariables.ProgramName}/data/");
            GVariables.SystemTempDir = (Environment.CurrentDirectory + $"/SEC/{GVariables.ProgramName}/cache/");
            GVariables.SystemDownloadDir = (Environment.CurrentDirectory + $"/SEC/{GVariables.ProgramName}/download/");
            GVariables.ProgramCSharpDir = (Environment.CurrentDirectory + "/Scripts/CS/");
            GVariables.ProgramJavaScriptDir = (Environment.CurrentDirectory + "/Scripts/JS/");
            GVariables.ProgramUIDir = (Environment.CurrentDirectory + "/Scripts/UI/");
            GVariables.CurrentLanguage = CultureInfo.CurrentUICulture.Name;
            GVariables.LanguageResPath = (Environment.CurrentDirectory + $"/Res/Lang/");
            if (!Directory.Exists(GVariables.ProgramDataDir))
                Directory.CreateDirectory(GVariables.ProgramDataDir);
            if (!Directory.Exists(GVariables.SystemTempDir))
                Directory.CreateDirectory(GVariables.SystemTempDir);
            if (!Directory.Exists(GVariables.SystemDownloadDir))
                Directory.CreateDirectory(GVariables.SystemDownloadDir);
            if (!Directory.Exists(GVariables.ProgramCSharpDir))
                Directory.CreateDirectory(GVariables.ProgramCSharpDir);
            if (!Directory.Exists(GVariables.ProgramJavaScriptDir))
                Directory.CreateDirectory(GVariables.ProgramJavaScriptDir);
            if (!Directory.Exists(GVariables.ProgramUIDir))
                Directory.CreateDirectory(GVariables.ProgramUIDir);
            if (!Directory.Exists(GVariables.LanguageResPath))
                Directory.CreateDirectory(GVariables.LanguageResPath);

            GVariables.ProgramPath = ApplicationEnvironment.ApplicationBasePath;
            GVariables.ShareMemory = new Management.SEMemory.GlobalMemory();

            Dispatcher.Init();
            SEMonitor.Init();
            GVariables.ScriptEngineGlobal = new ScriptEngine.SEScriptEngine();
            GVariables.ScriptEngineGlobal.Init(ScriptEngine.SEScriptEngine.EnableScriptType.All);
            //此处将加载ProgramInfo.js
            //先预编译Scripts\JS下的所有文档
            GVariables.ScriptEngineGlobal.CompileCodeFromFiles(Directory.GetFiles(GVariables.ProgramJavaScriptDir), ScriptEngine.SEScriptEngine.ScriptType.JavaScript);
            GVariables.ScriptEngineGlobal.AddDepending(typeof(GVariables));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Version));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEBase));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEMonitor));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEWindow));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEComplex));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEFFT));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEHuffman));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(CRC32));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Helper));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(STCCode));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Transform));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Vector3D));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Vector2D));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Dispatcher));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Tree));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(BitEditor));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Console));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Path));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Directory));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(File));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Dictionary<dynamic, dynamic>));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Math));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Console));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Thread));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SELogger));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEBorder));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEAnchor));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEColor));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEComponent));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEComponentType));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEControl));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEControls));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEThread));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEScriptEngine));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEUILL));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEUIAssembly));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEUIElement));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEUIParser));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(Socket));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(TcpClient));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(TcpListener));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(HttpClient));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(CultureInfo));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(System.Dynamic.DynamicObject));
            GVariables.ScriptEngineGlobal.AddDepending("DateTime", typeof(DateTime));
            GVariables.ScriptEngineGlobal.AddDepending("TimeSpan", typeof(TimeSpan));
            GVariables.ScriptEngineGlobal.AddDepending("OSt", typeof(OS));
            GVariables.ScriptEngineGlobal.AddDepending("GraphicsAPIt", typeof(GraphicsAPI));
            GVariables.ScriptEngineGlobal.AddDepending("CPUTypet", typeof(CPUType));
            GVariables.ScriptEngineGlobal.AddDepending(typeof(SEFontRenderer));

            
            GVariables.ScriptEngineGlobal.CompileCodeFromFiles(Directory.GetFiles(GVariables.ProgramCSharpDir), ScriptEngine.SEScriptEngine.ScriptType.CSharp);
            //GVariables.ScriptEngineGlobal.CompileCodeFromFile(GVariables.ProgramCSharpDir + "/CSChecker.cs", ScriptEngine.SEScriptEngine.ScriptType.CSharp);
            object? b = GVariables.ScriptEngineGlobal.RunMain("CSChecker.cs");
            if (!(bool)b)
                throw new Exception("执行CSChecker.cs脚本失败，这可能意味着CS引擎没有初始化成功".GetInCurrLang());
            b = GVariables.ScriptEngineGlobal.RunMain("JSChecker.js");
            if (!(bool)b)
                throw new Exception("执行JSChecker.js脚本失败，这可能意味着JS引擎没有初始化成功".GetInCurrLang());
            Global.LoadGlobalResource(GVariables.DefaultEngineResources);

            //GVariables.ScriptEngineGlobal.RunMain("ProgramInfo.js");
            GVariables.ScriptEngineGlobal.RunMain("EngineInit.cs");//自定义初始化
            //Something is error on MacOS
            //GVariables.AudioManager = new SEAudio.SEAudioManager();
            //GVariables.AudioManager.Initialize();
            GVariables.FontRenderer = new SEFontRenderer();
            GVariables.EngineDefaultFont = GVariables.FontRenderer.LoadFontFromFile("./Res/萝莉体 第二版.ttf", 120);
        }
    }
}
