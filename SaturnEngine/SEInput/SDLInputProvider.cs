using System.Runtime.InteropServices;
using SaturnEngine.SEMath;
using Silk.NET.SDL;

namespace SaturnEngine.SEInput
{
    /// <summary>
    /// 基于 SDL 的跨平台输入源，覆盖键盘、鼠标、手柄与触摸。
    /// 可在 Windows / macOS / Linux / Android / iOS / HarmonyOS NEXT 上工作。
    /// </summary>
    public sealed unsafe class SDLInputProvider : IInputProvider
    {
        private Sdl? _sdl;
        private readonly Dictionary<int, nint> _controllers = new();

        public string ProviderName => "SDL";
        public bool IsInitialized { get; private set; }

        public InputDeviceType[] SupportedDevices { get; } =
        {
            InputDeviceType.Keyboard,
            InputDeviceType.Mouse,
            InputDeviceType.Gamepad,
            InputDeviceType.Touch,
        };

        public void Initialize(nint windowHandle)
        {
            if (IsInitialized)
                return;

            _sdl = Sdl.GetApi();
            uint needed = Sdl.InitEvents | Sdl.InitGamecontroller | Sdl.InitJoystick;
            if (_sdl.WasInit(needed) != needed && _sdl.InitSubSystem(needed) != 0)
                throw new InvalidOperationException($"SDL 输入子系统初始化失败: {GetError()}");

            RefreshControllers();
            IsInitialized = true;
        }

        private string GetError()
        {
            byte* e = _sdl!.GetError();
            return e == null ? "unknown" : Marshal.PtrToStringUTF8((nint)e) ?? "unknown";
        }

        private void RefreshControllers()
        {
            var sdl = _sdl!;
            int count = sdl.NumJoysticks();
            for (int i = 0; i < count; i++)
            {
                if (_controllers.ContainsKey(i) || sdl.IsGameController(i) == SdlBool.False)
                    continue;

                var handle = sdl.GameControllerOpen(i);
                if (handle != null)
                    _controllers[i] = (nint)handle;
            }
        }

        public void Shutdown()
        {
            if (!IsInitialized)
                return;

            foreach (var h in _controllers.Values)
                _sdl!.GameControllerClose((GameController*)h);
            _controllers.Clear();
            IsInitialized = false;
        }

        public void Poll(SEInputState state)
        {
            if (!IsInitialized)
                return;

            var sdl = _sdl!;
            sdl.PumpEvents();
            RefreshControllers();

            PollKeyboard(sdl, state);
            PollMouse(sdl, state);
            PollGamepads(sdl, state);
            PollTouch(sdl, state);
        }

        private static void PollKeyboard(Sdl sdl, SEInputState state)
        {
            int numKeys;
            byte* keys = sdl.GetKeyboardState(&numKeys);
            if (keys == null)
                return;

            for (int scancode = 0; scancode < numKeys; scancode++)
            {
                var key = FromScancode((Scancode)scancode);
                if (key != Keys.None)
                    state.Keys[(int)key] = keys[scancode] != 0;
            }
        }

        private static void PollMouse(Sdl sdl, SEInputState state)
        {
            int x, y;
            uint buttons = sdl.GetMouseState(&x, &y);
            var position = new Vector2D(x, y);

            state.MouseDelta = new Vector2D(position.X - state.MousePosition.X, position.Y - state.MousePosition.Y);
            state.MousePosition = position;

            state.MouseButtons[(int)SEMouseButton.Left] = (buttons & 0x1) != 0;
            state.MouseButtons[(int)SEMouseButton.Middle] = (buttons & 0x2) != 0;
            state.MouseButtons[(int)SEMouseButton.Right] = (buttons & 0x4) != 0;
            state.MouseButtons[(int)SEMouseButton.X1] = (buttons & 0x8) != 0;
            state.MouseButtons[(int)SEMouseButton.X2] = (buttons & 0x10) != 0;
        }

        private void PollGamepads(Sdl sdl, SEInputState state)
        {
            foreach (var g in state.Gamepads)
                g.IsConnected = false;

            foreach (var (index, handle) in _controllers)
            {
                var ctrl = (GameController*)handle;
                var g = state.GetOrCreateGamepad(index);
                g.IsConnected = true;

                byte* name = sdl.GameControllerName(ctrl);
                if (name != null && string.IsNullOrEmpty(g.Name))
                    g.Name = Marshal.PtrToStringUTF8((nint)name) ?? string.Empty;

                for (int b = 0; b < (int)SEGamepadButton.Count; b++)
                    g.Buttons[b] = sdl.GameControllerGetButton(ctrl, ToSdlButton((SEGamepadButton)b)) != 0;

                // 摇杆为有符号 16 位，向上为负；引擎约定向上为正，故 Y 轴取反。
                g.Axes[(int)SEGamepadAxis.LeftX] = Normalize(sdl.GameControllerGetAxis(ctrl, GameControllerAxis.Leftx));
                g.Axes[(int)SEGamepadAxis.LeftY] = -Normalize(sdl.GameControllerGetAxis(ctrl, GameControllerAxis.Lefty));
                g.Axes[(int)SEGamepadAxis.RightX] = Normalize(sdl.GameControllerGetAxis(ctrl, GameControllerAxis.Rightx));
                g.Axes[(int)SEGamepadAxis.RightY] = -Normalize(sdl.GameControllerGetAxis(ctrl, GameControllerAxis.Righty));
                g.Axes[(int)SEGamepadAxis.LeftTrigger] = NormalizeTrigger(sdl.GameControllerGetAxis(ctrl, GameControllerAxis.Triggerleft));
                g.Axes[(int)SEGamepadAxis.RightTrigger] = NormalizeTrigger(sdl.GameControllerGetAxis(ctrl, GameControllerAxis.Triggerright));
            }
        }

        private static void PollTouch(Sdl sdl, SEInputState state)
        {
            int deviceCount = sdl.GetNumTouchDevices();
            for (int d = 0; d < deviceCount; d++)
            {
                long deviceId = sdl.GetTouchDevice(d);
                int fingerCount = sdl.GetNumTouchFingers(deviceId);
                for (int f = 0; f < fingerCount; f++)
                {
                    Finger* finger = sdl.GetTouchFinger(deviceId, f);
                    if (finger == null)
                        continue;

                    state.Touches.Add(new SETouchPoint
                    {
                        Id = finger->Id,
                        // SDL 提供的是 [0,1] 归一化坐标，此处保留原样由上层按窗口尺寸换算。
                        Position = new Vector2D(finger->X, finger->Y),
                        Delta = new Vector2D(0, 0),
                        Pressure = finger->Pressure,
                        Phase = SETouchPhase.Stationary,
                    });
                }
            }
        }

        private static float Normalize(short v) => v < 0 ? v / 32768.0f : v / 32767.0f;
        private static float NormalizeTrigger(short v) => Math.Max(0.0f, v / 32767.0f);

        private static GameControllerButton ToSdlButton(SEGamepadButton b) => b switch
        {
            SEGamepadButton.A => GameControllerButton.A,
            SEGamepadButton.B => GameControllerButton.B,
            SEGamepadButton.X => GameControllerButton.X,
            SEGamepadButton.Y => GameControllerButton.Y,
            SEGamepadButton.Back => GameControllerButton.Back,
            SEGamepadButton.Guide => GameControllerButton.Guide,
            SEGamepadButton.Start => GameControllerButton.Start,
            SEGamepadButton.LeftStick => GameControllerButton.Leftstick,
            SEGamepadButton.RightStick => GameControllerButton.Rightstick,
            SEGamepadButton.LeftShoulder => GameControllerButton.Leftshoulder,
            SEGamepadButton.RightShoulder => GameControllerButton.Rightshoulder,
            SEGamepadButton.DPadUp => GameControllerButton.DpadUp,
            SEGamepadButton.DPadDown => GameControllerButton.DpadDown,
            SEGamepadButton.DPadLeft => GameControllerButton.DpadLeft,
            SEGamepadButton.DPadRight => GameControllerButton.DpadRight,
            _ => GameControllerButton.Invalid,
        };

        public void SetVibration(int gamepadIndex, float leftMotor, float rightMotor)
        {
            if (!IsInitialized || !_controllers.TryGetValue(gamepadIndex, out var handle))
                return;

            _sdl!.GameControllerRumble(
                (GameController*)handle,
                (ushort)(Math.Clamp(leftMotor, 0f, 1f) * ushort.MaxValue),
                (ushort)(Math.Clamp(rightMotor, 0f, 1f) * ushort.MaxValue),
                1000);
        }

        public IEnumerable<InputDeviceInfo> EnumerateDevices()
        {
            var list = new List<InputDeviceInfo>
            {
                new() { DeviceType = InputDeviceType.Keyboard, DeviceName = "SDL Keyboard", IsConnected = true },
                new() { DeviceType = InputDeviceType.Mouse, DeviceName = "SDL Mouse", IsConnected = true },
            };

            if (IsInitialized)
            {
                foreach (var (index, handle) in _controllers)
                {
                    byte* name = _sdl!.GameControllerName((GameController*)handle);
                    list.Add(new InputDeviceInfo
                    {
                        DeviceType = InputDeviceType.Gamepad,
                        DeviceName = name == null ? $"Gamepad {index}" : Marshal.PtrToStringUTF8((nint)name) ?? $"Gamepad {index}",
                        DeviceSTC = (ulong)index,
                        IsConnected = true,
                    });
                }
            }

            return list;
        }

        /// <summary>把 SDL 扫描码映射到引擎的 <see cref="Keys"/>（虚拟键）取值。</summary>
        private static Keys FromScancode(Scancode s) => s switch
        {
            Scancode.ScancodeA => Keys.A,
            Scancode.ScancodeB => Keys.B,
            Scancode.ScancodeC => Keys.C,
            Scancode.ScancodeD => Keys.D,
            Scancode.ScancodeE => Keys.E,
            Scancode.ScancodeF => Keys.F,
            Scancode.ScancodeG => Keys.G,
            Scancode.ScancodeH => Keys.H,
            Scancode.ScancodeI => Keys.I,
            Scancode.ScancodeJ => Keys.J,
            Scancode.ScancodeK => Keys.K,
            Scancode.ScancodeL => Keys.L,
            Scancode.ScancodeM => Keys.M,
            Scancode.ScancodeN => Keys.N,
            Scancode.ScancodeO => Keys.O,
            Scancode.ScancodeP => Keys.P,
            Scancode.ScancodeQ => Keys.Q,
            Scancode.ScancodeR => Keys.R,
            Scancode.ScancodeS => Keys.S,
            Scancode.ScancodeT => Keys.T,
            Scancode.ScancodeU => Keys.U,
            Scancode.ScancodeV => Keys.V,
            Scancode.ScancodeW => Keys.W,
            Scancode.ScancodeX => Keys.X,
            Scancode.ScancodeY => Keys.Y,
            Scancode.ScancodeZ => Keys.Z,

            Scancode.Scancode0 => Keys.D0,
            Scancode.Scancode1 => Keys.D1,
            Scancode.Scancode2 => Keys.D2,
            Scancode.Scancode3 => Keys.D3,
            Scancode.Scancode4 => Keys.D4,
            Scancode.Scancode5 => Keys.D5,
            Scancode.Scancode6 => Keys.D6,
            Scancode.Scancode7 => Keys.D7,
            Scancode.Scancode8 => Keys.D8,
            Scancode.Scancode9 => Keys.D9,

            Scancode.ScancodeKP0 => Keys.NumPad0,
            Scancode.ScancodeKP1 => Keys.NumPad1,
            Scancode.ScancodeKP2 => Keys.NumPad2,
            Scancode.ScancodeKP3 => Keys.NumPad3,
            Scancode.ScancodeKP4 => Keys.NumPad4,
            Scancode.ScancodeKP5 => Keys.NumPad5,
            Scancode.ScancodeKP6 => Keys.NumPad6,
            Scancode.ScancodeKP7 => Keys.NumPad7,
            Scancode.ScancodeKP8 => Keys.NumPad8,
            Scancode.ScancodeKP9 => Keys.NumPad9,

            Scancode.ScancodeF1 => Keys.F1,
            Scancode.ScancodeF2 => Keys.F2,
            Scancode.ScancodeF3 => Keys.F3,
            Scancode.ScancodeF4 => Keys.F4,
            Scancode.ScancodeF5 => Keys.F5,
            Scancode.ScancodeF6 => Keys.F6,
            Scancode.ScancodeF7 => Keys.F7,
            Scancode.ScancodeF8 => Keys.F8,
            Scancode.ScancodeF9 => Keys.F9,
            Scancode.ScancodeF10 => Keys.F10,
            Scancode.ScancodeF11 => Keys.F11,
            Scancode.ScancodeF12 => Keys.F12,

            Scancode.ScancodeEscape => Keys.Escape,
            Scancode.ScancodeSpace => Keys.Space,
            Scancode.ScancodeReturn => Keys.Enter,
            Scancode.ScancodeTab => Keys.Tab,
            Scancode.ScancodeBackspace => Keys.Back,
            Scancode.ScancodeInsert => Keys.Insert,
            Scancode.ScancodeDelete => Keys.Delete,
            Scancode.ScancodeRight => Keys.Right,
            Scancode.ScancodeLeft => Keys.Left,
            Scancode.ScancodeDown => Keys.Down,
            Scancode.ScancodeUp => Keys.Up,
            Scancode.ScancodePageup => Keys.PageUp,
            Scancode.ScancodePagedown => Keys.PageDown,
            Scancode.ScancodeHome => Keys.Home,
            Scancode.ScancodeEnd => Keys.End,
            Scancode.ScancodeCapslock => Keys.CapsLock,
            Scancode.ScancodeScrolllock => Keys.Scroll,
            Scancode.ScancodeNumlockclear => Keys.NumLock,
            Scancode.ScancodePrintscreen => Keys.PrintScreen,
            Scancode.ScancodePause => Keys.Pause,

            Scancode.ScancodeLshift => Keys.LShiftKey,
            Scancode.ScancodeRshift => Keys.RShiftKey,
            Scancode.ScancodeLctrl => Keys.LControlKey,
            Scancode.ScancodeRctrl => Keys.RControlKey,
            Scancode.ScancodeLalt => Keys.LMenu,
            Scancode.ScancodeRalt => Keys.RMenu,
            Scancode.ScancodeLgui => Keys.LWin,
            Scancode.ScancodeRgui => Keys.RWin,

            _ => Keys.None,
        };

        public void Dispose() => Shutdown();
    }
}
