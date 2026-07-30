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
        /// <summary>
        /// 是否允许线程在不同核心间迁移
        /// </summary>
        public bool Moveable;
        /// <summary>
        /// 是否为数据传输密集型任务（优先分配到X3D大三缓核心）
        /// </summary>
        public bool TransferIntensive;
        /// <summary>
        /// 原生线程ID（用于跨平台亲和性设置）
        /// </summary>
        public nint NativeTID;
        /// <summary>
        /// 上次迁移时间（用于防抖）
        /// </summary>
        internal double LastMigrateTime;
        /// <summary>
        /// 迁移计数（用于检测频繁迁移，窗口过期后重置）
        /// </summary>
        internal int MigrateCount;
        /// <summary>
        /// 频繁迁移检测窗口起始时间
        /// </summary>
        internal double MigrateWindowStart;
        /// <summary>
        /// 迁移冷却截止时间（搁置期结束后自动恢复调度）
        /// </summary>
        internal double MigrateCooldownUntil;
        /// <summary>
        /// 迁移滞后确认计数：连续多个决策周期满足迁移条件才真正迁移
        /// </summary>
        internal int MigrateStableCount;
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
        public static ulong[] BusyLevelPerThread;
        /// <summary>
        /// 调度器锁，保护 Thrs 和 UsagePerThread 的访问
        /// </summary>
        static readonly object DispatchLock = new object();

        public struct PresentRun
        {
            public int ThreadID;
            public ThreadStart ts;
            public SEThread Owner;
        }

        static int GetSuitableThreadID(ThreadPriority tp, bool Moveable = false, bool TransferIntensive = false)
        {
            int id = 0;
            // 权重数组，索引为逻辑线程ID
            int[] level = new int[CPUThreadCount];

            if (TransferIntensive)
            {
                // 数据密集型任务：优先X3D与高频核心（额外加分）
                for (int i = 0, k = 0; k < CPUCoreCount; k++)
                {
                    int coreType = DefCore[k];
                    bool hasHT = (coreType % 2 == 0); // 偶数表示有超线程

                    switch (coreType)
                    {
                        case 0:  // 未知
                            level[i] += 50;
                            break;

                        // 不具备超线程的组
                        case 1:  // 普通性能核，无超线程
                            level[i] += 200;
                            break;
                        case 3:  // 高频性能核，无超线程
                            level[i] += 300;
                            break;
                        case 5:  // 能效核，无超线程
                            level[i] += 100;
                            break;
                        case 7:  // X3D大三缓核，无超线程（数据密集最优）
                            level[i] += 500;
                            break;
                        case 9:  // LPE核，无超线程
                            level[i] += 50;
                            break;
                        case 11: // 未知核，无超线程
                            level[i] += 50;
                            break;

                        // 具备超线程的组
                        case 2:  // 普通性能核，有超线程
                            level[i] += 200;
                            i++;
                            level[i] += 200;
                            break;
                        case 4:  // 高频性能核，有超线程
                            level[i] += 300;
                            i++;
                            level[i] += 300;
                            break;
                        case 6:  // 能效核，有超线程
                            level[i] += 100;
                            i++;
                            level[i] += 100;
                            break;
                        case 8:  // X3D大三缓核，有超线程（数据密集最优）
                            level[i] += 500;
                            i++;
                            level[i] += 500;
                            break;
                        case 10: // LPE核，有超线程
                            level[i] += 50;
                            i++;
                            level[i] += 50;
                            break;
                        case 12: // 未知核，有超线程
                            level[i] += 50;
                            i++;
                            level[i] += 50;
                            break;
                    }

                    if (!hasHT) i++;
                }
            }
            else
            {
                // 非数据密集型：根据优先级选择核心类型
                for (int i = 0, k = 0; k < CPUCoreCount; k++)
                {
                    int coreType = DefCore[k];
                    bool hasHT = (coreType % 2 == 0);
                    int baseScore = 0;

                    // 根据优先级和核心类型打分
                    switch (tp)
                    {
                        case ThreadPriority.Highest:
                        case ThreadPriority.AboveNormal:
                            // 高优先级：高频>普通>X3D>能效>LPE
                            switch (coreType)
                            {
                                case 3: case 4: baseScore = 400; break; // 高频核最优
                                case 1: case 2: baseScore = 350; break; // 普通性能核次之
                                case 7: case 8: baseScore = 300; break; // X3D核靠后（频率略低）
                                case 5: case 6: baseScore = 150; break; // 能效核
                                case 9: case 10: baseScore = 50; break;  // LPE核
                                default: baseScore = 100; break;
                            }
                            break;

                        case ThreadPriority.Normal:
                            // 普通优先级：普通核>高频>X3D>能效>LPE
                            switch (coreType)
                            {
                                case 1: case 2: baseScore = 350; break;
                                case 3: case 4: baseScore = 300; break;
                                case 7: case 8: baseScore = 250; break;
                                case 5: case 6: baseScore = 200; break;
                                case 9: case 10: baseScore = 100; break;
                                default: baseScore = 150; break;
                            }
                            break;

                        case ThreadPriority.BelowNormal:
                        case ThreadPriority.Lowest:
                            // 低优先级：能效>LPE>普通>高频>X3D
                            switch (coreType)
                            {
                                case 5: case 6: baseScore = 400; break;  // 能效核最优
                                case 9: case 10: baseScore = 350; break; // LPE核次之
                                case 1: case 2: baseScore = 250; break;  // 普通核
                                case 3: case 4: baseScore = 200; break;  // 高频核
                                case 7: case 8: baseScore = 150; break;  // X3D核最后
                                default: baseScore = 100; break;
                            }
                            break;
                    }

                    // 分配到逻辑线程
                    if (hasHT)
                    {
                        level[i] += baseScore;
                        i++;
                        level[i] += baseScore;
                        i++;
                    }
                    else
                    {
                        level[i] += baseScore;
                        i++;
                    }
                }
            }

            // 按照核心线程使用情况再次加分/扣分
            lock (DispatchLock)
            {
                ulong[] UPT = UsagePerThread;
                ulong[] BLT = BusyLevelPerThread;

                for (int i = 0; i < CPUThreadCount; i++)
                {
                    // 占用惩罚：每个已占用线程扣50分
                    level[i] -= (int)(UPT[i] * 50);

                    // BusyLevel 惩罚：每个单位强度扣20分
                    level[i] -= (int)(BLT[i] * 20);

                    // 超线程兄弟线程繁忙额外扣分
                    int siblingIndex = GetHyperThreadSibling(i);
                    if (siblingIndex >= 0 && siblingIndex < CPUThreadCount)
                    {
                        if (UPT[siblingIndex] > 0)
                        {
                            level[i] -= 30; // 兄弟线程有占用，扣30分
                        }
                        if (BLT[siblingIndex] > 5)
                        {
                            level[i] -= 20; // 兄弟线程繁忙，再扣20分
                        }
                    }
                }
            }

            // 找到最高分的线程
            int maxScore = level[0];
            id = 0;
            for (int i = 1; i < CPUThreadCount; i++)
            {
                if (level[i] > maxScore)
                {
                    maxScore = level[i];
                    id = i;
                }
            }

            return id;
        }

        /// <summary>
        /// 获取超线程兄弟逻辑线程索引
        /// </summary>
        static int GetHyperThreadSibling(int logicalThreadId)
        {
            int accumulated = 0;
            for (int k = 0; k < CPUCoreCount; k++)
            {
                int coreType = DefCore[k];
                bool hasHT = (coreType % 2 == 0);

                if (hasHT)
                {
                    // 有超线程，占两个逻辑线程
                    if (logicalThreadId == accumulated)
                        return accumulated + 1; // 返回兄弟
                    if (logicalThreadId == accumulated + 1)
                        return accumulated;     // 返回兄弟
                    accumulated += 2;
                }
                else
                {
                    // 无超线程
                    if (logicalThreadId == accumulated)
                        return -1; // 无兄弟
                    accumulated += 1;
                }
            }
            return -1; // 未找到
        }

        // 上次采样的CPU时间（用于Windows）
        static long[] lastIdleTime;
        static long[] lastKernelTime;
        static long[] lastUserTime;
        static bool samplerInitialized = false;
        // EMA平滑后的系统负载（消除瞬时毛刺，避免误触发迁移）
        static double[] EmaBusy;

        // Linux上次采样的CPU ticks
        static long[] lastLinuxIdleTicks;
        static long[] lastLinuxTotalTicks;

        /// <summary>
        /// 采样系统CPU负载并量化写入BusyLevelPerThread
        /// </summary>
        static void SampleCpuBusyLevels()
        {
            if (GVariables.OS == OS.Windows)
            {
                SampleCpuBusyLevels_Windows();
            }
            else if (GVariables.OS == OS.Linux)
            {
                SampleCpuBusyLevels_Linux();
            }
            // macOS和其他平台暂时跳过系统采样，只用SEThread权重

            // 叠加SEThread的任务强度
            AddSEThreadIntensity();
        }

        static void SampleCpuBusyLevels_Windows()
        {
            try
            {
                int size = Marshal.SizeOf<WindowsAPI.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>() * CPUThreadCount;
                IntPtr buffer = Marshal.AllocHGlobal(size);

                try
                {
                    int retLen;
                    int status = WindowsAPI.NtQuerySystemInformation(
                        WindowsAPI.SystemProcessorPerformanceInformation,
                        buffer,
                        size,
                        out retLen);

                    if (status != 0)
                        return;

                    if (!samplerInitialized)
                    {
                        lastIdleTime = new long[CPUThreadCount];
                        lastKernelTime = new long[CPUThreadCount];
                        lastUserTime = new long[CPUThreadCount];
                        samplerInitialized = true;
                    }

                    for (int i = 0; i < CPUThreadCount; i++)
                    {
                        IntPtr ptr = buffer + i * Marshal.SizeOf<WindowsAPI.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>();
                        var info = Marshal.PtrToStructure<WindowsAPI.SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>(ptr);

                        if (lastIdleTime[i] != 0)
                        {
                            long idleDelta = info.IdleTime - lastIdleTime[i];
                            long kernelDelta = info.KernelTime - lastKernelTime[i];
                            long userDelta = info.UserTime - lastUserTime[i];
                            long totalDelta = kernelDelta + userDelta;

                            if (totalDelta > 0)
                            {
                                double busyRatio = 1.0 - ((double)idleDelta / totalDelta);
                                if (busyRatio < 0) busyRatio = 0;
                                if (busyRatio > 1) busyRatio = 1;

                                // 量化：1个Lowest空转线程=1单位，假设其占用约5%CPU
                                // busyRatio * 100 = 百分比，除以5得到等效Lowest线程数
                                double quantized = (busyRatio * 100.0) / 5.0;
                                EmaBusy ??= new double[CPUThreadCount];
                                EmaBusy[i] = EmaBusy[i] * 0.7 + quantized * 0.3;

                                lock (DispatchLock)
                                {
                                    BusyLevelPerThread[i] = (ulong)EmaBusy[i];
                                }
                            }
                        }

                        lastIdleTime[i] = info.IdleTime;
                        lastKernelTime[i] = info.KernelTime;
                        lastUserTime[i] = info.UserTime;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Windows CPU采样失败: {ex.Message}");
            }
        }

        static void SampleCpuBusyLevels_Linux()
        {
            try
            {
                string[] lines = File.ReadAllLines("/proc/stat");

                if (!samplerInitialized)
                {
                    lastLinuxIdleTicks = new long[CPUThreadCount];
                    lastLinuxTotalTicks = new long[CPUThreadCount];
                    samplerInitialized = true;
                }

                int cpuIndex = 0;
                foreach (string line in lines)
                {
                    if (line.StartsWith("cpu") && !line.StartsWith("cpu "))
                    {
                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5)
                        {
                            // cpu0 user nice system idle iowait irq softirq...
                            long user = long.Parse(parts[1]);
                            long nice = long.Parse(parts[2]);
                            long system = long.Parse(parts[3]);
                            long idle = long.Parse(parts[4]);
                            long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;

                            long totalTicks = user + nice + system + idle + iowait;
                            long idleTicks = idle + iowait;

                            if (lastLinuxTotalTicks[cpuIndex] != 0)
                            {
                                long totalDelta = totalTicks - lastLinuxTotalTicks[cpuIndex];
                                long idleDelta = idleTicks - lastLinuxIdleTicks[cpuIndex];

                                if (totalDelta > 0)
                                {
                                    double busyRatio = 1.0 - ((double)idleDelta / totalDelta);
                                    if (busyRatio < 0) busyRatio = 0;
                                    if (busyRatio > 1) busyRatio = 1;

                                    double quantized = (busyRatio * 100.0) / 5.0;
                                    EmaBusy ??= new double[CPUThreadCount];
                                    EmaBusy[cpuIndex] = EmaBusy[cpuIndex] * 0.7 + quantized * 0.3;

                                    lock (DispatchLock)
                                    {
                                        BusyLevelPerThread[cpuIndex] = (ulong)EmaBusy[cpuIndex];
                                    }
                                }
                            }

                            lastLinuxIdleTicks[cpuIndex] = idleTicks;
                            lastLinuxTotalTicks[cpuIndex] = totalTicks;

                            cpuIndex++;
                            if (cpuIndex >= CPUThreadCount)
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Linux CPU采样失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 叠加SEThread的任务强度到BusyLevelPerThread
        /// </summary>
        static void AddSEThreadIntensity()
        {
            lock (DispatchLock)
            {
                // 按优先级定义强度权重（以Lowest=1为基准）
                Dictionary<ThreadPriority, double> intensityWeights = new Dictionary<ThreadPriority, double>
                {
                    { ThreadPriority.Lowest, 1.0 },
                    { ThreadPriority.BelowNormal, 2.0 },
                    { ThreadPriority.Normal, 3.0 },
                    { ThreadPriority.AboveNormal, 4.0 },
                    { ThreadPriority.Highest, 5.0 }
                };

                foreach (var thread in Thrs)
                {
                    if (thread.Threads >= 0 && thread.Threads < CPUThreadCount)
                    {
                        double intensity = intensityWeights.ContainsKey(thread._TP) 
                            ? intensityWeights[thread._TP] 
                            : 3.0;

                        BusyLevelPerThread[thread.Threads] += (ulong)intensity;
                    }
                }
            }
        }

        
        static bool Running = true;
        static void DispatcherClose()
        {
            Running = false;
            if (DispatcherWorkerThread != null)
            {
                Delay(0.5);
                DispatcherWorkerThread.Dispose();
                DispatcherWorkerThread = null;
            }

            
        }
        /// <summary>
        /// 获取线程优先级对应的任务强度（以Lowest=1为基准）
        /// </summary>
        static double GetIntensity(ThreadPriority tp)
        {
            switch (tp)
            {
                case ThreadPriority.Lowest: return 1.0;
                case ThreadPriority.BelowNormal: return 2.0;
                case ThreadPriority.Normal: return 3.0;
                case ThreadPriority.AboveNormal: return 4.0;
                case ThreadPriority.Highest: return 5.0;
                default: return 3.0;
            }
        }

        /// <summary>
        /// 调度工作线程
        /// </summary>
        static void DispatcherWorker()
        {
            //设置调度器线程的FPS，采样5FPS，但迁移决策只有1Hz
            DispatcherWorkerThread.SetFPS(5);

            const double DECISION_INTERVAL = 1.0;       // 迁移决策频率（秒），采样仍由5FPS进行
            const double MIN_RESIDENCE_SECONDS = 8.0;   // 最小驻留时间：线程至少在当前核上跑这么久才允许再迁移
            const int STABLE_PERIODS = 4;               // 滞后确认：连续多少个决策周期满足条件才真正迁移
            const int MAX_MIGRATIONS_PER_TICK = 1;      // 每个决策周期最多迁移线程数（防止集体涌向同一核）
            const ulong OVERLOAD_BUSY = 17;             // 过载阈值（≈85%占用），超过才考虑迁出
            const ulong COMFORT_BUSY = 12;              // 舒适区上限（≈60%占用），目标核必须处于舒适区才允许迁入
            const int MIGRATE_FREQUENT_THRESHOLD = 3;   // 窗口内迁移次数阈值
            const double MIGRATE_FREQUENT_WINDOW = 40.0;// 频繁迁移检测窗口（秒）

            Stopwatch sw = Stopwatch.StartNew();
            double lastDecisionTime = 0;

            while (Running)
            {
                double currentTime = sw.Elapsed.TotalSeconds;

                // 采样系统CPU负载（含EMA平滑），保持高频以便平滑值及时跟踪趋势
                SampleCpuBusyLevels();

                // 迁移决策降频1Hz，减少切换频率
                bool platformOk = GVariables.OS == OS.Windows || GVariables.OS == OS.Linux || GVariables.OS == OS.MacOS;
                if (platformOk && currentTime - lastDecisionTime >= DECISION_INTERVAL)
                {
                    lastDecisionTime = currentTime;
                    int migratedThisTick = 0;

                    List<SEThread> threadsSnapshot;
                    lock (DispatchLock)
                    {
                        threadsSnapshot = new List<SEThread>(Thrs);
                    }

                    // 第一步：收集处于过载核心上、可参与迁移的候选 worker
                    // 只有当前核过载才考虑迁出，其余线程一律不动（缓存热度优先）
                    List<(SEThread t, ulong coreBusy, double intensity)> candidates = new();
                    lock (DispatchLock)
                    {
                        foreach (var t in threadsSnapshot)
                        {
                            if (!t.Moveable || t.Threads < 0 || t.Threads >= CPUThreadCount)
                                continue;

                            // 搁置期（到期自动恢复）
                            if (currentTime < t.MigrateCooldownUntil)
                                continue;

                            // 最小驻留时间：刚迁移过的线程先让它跑稳
                            if (t.LastMigrateTime != 0 && currentTime - t.LastMigrateTime < MIN_RESIDENCE_SECONDS)
                                continue;

                            // 频繁迁移检测：窗口过期重置计数
                            if (currentTime - t.MigrateWindowStart > MIGRATE_FREQUENT_WINDOW)
                            {
                                t.MigrateWindowStart = currentTime;
                                t.MigrateCount = 0;
                            }
                            if (t.MigrateCount >= MIGRATE_FREQUENT_THRESHOLD)
                            {
                                // 进入搁置期，10秒后自动恢复并重置计数窗口
                                t.MigrateCooldownUntil = currentTime + 10.0;
                                t.MigrateCount = 0;
                                t.MigrateStableCount = 0;
                                t.MigrateWindowStart = currentTime + 10.0;
                                continue;
                            }

                            ulong busy = BusyLevelPerThread[t.Threads];
                            if (busy >= OVERLOAD_BUSY)
                            {
                                candidates.Add((t, busy, GetIntensity(t._TP)));
                            }
                            else
                            {
                                // 核心不过载，重置滞后计数（条件不再持续成立）
                                t.MigrateStableCount = 0;
                            }
                        }
                    }

                    // 第二步：按“核心越忙越优先、worker强度越小越优先迁出”排序
                    // 迁出低强度worker代价最小，同时能给大任务腾出空间
                    candidates.Sort((a, b) =>
                    {
                        int cmp = b.coreBusy.CompareTo(a.coreBusy);
                        if (cmp != 0) return cmp;
                        return a.intensity.CompareTo(b.intensity);
                    });

                    foreach (var (t, _, intensity) in candidates)
                    {
                        if (migratedThisTick >= MAX_MIGRATIONS_PER_TICK)
                            break;

                        int currentCore = t.Threads;
                        int bestCore = GetSuitableThreadID(t._TP, t.Moveable, t.TransferIntensive);

                        if (bestCore == currentCore)
                        {
                            t.MigrateStableCount = 0;
                            continue;
                        }

                        // 舒适区间判据：目标核当前必须在舒适区内，
                        // 且迁入该worker后预计仍不会过载，否则迁过去又得迁走，徒增抖动
                        ulong targetBusy;
                        lock (DispatchLock)
                        {
                            targetBusy = BusyLevelPerThread[bestCore];
                        }
                        if (targetBusy > COMFORT_BUSY || targetBusy + (ulong)intensity >= OVERLOAD_BUSY)
                        {
                            t.MigrateStableCount = 0;
                            continue;
                        }

                        // 滞后确认：连续 STABLE_PERIODS 个决策周期都满足条件才真正迁移，
                        // 避免瞬时尖峰把刚跑稳的worker切走
                        t.MigrateStableCount++;
                        if (t.MigrateStableCount < STABLE_PERIODS)
                            continue;
                        t.MigrateStableCount = 0;

                        if (MigrateThread(t, bestCore))
                        {
                            lock (DispatchLock)
                            {
                                if (currentCore >= 0 && currentCore < CPUThreadCount && UsagePerThread[currentCore] > 0)
                                    UsagePerThread[currentCore]--;
                                UsagePerThread[bestCore]++;

                                t.Threads = bestCore;
                                t.LastMigrateTime = currentTime;
                                t.MigrateCount++;
                            }
                            migratedThisTick++;
                        }
                    }
                }

                DispatcherWorkerThread.WaitForFPS();
            }
        }

        /// <summary>
        /// 迁移线程到指定核心
        /// </summary>
        static bool MigrateThread(SEThread thread, int targetCore)
        {
            if (targetCore < 0 || targetCore >= CPUThreadCount)
                return false;

            // NativeTID 还未被worker线程写入，跳过本次迁移
            if (thread.NativeTID == 0)
                return false;

            try
            {
                if (GVariables.OS == OS.Windows)
                {
                    // SetThreadAffinityMask 要求 SET_INFORMATION + QUERY_INFORMATION 两个权限
                    const uint THREAD_ACCESS = 0x0020 | 0x0040;
                    IntPtr hThread = WindowsAPI.OpenThread(THREAD_ACCESS, false, (int)thread.NativeTID);

                    if (hThread == IntPtr.Zero)
                    {
                        SELogger.Warn($"OpenThread失败, TID={thread.NativeTID}, Err={Marshal.GetLastWin32Error()}");
                        return false;
                    }
                    try
                    {
                        IntPtr mask = new IntPtr(1L << targetCore);
                        // 返回值为原亲和性掩码，0 表示失败——必须检查！
                        IntPtr prev = WindowsAPI.SetThreadAffinityMask(hThread, mask);
                        if (prev == IntPtr.Zero)
                        {
                            SELogger.Warn($"SetThreadAffinityMask失败, TID={thread.NativeTID}, 目标核心={targetCore}, Err={Marshal.GetLastWin32Error()}");
                            return false;
                        }
                        return true;
                    }
                    finally
                    {
                        WindowsAPI.CloseHandle(hThread);
                    }
                }
                else if (GVariables.OS == OS.Linux)
                {
                    var mask = new LinuxAPI.L_cpu_set_t { bits = new byte[128] };
                    mask.bits[targetCore / 8] |= (byte)(1 << (targetCore % 8));
                    // 返回0才是成功
                    if (LinuxAPI.sched_setaffinity((int)thread.NativeTID, new IntPtr(mask.bits.Length), ref mask) != 0)
                    {
                        SELogger.Warn($"sched_setaffinity失败, TID={thread.NativeTID}, 目标核心={targetCore}");
                        return false;
                    }
                    return true;
                }
                else if (GVariables.OS == OS.MacOS)
                {
                    var mask = new MacOSAPI.M_cpu_set_t { bits = new byte[128] };
                    mask.bits[targetCore / 8] |= (byte)(1 << (targetCore % 8));
                    if (MacOSAPI.pthread_setaffinity_np((ulong)thread.NativeTID, new IntPtr(mask.bits.Length), ref mask) != 0)
                    {
                        SELogger.Warn($"pthread_setaffinity_np失败, TID={thread.NativeTID}, 目标核心={targetCore}");
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"线程迁移失败: {ex.Message}");
            }

            return false;
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
        /// <summary>
        /// 创建线程，自动调度到合适的CPU核心上，Moveable为true则允许线程在不同核心间迁移，TransferIntensive为true则意味着线程会频繁进行数据传输，调度器会优先将其分配到高缓存核心上
        /// </summary>
        /// <param name="ts">worker</param>
        /// <param name="tp">等级</param>
        /// <param name="Moveable">是否允许线程在不同核心间迁移</param>
        /// <param name="TransferIntensive">是否意味着线程会频繁进行数据传输</param>
        /// <returns></returns>
        public static SEThread CreateThread(ThreadStart ts, ThreadPriority tp, bool Moveable = true, bool TransferIntensive = false)
        {
            PresentRun pr = new PresentRun();
            pr.ts = ts;

            if (GVariables.OS == OS.Windows || GVariables.OS == OS.Linux || GVariables.OS == OS.MacOS)
            {
                int tid = GetSuitableThreadID(tp, Moveable, TransferIntensive);

                SEThread st = new SEThread();
                st.Threads = tid;
                st._Main = new Thread(SetThreadRun);
                st._Main.Priority = tp;
                st._TP = tp;
                st.Moveable = Moveable;
                st.TransferIntensive = TransferIntensive;
                pr.ThreadID = st.Threads;
                pr.Owner = st;
                st.Tag = pr;

                // 加入跟踪列表，并立即预登记占用，
                // 否则连续创建多个线程时选核算法看不到彼此，全部挤到同一核心
                lock (DispatchLock)
                {
                    Thrs.Add(st);
                    ThreadsOnRunning++;
                    if (tid >= 0 && tid < CPUThreadCount)
                        UsagePerThread[tid]++;
                }

                return st;
            }
            else
            {
                // 其他平台使用回退方案
                SEThread st = new SEThread();
                st.Threads = -1;
                st._Main = new Thread(SetThreadRun);
                st._Main.Priority = tp;
                st._TP = tp;
                st.Moveable = Moveable;
                st.TransferIntensive = TransferIntensive;
                pr.ThreadID = st.Threads;
                pr.Owner = st;
                st.Tag = pr;

                lock (DispatchLock)
                {
                    Thrs.Add(st);
                    ThreadsOnRunning++;
                }

                return st;
            }
        }

        private static void SetThreadRun(object? o)
        {
            PresentRun pr = (PresentRun)o;

            // 记录原生线程ID
            if (pr.Owner != null)
            {
                if (GVariables.OS == OS.Windows)
                {
                    pr.Owner.NativeTID = WindowsAPI.GetCurrentThreadId();
                }
                else if (GVariables.OS == OS.Linux)
                {
                    pr.Owner.NativeTID = LinuxAPI.gettid();
                }
                else if (GVariables.OS == OS.MacOS)
                {
                    pr.Owner.NativeTID = (nint)MacOSAPI.pthread_self();
                }
            }

            if (pr.ThreadID > CPUThreadCount)
            {
                pr.ThreadID = 0;
            }

            if (pr.ThreadID >=0 && SetCurrentThreadAffinity(pr.ThreadID))
            {
                // Owner不为null时占用已在CreateThread中预登记，此处不再重复计数
                if (pr.Owner == null)
                {
                    lock (DispatchLock)
                    {
                        UsagePerThread[pr.ThreadID]++;
                    }
                }
                pr.ts.Invoke();
                if (pr.Owner == null)
                {
                    lock (DispatchLock)
                    {
                        UsagePerThread[pr.ThreadID]--;
                    }
                }

            }
            else
            {
                pr.ts.Invoke();

            }

            // 线程结束时从Thrs移除并释放占用（使用迁移后的当前核心）
            if (pr.Owner != null)
            {
                lock (DispatchLock)
                {
                    Thrs.Remove(pr.Owner);
                    ThreadsOnRunning--;
                    int core = pr.Owner.Threads;
                    if (core >= 0 && core < CPUThreadCount && UsagePerThread[core] > 0)
                        UsagePerThread[core]--;
                }
            }

            /*
            if (GVariables.OS == OS.Windows && pr.ThreadID >= 0)
            {
                if (CPUThreadCount > 64)
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
            */
        }
        public unsafe class WindowsAPI
        {
            [DllImport("kernel32.dll")]
            public static extern IntPtr GetCurrentThread();
            [DllImport("kernel32.dll")]
            public static extern int GetCurrentThreadId();
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, int dwThreadId);
            [DllImport("kernel32.dll", SetLastError = true)]
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

            // NtQuerySystemInformation for CPU usage sampling
            [DllImport("ntdll.dll")]
            public static extern int NtQuerySystemInformation(
                int SystemInformationClass,
                IntPtr SystemInformation,
                int SystemInformationLength,
                out int ReturnLength);

            public const int SystemProcessorPerformanceInformation = 8;

            [StructLayout(LayoutKind.Sequential)]
            public struct SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION
            {
                public long IdleTime;
                public long KernelTime;
                public long UserTime;
                public long DpcTime;
                public long InterruptTime;
                public uint InterruptCount;
            }

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
            [DllImport("libc", SetLastError = true)]
            public static extern int gettid();
            [StructLayout(LayoutKind.Sequential)]
            public struct L_cpu_set_t
            {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024 / 8)]
                public byte[] bits;
            }
        }
        /// <summary>
        /// 0为未知，1为性能核心（无超线程，普通），2为性能核心（开启超线程，普通），3为性能核心（无超线程，频率最高（体质最好）），4为性能核心（开启超线程，频率最高（体质最好）），5为能效核心（无超线程），6为能效核心（开启超线程），7为大3缓核心（专指有X3D的有3DCache的CCD的核心，无超线程），8为大3缓核心（专指有X3D的有3DCache的CCD的核心，开启超线程），9为LPE核心（无超线程），10为LPE核心（开启超线程），11为未知核心（无超线程），12为未知核心（开启超线程）。
        /// 注：NOVALAKE的CPU有大三缓，但是与X3D不同，按照普通处理
        /// </summary>
        public static int[] DefCore;
        public static int CPUCoreCount;
        public static int CPUThreadCount;
        /// <summary>
        /// 将指定线程挂载到指定核心上,核心超出就默认挂载在CPU0，报错则无作为,Linux也许要root权限，MacOS不知道
        /// </summary>
        /// <param name="tid"></param>
        public static bool SetWhichThreadAffinity(nint id, int tid)
        {
            if (tid > CPUThreadCount)
            {
                tid = 0;
            }
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var mask = new IntPtr(1 << tid);
                    WindowsAPI.SetThreadAffinityMask(id, mask);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var mask = new LinuxAPI.L_cpu_set_t { bits = new byte[128] };
                    mask.bits[tid / 8] |= (byte)(1 << (tid % 8));
                    LinuxAPI.sched_setaffinity(0, new IntPtr(mask.bits.Length), ref mask);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    ulong threadId = (ulong)id.ToInt64();
                    var mask = new MacOSAPI.M_cpu_set_t { bits = new byte[128] };
                    mask.bits[tid / 8] |= (byte)(1 << (tid % 8));
                    MacOSAPI.pthread_setaffinity_np(threadId, new IntPtr(mask.bits.Length), ref mask);
                }
                else
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("挂载线程到指定核心出错");
                Console.WriteLine(ex);
                return false;
            }
        }

        /// <summary>
        /// 将当前线程挂载到指定核心上,核心超出就默认挂载在CPU0，报错则无作为,Linux也许要root权限，MacOS不知道
        /// </summary>
        /// <param name="coreId"></param>
        public static bool SetCurrentThreadAffinity(int coreId)
        {
            if (coreId > CPUThreadCount)
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
                else
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("挂载线程到指定核心出错");
                Console.WriteLine(ex);
                return false;
            }
        }

        [Obsolete("Linux最好别用，有BUG")]
        public static void SetThreadAffinity(nint threadId, int coreId)
        {
            if (coreId > CPUThreadCount)
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
            
            CPUCoreCount = (int)cmp.NumberOfCores;
            CPUThreadCount = (int)cmp.NumberOfLogicalProcessors;

            string cpuName = string.IsNullOrWhiteSpace(cmp.Name) || cmp.Name.Equals("unknown", StringComparison.OrdinalIgnoreCase)
                ? CpuTopology.GetCpuBrandString() ?? cmp.Name
                : cmp.Name;
            GVariables.CpuName = cpuName;
            string nmc = cpuName.ToLowerInvariant();
            if (nmc.Contains("amd"))
            {
                GVariables.CpuVendor = CpuVendor.AMD;
            }
            else if (nmc.Contains("intel"))
            {
                GVariables.CpuVendor = CpuVendor.Intel;
            }

            CpuTopology.Detect((int)cmp.NumberOfCores, (int)cmp.NumberOfLogicalProcessors);
            Thrs = new List<SEThread>(0);
            UsagePerThread = new ulong[(int)cmp.NumberOfLogicalProcessors];
            BusyLevelPerThread = new ulong[(int)cmp.NumberOfLogicalProcessors];

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
                                GVariables.CpuType = CPUType.AMD_Ryzen5_X3D;
                            }
                            else if (nmc.Contains("ryzen 7"))
                            {
                                GVariables.CpuType = CPUType.AMD_Ryzen7_X3D;
                            }
                            else if (nmc.Contains("ryzen 9"))
                            {
                                GVariables.CpuType = CPUType.AMD_Ryzen9_X3D;
                            }
                        }
                        else
                        {
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
                            GVariables.CpuType = CPUType.Intel_CoreUltra_5;
                        }
                        else if (nmc.Contains("ultra 7"))
                        {
                            GVariables.CpuType = CPUType.Intel_CoreUltra_7;
                        }
                        else if (nmc.Contains("ultra 9"))
                        {
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
                        }
                        else if (nmc.Contains("i5"))
                        {
                            GVariables.CpuType = CPUType.Intel_Core_i5;
                        }
                        else if (nmc.Contains("i7"))
                        {
                            GVariables.CpuType = CPUType.Intel_Core_i7;
                        }
                        else if (nmc.Contains("i9"))
                        {
                            GVariables.CpuType = CPUType.Intel_Core_i9;
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
                    GVariables.CpuType = CPUType.Intel_Celeron_G;
                }
            }
            else if (nmc.Contains("apple") || Regex.IsMatch(nmc, @"\bm\d{1,2}\b") || Regex.IsMatch(nmc, @"\ba\d{1,2}\b"))
            {
                GVariables.CpuVendor = CpuVendor.Apple;

                Match m = Regex.Match(nmc, @"\bm\s*(\d{1,2})\b");
                Match a = Regex.Match(nmc, @"\ba\s*(\d{1,2})\b");
                if (m.Success)
                {
                    GVariables.CpuVersion = m.Groups[1].Value;
                    if (nmc.Contains("ultra"))
                    {
                        GVariables.CpuType = CPUType.Apple_M_Ultra;
                    }
                    else if (nmc.Contains("max"))
                    {
                        GVariables.CpuType = CPUType.Apple_M_Max;
                    }
                    else if (nmc.Contains("pro"))
                    {
                        GVariables.CpuType = CPUType.Apple_M_Pro;
                    }
                    else
                    {
                        GVariables.CpuType = CPUType.Apple_M;
                    }
                }
                else if (a.Success)
                {
                    GVariables.CpuVersion = a.Groups[1].Value;
                    GVariables.CpuType = nmc.Contains("pro") ? CPUType.Apple_A_Pro : CPUType.Apple_A;
                }
                else
                {
                    GVariables.CpuVersion = "0";
                }
            }
            else if (nmc.Contains("snapdragon") || Regex.IsMatch(nmc, @"\bsm[4687]\d+\b"))
            {
                GVariables.CpuVendor = CpuVendor.Quacomm;
                if (nmc.Contains("elite gen"))
                {
                    GVariables.CpuType = CPUType.Qualcomm_Snapdragon_8Elite_Gen;
                }
                else if (nmc.Contains("elite"))
                {
                    GVariables.CpuType = CPUType.Qualcomm_Snapdragon_8Elite;
                }
                else if (nmc.Contains("8 gen") || nmc.Contains("8+") || Regex.IsMatch(nmc, @"\b(8\d{2}|sm8\d+)\b"))
                {
                    GVariables.CpuType = CPUType.Qualcomm_Snapdragon_8;
                }
                else if (nmc.Contains("7 gen") || Regex.IsMatch(nmc, @"\b(7\d{2}|sm7\d+)\b"))
                {
                    GVariables.CpuType = CPUType.Qualcomm_Snapdragon_7;
                }
                else if (nmc.Contains("6 gen") || Regex.IsMatch(nmc, @"\b(6\d{2}|sm6\d+)\b"))
                {
                    GVariables.CpuType = CPUType.Qualcomm_Snapdragon_6;
                }
                else if (nmc.Contains("4 gen") || Regex.IsMatch(nmc, @"\b(4\d{2}|sm4\d+)\b"))
                {
                    GVariables.CpuType = CPUType.Qualcomm_Snapdragon_4;
                }

                Match gen = Regex.Match(nmc, @"\b[4687]\s*gen\s*(\d+)\b");
                Match model = Regex.Match(nmc, @"\b(?:sm)?([4687]\d{2,4})\b");
                GVariables.CpuVersion = gen.Success ? gen.Groups[1].Value : model.Success ? model.Groups[1].Value : "0";
            }
            else if (nmc.Contains("dimensity") || Regex.IsMatch(nmc, @"\bmt\d+\b"))
            {
                GVariables.CpuVendor = CpuVendor.MediaTek;
                Match model = Regex.Match(nmc, @"\b(?:dimensity|mt)\s*(\d{4})\b");
                int version = model.Success && int.TryParse(model.Groups[1].Value, out int parsed) ? parsed : 0;
                GVariables.CpuVersion = version > 0 ? version.ToString() : "0";

                if (version >= 9000)
                {
                    GVariables.CpuType = CPUType.MediaTek_Dimensity_9000;
                }
                else if (version >= 8000)
                {
                    GVariables.CpuType = CPUType.MediaTek_Dimensity_8000;
                }
                else if (version >= 7000)
                {
                    GVariables.CpuType = CPUType.MediaTek_Dimensity_7000;
                }
                else if (version >= 6000)
                {
                    GVariables.CpuType = CPUType.MediaTek_Dimensity_6000;
                }
            }
            else if (nmc.Contains("kirin"))
            {
                GVariables.CpuVendor = CpuVendor.Hisilicon;
                Match model = Regex.Match(nmc, @"\bkirin\s*(\d{4})\b");
                int version = model.Success && int.TryParse(model.Groups[1].Value, out int parsed) ? parsed : 0;
                GVariables.CpuVersion = version > 0 ? version.ToString() : "0";
                GVariables.CpuType = version >= 9000 ? CPUType.Hisilicon_Kirin_9000 : CPUType.Hisilicon_Kirin_8000;
            }
            else if (nmc.Contains("exynos"))
            {
                GVariables.CpuVendor = CpuVendor.Samsung;
                Match model = Regex.Match(nmc, @"\bexynos\s*(\d{3,5})\b");
                GVariables.CpuVersion = model.Success ? model.Groups[1].Value : "0";
                GVariables.CpuType = CPUType.Samsung_Exynos;
            }
            else if (nmc.Contains("unisoc") || nmc.Contains("tanggula") || Regex.IsMatch(nmc, @"\bt[78]\d+\b"))
            {
                GVariables.CpuVendor = CpuVendor.Unisoc;
                Match model = Regex.Match(nmc, @"\b(?:tanggula|unisoc|t)\s*(\d{3,5})\b");
                GVariables.CpuVersion = model.Success ? model.Groups[1].Value : "0";
                GVariables.CpuType = CPUType.Unisoc_Tanggula;
            }
            else//其他型号以后再说
            {
                Console.WriteLine("其他处理器");
                GVariables.CpuVendor = CpuVendor.Unknown;
                GVariables.CpuType = CPUType.Other;
            }
            SetCpuTopologyDefCore((int)cmp.NumberOfCores, (int)cmp.NumberOfLogicalProcessors);
            GVariables.OnEngineClose += OnCLe;


            SELogger.Log("显示CPU信息");
            for(int i =0;i < DefCore.Length;i++)
            {
                SELogger.Log($"核心{i}类型:{DefCore[i]}");
            }

            DispatcherWorkerThread = CreateThreadORG(DispatcherWorker, ThreadPriority.Normal);
            DispatcherWorkerThread.Start();  // 启动调度器线程
            GVariables.OnEngineClose += DispatcherClose;

        }
        static SEThread? DispatcherWorkerThread;

        

        private static void SetCpuTopologyDefCore(int fallbackPhysicalCoreCount, int fallbackLogicalProcessorCount)
        {
            if (CpuTopology.Cores.Length == 0)
            {
                CpuTopology.SetFallback(fallbackPhysicalCoreCount, fallbackLogicalProcessorCount);
            }

            int physicalCoreCount = CpuTopology.PhysicalCoreCount > 0 ? CpuTopology.PhysicalCoreCount : Math.Max(1, fallbackPhysicalCoreCount);
            DefCore = new int[physicalCoreCount];

            for (int i = 0; i < DefCore.Length; i++)
            {
                if (i < CpuTopology.Cores.Length)
                {
                    DefCore[i] = CpuTopology.ToDefCore(in CpuTopology.Cores[i]);
                }
                else
                {
                    DefCore[i] = fallbackLogicalProcessorCount > fallbackPhysicalCoreCount ? 12 : 11;
                }
            }
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
                
                



                DefCore = new int[Environment.ProcessorCount];
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
GVariables.CpuType = CPUType.AMD_Ryzen5_X3D;
                                        }
                                        else if (nmc.Contains("ryzen 7"))
                                        {
GVariables.CpuType = CPUType.AMD_Ryzen7_X3D;
                                        }
                                        else if (nmc.Contains("ryzen 9"))
                                        {
                                            GVariables.CpuType = CPUType.AMD_Ryzen9_X3D;
                                        }
                                    }
                                    else
                                    {
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
                                    }
                                    else if (nmc.Contains("ultra 5"))
                                    {
                                        GVariables.CpuType = CPUType.Intel_CoreUltra_5;
                                    }
                                    else if (nmc.Contains("ultra 7"))
                                    {
                                        GVariables.CpuType = CPUType.Intel_CoreUltra_7;
                                    }
                                    else if (nmc.Contains("ultra 9"))
                                    {
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
}
                                    else if (nmc.Contains("i5"))
                                    {
                                        GVariables.CpuType = CPUType.Intel_Core_i5;
                                        if (int.Parse(GVariables.CpuVersion) > 11)
                                        {
                                        }
                                        else
                                        {
}
                                    }
                                    else if (nmc.Contains("i7"))
                                    {
                                        GVariables.CpuType = CPUType.Intel_Core_i7;
                                        if (int.Parse(GVariables.CpuVersion) > 11)
                                        {
                                        }
                                        else
                                        {
}
                                    }
                                    else if (nmc.Contains("i9"))
                                    {
                                        GVariables.CpuType = CPUType.Intel_Core_i9;
                                        if (int.Parse(GVariables.CpuVersion) > 11)
                                        {
                                        }
                                        else
                                        {
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
