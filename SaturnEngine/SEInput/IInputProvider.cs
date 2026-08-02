using SaturnEngine.SEMath;

namespace SaturnEngine.SEInput
{
    /// <summary>标准鼠标按键。</summary>
    public enum SEMouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2,
        X1 = 3,
        X2 = 4,
    }

    /// <summary>符合 XInput / SDL GameController 标准布局的手柄按键。</summary>
    public enum SEGamepadButton
    {
        A = 0,
        B = 1,
        X = 2,
        Y = 3,
        Back = 4,
        Guide = 5,
        Start = 6,
        LeftStick = 7,
        RightStick = 8,
        LeftShoulder = 9,
        RightShoulder = 10,
        DPadUp = 11,
        DPadDown = 12,
        DPadLeft = 13,
        DPadRight = 14,
        Count = 15,
    }

    /// <summary>手柄模拟轴，取值统一归一化。</summary>
    public enum SEGamepadAxis
    {
        /// <summary>左摇杆横轴，[-1, 1]，右为正。</summary>
        LeftX = 0,
        /// <summary>左摇杆纵轴，[-1, 1]，上为正。</summary>
        LeftY = 1,
        RightX = 2,
        RightY = 3,
        /// <summary>左扳机，[0, 1]。</summary>
        LeftTrigger = 4,
        /// <summary>右扳机，[0, 1]。</summary>
        RightTrigger = 5,
        Count = 6,
    }

    /// <summary>触摸点的生命周期阶段。</summary>
    public enum SETouchPhase
    {
        Began,
        Moved,
        Stationary,
        Ended,
        Canceled,
    }

    /// <summary>单个触摸点，坐标为窗口客户区像素坐标。</summary>
    public struct SETouchPoint
    {
        public long Id;
        public Vector2D Position;
        public Vector2D Delta;
        public float Pressure;
        public SETouchPhase Phase;
    }

    /// <summary>单个手柄的完整状态快照。</summary>
    public class SEGamepadState
    {
        public int Index;
        public string Name = string.Empty;
        public bool IsConnected;
        public readonly bool[] Buttons = new bool[(int)SEGamepadButton.Count];
        public readonly float[] Axes = new float[(int)SEGamepadAxis.Count];

        public bool GetButton(SEGamepadButton b) => Buttons[(int)b];
        public float GetAxis(SEGamepadAxis a) => Axes[(int)a];

        public void Reset()
        {
            Array.Clear(Buttons);
            Array.Clear(Axes);
            IsConnected = false;
        }
    }

    /// <summary>
    /// 一帧的输入状态。由各 <see cref="IInputProvider"/> 写入，由 <see cref="InputManager"/> 读取。
    /// </summary>
    public class SEInputState
    {
        /// <summary>与 <see cref="Keys"/> 取值对应的按键按下表。</summary>
        public readonly bool[] Keys = new bool[512];
        public readonly bool[] MouseButtons = new bool[8];

        public Vector2D MousePosition;
        public Vector2D MouseDelta;
        public float MouseWheel;

        public readonly List<SETouchPoint> Touches = new();
        public readonly List<SEGamepadState> Gamepads = new();

        /// <summary>本帧产生的文本输入（已完成 IME 组合）。</summary>
        public string TextInput = string.Empty;

        /// <summary>清除逐帧累积量，保留持续性状态（按键按下、手柄连接等）。</summary>
        public void BeginFrame()
        {
            MouseDelta = new Vector2D(0, 0);
            MouseWheel = 0;
            TextInput = string.Empty;
            Touches.Clear();
        }

        /// <summary>获取（必要时创建）指定索引的手柄状态。</summary>
        public SEGamepadState GetOrCreateGamepad(int index)
        {
            foreach (var g in Gamepads)
            {
                if (g.Index == index)
                    return g;
            }
            var created = new SEGamepadState { Index = index };
            Gamepads.Add(created);
            return created;
        }
    }

    /// <summary>
    /// 平台输入源抽象。一个 Provider 只负责它能覆盖的设备类别，
    /// 多个 Provider 可以同时启用（例如 Win32 Hook 提供键鼠、XInput 提供手柄）。
    /// </summary>
    public interface IInputProvider : IDisposable
    {
        string ProviderName { get; }
        bool IsInitialized { get; }

        /// <summary>本 Provider 能够提供数据的设备类别。</summary>
        InputDeviceType[] SupportedDevices { get; }

        /// <summary>绑定到目标窗口并开始采集。<paramref name="windowHandle"/> 可为 0 表示无窗口关联。</summary>
        void Initialize(nint windowHandle);

        void Shutdown();

        /// <summary>把最新的设备数据写入 <paramref name="state"/>，每帧调用一次。</summary>
        void Poll(SEInputState state);

        /// <summary>设置手柄震动强度，取值 [0, 1]。不支持时应静默忽略。</summary>
        void SetVibration(int gamepadIndex, float leftMotor, float rightMotor);

        /// <summary>枚举当前已连接的设备。</summary>
        IEnumerable<InputDeviceInfo> EnumerateDevices();
    }
}
