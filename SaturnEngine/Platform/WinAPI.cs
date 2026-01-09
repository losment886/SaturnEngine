
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.Performance;
using SaturnEngine.SEInput;
using SaturnEngine.SEMath;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SaturnEngine.Platform
{
    public unsafe class WinAPI
    {
        public static readonly Guid DXGI_DEBUG_ALL = new Guid(0xe48ae283, 0xda80, 0x490b, 0x87, 0xe6, 0x43, 0xe9, 0xa9, 0xcf, 0xda, 0x8);
        public static readonly Guid DXGI_DEBUG_DX = new Guid(0x35cdd7fc, 0x13b2, 0x421d, 0xa5, 0xd7, 0x7e, 0x44, 0x51, 0x28, 0x7d, 0x64);
        public static readonly Guid DXGI_DEBUG_DXGI = new Guid(0x25cddaa4, 0xb1c6, 0x47e1, 0xac, 0x3e, 0x98, 0x87, 0x5b, 0x5a, 0x2e, 0x2a);
        public static readonly Guid DXGI_DEBUG_APP = new Guid(0x6cd6e01, 0x4219, 0x4ebd, 0x87, 0x9, 0x27, 0xed, 0x23, 0x36, 0xc, 0x62);

        /// <summary>
        /// Windows下的输入
        /// </summary>
        public unsafe partial class InputWin
        {
            /// <summary>
            /// 手柄类，用于获取和设置手柄输入，震动等功能，写得好像有点怪异
            /// </summary>
            public class JoyStick
            {
                public static string[] GetJoySticks()
                {
                    var di = new DirectInput();
                    string[] s;// = new string[0];
                    var l = gldi = di.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AllDevices).ToArray();
                    s = new string[l.Length];
                    for (int i = 0; i < l.Length; i++)
                    {
                        s[i] = $"{l[i].ProductName}-|-{l[i].InstanceName}";
                    }
                    return s;
                }
                public static DeviceInstance[] gldi = new DeviceInstance[0];
                public static int NIndex = 0;
                static bool usin = false;
                static bool last = false;
                public static bool SetWhichJoyStick(int index, int freq = 250)
                {
                    var di = new DirectInput();
                    if (gldi.Length <= 0)
                    {
                        gldi = di.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AllDevices).ToArray();
                    }
                    

                    if (index >= gldi.Length)
                        return false;
                    usin = false;
                    NIndex = index;


                    var d = gldi[index];
                    var je = new Joystick(di, d.InstanceGuid);
                    je.Acquire();
                    var dev = new Device(je.NativePointer);
                    Controller c = new Controller((UserIndex)index);
                    Vibration v = new Vibration();
                    SEThread st = new SEThread();
                    st= Dispatcher.CreateThreadORG(PPFC, ThreadPriority.Normal);
                    st.SetFPS(1000);
                    //Console.WriteLine("控制器输入！！！");
                    return true;
                    void PPFC()
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        sw.Start();
                        JoystickState js;
                        long ot = 0;
                        long nt = 0;
                        long et = 0;
                        double dly = 0;
                        if (last)
                        {
                            while (last)//设置延迟等待上一个手柄管控线程退出
                            {
                                Dispatcher.Delay(0.01);
                            }
                            Dispatcher.Delay(0.01);
                        }
                        usin = true;
                        last = true;
                        while (GVariables.EngineRunning && usin)
                        {
                            nt = sw.ElapsedMilliseconds;
                            try
                            {
                                js = je.GetCurrentState();
                            }
                            catch
                            {
                                usin = false;
                                NIndex = -1;
                                last = false;
                                //Console.WriteLine("控制器离线");
                                return;
                            }
                            v.LeftMotorSpeed = BasicInput.JoyStickLeftMotorSpeed;
                            v.RightMotorSpeed = BasicInput.JoyStickRightMotorSpeed;
                            for (int i = 260; i < 280; i++)
                            {
                                BasicInput.kpool[i] = false;
                            }
                            //Controller c = new Controller();
                            if (BasicInput.Focued)
                            {
                                if (BasicInput.cache)
                                {
                                    //BasicInput.kpool
                                    BasicInput.Eviin = true;
                                    //BasicInput.cac.Push();
                                    switch (js.PointOfViewControllers[0])
                                    {
                                        case 0:
                                            BasicInput.cac.Push(new KeyValuePair<Keys, object>(Keys.JoyStickUp, true));
                                            //BasicInput.kpool[(int)Keys.JoyStickUp] = true;
                                            break;
                                        case 4500:
                                            BasicInput.cac.Push(new KeyValuePair<Keys, object>(Keys.JoyStickUpRight, true));
                                            break;
                                        case 9000:
                                            BasicInput.cac.Push(new KeyValuePair<Keys, object>(Keys.JoyStickRight, true));
                                            break;
                                        case 13500:
                                            BasicInput.cac.Push(new KeyValuePair<Keys, object>(Keys.JoyStickDownRight, true));
                                            break;
                                        case 18000:
                                            BasicInput.cac.Push(new KeyValuePair<Keys, object>(Keys.JoyStickDown, true));
                                            break;
                                        case 22500:
                                            BasicInput.cac.Push(new KeyValuePair<Keys, object>(Keys.JoyStickDownLeft, true));
                                            break;
                                        case 27000:
                                            BasicInput.cac.Push(new KeyValuePair<Keys, object>(Keys.JoyStickLeft, true));
                                            break;
                                        case 31500:
                                            BasicInput.cac.Push(new KeyValuePair<Keys, object>(Keys.JoyStickUpLeft, true));
                                            break;

                                    }
                                }
                                else
                                {

                                    switch (js.PointOfViewControllers[0])
                                    {
                                        case 0:
                                            BasicInput.kpool[(int)Keys.JoyStickUp] = true;
                                            break;
                                        case 4500:
                                            BasicInput.kpool[(int)Keys.JoyStickUpRight] = true;
                                            break;
                                        case 9000:
                                            BasicInput.kpool[(int)Keys.JoyStickRight] = true;
                                            break;
                                        case 13500:
                                            BasicInput.kpool[(int)Keys.JoyStickDownRight] = true;
                                            break;
                                        case 18000:
                                            BasicInput.kpool[(int)Keys.JoyStickDown] = true;
                                            break;
                                        case 22500:
                                            BasicInput.kpool[(int)Keys.JoyStickDownLeft] = true;
                                            break;
                                        case 27000:
                                            BasicInput.kpool[(int)Keys.JoyStickLeft] = true;
                                            break;
                                        case 31500:
                                            BasicInput.kpool[(int)Keys.JoyStickUpLeft] = true;
                                            break;

                                    }
                                }
                            }
                            else
                            {
                                v.LeftMotorSpeed = 0;
                                v.RightMotorSpeed = 0;

                            }
                            c.SetVibration(v);
                            st.WaitForFPS();

                            ot = nt;
                        }
                        last = false;
                        usin = false;
                    }
                }
            }
            public class Mouse
            {
                /// <summary>
                /// 鼠标钩子注册码
                /// </summary>
                public const int WH_MOUSE_LL = 14;
                /// <summary>
                /// 鼠标移动
                /// </summary>
                public const int WM_MOUSEMOVE = 0x200;
                /// <summary>
                /// 鼠标左键按下
                /// </summary>
                public const int WM_LBUTTONDOWN = 0x201;
                /// <summary>
                /// 鼠标左键抬起
                /// </summary>
                public const int WM_LBUTTONUP = 0x202;
                /// <summary>
                /// 鼠标左键双击
                /// </summary>
                public const int WM_LBUTTONDBLCLK = 0x203;
                /// <summary>
                /// 鼠标右键按下
                /// </summary>
                public const int WM_RBUTTONDOWN = 0x204;
                /// <summary>
                /// 鼠标右键抬起
                /// </summary>
                public const int WM_RBUTTONUP = 0x205;
                /// <summary>
                /// 鼠标右键双击
                /// </summary>
                public const int WM_RBUTTONDBLCLK = 0x206;
                /// <summary>
                /// 鼠标中键按下
                /// </summary>
                public const int WM_MBUTTONDOWN = 0x207;
                /// <summary>
                /// 鼠标中键抬起
                /// </summary>
                public const int WM_MBUTTONUP = 0x208;
                /// <summary>
                /// 鼠标中键双击
                /// </summary>
                public const int WM_MBUTTONDBLCLK = 0x209;
                /// <summary>
                /// 鼠标滚轮滚动
                /// </summary>
                public const int WM_MOUSEWHEEL = 0x20A;
                /// <summary>
                /// 鼠标侧键按下
                /// </summary>
                public const int WM_XBUTTONDOWN = 0x20B;
                /// <summary>
                /// 鼠标侧键释放
                /// </summary>
                public const int WM_XBUTTONUP = 0x20C;
                /// <summary>
                /// 鼠标侧键双击
                /// </summary>
                public const int WM_XBUTTONDBLCLK = 0x20D;
                /// <summary>
                /// Hook是否有效
                /// </summary>
                public static bool IsHookInvoke { get; set; }

                [StructLayout(LayoutKind.Sequential)]
                public struct MouseEvent
                {
                    public POINT pt;
                    public int mouseData;
                    public int flags;
                    public int time;
                    public int dwExtraInfo;
                }
                public delegate int MouseEventProcess(int wParam, MouseEvent me);
                /// <summary>
                /// 传递函数
                /// </summary>
                public static event MouseEventProcess MouseProcess;
                private static HookProc HP;
                public static IntPtr HookID = IntPtr.Zero;
                /// <summary>
                /// 设置Hook是否传递
                /// </summary>
                public static bool AllowPass { get; set; } = true;
                public static bool InstallHook()
                {
                    if (!IsHookInvoke || HP == null || HookID == 0)
                    {
                        HP = new HookProc(MouseLoop);
                        HookID = SetWindowsHookEx(WH_MOUSE_LL, HP, Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]), 0);
                        IsHookInvoke = true;
                        if (HookID == 0)
                        {
                            IsHookInvoke = false;
                            HP = null;
                            SELogger.Warn("鼠标钩子安装失败！");
                        }
                        else
                        {
                            SELogger.Log("鼠标钩子安装成功！");
                        }
                    }
                    return IsHookInvoke;
                }
                public static bool UninstallHook()
                {
                    if (IsHookInvoke && HP != null && HookID != 0)
                    {
                        int ok = UnhookWindowsHookEx(HookID);
                        if (ok != 0)
                        {
                            HP = null;
                            IsHookInvoke = false;
                            HookID = 0;
                            SELogger.Log("鼠标钩子卸载成功！");
                        }
                        return ok != 0;
                    }
                    SELogger.Warn("鼠标钩子卸载失败！");
                    return false;
                }
                private static int MouseLoop(int nCode, int wParam, IntPtr lParam)
                {

                    if (MouseProcess != null)
                    {
                        MouseEvent mv = (MouseEvent)Marshal.PtrToStructure(lParam, typeof(MouseEvent));
                        int v = MouseProcess.Invoke(wParam, mv);
                        if (!AllowPass || v != 0)
                            return 1;
                    }

                    //Console.WriteLine($"KEY:nCode{nCode} ||wParam:{wParam}");
                    return CallNextHookEx(HookID, nCode, wParam, lParam);
                }
            }
            public class Keyboard
            {
                /// <summary>
                /// 键盘钩子注册码
                /// </summary>
                public const int WH_KETBOARD_LL = 13;
                /// <summary>
                /// 键盘被按下
                /// </summary>
                public const int WM_KEYDOWN = 0x100;
                /// <summary>
                /// 键盘被松开
                /// </summary>
                public const int WM_KEYUP = 0x101;
                /// <summary>
                /// 键盘被按下，这个是系统键被按下，例如Alt、Ctrl等键
                /// </summary>
                public const int WM_SYSKEYDOWN = 0x104;
                /// <summary>
                /// 键盘被松开，这个是系统键被松开，例如Alt、Ctrl等键
                /// </summary>
                public const int WM_SYSKEYUP = 0x105;
                /// <summary>
                /// Hook是否有效
                /// </summary>
                public static bool IsHookInvoke { get; set; }
                [StructLayout(LayoutKind.Sequential)]
                public struct KeyboardEvent
                {
                    public int vkCode; //表示一个在1到254间的虚似键盘码 
                    public int scanCode; //表示硬件扫描码 
                    public int flags;
                    public int time;
                    public int dwExtraInfo;
                }
                public delegate int KeyboardEventProcess(int wParam, KeyboardEvent me);
                /// <summary>
                /// 传递函数
                /// </summary>
                public static event KeyboardEventProcess KeyboardProcess;
                private static HookProc HP;
                public static IntPtr HookID = IntPtr.Zero;
                /// <summary>
                /// 设置Hook是否传递
                /// </summary>
                public static bool AllowPass { get; set; } = true;
                public static bool InstallHook()
                {
                    if (!IsHookInvoke || HP == null || HookID == 0)
                    {
                        HP = new HookProc(KeyBoardLoop);
                        HookID = SetWindowsHookEx(WH_KETBOARD_LL, HP, Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]), 0);
                        IsHookInvoke = true;
                        if (HookID == 0)
                        {
                            IsHookInvoke = false;
                            HP = null;
                            SELogger.Warn("键盘钩子安装失败！");
                        }
                        else
                        {
                            SELogger.Log("键盘钩子安装成功！");
                        }
                    }
                    return IsHookInvoke;
                }
                public static bool UninstallHook()
                {
                    if (IsHookInvoke && HP != null && HookID != 0)
                    {
                        int ok = UnhookWindowsHookEx(HookID);
                        if (ok != 0)
                        {
                            HP = null;
                            IsHookInvoke = false;
                            HookID = 0;
                            SELogger.Log("键盘钩子卸载成功！");
                        }
                        return ok != 0;
                    }
                    SELogger.Warn("键盘钩子卸载失败！");
                    return false;
                }
                private static int KeyBoardLoop(int nCode, int wParam, IntPtr lParam)
                {

                    if (KeyboardProcess != null)
                    {
                        KeyboardEvent mv = (KeyboardEvent)Marshal.PtrToStructure(lParam, typeof(KeyboardEvent));
                        int v = KeyboardProcess.Invoke(wParam, mv);
                        if (!AllowPass || v != 0)
                            return 1;
                    }

                    //Console.WriteLine($"KEY:nCode{nCode} ||wParam:{wParam}");
                    return CallNextHookEx(HookID, nCode, wParam, lParam);

                }
            }
            /// <summary>
            /// Hook处理类
            /// </summary>
            /// <param name="nCode">如果代码小于零，则挂钩过程必须将消息传递给CallNextHookEx函数，而无需进一步处理，并且应返回CallNextHookEx返回的值。</param>
            /// <param name="wParam">基础事件值</param>
            /// <param name="lParam">进阶事件指针</param>
            /// <returns></returns>
            public delegate int HookProc(int nCode, int wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern int SetCursorPos(int x, int y);

            /// <summary>
            /// 设钩子
            /// </summary>
            /// <param name="idHook">要安装的挂钩过程的类型。</param>
            /// <param name="lpfn">指向挂钩过程的指针。 如果 dwThreadId 参数为零或指定由其他进程创建的线程的标识符， 则 lpfn 参数必须指向 DLL 中的挂钩过程。 否则， lpfn 可以指向与当前进程关联的代码中的挂钩过程。</param>
            /// <param name="hInstance">DLL 的句柄，包含 lpfn 参数指向的挂钩过程。 如果 dwThreadId 参数指定当前进程创建的线程，并且挂钩过程位于与当前进程关联的代码中，则必须将 hMod 参数设置为 NULL。</param>
            /// <param name="threadId">要与之关联的挂钩过程的线程的标识符。 对于桌面应用，如果此参数为零，则挂钩过程与调用线程在同一桌面中运行的所有现有线程相关联。</param>
            /// <returns>如果函数成功，则返回值是挂钩过程的句柄。如果函数失败，则返回值为 NULL。 要获得更多的错误信息，请调用 GetLastError。</returns>
            [DllImport("user32.dll", EntryPoint = "SetWindowsHookEx", SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hInstance, int threadId);
            /// <summary>
            /// 删钩子
            /// </summary>
            /// <param name="idHook">要移除的挂钩的句柄。 此参数是由先前调用 SetWindowsHookEx 获取的挂钩句柄。</param>
            /// <returns>如果该函数成功，则返回值为非零值。如果函数失败，则返回值为零。 要获得更多的错误信息，请调用 GetLastError。</returns>
            [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "UnhookWindowsHookEx", SetLastError = true)]
            private static extern int UnhookWindowsHookEx(IntPtr idHook);
            /// <summary>
            /// 调用下一个钩子
            /// </summary>
            /// <param name="idHook">忽略此参数。</param>
            /// <param name="nCode">传递给当前挂钩过程的挂钩代码。 下一个挂钩过程使用此代码来确定如何处理挂钩信息。</param>
            /// <param name="wParam">传递给当前挂钩过程的 wParam 值。 此参数的含义取决于与当前挂钩链关联的挂钩类型。</param>
            /// <param name="lParam">传递给当前挂钩过程的 lParam 值。 此参数的含义取决于与当前挂钩链关联的挂钩类型。</param>
            /// <returns>类型： LRESULT  此值由链中的下一个挂钩过程返回。 当前挂钩过程还必须返回此值。 返回值的含义取决于挂钩类型。 有关详细信息，请参阅各个挂钩过程的说明。</returns>
            [DllImport("user32.dll", EntryPoint = "CallNextHookEx", SetLastError = true)]
            private static extern int CallNextHookEx(IntPtr idHook, int nCode, nint wParam, IntPtr lParam);
            //Keys from WinForm


        }
    }
}
