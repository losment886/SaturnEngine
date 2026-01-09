using SaturnEngine.Asset;
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.Performance;
using SaturnEngine.Platform;
using SaturnEngine.SEInput;
using SaturnEngine.SEMath;
using Silk.NET.GLFW;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SaturnEngine.SEGraphics
{
    public unsafe class SEWindowOpenGL : SEWindow
    {

        public Cursor* ThisCursor { get; private set; }
        public WindowHandle* ThisWindow { get; private set; }



        public Silk.NET.GLFW.Monitor* ThisMonitor { get { return Monitors[UseMonitorIndex]; } }
        public Image ThisICON { get; private set; }
        public int GamePadID { get; private set; } = -1;
        public int JoystickID { get; private set; } = -1;

        public Silk.NET.GLFW.Monitor** Monitors { get; private set; } = null;

        public override void SetMonitorIndex(int index)
        {
            if (index < 0 || index >= TotalMonitorCount)
            {
                index = 0;
            }
            UseMonitorIndex = index;
        }
        public override void SetRenderRate(double rt)
        {
            RenderRate = rt;
            if (rt == 0)
                RenderRate = 60;
            //RenderThread?.SetFPS((int)RenderRate);
        }
        double er = 0;
        double rr = 0;
        double mtr = 0;
        double ar = 0;

        private void SetBackRate()
        {
            er = EventRate;
            rr = RenderRate;
            mtr = MainThreadRate;
            ar = AudioRate;
            EventRate = EventRateBackground;
            AudioRate = AudioRateBackground;
            RenderRate = RenderRateBackground;
            MainThreadRate = MainThreadRateBackground;

        }
        private void SetForceRate()
        {
            EventRate = er;
            RenderRate = rr;
            MainThreadRate = mtr;
            AudioRate = ar;
        }
        public override void SetPosition(Vector2D pos)
        {
            //var f = SetPosition_;
            Delegates.Add(()=>{ SetPosition_(pos); });//Avoiding closure issues
        }
        private void SetPosition_(Vector2D pos)
        {
            Position = pos;
            if (ThisWindow != null)
            {
                Glfw g = Glfw.GetApi();
                g.SetWindowPos(ThisWindow, (int)pos.X, (int)pos.Y);
            }
        }
        public override void SetSize(Vector2D size)
        {
            //var f = SetSize_;
            Delegates.Add(()=>{ SetSize_(size); });//Avoiding closure issues
        }
        private void SetSize_(Vector2D size)
        {
            if (Renderer != null)
            {
                Renderer.SetSize((int)size.X, (int)size.Y);
            }
            Size = size;
            if (ThisWindow != null)
            {
                Glfw g = Glfw.GetApi();
                g.SetWindowSize(ThisWindow, (int)size.X, (int)size.Y);
            }
        }
        public override void UseVirtualCursorInput(bool us)
        {
            //var f = UseVirtualCursorInput_;
            Delegates.Add(()=>{ UseVirtualCursorInput_(us); });
        }
        private void UseVirtualCursorInput_(bool us)
        {
            if (us)
            {
                IsUseVirtualCursor = true;
                ShowCursor_(false);
            }
            else
            {
                IsUseVirtualCursor = false;
                ShowCursor_(true);
            }
        }
        public override void SetResizable(bool resizable)
        {
            //var f = SetResizable_;
            Delegates.Add(()=>{ SetResizable_(resizable); });
        }
        private void SetResizable_(bool resizable)
        {
            Resizable = resizable;
            if (ThisWindow != null)
            {
                Glfw g = Glfw.GetApi();
                g.SetWindowAttrib(ThisWindow, WindowAttributeSetter.Resizable, resizable);
            }
        }
        Vector2D cacpos;
        Vector2D cacsize;
        public override void SetFullScreen(bool fullscreen)
        {
            //var f = SetFullScreen_;
            Delegates.Add(()=>{ SetFullScreen_(fullscreen); });
        }
        private void SetFullScreen_(bool fullscreen)
        {

            if (ThisWindow != null && ThisMonitor != null)
            {
                Glfw g = Glfw.GetApi();
                if (fullscreen)
                {
                    if (FullScreen)
                        return; //如果已经是全屏，则不需要设置
                    //Console.WriteLine("PP");
                    //Console.WriteLine(Position);
                    //Console.WriteLine(Size);
                    VideoMode* mode = g.GetVideoMode(ThisMonitor);
                    g.GetMonitorPos(ThisMonitor, out int x, out int y);

                    g.SetWindowMonitor(ThisWindow, ThisMonitor, x, y, mode->Width, mode->Height, 1000000);
                    cacpos = Position;
                    cacsize = Size;
                    Position = new Vector2D(x, y);
                    Size = new Vector2D(mode->Width, mode->Height);
                    BasicInput.WSize = new POINT((int)Size.X, (int)Size.Y);
                    BasicInput.WPosi = new POINT((int)Position.X, (int)Position.Y);
                    Renderer.SetSize((int)Size.X, (int)Size.Y);
                    Renderer.SetPosition((int)Position.X, (int)Position.Y);
                }
                else
                {
                    if (!FullScreen)
                    {
                        //如果不是全屏，则不需要设置位置和大小
                        return;
                    }
                    //Console.WriteLine("CH");
                    //Console.WriteLine(Position);
                    //Console.WriteLine(Size);
                    Position = cacpos;
                    Size = cacsize;
                    //Console.WriteLine(Position);
                    //Console.WriteLine(Size);
                    g.SetWindowMonitor(ThisWindow, null, (int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y, 1000000);
                    BasicInput.WSize = new POINT((int)Size.X, (int)Size.Y);
                    BasicInput.WPosi = new POINT((int)Position.X, (int)Position.Y);
                    Renderer.SetSize((int)Size.X, (int)Size.Y);
                    Renderer.SetPosition((int)Position.X, (int)Position.Y);
                }
            }
            FullScreen = fullscreen;
        }
        public override void SetTitle(string title)
        {
            //var f = SetTitle_;
            Delegates.Add(()=>{ SetTitle_(title); });
        }
        private void SetTitle_(string title)
        {
            Title = title;
            if (ThisWindow != null)
            {
                Glfw g = Glfw.GetApi();
                g.SetWindowTitle(ThisWindow, title);
            }
        }
        public override void SetICONFromImage(SEImageFile fp)
        {
            //var f = SetICONFromImage_;
            Delegates.Add(()=>{ SetICONFromImage_(fp); });
        }
        private void SetICONFromImage_(SEImageFile fp)
        {
            Image i = new Image();

            SixLabors.ImageSharp.Image<Rgba32> igc = fp.GetImage().CloneAs<Rgba32>();
            i.Width = igc.Width;
            i.Height = igc.Height;
            byte[] bf = new byte[i.Width * i.Height * 4];//rgba32

            igc.CopyPixelDataTo(bf);
            byte* ppc = (byte*)Marshal.AllocHGlobal(bf.Length).ToPointer();
            i.Pixels = ppc;
            Parallel.For(0, bf.Length, (j) =>//copy to i.Pixels
            {
                ppc[j] = bf[j];
            });
            //i.Pixels = (byte*)igc.GetPixelMemoryGroup().GetPixelSpan().GetReference().ToPointer();

            //i.Pixels = (byte*)ppc.ToPointer();
            /*
            for (int j = 0; j < dd.Length; j++)
            {
                bt[j] = dd[j];
            }
            */
            ThisICON = i;
            if (ThisWindow != null)
            {
                Glfw g = Glfw.GetApi();
                g.SetWindowIcon(ThisWindow, 1, &i);
            }
            Marshal.FreeHGlobal((nint)ppc);
        }
        public override void SetCursorFromImage(SEImageFile fp, int x = 0, int y = 0)
        {
            //var f = SetCursorFromImage_;
            Delegates.Add(()=>{ SetCursorFromImage_(fp, x, y); });
        }
        private void SetCursorFromImage_(SEImageFile fp, int x = 0, int y = 0)
        {
            Image i = new Image();

            SixLabors.ImageSharp.Image<Rgba32> igc = fp.GetImage().CloneAs<Rgba32>();
            i.Width = igc.Width;
            i.Height = igc.Height;
            byte[] bf = new byte[i.Width * i.Height * 4];//rgba32

            igc.CopyPixelDataTo(bf);
            byte* ppc = (byte*)Marshal.AllocHGlobal(bf.Length).ToPointer();
            i.Pixels = ppc;
            Parallel.For(0, bf.Length, (j) =>//copy to i.Pixels
            {
                ppc[j] = bf[j];
            });
            ThisCursor = Glfw.GetApi().CreateCursor(&i, x, y);
            if (ThisWindow != null)
            {
                Glfw g = Glfw.GetApi();
                g.SetCursor(ThisWindow, ThisCursor);
            }
            Marshal.FreeHGlobal((nint)ppc);
        }
        public override void LockCursor(bool lockCursor)
        {
            IsCursorLocked = lockCursor;
            //it seem like SILK.NET.GLFW not support locking cursor, we will do this in the EventThread
        }
        public override void SetCursorEndlessMove(bool endlessMove)
        {
            IsEndlessCursorMove = endlessMove;

        }
        public override void ShowCursor(bool showCursor)
        {
            //var f = ShowCursor_;
            Delegates.Add(()=>{ ShowCursor_(showCursor); });
        }
        private void ShowCursor_(bool showCursor)
        {
            IsCursorVisible = showCursor;
            if (ThisWindow != null)
            {
                Glfw g = Glfw.GetApi();
                g.SetInputMode(ThisWindow, CursorStateAttribute.Cursor, showCursor ? CursorModeValue.CursorNormal : CursorModeValue.CursorHidden);
            }
        }
        public override void UseLogicCursorInput(bool useLogicCursor)
        {
            //var f = UseLogicCursorInput_;
            Delegates.Add(()=>{ UseLogicCursorInput_(useLogicCursor); });
        }
        private void UseLogicCursorInput_(bool useLogicCursor)
        {
            IsUseLogicCursor = useLogicCursor;
            if (ThisWindow != null && !BasicInput.UseWinHook)
            {
                Glfw g = Glfw.GetApi();
                g.SetInputMode(ThisWindow, CursorStateAttribute.Cursor, useLogicCursor ? CursorModeValue.CursorDisabled : CursorModeValue.CursorNormal);
            }
        }
        /// <summary>
        /// 创建窗口，你可以在调用此函数之前设置窗口的属性，例如大小、标题等。
        /// </summary>
        public override void CreateWindow()
        {
            Glfw g = Glfw.GetApi();
            if (GVariables.GraphicsAPI == GraphicsAPI.OpenGL || GVariables.GraphicsAPI == GraphicsAPI.OpenGL2D)
            {
                g.WindowHint(WindowHintInt.ContextVersionMajor, 3);
                g.WindowHint(WindowHintInt.ContextVersionMinor, 3);
                g.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
                //g.WindowHint(WindowHintClientApi.ClientApi,ClientApi.OpenGL);
                g.WindowHint(WindowHintBool.OpenGLForwardCompat, true);
                g.WindowHint(WindowHintBool.DoubleBuffer, false);
            }
            else
            {
                g.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);
                g.WindowHint(WindowHintBool.Resizable, Resizable);
                g.WindowHint(WindowHintBool.DoubleBuffer, false);
            }

            //ThisMonitor = g.GetPrimaryMonitor();
            Monitors = g.GetMonitors(out int co);
            TotalMonitorCount = co;
            //ThisMonitor = Monitors[UseMonitorIndex];
            VideoMode* mode = g.GetVideoMode(ThisMonitor);
            ThisWindow = FullScreen ? g.CreateWindow(mode->Width, mode->Height, Title, ThisMonitor, null) : g.CreateWindow((int)Size.X, (int)Size.Y, Title, null, null);
            if (ThisWindow == null || ThisWindow == (WindowHandle*)(0x00))
            {
                g.Terminate();
                throw new Exception("Failed to create window!");
            }

            g.GetWindowSize(ThisWindow, out int w, out int h);
            g.GetWindowPos(ThisWindow, out int px, out int py);
            //g.GetJoystickAxes(Joystick.Joystick1, out float* axes, out int count);
            //if()
            BasicInput.WSize = new POINT(w, h);
            Position = new Vector2D(px, py);
            BasicInput.WPosi = new POINT(px, py);
            g.SetWindowMonitor(ThisWindow, null, (int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y, 1000000);
        }
        private void WorkThreadFunc()
        {
            Glfw g = Glfw.GetApi();
            double cac = 0;
            double cur = 0;
            double dly = 1 / EventRate;
            double old = 0;
            long u = 0;
            double tts = 0;
            double ups = 0;
            while (GVariables.EngineRunning)
            {
                u++;
                cur = g.GetTime();
                cac = cur - old;
                if (GVariables.ThisGame.UIScene != null)
                {
                    GVariables.ThisGame.UIScene.Update((float)cac);
                }
                UpdateLoop.Update((float)cac);
                dly = 1 / EventRate;


                if (cur - tts >= 1)
                {
                    ups = u / (cur - tts);
                    Console.WriteLine("Event Update Rate:" + ups);
                    u = 0;
                    tts = cur;
                }
                UpdateThread.WaitForFPS();
                old = cur;
            }
        }

        public override void Initialize()
        {
            Glfw g = Glfw.GetApi();
            g.Init();
            Position = new Vector2D(0, 0);
            Size = new Vector2D(800, 600);
            //SetRenderRate(300);

        }
        public override void Close()
        {
            Glfw.GetApi().SetWindowShouldClose(ThisWindow, true);
        }
        ulong p;
        private void RenderThreadFunc()
        {
            Glfw g = Glfw.GetApi();
            double cac = 0;
            double cur = 0;
            double dly = 1 / RenderRate;
            double old = 0;
            long u = 0;
            double tts = 0;
            double ups = 0;
            //var func = Renderer.RenderFrame;

            while (GVariables.EngineRunning)
            {
                u++;
                cur = g.GetTime();
                cac = cur - old;
                if (!Delegates.IsInvoking)
                {
                    RenderDel.ProcessEvent();
                    //do
                    RenderDel.InvokeAll();
                    dly = 1 / RenderRate;
                    Renderer.PrepareFrame(cac);
                    Delegates.Add(()=>{Renderer.RenderFrame(cac);});//主要画面更新在主线程运行，准备工作在副线程运行

                    if (cur - tts >= 1)
                    {
                        ups = u / (cur - tts);
                        Console.WriteLine("Render Update Rate:" + ups);
                        u = 0;
                        tts = cur;
                    }
                }
                RenderThread.WaitForFPS();
                old = cur;
            }
        }
        private void AudioThreadFunc()
        {
            Glfw g = Glfw.GetApi();
            double cac = 0;
            double cur = 0;
            double dly = 1 / RenderRate;
            double old = 0;
            long u = 0;
            double tts = 0;
            double ups = 0;
            var func = Renderer.RenderFrame;
            while (GVariables.EngineRunning)
            {
                u++;
                cur = g.GetTime();
                cac = cur - old;
                AudioDel.ProcessEvent();
                //do

                dly = 1 / AudioRate;

                AudioDel.InvokeAll();

                if (cur - tts >= 1)
                {
                    ups = u / (cur - tts);
                    Console.WriteLine("Audio Update Rate:" + ups);
                    u = 0;
                    tts = cur;
                }
                AudioThread.WaitForFPS();
                old = cur;
            }
        }
        public override void RunWindow()
        {

            if (GVariables.OS == OS.Windows)
            {
                switch (GVariables.GraphicsAPI)
                {
                    case Global.GraphicsAPI.DirectX:

                        Renderer = new SEDirectX12Render();
                        Renderer.Initialize();
                        break;
                    case Global.GraphicsAPI.Vulkan:

                        //通用VK
                        Renderer = new SEVulkanRender();
                        Renderer.Initialize();
                        break;
                    case Global.GraphicsAPI.OpenGL:
                        Renderer = new SEOpenGLRender();
                        Renderer.Initialize();
                        break;
                    case GraphicsAPI.OpenGL2D:
                        Renderer = new SE2DOpenGLRender();
                        Renderer.Initialize();
                        break;
                    case GraphicsAPI.SDL2D:
                        Renderer = new SE2DSDLRender();
                        Renderer.Initialize();
                        break;
                    default:
                        Renderer = new SEVulkanRender();
                        Renderer.Initialize();
                        break;

                }
            }
            else
            {
                switch (GVariables.GraphicsAPI)
                {
                    case Global.GraphicsAPI.Vulkan:

                        //通用VK
                        Renderer = new SEVulkanRender();
                        Renderer.Initialize();
                        break;
                    case Global.GraphicsAPI.OpenGL:
                        Renderer = new SEOpenGLRender();
                        Renderer.Initialize();
                        break;
                    case GraphicsAPI.OpenGL2D:
                        Renderer = new SE2DOpenGLRender();
                        Renderer.Initialize();
                        break;
                    case GraphicsAPI.SDL2D:
                        Renderer = new SE2DSDLRender();
                        Renderer.Initialize();
                        break;
                    default:
                        Renderer = new SEVulkanRender();
                        Renderer.Initialize();
                        break;
                }
            }
            //SELogger.Log(GVariables.SystemTempDir);
            //SELogger.Log("检查STEAM接入情况");

            //Not available for now due to MacOS
            /*
            if (Steamworks.SteamAPI.IsSteamRunning())
            {
                if (!Steamworks.SteamAPI.Init())
                {
                    SELogger.Log("STEAM初始化失败");
                }
                else
                {
                    SELogger.Log("STEAM初始化成功");
                    var hst = Steamworks.SteamAPI.GetHSteamUser();
                    SELogger.Log("用户名:" + Steamworks.SteamFriends.GetPersonaName());
                    Steamworks.SteamAPI.Shutdown();
                }
            }
            else
            {
                SELogger.Log("STEAM未运行");
            }
            */

            var dl = Renderer.GetDeviceNames();
            int id = 0;
            int co = 0;
            if (dl.Length > 0)
            {
                Console.WriteLine("渲染器设备列表:");
                foreach (var item in dl)
                {
                    Console.WriteLine(item);
                    if (GVariables.GraphicsAPI == GraphicsAPI.SDL2D && item.ToLower().IndexOf("d12") > 1)
                    {
                        id = co;
                    }
                    co++;
                }
            }
            else
            {
                Console.WriteLine("渲染器没有可用的设备");
            }

            if (!Renderer.CreateDevice(id))
            {
                throw new Exception("Failed to create rendering device!");
            }

            BasicInput.WSize = new POINT((int)Size.X, (int)Size.Y);
            BasicInput.WPosi = new POINT((int)Position.X, (int)Position.Y);
            BasicInput.ThisWindow = ThisWindow;
            //EventThread = Dispatcher.CreateThreadORG(EVLP, ThreadPriority.Highest);
            Glfw g = Glfw.GetApi();
            bool ush = false;
            if (GVariables.OS == OS.Windows && GVariables.AllowUseWinHook)
            {
                //Silk.NET.XInput.XInput x = Silk.NET.XInput.XInput.GetApi();

                //use hook for windows
                //EventThread = Dispatcher.CreateThreadORG(EVLP, ThreadPriority.Highest);
                //!WinAPI.InputWin.Mouse.MouseProcess = WinAPI.InputWin.
                WinAPI.InputWin.Mouse.MouseProcess += BasicInput.WinMouseProcess;
                WinAPI.InputWin.Keyboard.KeyboardProcess += BasicInput.WinKeyboardProcess;
                ush = true;
                if (!WinAPI.InputWin.Mouse.InstallHook())
                {
                    ush = false;
                    BasicInput.UseWinHook = false;
                }
                if (!WinAPI.InputWin.Keyboard.InstallHook())
                {
                    ush = false;
                    BasicInput.UseWinHook = false;
                    WinAPI.InputWin.Mouse.UninstallHook();
                }
                WinAPI.InputWin.JoyStick.SetWhichJoyStick(0);

                //Silk.NET.XInput.XInput.GetApi().SetState();
                // State s;

                //Console.WriteLine("NM" + g.GetJoystickName(0));
            }
            else
            {
                BasicInput.UseWinHook = false;
                BasicInput.UseGLFWInput();
            }
            if (ush)
            {
                g.SetInputMode(ThisWindow, CursorStateAttribute.Cursor, false);
                Console.WriteLine("使用WINHOOK作为输入");
            }


            Renderer.SetScene(GVariables.ThisGame.CurrentSceneIndex);

            double mx = 0, my = 0;
            double currenttime = 0;
            double lasttime = 0;
            double ppp = 0;
            double fps = 0;
            int w, h, px, py;

            //GVariables.AudioManager.CreateDevice();

            MainThread = Dispatcher.CreateThreadFromExistedThread();
            UpdateThread = Dispatcher.CreateThreadORG(WorkThreadFunc, ThreadPriority.AboveNormal);
            RenderThread = Dispatcher.CreateThreadORG(RenderThreadFunc, ThreadPriority.Normal);
            AudioThread = Dispatcher.CreateThreadORG(AudioThreadFunc, ThreadPriority.BelowNormal);

            er = EventRate;
            rr = RenderRate;
            mtr = MainThreadRate;
            ar = AudioRate;

            SetForceRate();

            bool hassetbool = false;

            UpdateThread.Start();
            RenderThread.Start();
            AudioThread.Start();

            if (GVariables.ThisGame.UIScene != null)
            {
                Renderer.SetUIScene(true);
            }
            bool setrat = false;

            double omx = 0, omy = 0;
            double dfr = MainThreadRate;
            //BasicInput.KeysToPrevent.Add(SEInput.Keys.LWin);
            while (GVariables.EngineRunning)
            {

                currenttime = g.GetTime();
                BasicInput.Focued = g.GetWindowAttrib(ThisWindow, WindowAttributeGetter.Focused);
                if (!ush)
                    BasicInput.GLFWProcess();

                BasicInput.BeforeUpdate();
                p++;
                if (!BasicInput.Focued)
                {
                    if (!hassetbool)
                    {
                        hassetbool = true;
                        SetBackRate();
                        if (ush)
                        {
                            if(GVariables.OS == OS.Windows && GVariables.AllowUseWinHook)
                            {
                                WinAPI.InputWin.Mouse.UninstallHook();
                                WinAPI.InputWin.Keyboard.UninstallHook();
                            }
                            
                        }
                    }
                }
                else
                {

                    

                    if (hassetbool)
                    {
                        SetForceRate();
                        hassetbool = false;
                        if(GVariables.OS == OS.Windows && GVariables.AllowUseWinHook)
                        {
                            if (!WinAPI.InputWin.Mouse.InstallHook())
                            {
                                ush = false;
                                BasicInput.UseWinHook = false;
                            }
                            if (!WinAPI.InputWin.Keyboard.InstallHook())
                            {
                                ush = false;
                                BasicInput.UseWinHook = false;
                                WinAPI.InputWin.Mouse.UninstallHook();
                            }
                        }
                    }
                }

                if (BasicInput.MouseMoved && BasicInput.Focued)
                {
                    if (setrat == false)
                    {
                        //SetForceRate();
                        setrat = true;
                        MainThreadRate = 8000;

                    }
                }
                else
                {
                    if (setrat)
                    {
                        //SetBackRate();
                        setrat = false;
                        MainThreadRate = dfr;
                    }
                }
                //Console.WriteLine(nt);
                if (g.WindowShouldClose(ThisWindow))
                {
                    if(GVariables.OS == OS.Windows && BasicInput.UseWinHook)
                    {
                        WinAPI.InputWin.Mouse.UninstallHook();
                        WinAPI.InputWin.Keyboard.UninstallHook();
                    }
                    CloseWindow();

                }
                else
                {
                    g.PollEvents();
                    Delegates.ProcessEvent();
                    Delegates.InvokeAll();
                    /*
                    string vc = "";
                    float* f = g.GetJoystickAxes(0, out int co);
                    for (int i = 0; i < co; i++)
                    {
                        vc += $"NO:{i}-AV={f[i]} | ";
                    }
                    byte* bc = g.GetJoystickButtons(0, out co);
                    for(int i = 0;i < co; i++)
                    {
                        vc += $"#NO:{i}-BT={bc[i]} | ";
                    }
                    Console.WriteLine(vc);
                    */
                    //Console.WriteLine($"MVD:{BasicInput.MouseMoved}||LGPOSI=>{BasicInput.CursorLogicPosition.x}={BasicInput.CursorLogicPosition.y}||==>NPOSI{BasicInput.CursorPosition.x}={BasicInput.CursorPosition.y}||==>LSTPOSI{BasicInput.LastFrameCursorPosition.x}={BasicInput.LastFrameCursorPosition.y}||WPOSI{Position}||WSIZE{Size}");
                    //UP RG DW LF
                    //10 11 12 13
                    if (!FullScreen)
                    {
                        g.GetWindowSize(ThisWindow, out w, out h);

                        g.GetWindowPos(ThisWindow, out px, out py);
                        //g.GetJoystickAxes(Joystick.Joystick1, out float* axes, out int count);
                        //if()
                        if (w != Size.X || h != Size.Y)
                        {
                            //窗口大小发生变化
                            //Console.WriteLine("SIZCHANGE!!");
                            Size = new Vector2D(w, h);
                            BasicInput.WSize = new POINT(w, h);
                            //g.SetWindowSize(ThisWindow, w, h);
                            Renderer.SetSize(w, h);
                            SetForceRate();
                        }
                        if (px != Position.X || py != Position.Y)
                        {
                            //窗口位置发生变化
                            //Console.WriteLine("POSCHANGE!!");
                            Position = new Vector2D(px, py);
                            BasicInput.WPosi = new POINT(px, py);
                            //g.SetWindowSize(ThisWindow, w, h);
                            Renderer.SetPosition(px, py);
                            SetForceRate();
                        }
                    }
                    mx = BasicInput.CursorLogicPosition.x;
                    my = BasicInput.CursorLogicPosition.y;
                    if (IsUseLogicCursor)
                    {
                        if (omx != mx || my != omy)
                        {
                            Console.WriteLine($"LGPOSI=>{BasicInput.CursorLogicPosition.x}={BasicInput.CursorLogicPosition.y}||==>NPOSI{BasicInput.CursorPosition.x}={BasicInput.CursorPosition.y}||==>LSTPOSI{BasicInput.LastFrameCursorPosition.x}={BasicInput.LastFrameCursorPosition.y}||WPOSI{Position}||WSIZE{Size}");
                            omx = mx;
                            omy = my;
                        }
                    }
                    
                    //cac = g.GetTime() - currenttime;



                    //SELogger.Log("SEObjects Count : " + GVariables.SEObjects.Count);


                    BasicInput.AfterUpdate();


                    if ((currenttime - ppp) > 0.2)
                    {
                        fps = p / (currenttime - ppp);
                        g.SetWindowTitle(ThisWindow, $"FPS:{fps} ||SEObjects Count:{GVariables.SEObjects.Count}|| Cursor Position:{BasicInput.CursorLogicPosition}");
                        p = 0;
                        ppp = g.GetTime();
                    }



                    MainThread.WaitForFPS();
                }
                lasttime = currenttime;
            }
        }
        public void CloseWindow()
        {
            GVariables.EngineRunning = false;
            GVariables.ThisGame?.Exit();
            GVariables.OnClose();
        }

        public override nint GetWindowHandle()
        {
            return (nint)((void*)ThisWindow);
        }

        public SEWindowOpenGL()
        {
            HostType = WindowHostType.Glfw;
        }
    }
}
