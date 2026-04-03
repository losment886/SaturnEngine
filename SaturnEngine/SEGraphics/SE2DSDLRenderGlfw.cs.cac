using MoonSharp.Interpreter;
using SaturnEngine.Asset;
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.Security;
using SaturnEngine.SEMath;
//using Hexa.NET.SDL3;
using Silk.NET.SDL;
using Silk.NET.Input;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;
//using Hexa.NET.SDL3;
using SixLabors.ImageSharp.Processing;

namespace SaturnEngine.SEGraphics
{
    public unsafe class SE2DSDLRenderGlfw : Render
    {
        public Renderer* RendererPtr;

        public Window* SDLWindowPtr;
        public Vector3D BackgroundColor = new Vector3D(0, 0, 0);
        //public List<nint> uitextures = new List<nint>();
        public Dictionary<ulong, nint> uitextures = new Dictionary<ulong, nint>();
        public Sdl SDL;
        public SE2DSDLRenderGlfw()
        {
            SDL = Sdl.GetApi();
            //SDL.Init();
        }

        public override void Close()
        {

        }

        public override bool CreateDevice(int index = 0)
        {
            //nint hWnd = GetActiveWindow();
            SELogger.Log("Creating SDL2D Renderer");
            var w = GVariables.MainWindows[0].GetWindowHandle();
            SELogger.Log("Got window handle");
            if (GVariables.CurrentWindowHostType == WindowHostType.SDL)
            {
                Hexa.NET.SDL3.SDLWindowPtr sw = new Hexa.NET.SDL3.SDLWindowPtr((Hexa.NET.SDL3.SDLWindow*)w);
                SDLWindowPtr = (Window*)sw.Handle;
            }
            else
            {
                var windowhl = (void*)(w);
                //var pp = *(Silk.NET.GLFW.WindowHandle*)(w);
                //var g = Silk.NET.GLFW.Glfw.GetApi();

                //g.MakeContextCurrent(pp);
                //Silk.NET.GLFW.Glfw.GetApi().GetWin32Window(windowhl, out nint hWnd);
                //Sdl.Windows l;
                //SDL.CreateWindowFrom()
                //Silk.NET.Windowing.IWindow nativeWindow = (Silk.NET.Windowing.IWindow)(object)pp;
                //Silk.NET.Windowing.Window..Native.Cocoa.Value;
                //SDLWindowPtr = new SDLWindowPtr((SDLWindow*)w);
            }
            SELogger.Log("Created SDL Window from handle");
            if ((nint)SDLWindowPtr == IntPtr.Zero)
                return false;
            RendererPtr = SDL.CreateRenderer(SDLWindowPtr, index, (uint)RendererFlags.Accelerated);
            SELogger.Log("Created SDL Render from handle");
            SDL.RenderSetVSync(RendererPtr, 0);
            if ((nint)RendererPtr != IntPtr.Zero)
                return true;

            return false;
        }

        public override void DestroyDevice()
        {
            SDL.DestroyRenderer(RendererPtr);
            SDL.Quit();
        }

        public override string[] GetDeviceNames()
        {
            int co = SDL.GetNumRenderDrivers();
            //.SDL_RendererInfo?[] ri = new SDL.SDL_RendererInfo?[co];
            RendererInfo c;
            List<string> nm = new List<string>();
            for (int i = 0; i < co; i++)
            {

                if (SDL.GetRenderDriverInfo(i, &c) >= 0)
                {
                    //ri[i] = c;
                    nm.Add(Marshal.PtrToStringUTF8(new nint(c.Name)));
                }
                //SDL.SDL_Log("", ri.name);
                //Marshal.
                //string st = new string((char*)ri.name.ToPointer());
                //String.Create()
                //string ss = new string((sbyte*)ri.name.ToPointer(), 0, 16, Encoding.ASCII);
                //Console.WriteLine($"No.{i} {st} | {ss}");
            }
            return nm.ToArray();
        }
        private void UpdateUIControlPositions(double deltaTime)
        {
            //GVariables.ThisGame.UIScene.Controls.Flush(GVariables.MainWindow.Size);

        }
        public override void Initialize()
        {
            //SDL.Init(Sdl.InitVideo);
            //SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG);

            if (GVariables.CurrentWindowHostType != WindowHostType.SDL)
            {
                SDL.EventState((uint)EventType.Mousemotion, Sdl.Disable);
                SDL.EventState((uint)EventType.Mousebuttondown, Sdl.Disable);
                SDL.EventState((uint)EventType.Mousebuttonup, Sdl.Disable);
                SDL.EventState((uint)EventType.Mousewheel, Sdl.Disable);
                SDL.EventState((uint)EventType.Keydown, Sdl.Disable);
                SDL.EventState((uint)EventType.Keyup, Sdl.Disable);
                SDL.EventState((uint)EventType.Keymapchanged, Sdl.Disable);

                SDL.ShowCursor(Sdl.Disable);
            }
        }
        bool framepping = false;
        bool rendering = false;
        public override void PrepareFrame(double deltaTime)
        {
            framepping = true;
            if (!rendering)
            {
                try
                {

                    UpdateUIControlPositions(deltaTime);
                }
                catch (Exception ex)
                {
                    //Console.WriteLine("Error when prepare frame: " + ex);
                }
            }
            framepping = false;
        }

        public override void RenderFrame(double deltaTime)
        {
            /*
            if (rui && GVariables.ThisGame.UIScene != null && !ppr)
            {
                ppr = true;
                SetUIScene(true);
            }*/
            rendering = true;
            if (!framepping)
            {
                SDL.SetRenderDrawColor(RendererPtr, (byte)BackgroundColor.X, (byte)BackgroundColor.Y, (byte)BackgroundColor.Z, 255);
                SDL.RenderClear(RendererPtr);

                if (rui && curui != null)
                {
                    Silk.NET.Maths.Rectangle<int> r = new Silk.NET.Maths.Rectangle<int>();
                    //Sdl.Rect r = new SDL.SDL_Rect();
                    Point c = new Point();
                    Silk.NET.Maths.Rectangle<int> s = new Silk.NET.Maths.Rectangle<int>();
                    foreach (var v in curui.Controls.Controls)
                    {

                        if (v.Spirit != null && v.Spirit.IsLoaded)
                        {
                            //SELogger.Log("E");
                            if (uitextures.TryGetValue(v.Uuid.ID, out var texture))
                            {
                                r.Size.X = (int)v.Size.X;
                                r.Size.Y = (int)v.Size.Y;
                                r.Origin.X = (int)v.Position?[0][0];
                                r.Origin.Y = (int)v.Position?[0][1];
                                s.Size.Y = (int)v.Spirit.BaseImage.Size.Y;
                                s.Size.X = (int)v.Spirit.BaseImage.Size.X;
                                s.Origin.X = 0;
                                s.Origin.Y = 0;
                                c.X = s.Size.X / 2; ;
                                c.Y = s.Size.Y / 2;
                                SDL.RenderCopyEx(RendererPtr, (Texture*)texture, &s, &r, 180 + v.Angle, &c, RendererFlip.Horizontal);
                                //SELogger.Log("R");
                            }
                        }
                    }
                }

                SDL.RenderPresent(RendererPtr);
            }
            rendering = false;
        }

        public override void SetPosition(int x, int y)
        {

        }

        public override void SetScene(int index)
        {
            /*
            var s = GVariables.ThisGame.ThisScenes[index];
            foreach (var v in s.ThisGameObjects)
            {

            }*/
        }

        public override void SetSize(int width, int height)
        {

        }

        public override bool CheckSupport(Feature f)
        {
            switch (f)
            {
                case Feature.HDR:

                    break;
            }
            return false;
        }

        public override void SetFeature(Feature f, bool enable)
        {
            throw new NotImplementedException();
        }
        bool rui = false;
        void LoadUIC()
        {
            if (rui && curui != null)
            {
                SELogger.Log("load uic");
                uitextures.Clear();
                foreach (var v in curui.Controls.Controls)
                {
                    if (v.Spirit != null && v.Spirit.IsLoaded)
                    {
                        var tt = ConvertImageToTexture(v.Spirit.BaseImage.GetImage().CloneAs<Rgba32>(), (nint)RendererPtr);
                        uitextures.Add(v.Uuid.ID, tt);
                        SELogger.Log("ok uic " + v.Uuid.ToString());
                    }
                }
            }
        }
        UIScene? curui;
        bool ppr = false;
        public override void SetUIScene(bool enable)
        {
            //throw new NotImplementedException();
            /*
            rui = enable;
            if (enable && GVariables.ThisGame.UIScene != null)
            {
                curui = GVariables.ThisGame.UIScene;
                LoadUIC();
            }*/
        }

        public IntPtr ConvertImageToTexture(SixLabors.ImageSharp.Image<Rgba32> image, IntPtr renderer)
        {
            // ����ͼ������
            image.Mutate(x => x.Flip(FlipMode.Vertical)); // SDL ������ϵԭ�������Ͻ�

            byte[] pixelData = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(pixelData);

            // ʹ�ù̶��ڴ��
            GCHandle handle = GCHandle.Alloc(pixelData, GCHandleType.Pinned);
            try
            {
                IntPtr pixels = handle.AddrOfPinnedObject();

                // ��������
                IntPtr texture = (nint)SDL.CreateTexture(
                    (Renderer*)renderer,
                    Sdl.PixelformatAbgr8888,
                    (int)TextureAccess.Static,
                    image.Width,
                    image.Height
                );

                if (texture == IntPtr.Zero)
                {
                    throw new Exception($"��������ʧ��: {Helper.PTRGetString((nint)SDL.GetError())}");
                }

                // ������������
                int result = SDL.UpdateTexture(
                    (Texture*)texture,
                    (Silk.NET.Maths.Rectangle<int>*)IntPtr.Zero,
                    pixels.ToPointer(),
                    image.Width * 4
                );

                if (result != 0)
                {
                    SDL.DestroyTexture((Texture*)texture);
                    throw new Exception($"��������ʧ��: {Helper.PTRGetString((nint)SDL.GetError())}");
                }

                // �����������ģʽ����ѡ��
                SDL.SetTextureBlendMode((Texture*)texture, BlendMode.Blend);

                return texture;
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }
    }
}
