using SaturnEngine.Asset;
using SaturnEngine.SEMath;
//using Hexa.NET.SDL3;
using Silk.NET.SDL;
using SaturnEngine.Global;
using SaturnEngine.Management;

namespace SaturnEngine.SEGraphics;

public unsafe class SEWindowSDL : SEWindow
{
    public uint SDLFlag = 0;
    public Window* window;
    public Sdl SDL;
    public override void CreateWindow()
    {
        window = SDL.CreateWindow(Title,0,0, (int)Size.X, (int)Size.Y, 0);
        SDL.SetWindowResizable(window, SdlBool.True);
        SDL.SetWindowPosition(window, (int)Position.X, (int)Position.Y);
        
    }
    public override void LockCursor(bool lockCursor)
    {
        throw new NotImplementedException();
    }
    public override void Initialize()
    {
        SDL = Sdl.GetApi();

        SDLFlag = Sdl.InitVideo | Sdl.InitSensor | Sdl.InitNoparachute | Sdl.InitEvents;// SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_SENSOR| SDL.SDL_INIT_CAMERA;
        if (GVariables.OS != OS.Windows)
        {
            //windows下使用winhook作为输入，directsound作为输出，xinput输入
            SDLFlag |= Sdl.InitAudio  | Sdl.InitGamecontroller | Sdl.InitJoystick;
        }

        if (SDL.Init(SDLFlag) > 0)
        {
            SELogger.Error($"无法初始化SDL: {Helper.PTRGetString(SDL.GetError())}", "SEWindowSDL");
        }
    }
    public override void OnClose()
    {
        SDL.DestroyWindow( window);
        SDL.Quit();

    }
    public override void OnStart()
    {
        SDL.ShowWindow(window);
        //TODO: create render
        //测试版本默认全平台的vulkan1.1
        Renderer = new SEVulkanRender(this);
        Renderer.Initialize();


    }

    private Event e = new Event();
    public override void OnUpdate()
    {
        while (SDL.PollEvent(ref e) > 0)
        {
            if (e.Type == (uint)EventType.Quit)
            {
                SELogger.Log("收到退出事件，正在关闭窗口", "SEWindowSDL");
                Close();
            }
            //TODO: 可以添加一些判断代码
            
        }
        
        Delegates.ProcessEvent();
        Delegates.InvokeAll();
        //SDL.SetWindowTitle(window, $"FPS:{MainThread.Currentfps} ||SEObjects Count:{GVariables.SEObjects.Count}");
        //SELogger.Log($"FPS:{MainThread.Currentfps} ||SEObjects Count:{GVariables.SEObjects.Count}", "SEWindowSDL");

    }
    public override IntPtr GetWindowHandle()
    {
        return new IntPtr(window);
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