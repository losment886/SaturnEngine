using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Management.Event;
using SaturnEngine.Performance;
using SaturnEngine.SEMath;
using Silk.NET.SDL;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaturnEngine.SEGraphics
{
    public class SEWindowSDL : SEWindow
    {
        string _title;
        nint _window;
        

        double _EventRate = 1000;
        double _RenderRate = 300;
        double _AudioRate = 1000;
        double _MainThreadRate = 1000;

        public SEThread MainThread { get; private set; }
        public SEThread UpdateThread { get; private set; }
        public SEThread RenderThread { get; private set; }
        public SEThread AudioThread { get; private set; }
        public SEThread PhysicsThread { get; private set; }
        public SEThread NetworkThread { get; private set; }
        public SEThread VideoThread { get; private set; }
        public SEThread InputThread { get; private set; }//if hook?


        public Vector2D Size { get; private set; }
        public Vector2D Position { get; private set; }
        public string Title { get => _title; private set => SetTitle(value); }
        public bool Resizable { get; private set; } = true;
        public bool FullScreen { get; private set; } = false;
        public double EventRate { get { return _EventRate; } private set { _EventRate = value; UpdateThread?.SetFPS((int)_EventRate); } }
        public double RenderRate { get { return _RenderRate; } private set { _RenderRate = value; RenderThread?.SetFPS((int)_RenderRate); } }
        public double AudioRate { get { return _AudioRate; } private set { _AudioRate = value; AudioThread?.SetFPS((int)_AudioRate); } }
        public double MainThreadRate { get { return _MainThreadRate; } private set { _MainThreadRate = value; MainThread?.SetFPS((int)_MainThreadRate); } }
        public double EventRateBackground { get; private set; } = 60;
        public double AudioRateBackground { get; private set; } = 60;
        public double RenderRateBackground { get; private set; } = 60;
        public double MainThreadRateBackground { get; private set; } = 500;
        public bool TearingSupport { get; private set; } = false;
        public bool UseTearing { get; private set; } = false;
        public bool UseHDR { get; private set; } = false;
        public bool HDRSupport { get; private set; } = false;
        public Render Renderer { get; private set; } = null!; //渲染器
        public DelegateQueue Delegates { get; private set; } = new DelegateQueue("Main Thread DIL");//主队列事件执行
        public DelegateQueue RenderDel { get; private set; } = new DelegateQueue("Render Thread DIL");//主队列事件执行
        public DelegateQueue AudioDel { get; private set; } = new DelegateQueue("Audio Thread DIL");//主队列事件执行
        public IUpdateLoop UpdateLoop { get; set; } = null;
        public void SetTitle_(string title)
        {
            //this._title = title; 
            this._title = title;
            //SDL.SDL_SetWindowTitle(_window, _title);
        }

        public override void CreateWindow()
        {
            //SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING);
            //_window =SDL.SDL_CreateWindow(Title, (int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y, SDL.SDL_WindowFlags.SDL_WINDOW_VULKAN | SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL.SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI);
        }

        public override void SetMonitorIndex(int index)
        {
            throw new NotImplementedException();
        }

        public override void SetRenderRate(double rt)
        {
            throw new NotImplementedException();
        }

        public override void SetPosition(Vector2D pos)
        {
            throw new NotImplementedException();
        }

        public override void SetSize(Vector2D size)
        {
            throw new NotImplementedException();
        }

        public override void UseVirtualCursorInput(bool us)
        {
            throw new NotImplementedException();
        }

        public override void SetResizable(bool resizable)
        {
            throw new NotImplementedException();
        }

        public override void SetFullScreen(bool fullscreen)
        {
            throw new NotImplementedException();
        }

        public override void SetTitle(string title)
        {
            //var f = SetTitle_;
            Delegates.Add(()=>{ SetTitle_(title); });
        }

        public override void SetICONFromImage(SEImageFile fp)
        {
            throw new NotImplementedException();
        }

        public override void SetCursorFromImage(SEImageFile fp, int x = 0, int y = 0)
        {
            throw new NotImplementedException();
        }

        public override void LockCursor(bool lockCursor)
        {
            throw new NotImplementedException();
        }

        public override void SetCursorEndlessMove(bool endlessMove)
        {
            throw new NotImplementedException();
        }

        public override void ShowCursor(bool showCursor)
        {
            throw new NotImplementedException();
        }

        public override void UseLogicCursorInput(bool useLogicCursor)
        {
            throw new NotImplementedException();
        }

        public override void Initialize()
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            throw new NotImplementedException();
        }

        public override void RunWindow()
        {
            throw new NotImplementedException();
        }

        public override nint GetWindowHandle()
        {
            throw new NotImplementedException();
        }
    }
}
