//#define SaturnEngine_Release

using System.Diagnostics;
using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.Management.IO;
using SaturnEngine.Performance;
using SaturnEngine.ScriptEngine;
using SaturnEngine.SEAudio;
using SaturnEngine.Security;
using SaturnEngine.SEGraphics;
using SaturnEngine.SEInput;
using SaturnEngine.SEMath;
using SaturnEngine.SEUI;
using SaturnEngine.SEUIControls;
//using SEDumper;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SaturnEngine.Management.SEMemory;

namespace Windows_Test_Project
{
    internal class Program
    {

        static void Main(string[] args)
        {
            int s = 20;
            Console.Write("C");
            Thread.Sleep(s);
            Console.Write("i");
            Thread.Sleep(s);
            Console.Write("a");
            Thread.Sleep(s);
            Console.Write("l");
            Thread.Sleep(s);
            Console.Write("l");
            Thread.Sleep(s);
            Console.Write("o");
            Thread.Sleep(s);
            Console.Write("～");
            Thread.Sleep(s);
            Console.Write("(");
            Thread.Sleep(s);
            Console.Write("∠");
            Thread.Sleep(s);
            Console.Write("·");
            Thread.Sleep(s);
            Console.Write("ω");
            Thread.Sleep(s);
            Console.Write("<");
            Thread.Sleep(s);
            Console.Write(")");
            Thread.Sleep(s);
            Console.Write("⌒");
            Thread.Sleep(s);
            Console.Write("★");
            Thread.Sleep(s);
            Console.Write("\n");
            Thread.Sleep(s);
            Console.Write("少");
            Thread.Sleep(s);
            Console.Write("女");
            Thread.Sleep(s);
            Console.Write("祈");
            Thread.Sleep(s);
            Console.Write("祷");
            Thread.Sleep(s);
            Console.Write("中");
            Thread.Sleep(s);
            Console.Write(".");
            Thread.Sleep(s);
            Console.Write(".");
            Thread.Sleep(s);
            Console.Write(".");
            Thread.Sleep(s);
            Console.Write(".");
            Thread.Sleep(s);
            Console.Write(".");
            Thread.Sleep(s);
            Console.Write(".");
            Thread.Sleep(s);
            Console.Write("\n");
            Console.WriteLine("SaturnEngine Test Program");
            Console.WriteLine(Environment.OSVersion.Version);
            Console.WriteLine(Environment.OSVersion.ServicePack);
            Console.WriteLine(Environment.OSVersion.Platform);
            Console.WriteLine(Environment.OSVersion.VersionString);
            Console.WriteLine(RuntimeEnvironment.GetSystemVersion());
            Console.WriteLine(RuntimeInformation.OSArchitecture);
            Console.WriteLine(RuntimeInformation.OSDescription);
            Console.WriteLine(RuntimeInformation.FrameworkDescription);
            CultureInfo currentCulture = CultureInfo.CurrentCulture;
            CultureInfo currentUICulture = CultureInfo.CurrentUICulture;
            Console.WriteLine($"当前区域设置: {currentCulture.Name}"); // 例如: zh-CN
            Console.WriteLine($"当前UI语言: {currentUICulture.Name}");
            Console.WriteLine($"显示名称: {currentCulture.DisplayName}");
            Console.WriteLine($"英文名称: {currentCulture.EnglishName}");
            Console.WriteLine($"本地名称: {currentCulture.NativeName}");



            
            SELogger.Log("启动错误监视程序");
            //SEDumperFunction.StartDumper();
            //SELogger.Input();
            SELogger.Log("初始化主机");
            GameHost gh = new GameHost();

            //============ 线程调度性能对比测试 ============
            const int BENCH_THREADS = 32;       // 测试线程
            const double BENCH_SECONDS = 10;   // 每轮测试时长（秒）

            long[] counters = new long[BENCH_THREADS];
            bool benchRunning = false;


            ThreadPriority[] tps = new ThreadPriority[BENCH_THREADS];

            SELogger.Log($"线程调度性能测试: {BENCH_THREADS}线程, 每轮{BENCH_SECONDS}秒");
            SELogger.Log("线程优先级分配:");
            for(int i = 0; i < BENCH_THREADS; i++)
            {
                int tl = Random.Shared.Next(0, 6);
                if (tl >= 4)
                {
                    tps[i] = ThreadPriority.Highest;
                }
                else
                {
                    tps[i] = (ThreadPriority)tl;
                }
                SELogger.Log($"  线程{i}: 优先级 {tps[i]}");
            }


            // 统一的负载算法：浮点密集运算，统计迭代次数
            ThreadStart MakeWorker(int idx) => () =>
            {
                double x = idx + 1.0;
                long local = 0;
                while (Volatile.Read(ref benchRunning))
                {
                    for (int n = 0; n < 10000; n++)
                    {
                        x = Math.Sqrt(x * 1.0000001 + 1.0) + Math.Sin(x) * 0.5;
                    }
                    local += 10000;
                    counters[idx] = local;
                }
            };

            // 构建"大核到小核"的逻辑线程顺序：高频 > X3D > 普通 > 能效 > LPE > 未知
            List<int> BuildBigToSmallCoreOrder()
            {
                var order = new List<(int tid, int rank)>();
                int li = 0;
                for (int k = 0; k < Dispatcher.CPUCoreCount && li < Dispatcher.CPUThreadCount; k++)
                {
                    int type = Dispatcher.DefCore[k];
                    bool ht = type != 0 && type % 2 == 0; // 偶数=有超线程
                    int rank = type switch
                    {
                        3 or 4 => 5,   // 高频性能核
                        7 or 8 => 4,   // X3D大三缓核
                        1 or 2 => 3,   // 普通性能核
                        5 or 6 => 2,   // 能效核
                        9 or 10 => 1,  // LPE核
                        _ => 0
                    };
                    order.Add((li++, rank));
                    if (ht && li < Dispatcher.CPUThreadCount)
                        order.Add((li++, rank));
                }
                // 拓扑没覆盖到的逻辑线程补齐
                while (li < Dispatcher.CPUThreadCount)
                    order.Add((li++, 0));
                return order.OrderByDescending(o => o.rank).Select(o => o.tid).ToList();
            }
            List<int> coreOrder = BuildBigToSmallCoreOrder();

            // 执行一轮测试：threadCount个线程，factory(worker, 槽位) 创建线程，返回总分（百万次运算/秒）
            double RunBench(int threadCount, Func<ThreadStart, int, SEThread> factory)
            {
                Array.Clear(counters, 0, counters.Length);
                Volatile.Write(ref benchRunning, true);

                SEThread[] ths = new SEThread[threadCount];
                for (int i = 0; i < threadCount; i++)
                    ths[i] = factory(MakeWorker(i), i);
                for (int i = 0; i < threadCount; i++)
                    ths[i].Start();

                Thread.Sleep((int)(BENCH_SECONDS * 1000));
                Volatile.Write(ref benchRunning, false);

                for (int i = 0; i < threadCount; i++)
                    ths[i]._Main.Join();

                long total = 0;
                for (int i = 0; i < threadCount; i++)
                    total += counters[i];
                Thread.Sleep(1000); // 冷却，让系统负载采样归零
                return total / BENCH_SECONDS / 1_000_000.0;
            }

            // 测试一种模式：先单核分，再16线程综合分
            void BenchMode(string name, Func<ThreadStart, int, SEThread> factory)
            {
                Console.WriteLine($"----- {name} -----");
                double single = RunBench(1, factory);
                Console.WriteLine($"  单核分数    : {single:F2}");
                Thread.Sleep(1000);
                double multi = RunBench(BENCH_THREADS, factory);
                Console.WriteLine($"  {BENCH_THREADS}线程综合分: {multi:F2}  (多核加速比 x{multi / single:F2})");
                Thread.Sleep(1000);
            }

            Console.WriteLine("============ 线程调度性能测试 ============");
            Console.WriteLine($"CPU核心数: {Dispatcher.CPUCoreCount}  逻辑线程数: {Dispatcher.CPUThreadCount}");
            Console.WriteLine($"固定核心分配顺序(大核->小核): {string.Join(",", coreOrder.Take(BENCH_THREADS))}");

            // ① 系统调度：CreateThreadORG，不设亲和性，完全交给操作系统
            BenchMode("系统调度 (CreateThreadORG)",
                (ts, i) => Dispatcher.CreateThreadORG(ts, tps[i]));

            // ② 固定核心：CreateThread(ts, tp, tid)，按大核到小核排开
            BenchMode("固定核心 大核->小核 (CreateThread tid)",
                (ts, i) => Dispatcher.CreateThread(ts, tps[i], coreOrder[i % coreOrder.Count]));

            // ③ 引擎调度：CreateThread(ts, tp, Moveable)，自动选核+动态迁移
            BenchMode("引擎调度 (CreateThread Moveable)",
                (ts, i) => Dispatcher.CreateThread(ts, tps[i], true, false));

            Console.WriteLine("============ 测试结束 ============");

            Console.ReadLine();


            SELogger.Log("启用无线调试");
            SENLTcpHostConfig hc = new SENLTcpHostConfig();
            hc.HostName = "DebuggerHost";
            hc.ListenIp = IPAddress.Any;
            hc.ListenPort = 10550;
            SENLDebuggerFunctionConfig fc = new SENLDebuggerFunctionConfig();
            fc.FunctionList = new List<KeyValuePair<string, Action<string[]>>>();
            fc.FunctionList.Add(new KeyValuePair<string, Action<string[]>>("TestFunc", (args) => { SELogger.Log("TestFunc called with args: " + string.Join(", ", args)); }));
            fc.FunctionList.Add(new KeyValuePair<string, Action<string[]>>("Close", (args) => { SELogger.Log("This Window Will Be Closed"); GVariables.MainWindows[0].Close(); }));
            SENetLogger.Register(SENLHostType.TCP, SENLTcpMethod.Host, hc, fc);
            SELogger.Log("加载游戏");
            gh.LoadGame(new BasicGame());
            //SELogger.Log("设置窗口样式");
            //gh.SetWindowStyle(WindowStyle.GetDefault());
            SELogger.Log("启动游戏主机");
            gh.Start();
        }

        public class BasicScene : Scene
        {
            public BasicScene(string nm = "BasicScene", string desc = "This is a basic scene")
                : base(nm, desc)
            {
            }
            public override void OnLoad()
            {
                Console.WriteLine("Basic Scene Loaded");

            }
            public override void OnActivity()
            {
                Console.WriteLine("Basic Scene Activated");
            }
            public override void OnLeave()
            {
                Console.WriteLine("Leaving Basic Scene");
            }
            public override void OnExit()
            {
                Console.WriteLine("Exiting Basic Scene");
            }

            public override void Update(double deltaTime)
            {
                /*
                //Console.WriteLine($"Updating Basic Scene with delta time: {deltaTime}");
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.Escape))
                {
                    GVariables.MainWindow.Close();
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.Q))
                {
                    GVariables.MainWindow.SetRenderRate(10000);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.W))
                {
                    GVariables.MainWindow.SetRenderRate(1000);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.E))
                {
                    GVariables.MainWindow.SetRenderRate(500);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.R))
                {
                    GVariables.MainWindow.SetRenderRate(200);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.T))
                {
                    GVariables.MainWindow.SetRenderRate(100);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.Y))
                {
                    GVariables.MainWindow.SetRenderRate(60);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.A))
                {
                    //IsCursorLocked = true;
                    GVariables.MainWindow.LockCursor(true);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.S))
                {
                    //IsCursorLocked = false;
                    GVariables.MainWindow.LockCursor(false);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.Z))
                {
                    //IsEndlessCursorMove = true;
                    GVariables.MainWindow.SetCursorEndlessMove(true);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.X))
                {
                    //IsEndlessCursorMove = false;
                    GVariables.MainWindow.SetCursorEndlessMove(false);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.LControlKey))
                {
                    GVariables.MainWindow.ShowCursor(true);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.CapsLock))
                {
                    GVariables.MainWindow.ShowCursor(false);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.F1))
                {
                    GVariables.MainWindow.UseVirtualCursorInput(true);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.F2))
                {
                    GVariables.MainWindow.UseVirtualCursorInput(false);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.F3))
                {
                    GVariables.MainWindow.UseLogicCursorInput(true);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.F4))
                {
                    GVariables.MainWindow.UseLogicCursorInput(false);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.F11))
                {
                    GVariables.MainWindow.SetFullScreen(true);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.F12))
                {
                    GVariables.MainWindow.SetFullScreen(false);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.D0))
                {
                    GVariables.MainWindow.SetMonitorIndex(0);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.D1))
                {
                    GVariables.MainWindow.SetMonitorIndex(1);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.D2))
                {
                    GVariables.MainWindow.SetMonitorIndex(2);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.D3))
                {
                    GVariables.MainWindow.SetMonitorIndex(3);
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.O))
                {
                    BasicInput.JoyStickLeftMotorSpeed = 65535;
                }
                else
                {
                    BasicInput.JoyStickLeftMotorSpeed = 0;
                }
                if (BasicInput.IfKeyDown(SaturnEngine.SEInput.Keys.P))
                {
                    BasicInput.JoyStickRightMotorSpeed = 65535;
                }
                else
                {
                    BasicInput.JoyStickRightMotorSpeed = 0;
                }
                if (BasicInput.IfKeyDown(Keys.V))
                {
                    GVariables.AudioManager.PlayChannel(S1);
                }
                if (BasicInput.IfKeyDown(Keys.B))
                {
                    GVariables.AudioManager.PlayChannel(S2);
                }
                if (BasicInput.IfKeyDown(Keys.N))
                {
                    GVariables.AudioManager.StopChannel(S1);
                }
                if (BasicInput.IfKeyDown(Keys.M))
                {
                    GVariables.AudioManager.PlayChannel(S2);
                }
                if (BasicInput.IfKeyDown(Keys.OemQuestion))
                {
                    //GVariables.AudioManager.PlayChannel(S1);
                    //Eto.Platform.Initialize();
                    Console.WriteLine("输入音频地址:");//
                    string[] fps = [Console.ReadLine(), Console.ReadLine()];
                    GVariables.AudioManager.AddChannel(fps[0], S1);
                    GVariables.AudioManager.AddChannel(fps[1], S2);
                }
                if (BasicInput.IfKeyDown(Keys.Up))
                {
                    if (BasicInput.IfKeyDown(Keys.J))
                    {
                        //Console.WriteLine("R++ : " + (GVariables.MainWindow.Renderer as SE2DSDLRenderSDL).BackgroundColor.X);
                        (GVariables.MainWindow.Renderer as SE2DSDLRenderSDL).BackgroundColor.X += 0.1;
                    }
                    if (BasicInput.IfKeyDown(Keys.K))
                    {
                        (GVariables.MainWindow.Renderer as SE2DSDLRenderSDL).BackgroundColor.Y += 0.1;
                    }
                    if (BasicInput.IfKeyDown(Keys.L))
                    {
                        (GVariables.MainWindow.Renderer as SE2DSDLRenderSDL).BackgroundColor.Z += 0.1;
                    }
                }
                if (BasicInput.IfKeyDown(Keys.Down))
                {
                    if (BasicInput.IfKeyDown(Keys.J))
                    {
                        (GVariables.MainWindow.Renderer as SE2DSDLRenderSDL).BackgroundColor.X -= 0.1;
                    }
                    if (BasicInput.IfKeyDown(Keys.K))
                    {
                        (GVariables.MainWindow.Renderer as SE2DSDLRenderSDL).BackgroundColor.Y -= 0.1;
                    }
                    if (BasicInput.IfKeyDown(Keys.L))
                    {
                        (GVariables.MainWindow.Renderer as SE2DSDLRenderSDL).BackgroundColor.Z -= 0.1;
                    }
                }
                if (BasicInput.IfKeyDown(Keys.Back))
                {
                    if (!cl)
                        cl = true;
                }
                else
                {
                    if (cl)
                    {
                        cl = false;
                        SEMonitor.AllowLog = !SEMonitor.AllowLog;
                        SELogger.Log("是否显示信息:" + SEMonitor.AllowLog);
                    }
                }
                */
            }
            bool cl = false;
            SEAudioManager.SEChannel S1 = new SEAudioManager.SEChannel();
            SEAudioManager.SEChannel S2 = new SEAudioManager.SEChannel();
        }
        public class BasicGame : Game
        {
            public BasicGame(string nm = "BasicGame", string desc = "This is a basic game")
                : base(nm, desc)
            {
                
            }
            public override void Exit()
            {

            }

            public override void Initialize()
            {
                Console.WriteLine("NEW GAME!!!");
                SELogger.Log("加载window设置");
                ThisWindow.SetAttribute(SEWindowAttribute.Window_Title, "Saturn Engine Test Program");
                ThisWindow.SetAttribute(SEWindowAttribute.Window_Resizable, true);
                ThisWindow.SetAttribute(SEWindowAttribute.Render_API, GraphicsAPI.None);
                ThisWindow.SetAttribute(SEWindowAttribute.Window_Size, new Vector2D(800, 600));
                ThisWindow.SetAttribute(SEWindowAttribute.Window_Position, new Vector2D(100, 100));
                ThisWindow.SetAttribute(SEWindowAttribute.Window_FullScreen, false);
                ThisWindow.HostType = WindowHostType.SDL;
                AddScene(new BasicScene());
                LoadScene(STCCode.GetSTC("BasicScene"));
                UIScene ui = new UIScene();
                //ui.LoadUICode("SEButton<RB>[10,10,10,10](Size=\"100,100\",OnMouseEnter=\"PPC\",OnMouseDown=\"PMW\"){ Script @C{ public static void PPC(){ Console.WriteLine(\"OKOKOKOKOK\");} public static void PMW() { SELogger.Log(\"OnMouseDown!!!\");}} }");
                //ui.LoadUICode("SEButton<RB>[10,10,10,10](Size=\"100,100\",OnMouseEnter=\"*PPC\",OnMouseDown=\"*PMW\"){ Script @J{ function PPC(){ Console.WriteLine(\"OKOKOKOKOK\");} function PMW() { SELogger.Log(\"OnMouseDown!!!\");}} }");
                SELogger.Log("编译");
                ui.LoadUIFromFile(GVariables.ProgramUIDir + "/CacUIC.txt");
                SELogger.Log("编译完成");
                //SEButton b = new SEButton();
                //b.Size = new Vector2D(100, 100);
                //b.Bind = SEAnchor.RightBottom;
                
                //ui.Controls.Controls[0].Spirit = new SESpirit();
                //ui.Controls.Controls[0].Spirit.Load(new SEImageFile());
                //ui.Controls.Controls[0].Spirit.BaseImage.CreateWithColor((int)ui.Controls.Controls[0].Size.X, (int)ui.Controls.Controls[0].Size.Y, SEColor.Cyan);
                //ui.Controls.Controls[1].Spirit = new SESpirit();
                //ui.Controls.Controls[1].Spirit.Load(new SEImageFile());
                //ui.Controls.Controls[1].Spirit.BaseImage.CreateWithColor((int)ui.Controls.Controls[0].Size.X, (int)ui.Controls.Controls[0].Size.Y, SEColor.Yellow);
                //ui.Controls.Add(b);
                UIScene = ui;

            }

            public override void OnFocus()
            {

            }

            public override void OnLeave()
            {

            }

            public override void OnUpdate(double deltaTime)
            {

            }
        }
    }

}
