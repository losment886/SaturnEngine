using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.SEAudio;
using SaturnEngine.Security;
using SaturnEngine.SEGraphics;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaturnEngine.AvaloniaExport
{
    public class SEAvaloniaControl : Control
    {
        public override void Render(DrawingContext context)
        {
            base.Render(context);
        }
    }
    public class SEAvaloniaControlHost : NativeControlHost
    {
        public nint hwnd;
        //public GameHost GameHost { get; set; } = null!;
        public Game ThisGame;

        public SEAvaloniaControlHost()
        {
            ThisGame = new BasicGame();
        }
        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            var handle = base.CreateNativeControlCore(parent);
            hwnd = handle.Handle;
            GameHost gh = new GameHost();
            GVariables.CurrentWindowHostType = WindowHostType.Avalonia;
            gh.LoadGame(ThisGame);
            (gh.SW as SEWindowAvalonia).hwnd = hwnd;
            gh.Start();
            //this.SetCurrentValue(GameHostProperty, gh);
            //GVariables.ThisGameHost = gh;
            return handle;
        }
        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            
        }
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            //context.
        }
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

        public override void Update(float deltaTime)
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
        public override void Exit()
        {

        }

        public override void Initialize()
        {
            Console.WriteLine("NEW GAME!!!");
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

        public override void OnUpdate(float deltaTime)
        {

        }
    }
}
