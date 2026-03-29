using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.Management.Event;
using SaturnEngine.Performance;
using SaturnEngine.Platform;
using SaturnEngine.SEInput;
using SaturnEngine.SEMath;
using Silk.NET.GLFW;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;


namespace SaturnEngine.SEGraphics
{
    public enum SEWindowAttribute
    {
        #region 窗体属性
        /// <summary>
        /// 获取或设置窗体尺寸，传入输出<see cref="SaturnEngine.SEMath.Vector2D"/>
        /// </summary>
        Window_Size,
        /// <summary>
        /// 获取或设置窗体相对所在monitor的位置，传入输出<see cref="SaturnEngine.SEMath.Vector2D"/>
        /// </summary>
        Window_Position,
        /// <summary>
        /// 获取或设置窗体标题，传入输出<see cref="string"/>
        /// </summary>
        Window_Title,
        /// <summary>
        /// 获取或设置窗体所在的monitor，传入输出<see cref="int"/>
        /// </summary>
        Window_Monitor,
        /// <summary>
        /// 获取或设置窗体是否可以自由拉伸，传入输出<see cref="bool"/>
        /// </summary>
        Window_Resizable,
        /// <summary>
        /// 获取或设置窗体是否全屏，传入输出<see cref="bool"/>
        /// </summary>
        Window_FullScreen,
        /// <summary>
        /// 获取或设置窗体图标，传入输出<see cref="SaturnEngine.Asset.SEImageFile"/>
        /// </summary>
        Window_Icon,
        /// <summary>
        /// 获取或设置窗体鼠标光标，传入输出<see cref="SaturnEngine.Asset.SEImageFile"/>
        /// </summary>
        Window_Cursor,
        #endregion
        
        #region 渲染属性
        /// <summary>
        /// 获取或设置主游戏事件刷新率，传入输出<see cref="double"/>
        /// </summary>
        Render_EventRate,
        /// <summary>
        /// 获取或设置渲染刷新率，传入输出<see cref="double"/>
        /// </summary>
        Render_RenderRate,
        /// <summary>
        /// 获取或设置声音管理器刷新率，传入输出<see cref="double"/>
        /// </summary>
        Render_AudioRate,
        /// <summary>
        /// 获取或设置主线程刷新率，传入输出<see cref="double"/>
        /// </summary>
        Render_MainThreadRate,
        /// <summary>
        /// 获取或设置主游戏事件失焦时刷新率，传入输出<see cref="double"/>
        /// </summary>
        Render_EventRateBackground,
        /// <summary>
        /// 获取或设置渲染失焦时刷新率，传入输出<see cref="double"/>
        /// </summary>
        Render_RenderRateBackground,
        /// <summary>
        /// 获取或设置声音管理器失焦时刷新率，传入输出<see cref="double"/>
        /// </summary>
        Render_AudioRateBackground,
        /// <summary>
        /// 获取或设置主线程失焦时刷新率，传入输出<see cref="double"/>
        /// </summary>
        Render_MainThreadRateBackground,
        /// <summary>
        /// 获取或设置是否启用hdr，传入输出<see cref="bool"/>
        /// </summary>
        Render_HDR,
        /// <summary>
        /// 获取或设置是否启用Sync，传入输出<see cref="bool"/>
        /// </summary>
        Render_Sync,
        /// <summary>
        /// 获取或设置渲染图像API，传入输出<see cref="SaturnEngine.Global.GraphicsAPI"/>，此项在<code>SEWindow.CreateWindow();</code>运行后无法更改。
        /// </summary>
        Render_API,
        /// <summary>
        /// 获取或设置渲染图像类型，传入输出<see cref="SaturnEngine.Global.ProgramTypes"/>，此项在<code>SEWindow.CreateWindow();</code>运行后无法更改。
        /// </summary>
        Render_Type,
        /// <summary>
        /// 获取或设置渲染图像API最低版本，传入输出<see cref="SaturnEngine.Asset.VERSION"/>，此项在<code>SEWindow.CreateWindow();</code>运行后无法更改。
        /// </summary>
        Render_BaseVersion,
        /// <summary>
        /// 获取或设置渲染图像API目标版本，传入输出<see cref="SaturnEngine.Asset.VERSION"/>，此项在<code>SEWindow.CreateWindow();</code>运行后无法更改。
        /// </summary>
        Render_AimVersion,
        #endregion

        #region 光标属性
        /// <summary>
        /// 获取或设置是否显示鼠标指针，传入输出<see cref="bool"/>
        /// </summary>
        Cursor_Show,
        

        #endregion
        
        /// <summary>
        /// 仅供统计
        /// </summary>
        Last
    }
    public abstract class SEWindow : SEBase
    {

        public SEWindow() 
        {
            GVariables.MainWindows.Add(this);
            for (int i = 0; i < (int)SEWindowAttribute.Last; i++)
            {
                Attributes.Add((SEWindowAttribute)i, null!);
            }
            // Set default values
            Attributes[SEWindowAttribute.Window_Title] = "Saturn Engine";
            Attributes[SEWindowAttribute.Window_Resizable] = true;
            Attributes[SEWindowAttribute.Window_FullScreen] = false;
            Attributes[SEWindowAttribute.Render_EventRate] = 1000.0;
            Attributes[SEWindowAttribute.Render_RenderRate] = 300.0;
            Attributes[SEWindowAttribute.Render_AudioRate] = 1000.0;
            Attributes[SEWindowAttribute.Render_MainThreadRate] = 1000.0;
            Attributes[SEWindowAttribute.Render_EventRateBackground] = 60.0;
            Attributes[SEWindowAttribute.Render_RenderRateBackground] = 60.0;
            Attributes[SEWindowAttribute.Render_AudioRateBackground] = 60.0;
            Attributes[SEWindowAttribute.Render_MainThreadRateBackground] = 500.0;
            Attributes[SEWindowAttribute.Window_Monitor] = 0;
            Attributes[SEWindowAttribute.Cursor_Show] = true;
            Attributes[SEWindowAttribute.Window_Size] = default(Vector2D);
            Attributes[SEWindowAttribute.Window_Position] = default(Vector2D);
        }

        ~SEWindow()
        {
            GVariables.MainWindows.Remove(this);
        }
        public WindowHostType HostType { get; set; }
        public GraphicsAPI RenderApi { get=>(GraphicsAPI)Attributes[SEWindowAttribute.Render_API]; internal set=>Attributes[SEWindowAttribute.Render_API] = value; }
        public ProgramTypes RenderType { get => (ProgramTypes)Attributes[SEWindowAttribute.Render_Type]; internal set => Attributes[SEWindowAttribute.Render_Type] = value; }
        public VERSION AimApiVersion { get => (VERSION)Attributes[SEWindowAttribute.Render_AimVersion]; internal set => Attributes[SEWindowAttribute.Render_AimVersion] = value; }
        public VERSION BaseApiVersion { get => (VERSION)Attributes[SEWindowAttribute.Render_BaseVersion]; internal set => Attributes[SEWindowAttribute.Render_BaseVersion] = value; }
        public SEThread? MainThread { get; internal set; }
        public SEThread? UpdateThread { get; internal set; }
        public SEThread? RenderThread { get; internal set; }
        public SEThread? AudioThread { get; internal set; }
        public SEThread? PhysicsThread { get; internal set; }
        public SEThread? NetworkThread { get; internal set; }
        public SEThread? VideoThread { get; internal set; }
        public SEThread? InputThread { get; internal set; }//if hook?
        public Vector2D Size { get => (Vector2D)Attributes[SEWindowAttribute.Window_Size]; internal set => Attributes[SEWindowAttribute.Window_Size] = value; }
        public Vector2D Position { get => (Vector2D)Attributes[SEWindowAttribute.Window_Position]; internal set => Attributes[SEWindowAttribute.Window_Position] = value; }
        public string Title { get => (string)Attributes[SEWindowAttribute.Window_Title]; internal set => Attributes[SEWindowAttribute.Window_Title] = value; }
        public bool Resizable { get => (bool)Attributes[SEWindowAttribute.Window_Resizable]; internal set => Attributes[SEWindowAttribute.Window_Resizable] = value; }
        public bool FullScreen { get => (bool)Attributes[SEWindowAttribute.Window_FullScreen]; internal set => Attributes[SEWindowAttribute.Window_FullScreen] = value; }
        public double EventRate { get => (double)Attributes[SEWindowAttribute.Render_EventRate]; internal set { Attributes[SEWindowAttribute.Render_EventRate] = value; UpdateThread?.SetFPS((int)value); } }
        public double RenderRate { get => (double)Attributes[SEWindowAttribute.Render_RenderRate]; internal set { Attributes[SEWindowAttribute.Render_RenderRate] = value; RenderThread?.SetFPS((int)value); } }
        public double AudioRate { get => (double)Attributes[SEWindowAttribute.Render_AudioRate]; internal set { Attributes[SEWindowAttribute.Render_AudioRate] = value; AudioThread?.SetFPS((int)value); } }
        public double MainThreadRate { get => (double)Attributes[SEWindowAttribute.Render_MainThreadRate]; internal set { Attributes[SEWindowAttribute.Render_MainThreadRate] = value; MainThread?.SetFPS((int)value); } }
        public double EventRateBackground { get => (double)Attributes[SEWindowAttribute.Render_EventRateBackground]; internal set => Attributes[SEWindowAttribute.Render_EventRateBackground] = value; }
        public double AudioRateBackground { get => (double)Attributes[SEWindowAttribute.Render_AudioRateBackground]; internal set => Attributes[SEWindowAttribute.Render_AudioRateBackground] = value; }
        public double RenderRateBackground { get => (double)Attributes[SEWindowAttribute.Render_RenderRateBackground]; internal set => Attributes[SEWindowAttribute.Render_RenderRateBackground] = value; }
        public double MainThreadRateBackground { get => (double)Attributes[SEWindowAttribute.Render_MainThreadRateBackground]; internal set => Attributes[SEWindowAttribute.Render_MainThreadRateBackground] = value; }
        public int UseMonitorIndex { get => (int)Attributes[SEWindowAttribute.Window_Monitor]; internal set => Attributes[SEWindowAttribute.Window_Monitor] = value; }
        public int TotalMonitorCount { get; internal set; } = 0;
        public Render? Renderer { get; internal set; } = null!; //渲染器
        public DelegateQueue Delegates { get; internal set; } = new DelegateQueue("Main Thread DIL");//主队列事件执行
        public DelegateQueue RenderDel { get; internal set; } = new DelegateQueue("Render Thread DIL");//主队列事件执行
        public DelegateQueue AudioDel { get; internal set; } = new DelegateQueue("Audio Thread DIL");//主队列事件执行
        public IUpdateLoop UpdateLoop { get; set; } = null;
        public Game? OwnerGame { get; internal set; }
        
        internal Dictionary<SEWindowAttribute, object> Attributes = new Dictionary<SEWindowAttribute, object>();
        
        /// <summary>
        /// 光标是否可见
        /// </summary>
        public bool IsCursorVisible { get => (bool)Attributes[SEWindowAttribute.Cursor_Show]; internal set => Attributes[SEWindowAttribute.Cursor_Show] = value; }
        /// <summary>
        /// 是否将光标锁定在窗口内，开启后将无法移动光标到窗口外
        /// </summary>
        public bool IsCursorLocked { get { return BasicInput.WH_LCKCUR; } internal set { BasicInput.WH_LCKCUR = value; } }
        /// <summary>
        /// 是否使用逻辑光标，开启后将强制鼠标归中，并返回相对位移
        /// </summary>
        public bool IsUseLogicCursor { get { return BasicInput.WH_LGCCUR; } internal set { BasicInput.WH_LGCCUR = value; } }
        /// <summary>
        /// 是否光标无限移动，如果开启则光标会在窗口边界处无限移动，默认为false
        /// </summary>
        public bool IsEndlessCursorMove { get { return BasicInput.WH_ENSCUR; } internal set { BasicInput.WH_ENSCUR = value; } }
        /// <summary>
        /// 将渲染一个贴图作为虚拟光标，可以自定义光标样式以及移动速度，不得直接改变该变量，应该使用UseLogicCursorInput函数来设置，开启此项同时开启HideCursor
        /// </summary>
        public bool IsUseVirtualCursor { get { return BasicInput.WH_VTLCUR; } internal set { BasicInput.WH_VTLCUR = value; } }
        /// <summary>
        /// 创建窗口，但不会运行它，你可以在调用此函数之前设置窗口的属性，例如大小、标题等。
        /// </summary>
        public abstract void CreateWindow();
        /// <summary>
        /// 是否限制鼠标在窗口内
        /// </summary>
        /// <param name="lockCursor"></param>
        public abstract void LockCursor(bool lockCursor);
        /// <summary>
        /// 初始化
        /// </summary>
        public abstract void Initialize();
        /// <summary>
        /// 关闭窗口并释放资源
        /// </summary>
        public void Close()
        {
            OnClose();
        }

        public abstract void OnClose();

        public abstract void OnStart();
        
        /// <summary>
        /// 运行窗口，有所更改，此函数不再接管主线程，主线程的工作在OnMainThreadInvoke函数中执行，主线程将负责调用UpdateLoop的Update函数以及执行Delegates队列中的事件
        /// </summary>
        public void RunWindow()
        {
            OnStart();
            UpdateLoop = OwnerGame;
            MainThread = Dispatcher.CreateThreadORG(WorkSender, ThreadPriority.BelowNormal);
            MainThread.Start();
            
        }

        void WorkSender()
        {
            MainThread.SetFPS((int)MainThreadRate);
            
            while (GVariables.EngineRunning)
            {
                MainThreadQueue.Add(Worker);
                MainThread.WaitForFPS();
            }
        }

        private double lastt = 0;
        private double currt = 0;
        void Worker()
        {
            currt = GetCurrentTime();
            UpdateLoop.Update(currt - lastt);
            lastt = currt;
            OnUpdate();
        }
        public abstract void OnUpdate();
        public abstract nint GetWindowHandle();
        
        public abstract bool SetAttribute(SEWindowAttribute attribute, object value);
        
        public abstract object GetAttribute(SEWindowAttribute attribute);


    }
    
}
