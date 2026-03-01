using MoonSharp.Interpreter;
using SaturnEngine.Asset;
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.Security;
using SaturnEngine.SEMath;
using Hexa.NET.SDL3;
using Silk.NET.SDL;
using Silk.NET.Input;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;
//using Hexa.NET.SDL3;
using SixLabors.ImageSharp.Processing;

namespace SaturnEngine.SEGraphics
{
    public unsafe class SE2DSDLRenderSDL : Render
    {
        public SDLRendererPtr RendererPtr;

        public SDLWindowPtr SDLWindowPtr;
        public Vector3D BackgroundColor = new Vector3D(32, 32, 32);
        //public List<nint> uitextures = new List<nint>();
        public Dictionary<ulong, SDLTexturePtr> uitextures = new Dictionary<ulong, SDLTexturePtr>();
        //public Sdl SDL;
        public SE2DSDLRenderSDL()
        {
            //SDL = Sdl.GetApi();
            //SDL.Init();
        }

        public override void Close()
        {
            
        }

        public override bool CreateDevice(int index = 0)
        {
            //nint hWnd = GetActiveWindow();
            SELogger.Log("Creating SDL2D Renderer");
            var w = GVariables.MainWindow.GetWindowHandle();
            SELogger.Log("Got window handle");
            if (GVariables.CurrentWindowHostType == WindowHostType.SDL)
            {
                Hexa.NET.SDL3.SDLWindowPtr sw = new Hexa.NET.SDL3.SDLWindowPtr((Hexa.NET.SDL3.SDLWindow*)w);
                SDLWindowPtr = sw.Handle;
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
            if ((nint)SDLWindowPtr.Handle == IntPtr.Zero)
                return false;
            //RendererPtr = SDL.CreateRenderer(SDLWindowPtr, index, (uint)RendererFlags.Accelerated);
            //SDL.CreateRen
            //SDL.GetGPURendererDevice
            //SDLGPUDevicePtr dev = new SDLGPUDevicePtr();
            //SDLGPUDevice dv = new SDLGPUDevice();
            RendererPtr = SDL.CreateRenderer(SDLWindowPtr, SDL.GetGPUDriver(index));
            // SDL.CreateGPURenderer(dev, SDLWindowPtr);
            SELogger.Log("Created SDL Render from handle");
            //SDL.RenderSetVSync(RendererPtr, 0);
            if ((nint)RendererPtr.Handle == IntPtr.Zero)
                return true;

            return true;
        }

        public override void DestroyDevice()
        {
            SDL.DestroyRenderer(RendererPtr);
            //SDL.Quit();
        }

        public override string[] GetDeviceNames()
        {
            int co = SDL.GetNumRenderDrivers();
            //.SDL_RendererInfo?[] ri = new SDL.SDL_RendererInfo?[co];
            //RendererInfo c;
            List<string> nm = new List<string>();
            
            for (int i = 0; i < co; i++)
            {

                nm.Add(SDL.GetRenderDriverS(i));
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
            GVariables.ThisGame.UIScene.Controls.Flush(GVariables.MainWindow.Size);

        }
        public override void Initialize()
        {
            //SDL.Init(Sdl.InitVideo);
            //SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG);

            if (GVariables.CurrentWindowHostType != WindowHostType.SDL)
            {
                //SDL.SetModState();
                SDL.CaptureMouse(false);
                
                //SDL.EventState((uint)EventType.Mousemotion, Sdl.Disable);
                //SDL.EventState((uint)EventType.Mousebuttondown, Sdl.Disable);
                //SDL.EventState((uint)EventType.Mousebuttonup, Sdl.Disable);
                //SDL.EventState((uint)EventType.Mousewheel, Sdl.Disable);
                //SDL.EventState((uint)EventType.Keydown, Sdl.Disable);
                //SDL.EventState((uint)EventType.Keyup, Sdl.Disable);
                //SDL.EventState((uint)EventType.Keymapchanged, Sdl.Disable);

                SDL.HideCursor();
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
            if (rui && GVariables.ThisGame.UIScene != null && !ppr)
            {
                ppr = true;
                SetUIScene(true);
            }
            rendering = true;
            if (!framepping)
            {
                SDL.SetRenderDrawColor(RendererPtr, (byte)BackgroundColor.X, (byte)BackgroundColor.Y, (byte)BackgroundColor.Z, 255);
                SDL.RenderClear(RendererPtr);

                if (rui && curui != null)
                {
                    //Silk.NET.Maths.Rectangle<int> r = new Silk.NET.Maths.Rectangle<int>();
                    //Sdl.Rect r = new SDL.SDL_Rect();
                    //Point c = new Point();
                    //Silk.NET.Maths.Rectangle<int> s = new Silk.NET.Maths.Rectangle<int>();
                    SDLFRect r = new SDLFRect();
                    SDLFRect s = new SDLFRect();
                    SDLFPoint c = new SDLFPoint();
                    foreach (var v in curui.Controls.Controls)
                    {

                        if (v.Spirit != null && v.Spirit.IsLoaded)
                        {
                            //SELogger.Log("E");
                            if (uitextures.TryGetValue(v.Uuid.ID, out var texture))
                            {
                                r.W = (int)v.Size.X;
                                r.H = (int)v.Size.Y;
                                r.X = (int)v.Position?[0][0];
                                r.Y = (int)v.Position?[0][1];
                                s.H = (int)v.Spirit.BaseImage.Size.Y;
                                s.W = (int)v.Spirit.BaseImage.Size.X;
                                s.X = 0;
                                s.Y = 0;
                                c.X = s.W / 2;
                                c.Y = s.H / 2;

                                SDL.RenderTextureRotated(RendererPtr, texture, &s, &r, 180 + v.Angle, &c, SDLFlipMode.Horizontal);

                                //SDL.RenderCopyEx(RendererPtr, (Texture*)texture, &s, &r, 180 + v.Angle, &c, RendererFlip.Horizontal);
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
            var s = GVariables.ThisGame.ThisScenes[index];
            foreach (var v in s.ThisGameObjects)
            {

            }
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
                        var tt = ConvertImageToTexture(v.Spirit.BaseImage.GetImage().CloneAs<Rgba32>(), RendererPtr);
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
            rui = enable;
            if (enable && GVariables.ThisGame.UIScene != null)
            {
                curui = GVariables.ThisGame.UIScene;
                LoadUIC();
            }
        }

        public SDLTexturePtr ConvertImageToTexture(SixLabors.ImageSharp.Image<Rgba32> image, SDLRendererPtr renderer)
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
                SDLTexturePtr texture = SDL.CreateTexture(
                    renderer,
                    SDLPixelFormat.Abgr8888,
                    (int)TextureAccess.Static,
                    image.Width,
                    image.Height
                );

                if ((nint)texture.Handle == IntPtr.Zero)
                {
                    throw new Exception($"��������ʧ��: {Helper.PTRGetString((nint)SDL.GetError())}");
                }

                // ������������
                bool result = SDL.UpdateTexture(
                    texture,
                    new SDLRect(),
                    pixels.ToPointer(),
                    image.Width * 4
                );

                if (!result)
                {
                    SDL.DestroyTexture(texture);
                    throw new Exception($"��������ʧ��: {Helper.PTRGetString((nint)SDL.GetError())}");
                }

                // �����������ģʽ����ѡ��
                SDL.SetTextureBlendMode(texture, (uint)SDLBlendMode.Blend);

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
