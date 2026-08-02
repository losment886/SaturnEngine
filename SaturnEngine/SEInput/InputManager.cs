using System.Runtime.InteropServices;
using SaturnEngine.Base;
using SaturnEngine.Management;
using SaturnEngine.SEMath;

namespace SaturnEngine.SEInput
{
    /// <summary>
    /// 统一输入入口：聚合多个 <see cref="IInputProvider"/>，维护当前帧与上一帧状态，
    /// 并对外提供 Down / Pressed / Released 三态查询。
    /// </summary>
    public class InputManager : SEBase, IDisposable
    {
        private readonly List<IInputProvider> _providers = new();
        private SEInputState _current = new();
        private SEInputState _previous = new();

        public IReadOnlyList<IInputProvider> Providers => _providers;
        public SEInputState Current => _current;
        public SEInputState Previous => _previous;
        public bool IsEnable { get; private set; }

        /// <summary>手柄摇杆死区，低于该值的轴输入会被归零。</summary>
        public float StickDeadZone { get; set; } = 0.15f;

        public InputManager() : base("InputManager", "引擎输入管理器")
        {
        }

        /// <summary>
        /// 按当前平台装配默认输入源：Windows 上使用 XInput 处理手柄并以 SDL 补齐键鼠与触摸，
        /// 其余平台统一使用 SDL。
        /// </summary>
        public void InitializeDefault(nint windowHandle)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                AddProvider(new XInputProvider(), windowHandle);

            AddProvider(new SDLInputProvider(), windowHandle);

            if (!IsEnable)
                SELogger.Error("没有可用的输入源，输入功能已禁用。", "InputManager");
        }

        /// <summary>注册并初始化一个输入源，初始化失败时会被跳过。</summary>
        public bool AddProvider(IInputProvider provider, nint windowHandle)
        {
            try
            {
                provider.Initialize(windowHandle);
            }
            catch (Exception ex)
            {
                SELogger.Warn($"输入源 {provider.ProviderName} 初始化失败: {ex.Message}", "InputManager");
                provider.Dispose();
                return false;
            }

            _providers.Add(provider);
            IsEnable = true;
            SELogger.Log($"输入源已启用: {provider.ProviderName}", "InputManager");

            foreach (var device in provider.EnumerateDevices())
                Input.RaiseDeviceConnected(device);

            return true;
        }

        public void RemoveProvider(IInputProvider provider)
        {
            if (!_providers.Remove(provider))
                return;

            foreach (var device in provider.EnumerateDevices())
                Input.RaiseDeviceDisconnected(device);

            provider.Shutdown();
            provider.Dispose();
            IsEnable = _providers.Count > 0;
        }

        /// <summary>每帧开始时调用：交换状态并从所有输入源采集新数据。</summary>
        public void Update()
        {
            (_previous, _current) = (_current, _previous);

            CopyPersistentState(_previous, _current);
            _current.BeginFrame();

            foreach (var p in _providers)
            {
                try
                {
                    p.Poll(_current);
                }
                catch (Exception ex)
                {
                    SELogger.Warn($"输入源 {p.ProviderName} 采集失败: {ex.Message}", "InputManager");
                }
            }

            ApplyDeadZone(_current);
        }

        /// <summary>
        /// 复用被换出的状态对象前，先继承持续性状态，
        /// 避免只上报变化量的 Provider（如事件驱动的键盘钩子）丢失按下状态。
        /// </summary>
        private static void CopyPersistentState(SEInputState from, SEInputState to)
        {
            Array.Copy(from.Keys, to.Keys, from.Keys.Length);
            Array.Copy(from.MouseButtons, to.MouseButtons, from.MouseButtons.Length);
            to.MousePosition = from.MousePosition;

            to.Gamepads.Clear();
            foreach (var g in from.Gamepads)
            {
                var copy = to.GetOrCreateGamepad(g.Index);
                copy.Name = g.Name;
                copy.IsConnected = g.IsConnected;
                Array.Copy(g.Buttons, copy.Buttons, g.Buttons.Length);
                Array.Copy(g.Axes, copy.Axes, g.Axes.Length);
            }
        }

        private void ApplyDeadZone(SEInputState state)
        {
            foreach (var g in state.Gamepads)
            {
                ApplyStickDeadZone(g, SEGamepadAxis.LeftX, SEGamepadAxis.LeftY);
                ApplyStickDeadZone(g, SEGamepadAxis.RightX, SEGamepadAxis.RightY);
            }
        }

        private void ApplyStickDeadZone(SEGamepadState g, SEGamepadAxis xAxis, SEGamepadAxis yAxis)
        {
            float x = g.Axes[(int)xAxis];
            float y = g.Axes[(int)yAxis];
            float magnitude = MathF.Sqrt(x * x + y * y);

            if (magnitude <= StickDeadZone || magnitude <= 0)
            {
                g.Axes[(int)xAxis] = 0;
                g.Axes[(int)yAxis] = 0;
                return;
            }

            // 径向缩放：死区外重新映射到 [0, 1]，避免越过死区时数值跳变。
            float scaled = Math.Min((magnitude - StickDeadZone) / (1.0f - StickDeadZone), 1.0f) / magnitude;
            g.Axes[(int)xAxis] = x * scaled;
            g.Axes[(int)yAxis] = y * scaled;
        }

        #region 键盘

        public bool IsKeyDown(Keys key) => Get(_current.Keys, (int)key);
        public bool IsKeyPressed(Keys key) => Get(_current.Keys, (int)key) && !Get(_previous.Keys, (int)key);
        public bool IsKeyReleased(Keys key) => !Get(_current.Keys, (int)key) && Get(_previous.Keys, (int)key);
        public string TextInput => _current.TextInput;

        #endregion

        #region 鼠标

        public bool IsMouseDown(SEMouseButton b) => Get(_current.MouseButtons, (int)b);
        public bool IsMousePressed(SEMouseButton b) => Get(_current.MouseButtons, (int)b) && !Get(_previous.MouseButtons, (int)b);
        public bool IsMouseReleased(SEMouseButton b) => !Get(_current.MouseButtons, (int)b) && Get(_previous.MouseButtons, (int)b);

        public Vector2D MousePosition => _current.MousePosition;
        public Vector2D MouseDelta => _current.MouseDelta;
        public float MouseWheel => _current.MouseWheel;

        #endregion

        #region 手柄

        public IReadOnlyList<SEGamepadState> Gamepads => _current.Gamepads;

        public SEGamepadState? GetGamepad(int index)
        {
            foreach (var g in _current.Gamepads)
            {
                if (g.Index == index)
                    return g;
            }
            return null;
        }

        public bool IsGamepadButtonDown(int index, SEGamepadButton button)
            => GetGamepad(index)?.GetButton(button) ?? false;

        public bool IsGamepadButtonPressed(int index, SEGamepadButton button)
        {
            bool now = IsGamepadButtonDown(index, button);
            bool before = FindIn(_previous, index)?.GetButton(button) ?? false;
            return now && !before;
        }

        public bool IsGamepadButtonReleased(int index, SEGamepadButton button)
        {
            bool now = IsGamepadButtonDown(index, button);
            bool before = FindIn(_previous, index)?.GetButton(button) ?? false;
            return !now && before;
        }

        public float GetGamepadAxis(int index, SEGamepadAxis axis)
            => GetGamepad(index)?.GetAxis(axis) ?? 0.0f;

        /// <summary>向所有支持震动的输入源转发震动请求。</summary>
        public void SetVibration(int gamepadIndex, float leftMotor, float rightMotor)
        {
            leftMotor = Math.Clamp(leftMotor, 0.0f, 1.0f);
            rightMotor = Math.Clamp(rightMotor, 0.0f, 1.0f);
            foreach (var p in _providers)
                p.SetVibration(gamepadIndex, leftMotor, rightMotor);
        }

        private static SEGamepadState? FindIn(SEInputState state, int index)
        {
            foreach (var g in state.Gamepads)
            {
                if (g.Index == index)
                    return g;
            }
            return null;
        }

        #endregion

        #region 触摸

        public IReadOnlyList<SETouchPoint> Touches => _current.Touches;
        public int TouchCount => _current.Touches.Count;

        #endregion

        private static bool Get(bool[] array, int index)
            => (uint)index < (uint)array.Length && array[index];

        public void Dispose()
        {
            foreach (var p in _providers)
            {
                p.Shutdown();
                p.Dispose();
            }
            _providers.Clear();
            IsEnable = false;
        }
    }
}
