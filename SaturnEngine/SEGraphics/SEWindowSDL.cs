using SaturnEngine.Asset;
using SaturnEngine.SEMath;
using Hexa.NET.SDL3;
using SaturnEngine.Global;
using SaturnEngine.Management;

namespace SaturnEngine.SEGraphics;

public unsafe class SEWindowSDL : SEWindow
{
    public uint SDLFlag = 0;
    private SDLWindowPtr window;
    public override void CreateWindow()
    {
        window = SDL.CreateWindow(Title, (int)Size.X, (int)Size.Y, 0);
        SDL.SetWindowResizable(window, Resizable);
        SDL.SetWindowPosition(window, (int)Position.X, (int)Position.Y);
        
    }
    public override void LockCursor(bool lockCursor)
    {
        throw new NotImplementedException();
    }
    public override void Initialize()
    {
        SDLFlag = SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_SENSOR| SDL.SDL_INIT_CAMERA;
        if (GVariables.OS != OS.Windows)
        {
            //windows下使用winhook作为输入，directsound作为输出，xinput输入
            SDLFlag |= SDL.SDL_INIT_AUDIO | SDL.SDL_INIT_EVENTS | SDL.SDL_INIT_GAMEPAD | SDL.SDL_INIT_JOYSTICK  ;
        }

        if (!SDL.Init(SDLFlag))
        {
            SELogger.Error($"无法初始化SDL: {Helper.PTRGetString(SDL.GetError())}", "SEWindowSDL");
        }
    }
    public override void OnClose()
    {
        SDL.DestroyWindow(window);
        SDL.Quit();
    }
    public override void OnStart()
    {
        SDL.ShowWindow(window);
        //TODO: create render
        
        
        
    }

    private SDLEvent e = new SDLEvent();
    public override void OnUpdate()
    {
        while (SDL.PollEvent(ref e))
        {
            //TODO: 可以添加一些判断代码
        }
        
        Delegates.ProcessEvent();
        Delegates.InvokeAll();
        //SDL.SetWindowTitle(window, $"FPS:{MainThread.Currentfps} ||SEObjects Count:{GVariables.SEObjects.Count}");
        SELogger.Log($"FPS:{MainThread.Currentfps} ||SEObjects Count:{GVariables.SEObjects.Count}", "SEWindowSDL");
    }
    public override IntPtr GetWindowHandle()
    {
        return new IntPtr(window.Handle);
    }
    public override bool SetAttribute(SEWindowAttribute attribute, object value)
    {
        Attributes[attribute] = value;
        return true;
    }
    public override object GetAttribute(SEWindowAttribute attribute)
    {
        return Attributes[attribute];
    }
}