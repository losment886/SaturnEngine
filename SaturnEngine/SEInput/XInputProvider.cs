using System.Runtime.InteropServices;
using SharpDX.XInput;

namespace SaturnEngine.SEInput
{
    /// <summary>
    /// Windows 平台的 XInput 手柄输入源。相比 SDL 具备更低延迟与原生震动支持，
    /// 仅覆盖手柄，键鼠仍由 Win32 Hook 或 SDL 提供。
    /// </summary>
    public sealed class XInputProvider : IInputProvider
    {
        private const int MaxControllers = 4;
        private readonly Controller[] _controllers = new Controller[MaxControllers];

        public string ProviderName => "XInput";
        public bool IsInitialized { get; private set; }
        public InputDeviceType[] SupportedDevices { get; } = { InputDeviceType.XBoxController, InputDeviceType.Gamepad };

        public void Initialize(nint windowHandle)
        {
            if (IsInitialized)
                return;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("XInput 仅在 Windows 上可用。");

            _controllers[0] = new Controller(UserIndex.One);
            _controllers[1] = new Controller(UserIndex.Two);
            _controllers[2] = new Controller(UserIndex.Three);
            _controllers[3] = new Controller(UserIndex.Four);
            IsInitialized = true;
        }

        public void Shutdown() => IsInitialized = false;

        public void Poll(SEInputState state)
        {
            if (!IsInitialized)
                return;

            for (int i = 0; i < MaxControllers; i++)
            {
                var controller = _controllers[i];
                var g = state.GetOrCreateGamepad(i);

                if (!controller.IsConnected)
                {
                    if (g.IsConnected)
                        g.Reset();
                    continue;
                }

                if (!controller.GetState(out var xstate))
                    continue;

                g.IsConnected = true;
                if (string.IsNullOrEmpty(g.Name))
                    g.Name = $"XInput Controller {i}";

                var pad = xstate.Gamepad;
                var flags = pad.Buttons;

                g.Buttons[(int)SEGamepadButton.A] = flags.HasFlag(GamepadButtonFlags.A);
                g.Buttons[(int)SEGamepadButton.B] = flags.HasFlag(GamepadButtonFlags.B);
                g.Buttons[(int)SEGamepadButton.X] = flags.HasFlag(GamepadButtonFlags.X);
                g.Buttons[(int)SEGamepadButton.Y] = flags.HasFlag(GamepadButtonFlags.Y);
                g.Buttons[(int)SEGamepadButton.Back] = flags.HasFlag(GamepadButtonFlags.Back);
                g.Buttons[(int)SEGamepadButton.Start] = flags.HasFlag(GamepadButtonFlags.Start);
                g.Buttons[(int)SEGamepadButton.LeftStick] = flags.HasFlag(GamepadButtonFlags.LeftThumb);
                g.Buttons[(int)SEGamepadButton.RightStick] = flags.HasFlag(GamepadButtonFlags.RightThumb);
                g.Buttons[(int)SEGamepadButton.LeftShoulder] = flags.HasFlag(GamepadButtonFlags.LeftShoulder);
                g.Buttons[(int)SEGamepadButton.RightShoulder] = flags.HasFlag(GamepadButtonFlags.RightShoulder);
                g.Buttons[(int)SEGamepadButton.DPadUp] = flags.HasFlag(GamepadButtonFlags.DPadUp);
                g.Buttons[(int)SEGamepadButton.DPadDown] = flags.HasFlag(GamepadButtonFlags.DPadDown);
                g.Buttons[(int)SEGamepadButton.DPadLeft] = flags.HasFlag(GamepadButtonFlags.DPadLeft);
                g.Buttons[(int)SEGamepadButton.DPadRight] = flags.HasFlag(GamepadButtonFlags.DPadRight);
                // XInput 无 Guide 键的公开 API，保持为未按下。
                g.Buttons[(int)SEGamepadButton.Guide] = false;

                g.Axes[(int)SEGamepadAxis.LeftX] = Normalize(pad.LeftThumbX);
                g.Axes[(int)SEGamepadAxis.LeftY] = Normalize(pad.LeftThumbY);
                g.Axes[(int)SEGamepadAxis.RightX] = Normalize(pad.RightThumbX);
                g.Axes[(int)SEGamepadAxis.RightY] = Normalize(pad.RightThumbY);
                g.Axes[(int)SEGamepadAxis.LeftTrigger] = pad.LeftTrigger / 255.0f;
                g.Axes[(int)SEGamepadAxis.RightTrigger] = pad.RightTrigger / 255.0f;
            }
        }

        private static float Normalize(short v) => v < 0 ? v / 32768.0f : v / 32767.0f;

        public void SetVibration(int gamepadIndex, float leftMotor, float rightMotor)
        {
            if (!IsInitialized || (uint)gamepadIndex >= MaxControllers)
                return;

            var controller = _controllers[gamepadIndex];
            if (!controller.IsConnected)
                return;

            controller.SetVibration(new Vibration
            {
                LeftMotorSpeed = (ushort)(Math.Clamp(leftMotor, 0f, 1f) * ushort.MaxValue),
                RightMotorSpeed = (ushort)(Math.Clamp(rightMotor, 0f, 1f) * ushort.MaxValue),
            });
        }

        public IEnumerable<InputDeviceInfo> EnumerateDevices()
        {
            if (!IsInitialized)
                yield break;

            for (int i = 0; i < MaxControllers; i++)
            {
                if (!_controllers[i].IsConnected)
                    continue;

                yield return new InputDeviceInfo
                {
                    DeviceType = InputDeviceType.XBoxController,
                    DeviceName = $"XInput Controller {i}",
                    DeviceSTC = (ulong)i,
                    IsConnected = true,
                };
            }
        }

        public void Dispose() => Shutdown();
    }
}
