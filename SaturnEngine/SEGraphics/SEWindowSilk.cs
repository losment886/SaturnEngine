using SaturnEngine.Global;
using SaturnEngine.Management.SEMemory;
using SaturnEngine.Performance;
using SaturnEngine.SEInput;
using SaturnEngine.SEMath;
using Silk.NET.Windowing;
using GraphicsAPI = Silk.NET.Windowing.GraphicsAPI;

namespace SaturnEngine.SEGraphics;

public class SEWindowSilk : SEWindow
{
    public SEStaticPtr<Silk.NET.Windowing.IWindow> window;
    
    private void RenderThreadFunc()
    {
            
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
            cur = GetCurrentTime();
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
    public override void CreateWindow()
    {
        Silk.NET.Windowing.WindowOptions options = new Silk.NET.Windowing.WindowOptions()
        {
            Size = new Silk.NET.Maths.Vector2D<int>((int)Size.X, (int)Size.Y),
            Title = Title,
            Position = new Silk.NET.Maths.Vector2D<int>((int)Position.X, (int)Position.Y),
            API = (((SaturnEngine.Global.GraphicsAPI)Attributes[SEWindowAttribute.Render_API]) == SaturnEngine.Global.GraphicsAPI.Vulkan) ? GraphicsAPI.DefaultVulkan : GraphicsAPI.None,
        };
        window = new SEStaticPtr<IWindow>(Silk.NET.Windowing.Window.Create(options));
        
    }

    public override void Initialize()
    {
        //window.First.VkSurface
    }

    public override void OnClose()
    {
        window.First.Close();
    }

    public override void OnStart()
    {
        Renderer = new SEVulkanRender(this);
        Renderer.Initialize();
        BasicInput.WSize = new POINT((int)Size.X, (int)Size.Y);
        BasicInput.WPosi = new POINT((int)Position.X, (int)Position.Y);
        //BasicInput.ThisWindow = ThisWindow;
            
        Renderer.SetScene(this.OwnerGame.CurrentSceneIndex);
            
            
        MainThread = Dispatcher.CreateThreadFromExistedThread();
        //UpdateThread = Dispatcher.CreateThreadORG(WorkThreadFunc, ThreadPriority.AboveNormal);
        RenderThread = Dispatcher.CreateThreadORG(RenderThreadFunc, ThreadPriority.Normal);
        //AudioThread = Dispatcher.CreateThreadORG(AudioThreadFunc, ThreadPriority.BelowNormal);
    }

    public override void OnUpdate()
    {
        window.First.DoEvents();
        Delegates.ProcessEvent();
        Delegates.InvokeAll();
    }

    public unsafe override IntPtr GetWindowHandle()
    {
        return new IntPtr(window.Handle);
    }

    public override bool SetAttribute(SEWindowAttribute attribute, object value)
    {
        return false;
    }

    public override object GetAttribute(SEWindowAttribute attribute)
    {
        return null;
    }
}