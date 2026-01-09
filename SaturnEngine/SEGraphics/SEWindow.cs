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
    public abstract class SEWindow : SEBase
    {
        public WindowHostType HostType { get; internal set; }

        public SEThread MainThread { get; internal set; }
        public SEThread UpdateThread { get; internal set; }
        public SEThread RenderThread { get; internal set; }
        public SEThread AudioThread { get; internal set; }
        public SEThread PhysicsThread { get; internal set; }
        public SEThread NetworkThread { get; internal set; }
        public SEThread VideoThread { get; internal set; }
        public SEThread InputThread { get; internal set; }//if hook?
        public Vector2D Size { get; internal set; }
        public Vector2D Position { get; internal set; }
        public string Title { get; internal set; } = "Saturn Engine";
        public bool Resizable { get; internal set; } = true;
        public bool FullScreen { get; internal set; } = false;
        public double EventRate { get { return _EventRate; } internal set { _EventRate = value; UpdateThread?.SetFPS((int)_EventRate); } }
        public double RenderRate { get { return _RenderRate; } internal set { _RenderRate = value; RenderThread?.SetFPS((int)_RenderRate); } }
        public double AudioRate { get { return _AudioRate; } internal set { _AudioRate = value; AudioThread?.SetFPS((int)_AudioRate); } }
        public double MainThreadRate { get { return _MainThreadRate; } internal set { _MainThreadRate = value; MainThread?.SetFPS((int)_MainThreadRate); } }
        public double EventRateBackground { get; internal set; } = 60;
        public double AudioRateBackground { get; internal set; } = 60;
        public double RenderRateBackground { get; internal set; } = 60;
        public double MainThreadRateBackground { get; internal set; } = 500;
        public bool TearingSupport { get; internal set; } = false;
        public bool UseTearing { get; internal set; } = false;
        public bool UseHDR { get; internal set; } = false;
        public bool HDRSupport { get; internal set; } = false;
        public int UseMonitorIndex { get; internal set; } = 0;
        public int TotalMonitorCount { get; internal set; } = 0;
        public Render Renderer { get; internal set; } = null!; //渲染器
        public DelegateQueue Delegates { get; internal set; } = new DelegateQueue("Main Thread DIL");//主队列事件执行
        public DelegateQueue RenderDel { get; internal set; } = new DelegateQueue("Render Thread DIL");//主队列事件执行
        public DelegateQueue AudioDel { get; internal set; } = new DelegateQueue("Audio Thread DIL");//主队列事件执行
        public IUpdateLoop UpdateLoop { get; set; } = null;
        /// <summary>
        /// 光标是否可见
        /// </summary>
        public bool IsCursorVisible { get; internal set; } = true;
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
        /// 设置窗口渲染所在的监视器（全屏状态下）
        /// </summary>
        /// <param name="index"></param>
        public abstract void SetMonitorIndex(int index);
        /// <summary>
        /// 设置渲染的帧率
        /// </summary>
        /// <param name="rt"></param>
        public abstract void SetRenderRate(double rt);
        /// <summary>
        /// 创建窗口，但不会运行它，你可以在调用此函数之前设置窗口的属性，例如大小、标题等。
        /// </summary>
        public abstract void CreateWindow();
        /// <summary>
        /// 设置窗口位置
        /// </summary>
        /// <param name="pos"></param>
        public abstract void SetPosition(Vector2D pos);
        /// <summary>
        /// 设置窗口大小
        /// </summary>
        /// <param name="size"></param>
        public abstract void SetSize(Vector2D size);
        /// <summary>
        /// 是否使用虚拟光标（非SDL或OPENGL实现）
        /// </summary>
        /// <param name="us"></param>
        public abstract void UseVirtualCursorInput(bool us);
        /// <summary>
        /// 设置窗口是否可以手动调整大小
        /// </summary>
        /// <param name="resizable"></param>
        public abstract void SetResizable(bool resizable);
        /// <summary>
        /// 设置是否全屏显示
        /// </summary>
        /// <param name="fullscreen"></param>
        public abstract void SetFullScreen(bool fullscreen);
        /// <summary>
        /// 设置窗口标题
        /// </summary>
        /// <param name="title"></param>
        public abstract void SetTitle(string title);
        /// <summary>
        /// 设置窗口图标
        /// </summary>
        /// <param name="fp"></param>
        public abstract void SetICONFromImage(SEImageFile fp);
        /// <summary>
        /// 设置鼠标光标
        /// </summary>
        /// <param name="fp"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public abstract void SetCursorFromImage(SEImageFile fp, int x = 0, int y = 0);
        /// <summary>
        /// 是否限制鼠标在窗口内
        /// </summary>
        /// <param name="lockCursor"></param>
        public abstract void LockCursor(bool lockCursor);
        /// <summary>
        /// 是否设置鼠标可以无限拖动
        /// </summary>
        /// <param name="endlessMove"></param>
        public abstract void SetCursorEndlessMove(bool endlessMove);
        /// <summary>
        /// 是否显示光标
        /// </summary>
        /// <param name="showCursor"></param>
        public abstract void ShowCursor(bool showCursor);
        /// <summary>
        /// 是否使用逻辑位移输入
        /// </summary>
        /// <param name="useLogicCursor"></param>
        public abstract void UseLogicCursorInput(bool useLogicCursor);
        /// <summary>
        /// 初始化
        /// </summary>
        public abstract void Initialize();
        /// <summary>
        /// 关闭窗口并释放资源
        /// </summary>
        public abstract void Close();
        /// <summary>
        /// 运行窗口
        /// </summary>
        public abstract void RunWindow();

        public abstract nint GetWindowHandle();


        internal double _EventRate = 1000;
        internal double _RenderRate = 300;
        internal double _AudioRate = 1000;
        internal double _MainThreadRate = 1000;
    }
    
}
