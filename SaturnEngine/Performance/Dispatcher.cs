using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Global;
using SaturnEngine.Management;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using static SaturnEngine.Performance.Dispatcher;

namespace SaturnEngine.Performance
{
    /// <summary>
    /// 自调度Thread类
    /// </summary>
    public class SEThread : SEBase, IDisposable
    {
        public Thread _Main;
        public ThreadPriority _TP;
        /// <summary>
        /// 当前线程所在CPU核心
        /// </summary>
        public int Core;
        /// <summary>
        /// 当前线程所在CPU线程(与超线程相关)
        /// </summary>
        public int Threads;
        public nint TID;
        public nint WShandle = 0;
        public object Tag;
        int fps = 0;
        double dly = 0;
        long ppframe = 0;
        long wsframe = 0;
        double lasttim;
        bool hasused = false;
        Stopwatch sw;
        public int FrameRange { get; set; } = 5;
        public double Currentfps { get; internal set; }
        public void SetFPS(int f)
        {
            fps = f;
            if (f != 0)
                dly = 1.0 / f;
            else
                dly = 0;
            ppframe = f / FrameRange;
            if (ppframe < 1)
                ppframe = 1;
        }
        public int GetFPS()
        {
            return fps;
        }
        double lstint = 0;
        public void WaitForFPS()
        {
            if (!hasused)
            {
                //lasttim = Stopwatch.GetTimestamp();
                hasused = true;
                lasttim = sw.Elapsed.TotalMilliseconds;
            }
            else
            {
                if (fps != 0)
                {

                    wsframe++;
                    if (wsframe >= ppframe)
                    {
                        double fpss = wsframe / ((sw.Elapsed.TotalMilliseconds - lasttim) / 1000);
                        Currentfps = fpss;
                        wsframe = 0;
                        lasttim = sw.Elapsed.TotalMilliseconds;
                        if (fpss > fps)
                        {
                            dly += Func(fpss - fps);
                        }
                        else if (fpss < fps)
                        {
                            dly -= Func(fps - fpss);
                        }
                        if (dly < 0) dly = 0;
                    }

                    double ddy = sw.Elapsed.TotalMilliseconds - lstint;
                    lstint = sw.Elapsed.TotalMilliseconds;
                    if (ddy < dly)
                        Sleep(dly - ddy);
                }
            }
        }
        private double Func(double x)
        {
            //return 0.00001 + 0.00099 * ((1 - double.Pow(double.E, (-0.008 * x))) / (1 + double.Pow(double.E, (-0.008 * (x - 200)))));
            return 0.001 / (1 + Math.Exp(-0.02 * (x - 100))) + 0.00001;
        }
        private double F(double x)
        {
            return 0.005 / (1 + Math.Exp(-0.01 * (x - 400)));
        }
        public SEThread()
        {

        }
        public void Start()
        {
            Init();

            _Main.Start(Tag);
            TID = _Main.ManagedThreadId;
        }
        public void Init()
        {
            sw = Stopwatch.StartNew();
            if (GVariables.OS == OS.Windows)
            {
                try
                {
                    WShandle = WindowsAPI.CreateWaitableTimerEx(0, null, 3, 2031619U);//高精度
                    if (WShandle == nint.Zero)
                    {
                        //Console.WriteLine();
                        SELogger.Warn("创建高精度计时器失败".GetInCurrLang());
                    }
                }
                catch
                {
                    WShandle = 0;
                }
            }
        }
        public void Sleep(double s)
        {
            if (GVariables.OS == OS.Windows)
            {
                if (WShandle != nint.Zero)
                {
                    if (WindowsAPI.SetWaitableTimerEx(WShandle, WindowsAPI.GetFileTime(s), 0, null, default, 0, 0))
                    {
                        WindowsAPI.WaitForSingleObject(WShandle, uint.MaxValue);
                    }
                    else
                    {
                        int ms = (int)double.Floor(s * 1000);
                        if (ms == 0)
                        {
                            Delay(s);
                        }
                        else
                        {
                            double last = (s * 1000 - ms) / 1000;
                            Delay(last);
                            WindowsAPI.SleepWin(ms, false);
                        }
                    }
                }
                else
                {
                    int ms = (int)double.Floor(s * 1000);
                    if (ms == 0)
                    {
                        Delay(s);
                    }
                    else
                    {
                        double last = (s * 1000 - ms) / 1000;
                        Delay(last);
                        WindowsAPI.SleepWin(ms, false);
                    }
                }
            }
            else
            {
                int ms = (int)double.Floor(s * 1000);
                if (ms == 0)
                {
                    Delay(s);
                }
                else
                {
                    double last = (s * 1000 - ms) / 1000;
                    Delay(last);
                    Thread.Sleep(ms);
                }
            }
        }
        public void Dispose()
        {
            sw.Stop();
            sw = null;
            if (GVariables.OS == OS.Windows)
                if (WShandle != nint.Zero)
                    WindowsAPI.CloseHandle(WShandle);
        }
    }
    /// <summary>
    /// 管理CPU调度，TIP：如果改变CPU核心数会造成调度失常
    /// </summary>
    public unsafe class Dispatcher
    {
        /// <summary>
        /// 检查给定的线程是否为当前执行的线程。
        /// </summary>
        /// <param name="thread">要检查的SEThread实例。</param>
        /// <returns>如果给定的线程是当前正在执行的线程，则返回true；否则返回false。</returns>
        public static bool CheckThread(SEThread thread)
        {
            return Thread.CurrentThread.ManagedThreadId == thread.TID;
        }

        /// <summary>
        /// 检查当前线程是否为主线程。
        /// </summary>
        /// <returns>如果当前线程是主线程，则返回true；否则返回false。</returns>
        public static bool CheckMainThread()
        {
            return CheckThread(GVariables.ThisGameHost?.MainThread ?? new SEThread());
        }
        /// <summary>
        /// 精准延迟，但代价是CPU消耗高，使用Sleep函数自动调度
        /// </summary>
        /// <param name="s"></param>
        public static void Delay(double s)
        {
            var sw = Stopwatch.StartNew();

            //var spit = new SpinWait();
            /*
            int ms = (int)(s * 1000);
            if(ms > 2)
            {
                Thread.Sleep(ms);
                s -= ms;
            }
            */
            long ttt = (long)(s * Stopwatch.Frequency);
            while (sw.ElapsedTicks < ttt)
            {
                //spit.SpinOnce();
                Thread.SpinWait((int)(Stopwatch.Frequency * 0.000001));
            }
            sw.Stop();
        }


        /// <summary>
        /// 请优先使用SEThread.Sleep函数
        /// </summary>
        /// <param name="s"></param>
        public static void Sleep(double s)
        {
            int ms = (int)double.Floor(s * 1000);
            if (ms == 0)
            {
                Delay(s);
            }
            else
            {
                double last = (s * 1000 - ms) / 1000;
                Delay(last);
                Thread.Sleep(ms);
            }
        }
        ulong ThrID = 0;
        /// <summary>
        /// 仅显示由调度器创建的线程
        /// </summary>
        public static ulong ThreadsOnRunning = 0;
        /// <summary>
        /// 由CPU线程与子线程ID号索引子线程
        /// </summary>
        public static List<SEThread> Thrs { get; private set; }
        public static ulong[] UsagePerThread;

        public struct PresentRun
        {
            public int ThreadID;
            public ThreadStart ts;
        }
        public static SEThread CreateThreadFromExistedThread()
        {

            SEThread s = new SEThread();
            s._Main = Thread.CurrentThread;
            s.TID = s._Main.ManagedThreadId;
            s.Threads = -1;
            s._TP = s._Main.Priority;
            PresentRun pr = new PresentRun();
            pr.ThreadID = 0;
            pr.ts = null;
            s.Tag = pr;
            s.Init();
            return s;
        }
        public static SEThread CreateThreadORG(ThreadStart ts, ThreadPriority tp)
        {
            PresentRun pr = new PresentRun();
            pr.ts = ts;
            if (GVariables.OS == OS.Windows)
            {
                SEThread st = new SEThread();
                st.Threads = -1;
                st._Main = new Thread(SetThreadRun);
                st._Main.Priority = tp;
                pr.ThreadID = st.Threads;
                st.Tag = pr;
                st._TP = tp;
                return st;
            }
            else
            {
                SEThread st = new SEThread();
                st.Threads = -1;
                st._Main = new Thread(SetThreadRun);
                st._Main.Priority = tp;
                pr.ThreadID = st.Threads;

                st.Tag = pr;

                st._TP = tp;

                return st;
            }
        }
        public static SEThread CreateThread(ThreadStart ts, ThreadPriority tp, int tid)
        {
            PresentRun pr = new PresentRun();
            pr.ts = ts;
            if (GVariables.OS == OS.Windows)
            {
                SEThread st = new SEThread();
                st.Threads = tid;
                st._Main = new Thread(SetThreadRun);
                st._Main.Priority = tp;
                pr.ThreadID = st.Threads;
                st.Tag = pr;
                st._TP = tp;
                return st;
            }
            else
            {
                SEThread st = new SEThread();
                st.Threads = -1;
                st._Main = new Thread(SetThreadRun);
                st._Main.Priority = tp;
                pr.ThreadID = st.Threads;
                st.Tag = pr;
                st._TP = tp;
                return st;
            }
            return null;
        }
        [Obsolete("混合架构处理器MD大小核心交错排布，懒得每个核心设置了，哪天看看能不能动态获取大小核心排布状况，要么直接设置核心，要么用系统调度")]
        public static SEThread CreateThread(ThreadStart ts, ThreadPriority tp, bool moveable = false, bool TransferIntensive = false)
        {
            PresentRun pr = new PresentRun();
            pr.ts = ts;
            if (GVariables.OS == OS.Windows)
            {
                if (TransferIntensive)
                {
                    if (GVariables.CpuType == CPUType.AMD_Ryzen5_X3D || GVariables.CpuType == CPUType.AMD_Ryzen7_X3D || GVariables.CpuType == CPUType.AMD_Ryzen9_X3D)
                    {
                        //都X3D了，还有什么小核心（
                        int id = 0;
                        ulong usg = 10000;
                        for (int i = 0; i < UsagePerThread.Length; i++)
                        {
                            if (ScorePerCore[i] == 3)
                            {
                                if (UsagePerThread[i] < usg)
                                {
                                    id = i;
                                    usg = UsagePerThread[i];
                                }
                            }
                        }
                        SEThread st = new SEThread();
                        st.Threads = id;
                        st._Main = new Thread(SetThreadRun);
                        st._Main.Priority = tp;
                        pr.ThreadID = st.Threads;
                        st.Tag = pr;
                        st._TP = tp;
                        return st;
                    }
                    else
                    {
                        if (GVariables.CpuVendor == CpuVendor.AMD)
                        {
                            if (moveable)
                            {
                                SEThread st = new SEThread();
                                st.Threads = -1;
                                st._Main = new Thread(SetThreadRun);
                                st._Main.Priority = tp;
                                pr.ThreadID = st.Threads;
                                st.Tag = pr;
                                st._TP = tp;
                                return st;
                            }
                            else
                            {
                                int id = 0;
                                ulong usg = 10000;
                                for (int i = 0; i < UsagePerThread.Length; i++)
                                {
                                    if (ScorePerCore[i] == 1)
                                    {
                                        if (UsagePerThread[i] < usg)
                                        {
                                            id = i;
                                            usg = UsagePerThread[i];
                                        }
                                    }
                                }
                                SEThread st = new SEThread();
                                st.Threads = id;
                                st._Main = new Thread(SetThreadRun);
                                st._Main.Priority = tp;
                                pr.ThreadID = st.Threads;
                                st.Tag = pr;
                                st._TP = tp;
                                return st;
                            }
                        }
                        else if (GVariables.CpuVendor == CpuVendor.Intel)
                        {
                            SEThread st = new SEThread();
                            int id = 0;
                            ulong usg = 10000;
                            switch (tp)
                            {
                                case ThreadPriority.Lowest:
                                case ThreadPriority.BelowNormal:
                                case ThreadPriority.Normal:
                                    for (int i = 0; i < UsagePerThread.Length; i++)
                                    {
                                        if (ScorePerCore[i] == 2)
                                        {
                                            if (UsagePerThread[i] < usg)
                                            {
                                                id = i;
                                                usg = UsagePerThread[i];
                                            }
                                        }
                                    }
                                    break;
                                case ThreadPriority.AboveNormal:
                                case ThreadPriority.Highest:
                                    for (int i = 0; i < UsagePerThread.Length; i++)
                                    {
                                        if (ScorePerCore[i] == 1)
                                        {
                                            if (UsagePerThread[i] < usg)
                                            {
                                                id = i;
                                                usg = UsagePerThread[i];
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    st.Threads = -1;
                                    st._Main = new Thread(SetThreadRun);
                                    st._Main.Priority = tp;
                                    pr.ThreadID = st.Threads;
                                    st.Tag = pr;
                                    st._TP = tp;
                                    return st;
                            }
                            st.Threads = id;
                            st._Main = new Thread(SetThreadRun);
                            st._Main.Priority = tp;
                            pr.ThreadID = st.Threads;
                            st.Tag = pr;
                            st._TP = tp;
                            return st;
                        }
                    }
                }
                if (GVariables.CpuVendor == CpuVendor.AMD)
                {
                    if (moveable)
                    {
                        SEThread st = new SEThread();
                        st.Threads = -1;
                        st._Main = new Thread(SetThreadRun);
                        st._Main.Priority = tp;
                        pr.ThreadID = st.Threads;
                        st.Tag = pr;
                        st._TP = tp;
                        return st;
                    }
                    else
                    {
                        int id = 0;
                        ulong usg = 10000;
                        for (int i = 0; i < UsagePerThread.Length; i++)
                        {
                            if (ScorePerCore[i] == 1)
                            {
                                if (UsagePerThread[i] < usg)
                                {
                                    id = i;
                                    usg = UsagePerThread[i];
                                }
                            }
                        }
                        SEThread st = new SEThread();
                        st.Threads = id;
                        st._Main = new Thread(SetThreadRun);
                        st._Main.Priority = tp;
                        pr.ThreadID = st.Threads;
                        st.Tag = pr;
                        st._TP = tp;
                        return st;
                    }
                }
                else if (GVariables.CpuVendor == CpuVendor.Intel)
                {
                    SEThread st = new SEThread();
                    int id = 0;
                    ulong usg = 10000;
                    switch (tp)
                    {
                        case ThreadPriority.Lowest:
                        case ThreadPriority.BelowNormal:
                        case ThreadPriority.Normal:
                            for (int i = 0; i < UsagePerThread.Length; i++)
                            {
                                if (ScorePerCore[i] == 2)
                                {
                                    if (UsagePerThread[i] < usg)
                                    {
                                        id = i;
                                        usg = UsagePerThread[i];
                                    }
                                }
                            }
                            break;
                        case ThreadPriority.AboveNormal:
                        case ThreadPriority.Highest:
                            for (int i = 0; i < UsagePerThread.Length; i++)
                            {
                                if (ScorePerCore[i] == 1)
                                {
                                    if (UsagePerThread[i] < usg)
                                    {
                                        id = i;
                                        usg = UsagePerThread[i];
                                    }
                                }
                            }
                            break;
                        default:
                            st.Threads = -1;
                            st._Main = new Thread(SetThreadRun);
                            st._Main.Priority = tp;
                            pr.ThreadID = st.Threads;
                            st.Tag = pr;
                            st._TP = tp;
                            return st;
                    }
                    st.Threads = id;
                    st._Main = new Thread(SetThreadRun);

                    st._Main.Priority = tp;
                    pr.ThreadID = st.Threads;
                    st.Tag = pr;
                    st._TP = tp;
                    return st;
                }
            }
            else
            {
                SEThread st = new SEThread();
                st.Threads = -1;
                st._Main = new Thread(SetThreadRun);
                st._Main.Priority = tp;
                pr.ThreadID = st.Threads;
                st.Tag = pr;
                st._TP = tp;
                return st;
            }
            return null;
        }

        private static void SetThreadRun(object? o)
        {
            PresentRun pr = (PresentRun)o;
            if (pr.ThreadID > ScorePerCore.Length)
            {
                pr.ThreadID = 0;
            }
            if (GVariables.OS == OS.Windows && pr.ThreadID >= 0)
            {
                if (ScorePerCore.Length > 64)
                {
                    int gp = WindowsAPI.GetGroupIndex(pr.ThreadID);
                    int mask = (1 << (pr.ThreadID % WindowsAPI.GetActiveProcessorCount((short)gp)));
                    WindowsAPI.Group_Affinity ga = new WindowsAPI.Group_Affinity();
                    ga.Mask = mask;
                    ga.Group = (short)gp;
                    if (!WindowsAPI.SetThreadGroupAffinity(WindowsAPI.GetCurrentThread(), &ga, null))
                        throw new Exception();
                }
                else
                {
                    WindowsAPI.SetThreadAffinityMask(WindowsAPI.GetCurrentThread(), new nint(1 << pr.ThreadID));
                }

                pr.ts.Invoke();
            }
            else
            {
                pr.ts.Invoke();
            }

        }
        public unsafe class WindowsAPI
        {
            [DllImport("kernel32.dll")]
            public static extern IntPtr GetCurrentThread();
            [DllImport("kernel32.dll")]
            public static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwMask);
            [DllImport("kernel32.dll")]
            public static extern bool SetThreadGroupAffinity(nint hThread, Group_Affinity* GA, Group_Affinity* PreViousGA);
            [DllImport("kernel32.dll")]
            public static extern short GetActiveProcessorGroupCount();
            [DllImport("kernel32.dll")]
            public static extern int GetActiveProcessorCount(short groupNUM);
            [DllImport("kernel32.dll", EntryPoint = "SleepEx")]
            public static extern int SleepWin(int ms, bool alr);
            [DllImport("kernel32.dll")]
            public static extern nint CreateWaitableTimerEx(nint a, string? nm, int flg, uint aces);
            [DllImport("kernel32.dll")]
            public static extern bool SetWaitableTimerEx(IntPtr hTimer, in FILETIME lpDueTime, int lPeriod, Action? routine, IntPtr lpArgToCompletionRoutine, IntPtr reason, uint tolerableDelay);
            [DllImport("kernel32.dll")]
            public static extern bool WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
            public static FILETIME GetFileTime(TimeSpan ts)
            {
                ulong ul = unchecked((ulong)-ts.Ticks);
                return new FILETIME { dwHighDateTime = (int)(ul >> 32), dwLowDateTime = (int)(ul & 0xFFFFFFFF) };
            }
            public static FILETIME GetFileTime(double s)
            {
                TimeSpan ts = TimeSpan.FromSeconds(s);
                ulong ul = unchecked((ulong)-ts.Ticks);
                return new FILETIME { dwHighDateTime = (int)(ul >> 32), dwLowDateTime = (int)(ul & 0xFFFFFFFF) };
            }

            [DllImport("kernel32.dll")]
            public static extern bool CloseHandle(IntPtr hObject);
            public struct Group_Affinity
            {
                public long Mask;
                public short Group;
                public short[] Resseved = new short[3];

                public Group_Affinity()
                {
                }
            }

            public static int GetGroupIndex(int tid)
            {
                int count = tid + 1;
                short groupCount = GetActiveProcessorGroupCount();
                for (short i = 0; i < groupCount; i++)
                {
                    count = count - GetActiveProcessorCount(i);
                    if (count <= 0)
                    {
                        return i;
                    }
                }
                return -1; // tid is invalid
            }
        }
        public class MacOSAPI
        {
            /// <summary>
            /// 获取线程ID
            /// </summary>
            /// <returns></returns>
            [DllImport("libc", SetLastError = true)]
            public static extern ulong pthread_self();
            /// <summary>
            /// 设置线程所在核心
            /// </summary>
            /// <param name="thread"></param>
            /// <param name="cpusetsize"></param>
            /// <param name="mask"></param>
            /// <returns></returns>
            [DllImport("libc")]
            public static extern int pthread_setaffinity_np(ulong thread, IntPtr cpusetsize, ref M_cpu_set_t mask);
            [StructLayout(LayoutKind.Sequential)]
            public struct M_cpu_set_t
            {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024 / 8)]
                public byte[] bits;
            }






        }
        public class LinuxAPI
        {

            [DllImport("libc", SetLastError = true)]
            public static extern int sched_setaffinity(int pid, IntPtr cpusetsize, ref L_cpu_set_t mask);
            [StructLayout(LayoutKind.Sequential)]
            public struct L_cpu_set_t
            {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024 / 8)]
                public byte[] bits;
            }
        }
        /* 调度管理
         * tip：手机骁龙处理器最大核心为最后一个id
         * intel小核心放在后面，大核心在前面
         * amd高端双ccd要分开调度，x3d暂时不到
         * amd avx512加速
         * intel只有 10-11th有avx512
         */
        /// <summary>
        /// 0为未知，1为性能核心（线程），2为能效核心，3为大3缓核心（没有相关处理器给我实验，不到排在哪里）
        /// </summary>
        public static int[] ScorePerCore;

        /// <summary>
        /// 将指定线程挂载到指定核心上,核心超出就默认挂载在CPU0，报错则无作为,Linux也许要root权限，MacOS不知道
        /// </summary>
        /// <param name="coreId"></param>
        public static void SetWhichThreadAffinity(nint id, int coreId)
        {
            if (coreId > ScorePerCore.Length)
            {
                coreId = 0;
            }
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var mask = new IntPtr(1 << coreId);
                    WindowsAPI.SetThreadAffinityMask(id, mask);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var mask = new LinuxAPI.L_cpu_set_t { bits = new byte[128] };
                    mask.bits[coreId / 8] |= (byte)(1 << (coreId % 8));
                    LinuxAPI.sched_setaffinity(0, new IntPtr(mask.bits.Length), ref mask);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    ulong threadId = (ulong)id.ToInt64();
                    var mask = new MacOSAPI.M_cpu_set_t { bits = new byte[128] };
                    mask.bits[coreId / 8] |= (byte)(1 << (coreId % 8));
                    MacOSAPI.pthread_setaffinity_np(threadId, new IntPtr(mask.bits.Length), ref mask);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("挂载线程到指定核心出错");
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// 将当前线程挂载到指定核心上,核心超出就默认挂载在CPU0，报错则无作为,Linux也许要root权限，MacOS不知道
        /// </summary>
        /// <param name="coreId"></param>
        public static void SetCurrentThreadAffinity(int coreId)
        {
            if (coreId > ScorePerCore.Length)
            {
                coreId = 0;
            }
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {

                    var mask = new IntPtr(1 << coreId);
                    WindowsAPI.SetThreadAffinityMask(WindowsAPI.GetCurrentThread(), mask);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var mask = new LinuxAPI.L_cpu_set_t { bits = new byte[128] };
                    mask.bits[coreId / 8] |= (byte)(1 << (coreId % 8));
                    LinuxAPI.sched_setaffinity(0, new IntPtr(mask.bits.Length), ref mask);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    ulong threadId = MacOSAPI.pthread_self();
                    var mask = new MacOSAPI.M_cpu_set_t { bits = new byte[128] };
                    mask.bits[coreId / 8] |= (byte)(1 << (coreId % 8));
                    MacOSAPI.pthread_setaffinity_np(threadId, new IntPtr(mask.bits.Length), ref mask);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("挂载线程到指定核心出错");
                Console.WriteLine(ex);
            }
        }

        [Obsolete("Linux最好别用，有BUG")]
        public static void SetThreadAffinity(nint threadId, int coreId)
        {
            if (coreId > ScorePerCore.Length)
            {
                coreId = 0;
            }
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var mask = new IntPtr(1 << coreId);
                    WindowsAPI.SetThreadAffinityMask(threadId, mask);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var mask = new LinuxAPI.L_cpu_set_t { bits = new byte[128] };
                    mask.bits[coreId / 8] |= (byte)(1 << (coreId % 8));
                    LinuxAPI.sched_setaffinity(0, new IntPtr(mask.bits.Length), ref mask);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    ulong threadIdu = (ulong)threadId.ToInt64();
                    var mask = new MacOSAPI.M_cpu_set_t { bits = new byte[128] };
                    mask.bits[coreId / 8] |= (byte)(1 << (coreId % 8));
                    MacOSAPI.pthread_setaffinity_np(threadIdu, new IntPtr(mask.bits.Length), ref mask);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("挂载线程到指定核心出错");
                Console.WriteLine(ex);
            }
        }

        /*
        public static string GetCPUName()
        {
            
            if (GVariables.OS == OS.Windows)
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                    return obj["Name"].ToString();
                return "Unknown";
            }
            else if (GVariables.OS == OS.Linux || GVariables.OS == OS.Android)
            {
                var cpuInfo = File.ReadAllText("/proc/cpuinfo");
                var modelLine = cpuInfo.Split('\n').FirstOrDefault(line => line.StartsWith("model name"));
                return modelLine?.Split(':')[1].Trim() ?? "Unknown";
            }
            else if (GVariables.OS == OS.MacOS)
            {
                var process = new System.Diagnostics.Process()
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/usr/sbin/sysctl",
                        Arguments = "-n machdep.cpu.brand_string",
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    }
                };
                process.Start();
                return process.StandardOutput.ReadToEnd().Trim();
            }
            return "Unsupport";
        }*/
        public static void Init()
        {
            var hi = new Hardware.Info.HardwareInfo();
            //hi.RefreshAll();
            hi.RefreshCPUList();


            var cmp = hi.CpuList[0];//只使用一个CPU
            Console.WriteLine($"CORES:{cmp.NumberOfCores} LGC:{cmp.NumberOfLogicalProcessors} L1 Inst:{cmp.L1InstructionCacheSize} L1 Data:{cmp.L1DataCacheSize} L2:{cmp.L2CacheSize} L3:{cmp.L3CacheSize} FREQ:{cmp.CurrentClockSpeed}MHZ Maun:{cmp.Manufacturer} DESC:{cmp.Description} SockDesc:{cmp.SocketDesignation} Name:{cmp.Name} MaxFreq:{cmp.MaxClockSpeed}MHZ Caption:{cmp.Caption}");

            for (int i = 0; i < cmp.CpuCoreList.Count; i++)
            {
                Console.WriteLine(cmp.CpuCoreList[i]);
            }

            ScorePerCore = new int[Environment.ProcessorCount];
            Thrs = new List<SEThread>(0);
            UsagePerThread = new ulong[Environment.ProcessorCount];

            string nmc = cmp.Name.ToLower();
            if (nmc.Contains("amd"))
            {
                Match m = Regex.Match(nmc, @"\d{4,5}");
                if (m.Success)
                {
                    //Console.WriteLine(m.Value);
                    string vv = m.Value[..^3];
                    //Console.WriteLine(vv);
                    GVariables.CpuVersion = vv;
                }
                else
                {
                    GVariables.CpuVersion = "0";
                }
                GVariables.CpuVendor = CpuVendor.AMD;
                if (nmc.Contains("ryzen"))
                {
                    if (nmc.Contains("threadripper"))
                    {
                        for (int i = 0; i < Environment.ProcessorCount; i++)
                        {
                            ScorePerCore[i] = 1;
                        }
                        if (nmc.Contains("pro"))
                        {
                            GVariables.CpuType = CPUType.AMD_Ryzen_ThreadRipperPro;
                        }
                        else
                        {
                            GVariables.CpuType = CPUType.AMD_Ryzen_ThreadRipper;
                        }
                    }
                    else
                    {
                        if (nmc.Contains("x3d"))
                        {
                            if (nmc.Contains("ryzen 5"))
                            {
                                for (int i = 0; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 3;
                                }
                                GVariables.CpuType = CPUType.AMD_Ryzen5_X3D;
                            }
                            else if (nmc.Contains("ryzen 7"))
                            {
                                for (int i = 0; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 3;
                                }
                                GVariables.CpuType = CPUType.AMD_Ryzen7_X3D;
                            }
                            else if (nmc.Contains("ryzen 9"))
                            {
                                for (int i = 0; i < Environment.ProcessorCount / 2; i++)
                                {
                                    ScorePerCore[i] = 1;
                                }
                                for (int i = Environment.ProcessorCount / 2; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 3;
                                }
                                GVariables.CpuType = CPUType.AMD_Ryzen9_X3D;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < Environment.ProcessorCount; i++)
                            {
                                ScorePerCore[i] = 3;
                            }
                            if (nmc.Contains("ryzen 3"))
                            {
                                GVariables.CpuType = CPUType.AMD_Ryzen3;
                            }
                            else if (nmc.Contains("ryzen 5"))
                            {
                                GVariables.CpuType = CPUType.AMD_Ryzen5;
                            }
                            else if (nmc.Contains("ryzen 7"))
                            {
                                GVariables.CpuType = CPUType.AMD_Ryzen7;
                            }
                            else if (nmc.Contains("ryzen 9"))
                            {
                                GVariables.CpuType = CPUType.AMD_Ryzen9;
                            }
                        }
                    }
                }
                else
                {
                    GVariables.CpuType = CPUType.AMD_APU;
                }
            }
            else if (nmc.Contains("intel"))
            {
                GVariables.CpuVendor = CpuVendor.Intel;
                if (nmc.Contains("core"))
                {
                    if (nmc.Contains("ultra"))
                    {
                        Match m = Regex.Match(nmc, @"\d{3,5}");
                        if (m.Success)
                        {
                            //Console.WriteLine(m.Value);
                            string vv = m.Value[..^2];
                            //Console.WriteLine(vv);
                            GVariables.CpuVersion = vv;
                        }
                        else
                        {
                            GVariables.CpuVersion = "0";
                        }
                        if (nmc.Contains("ultra 5"))
                        {
                            for (int i = 0; i < 6; i++)
                            {
                                ScorePerCore[i] = 1;
                            }
                            for (int i = 6; i < Environment.ProcessorCount; i++)
                            {
                                ScorePerCore[i] = 2;
                            }
                            GVariables.CpuType = CPUType.Intel_CoreUltra_5;
                        }
                        else if (nmc.Contains("ultra 7"))
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                ScorePerCore[i] = 1;
                            }
                            for (int i = 8; i < Environment.ProcessorCount; i++)
                            {
                                ScorePerCore[i] = 2;
                            }
                            GVariables.CpuType = CPUType.Intel_CoreUltra_7;
                        }
                        else if (nmc.Contains("ultra 9"))
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                ScorePerCore[i] = 1;
                            }
                            for (int i = 8; i < Environment.ProcessorCount; i++)
                            {
                                ScorePerCore[i] = 2;
                            }
                            GVariables.CpuType = CPUType.Intel_CoreUltra_9;
                        }
                    }
                    else
                    {
                        Match m = Regex.Match(nmc, @"\d{4,5}");
                        if (m.Success)
                        {
                            //Console.WriteLine(m.Value);
                            string vv = m.Value[..^3];
                            //Console.WriteLine(vv);
                            GVariables.CpuVersion = vv;
                        }
                        else
                        {
                            GVariables.CpuVersion = "0";
                        }

                        if (nmc.Contains("i3"))
                        {
                            GVariables.CpuType = CPUType.Intel_Core_i3;
                            for (int i = 0; i < Environment.ProcessorCount; i++)
                            {
                                ScorePerCore[i] = 1;
                            }

                        }
                        else if (nmc.Contains("i5"))
                        {
                            GVariables.CpuType = CPUType.Intel_Core_i5;
                            if (int.Parse(GVariables.CpuVersion) > 11)
                            {
                                for (int i = 0; i < Environment.ProcessorCount && i < 12; i++)
                                {
                                    ScorePerCore[i] = 1;
                                }
                                for (int i = 12; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 2;
                                }
                            }
                            else
                            {
                                for (int i = 0; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 1;
                                }
                            }
                        }
                        else if (nmc.Contains("i7"))
                        {
                            GVariables.CpuType = CPUType.Intel_Core_i7;
                            if (int.Parse(GVariables.CpuVersion) > 11)
                            {
                                for (int i = 0; i < Environment.ProcessorCount && i < 16; i++)
                                {
                                    ScorePerCore[i] = 1;
                                }
                                for (int i = 16; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 2;
                                }
                            }
                            else
                            {
                                for (int i = 0; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 1;
                                }
                            }
                        }
                        else if (nmc.Contains("i9"))
                        {
                            GVariables.CpuType = CPUType.Intel_Core_i9;
                            if (int.Parse(GVariables.CpuVersion) > 11)
                            {
                                for (int i = 0; i < Environment.ProcessorCount && i < 16; i++)
                                {
                                    ScorePerCore[i] = 1;
                                }
                                for (int i = 16; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 2;
                                }
                            }
                            else
                            {
                                for (int i = 0; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 1;
                                }
                            }
                        }
                    }
                }
                else if (nmc.Contains("pentium"))
                {
                    Match m = Regex.Match(nmc, @"\d{4,5}");
                    if (m.Success)
                    {
                        //Console.WriteLine(m.Value);
                        string vv = m.Value[..^3];
                        //Console.WriteLine(vv);
                        GVariables.CpuVersion = vv;
                    }
                    else
                    {
                        GVariables.CpuVersion = "0";
                    }
                    for (int i = 0; i < Environment.ProcessorCount; i++)
                    {
                        ScorePerCore[i] = 1;
                    }
                    GVariables.CpuType = CPUType.Intel_Pentium_G;
                }
                else if (nmc.Contains("xeno"))
                {
                    Match m = Regex.Match(nmc, @"\d{4,5}");
                    if (m.Success)
                    {
                        //Console.WriteLine(m.Value);
                        string vv = m.Value[..^3];
                        //Console.WriteLine(vv);
                        GVariables.CpuVersion = vv;
                    }
                    else
                    {
                        GVariables.CpuVersion = "0";
                    }
                    for (int i = 0; i < Environment.ProcessorCount; i++)
                    {
                        ScorePerCore[i] = 1;
                    }
                    GVariables.CpuType = CPUType.Intel_Xeon;
                }
                else if (nmc.Contains("celeron"))
                {
                    Match m = Regex.Match(nmc, @"\d{4,5}");
                    if (m.Success)
                    {
                        //Console.WriteLine(m.Value);
                        string vv = m.Value[..^3];
                        //Console.WriteLine(vv);
                        GVariables.CpuVersion = vv;
                    }
                    else
                    {
                        GVariables.CpuVersion = "0";
                    }
                    for (int i = 0; i < Environment.ProcessorCount; i++)
                    {
                        ScorePerCore[i] = 1;
                    }
                    GVariables.CpuType = CPUType.Intel_Celeron_G;
                }
            }
            else//其他型号以后再说
            {
                Console.WriteLine("其他处理器");
            }
            GVariables.OnEngineClose += OnCLe;

        }
        public static void OnCLe()
        {
            if (GVariables.OS == OS.Windows)
            {
                for (int i = 0; i < Thrs.Count; i++)
                {
                    Thrs[i].Dispose();
                }
            }
        }
        /*
        public static void InitOLD()
        {
            //GVariables.CpuName = GetCPUName();

            if(GVariables.OS == OS.Windows|| GVariables.OS == OS.Linux || GVariables.OS == OS.MacOS)
            {
                
                



                ScorePerCore = new int[Environment.ProcessorCount];
                Thrs = new Dictionary<ulong, SEThread>[Environment.ProcessorCount];
                for (int i = 0; i < Environment.ProcessorCount; i++)
                {
                    Thrs[i] = new Dictionary<ulong, SEThread>(0);
                }
                UsagePerThread = new ulong[Environment.ProcessorCount];
                foreach (var hd in cmp.Hardware)
                {
                    if (hd.HardwareType == HardwareType.Cpu)
                    {
                        GVariables.CpuName = hd.Name;
                        Console.WriteLine(GVariables.CpuName);
                        //Console.ReadLine();
                        string nmc = GVariables.CpuName.ToLower();
                        
                        if (nmc.Contains("amd"))
                        {
                            Match m = Regex.Match(nmc, @"\d{4,5}");
                            if (m.Success)
                            {
                                Console.WriteLine(m.V2);
                                string vv = m.V2[..^3];
                                Console.WriteLine(vv);
                                GVariables.CpuVersion = vv;
                            }
                            else
                            {
                                GVariables.CpuVersion = "0";
                            }
                            GVariables.CpuVendor = CpuVendor.AMD;
                            if (nmc.Contains("ryzen"))
                            {
                                if (nmc.Contains("threadripper"))
                                {
                                    for (int i = 0; i < Environment.ProcessorCount; i++)
                                    {
                                        ScorePerCore[i] = 1;
                                    }
                                    if (nmc.Contains("pro"))
                                    {
                                        GVariables.CpuType = CPUType.AMD_Ryzen_ThreadRipperPro;
                                    }
                                    else
                                    {
                                        GVariables.CpuType = CPUType.AMD_Ryzen_ThreadRipper;
                                    }
                                }
                                else
                                {
                                    if (nmc.Contains("x3d"))
                                    {
                                        if (nmc.Contains("ryzen 5"))
                                        {
                                            for (int i = 0; i < Environment.ProcessorCount; i++)
                                            {
                                                ScorePerCore[i] = 3;
                                            }
                                            GVariables.CpuType = CPUType.AMD_Ryzen5_X3D;
                                        }
                                        else if (nmc.Contains("ryzen 7"))
                                        {
                                            for (int i = 0; i < Environment.ProcessorCount; i++)
                                            {
                                                ScorePerCore[i] = 3;
                                            }
                                            GVariables.CpuType = CPUType.AMD_Ryzen7_X3D;
                                        }
                                        else if (nmc.Contains("ryzen 9"))
                                        {
                                            for (int i = 0; i < Environment.ProcessorCount / 2; i++)
                                            {
                                                ScorePerCore[i] = 1;
                                            }
                                            for (int i = Environment.ProcessorCount / 2; i < Environment.ProcessorCount; i++)
                                            {
                                                ScorePerCore[i] = 3;
                                            }
                                            GVariables.CpuType = CPUType.AMD_Ryzen9_X3D;
                                        }
                                    }
                                    else
                                    {
                                        for (int i = 0; i < Environment.ProcessorCount; i++)
                                        {
                                            ScorePerCore[i] = 3;
                                        }
                                        if (nmc.Contains("ryzen 3"))
                                        {
                                            GVariables.CpuType = CPUType.AMD_Ryzen3;
                                        }
                                        else if (nmc.Contains("ryzen 5"))
                                        {
                                            GVariables.CpuType = CPUType.AMD_Ryzen5;
                                        }
                                        else if (nmc.Contains("ryzen 7"))
                                        {
                                            GVariables.CpuType = CPUType.AMD_Ryzen7;
                                        }
                                        else if (nmc.Contains("ryzen 9"))
                                        {
                                            GVariables.CpuType = CPUType.AMD_Ryzen9;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                GVariables.CpuType = CPUType.AMD_APU;
                            }
                        }
                        else if (nmc.Contains("intel"))
                        {
                            GVariables.CpuVendor = CpuVendor.Intel;
                            if (nmc.Contains("core"))
                            {
                                if (nmc.Contains("ultra"))
                                {
                                    Match m = Regex.Match(nmc, @"\d{3,5}");
                                    if (m.Success)
                                    {
                                        Console.WriteLine(m.V2);
                                        string vv = m.V2[..^2];
                                        Console.WriteLine(vv);
                                        GVariables.CpuVersion = vv;
                                    }
                                    else
                                    {
                                        GVariables.CpuVersion = "0";
                                    }
                                    if (nmc.Contains("ultra 3"))
                                    {
                                        GVariables.CpuType = CPUType.Intel_CoreUltra_3;
                                        for (int i = 0; i < 4; i++)
                                        {
                                            ScorePerCore[i] = 1;
                                        }
                                        for (int i = 4; i < Environment.ProcessorCount; i++)
                                        {
                                            ScorePerCore[i] = 2;
                                        }
                                    }
                                    else if (nmc.Contains("ultra 5"))
                                    {
                                        for (int i = 0; i < 6; i++)
                                        {
                                            ScorePerCore[i] = 1;
                                        }
                                        for (int i = 6; i < Environment.ProcessorCount; i++)
                                        {
                                            ScorePerCore[i] = 2;
                                        }
                                        GVariables.CpuType = CPUType.Intel_CoreUltra_5;
                                    }
                                    else if (nmc.Contains("ultra 7"))
                                    {
                                        for (int i = 0; i < 8; i++)
                                        {
                                            ScorePerCore[i] = 1;
                                        }
                                        for (int i = 8; i < Environment.ProcessorCount; i++)
                                        {
                                            ScorePerCore[i] = 2;
                                        }
                                        GVariables.CpuType = CPUType.Intel_CoreUltra_7;
                                    }
                                    else if (nmc.Contains("ultra 9"))
                                    {
                                        for (int i = 0; i < 8; i++)
                                        {
                                            ScorePerCore[i] = 1;
                                        }
                                        for (int i = 8; i < Environment.ProcessorCount; i++)
                                        {
                                            ScorePerCore[i] = 2;
                                        }
                                        GVariables.CpuType = CPUType.Intel_CoreUltra_9;
                                    }
                                }
                                else
                                {
                                    Match m = Regex.Match(nmc, @"\d{4,5}");
                                    if (m.Success)
                                    {
                                        Console.WriteLine(m.V2);
                                        string vv = m.V2[..^3];
                                        Console.WriteLine(vv);
                                        GVariables.CpuVersion = vv;
                                    }
                                    else
                                    {
                                        GVariables.CpuVersion = "0";
                                    }

                                    if (nmc.Contains("i3"))
                                    {
                                        GVariables.CpuType = CPUType.Intel_Core_i3;
                                        for (int i = 0; i < Environment.ProcessorCount; i++)
                                        {
                                            ScorePerCore[i] = 1;
                                        }
                                        
                                    }
                                    else if (nmc.Contains("i5"))
                                    {
                                        GVariables.CpuType = CPUType.Intel_Core_i5;
                                        if (int.Parse(GVariables.CpuVersion) > 11)
                                        {
                                            for (int i = 0; i < Environment.ProcessorCount && i < 12; i++)
                                            {
                                                ScorePerCore[i] = 1;
                                            }
                                            for (int i = 12; i < Environment.ProcessorCount; i++)
                                            {
                                                ScorePerCore[i] = 2;
                                            }
                                        }
                                        else
                                        {
                                            for (int i = 0; i < Environment.ProcessorCount; i++)
                                            {
                                                ScorePerCore[i] = 1;
                                            }
                                        }
                                    }
                                    else if (nmc.Contains("i7"))
                                    {
                                        GVariables.CpuType = CPUType.Intel_Core_i7;
                                        if (int.Parse(GVariables.CpuVersion) > 11)
                                        {
                                            for (int i = 0; i < Environment.ProcessorCount && i < 16; i++)
                                            {
                                                ScorePerCore[i] = 1;
                                            }
                                            for (int i = 16; i < Environment.ProcessorCount; i++)
                                            {
                                                ScorePerCore[i] = 2;
                                            }
                                        }
                                        else
                                        {
                                            for (int i = 0; i < Environment.ProcessorCount; i++)
                                            {
                                                ScorePerCore[i] = 1;
                                            }
                                        }
                                    }
                                    else if (nmc.Contains("i9"))
                                    {
                                        GVariables.CpuType = CPUType.Intel_Core_i9;
                                        if (int.Parse(GVariables.CpuVersion) > 11)
                                        {
                                            for (int i = 0; i < Environment.ProcessorCount && i < 16; i++)
                                            {
                                                ScorePerCore[i] = 1;
                                            }
                                            for (int i = 16; i < Environment.ProcessorCount; i++)
                                            {
                                                ScorePerCore[i] = 2;
                                            }
                                        }
                                        else
                                        {
                                            for (int i = 0; i < Environment.ProcessorCount; i++)
                                            {
                                                ScorePerCore[i] = 1;
                                            }
                                        }
                                    }
                                }
                            }
                            else if (nmc.Contains("pentium"))
                            {
                                Match m = Regex.Match(nmc, @"\d{4,5}");
                                if (m.Success)
                                {
                                    Console.WriteLine(m.V2);
                                    string vv = m.V2[..^3];
                                    Console.WriteLine(vv);
                                    GVariables.CpuVersion = vv;
                                }
                                else
                                {
                                    GVariables.CpuVersion = "0";
                                }
                                for (int i = 0; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 1;
                                }
                                GVariables.CpuType = CPUType.Intel_Pentium_G;
                            }
                            else if (nmc.Contains("xeno"))
                            {
                                Match m = Regex.Match(nmc, @"\d{4,5}");
                                if (m.Success)
                                {
                                    Console.WriteLine(m.V2);
                                    string vv = m.V2[..^3];
                                    Console.WriteLine(vv);
                                    GVariables.CpuVersion = vv;
                                }
                                else
                                {
                                    GVariables.CpuVersion = "0";
                                }
                                for (int i = 0; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 1;
                                }
                                GVariables.CpuType = CPUType.Intel_Xeon;
                            }
                            else if (nmc.Contains("celeron"))
                            {
                                Match m = Regex.Match(nmc, @"\d{4,5}");
                                if (m.Success)
                                {
                                    Console.WriteLine(m.V2);
                                    string vv = m.V2[..^3];
                                    Console.WriteLine(vv);
                                    GVariables.CpuVersion = vv;
                                }
                                else
                                {
                                    GVariables.CpuVersion = "0";
                                }
                                for (int i = 0; i < Environment.ProcessorCount; i++)
                                {
                                    ScorePerCore[i] = 1;
                                }
                                GVariables.CpuType = CPUType.Intel_Celeron_G;
                            }
                        }
                        else//其他型号以后再说
                        {
                            Console.WriteLine("其他处理器");
                        }
                        Console.WriteLine(GVariables.CpuName);
                    }
                    else if (hd.HardwareType == HardwareType.GpuIntel)
                    {
                        GVariables.GpuVendor = GpuVendor.Intel;
                        
                    }
                    else if (hd.HardwareType == HardwareType.GpuNvidia)
                    {
                        GVariables.GpuVendor = GpuVendor.Nvidia;
                    }
                    else if (hd.HardwareType == HardwareType.GpuAmd)
                    {
                        GVariables.GpuVendor = GpuVendor.AMD;
                    }
                    else if (hd.HardwareType == HardwareType.Memory)
                    {
                        Console.WriteLine(hd.GetReport());
                        Console.WriteLine(hd.Name);
                        foreach (var se in hd.Sensors)
                        {
                            Console.WriteLine(se.Name);
                            Console.WriteLine(se.SensorType);
                        }
                    }
                    Console.WriteLine(GVariables.GpuVendor);
                }
                cmp.Close();
            }
        }
        */
    }
}
