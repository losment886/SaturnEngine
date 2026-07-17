using SaturnEngine.Global;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SaturnEngine.Performance
{
    public enum CpuCoreKind : int
    {
        Unknown = 0,
        Performance = 1,
        PreferredPerformance = 2,
        Efficiency = 3,
        X3DCache = 4,
        LowPowerEfficiency = 5,
    }

    public struct PhysicalCoreInfo
    {
        public int CoreId;
        public int[] LogicalIds;
        public bool HasSmt;
        public int EfficiencyClass;
        public int SchedulingClass;
        public long MaxFreqKHz;
        public int L3GroupId;
        public long L3CacheSize;
        public CpuCoreKind Kind;
    }

    public static class CpuTopology
    {
        public static PhysicalCoreInfo[] Cores = Array.Empty<PhysicalCoreInfo>();
        public static int[] LogicalToPhysical = Array.Empty<int>();
        public static bool Detected;
        public static int PhysicalCoreCount;

        public static void Detect(int fallbackPhysicalCoreCount = 0, int fallbackLogicalProcessorCount = 0)
        {
            try
            {
                PhysicalCoreInfo[]? cores = null;
                int logicalCount = fallbackLogicalProcessorCount > 0 ? fallbackLogicalProcessorCount : Environment.ProcessorCount;

                if (GVariables.OS == OS.Windows || GVariables.OS == OS.XBox || OperatingSystem.IsWindows())
                {
                    cores = DetectWindows();
                }
                else if (GVariables.OS == OS.Linux || GVariables.OS == OS.Android || GVariables.OS == OS.HarmonyOS || OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
                {
                    cores = DetectLinuxSysfs();
                }
                else if (GVariables.OS == OS.MacOS || GVariables.OS == OS.IOS || OperatingSystem.IsMacOS() || OperatingSystem.IsIOS())
                {
                    cores = DetectAppleSysctl();
                }
                else if (GVariables.OS == OS.PlayStation)
                {
                    cores = DetectPlayStation();
                }

                if (cores is { Length: > 0 })
                {
                    SetDetectedCores(cores, logicalCount);
                    Detected = true;
                    return;
                }
            }
            catch
            {
            }

            SetFallback(fallbackPhysicalCoreCount, fallbackLogicalProcessorCount);
        }

        public static int ToDefCore(in PhysicalCoreInfo c)
        {
            return c.Kind switch
            {
                CpuCoreKind.Performance => c.HasSmt ? 2 : 1,
                CpuCoreKind.PreferredPerformance => c.HasSmt ? 4 : 3,
                CpuCoreKind.Efficiency => c.HasSmt ? 6 : 5,
                CpuCoreKind.X3DCache => c.HasSmt ? 8 : 7,
                CpuCoreKind.LowPowerEfficiency => c.HasSmt ? 10 : 9,
                CpuCoreKind.Unknown => c.HasSmt ? 12 : 11,
                _ => 0,
            };
        }

        public static string? GetCpuBrandString()
        {
            if (GVariables.OS == OS.MacOS || GVariables.OS == OS.IOS || OperatingSystem.IsMacOS() || OperatingSystem.IsIOS())
            {
                return ReadSysctlString("machdep.cpu.brand_string")
                    ?? ReadSysctlString("hw.model");
            }

            if (GVariables.OS == OS.Android || GVariables.OS == OS.HarmonyOS || OperatingSystem.IsAndroid())
            {
                return ReadAndroidSystemProperty("ro.soc.model")
                    ?? ReadAndroidSystemProperty("ro.hardware")
                    ?? ReadLinuxCpuInfoBrandString();
            }

            if (GVariables.OS == OS.Linux || OperatingSystem.IsLinux())
            {
                return ReadLinuxCpuInfoBrandString();
            }

            return null;
        }

        internal static PhysicalCoreInfo[] NormalizeCoreIds(IEnumerable<PhysicalCoreInfo> source)
        {
            return source
                .Where(static core => core.LogicalIds is { Length: > 0 })
                .OrderBy(static core => core.LogicalIds.Min())
                .Select((core, index) =>
                {
                    core.CoreId = index;
                    core.LogicalIds = core.LogicalIds.Order().Distinct().ToArray();
                    core.HasSmt = core.LogicalIds.Length > 1;
                    return core;
                })
                .ToArray();
        }

        internal static void MarkPreferredPerformanceCores(PhysicalCoreInfo[] cores)
        {
            if (cores.Length == 0)
            {
                return;
            }

            int[] perfIndexes = Enumerable.Range(0, cores.Length)
                .Where(i => cores[i].Kind == CpuCoreKind.Performance)
                .ToArray();
            if (perfIndexes.Length <= 1)
            {
                return;
            }

            //优先使用系统调度等级（Windows CPU Set 的 SchedulingClass，体质核心更高）
            if (TryMarkPreferredBy(cores, perfIndexes, static core => core.SchedulingClass))
            {
                return;
            }

            //回退到默频差异（Linux cpufreq 等每核真实值）
            TryMarkPreferredBy(cores, perfIndexes, static core => core.MaxFreqKHz);
        }

        private static bool TryMarkPreferredBy(PhysicalCoreInfo[] cores, int[] perfIndexes, Func<PhysicalCoreInfo, long> selector)
        {
            long max = perfIndexes.Select(i => selector(cores[i])).Max();
            if (max <= 0)
            {
                return false;
            }

            int[] preferred = perfIndexes.Where(i => selector(cores[i]) == max).ToArray();
            //全部性能核心指标相同则无法区分体质核心，不标记
            if (preferred.Length == 0 || preferred.Length == perfIndexes.Length)
            {
                return false;
            }

            foreach (int i in preferred)
            {
                cores[i].Kind = CpuCoreKind.PreferredPerformance;
            }

            return true;
        }

        internal static void SetDetectedCores(PhysicalCoreInfo[] cores, int logicalCount)
        {
            Cores = NormalizeCoreIds(cores);
            PhysicalCoreCount = Cores.Length;
            LogicalToPhysical = BuildLogicalToPhysical(Cores, logicalCount);
        }

        internal static int[] BuildLogicalToPhysical(PhysicalCoreInfo[] cores, int logicalCount)
        {
            int maxLogicalId = cores
                .SelectMany(static core => core.LogicalIds ?? Array.Empty<int>())
                .DefaultIfEmpty(-1)
                .Max();
            int count = Math.Max(logicalCount, maxLogicalId + 1);
            int[] logicalToPhysical = Enumerable.Repeat(-1, count).ToArray();

            for (int i = 0; i < cores.Length; i++)
            {
                foreach (int logicalId in cores[i].LogicalIds)
                {
                    if ((uint)logicalId < (uint)logicalToPhysical.Length)
                    {
                        logicalToPhysical[logicalId] = i;
                    }
                }
            }

            return logicalToPhysical;
        }

        public static void SetFallback(int physicalCoreCount = 0, int logicalProcessorCount = 0)
        {
            int logicalCount = logicalProcessorCount > 0 ? logicalProcessorCount : Environment.ProcessorCount;
            int coreCount = physicalCoreCount > 0 ? physicalCoreCount : Math.Max(1, logicalCount);
            bool hasSmt = logicalCount > coreCount;
            PhysicalCoreInfo[] cores = new PhysicalCoreInfo[coreCount];
            int logicalIndex = 0;
            int logicalPerCore = hasSmt ? Math.Max(1, logicalCount / coreCount) : 1;

            for (int i = 0; i < cores.Length; i++)
            {
                int count = i == cores.Length - 1 ? Math.Max(1, logicalCount - logicalIndex) : logicalPerCore;
                if (logicalIndex >= logicalCount)
                {
                    count = 1;
                }

                int[] logicalIds = Enumerable.Range(Math.Min(logicalIndex, Math.Max(0, logicalCount - 1)), count)
                    .Where(id => id < logicalCount)
                    .ToArray();
                if (logicalIds.Length == 0)
                {
                    logicalIds = new[] { Math.Min(i, Math.Max(0, logicalCount - 1)) };
                }

                cores[i] = new PhysicalCoreInfo
                {
                    CoreId = i,
                    LogicalIds = logicalIds,
                    HasSmt = hasSmt,
                    EfficiencyClass = -1,
                    L3GroupId = -1,
                    Kind = CpuCoreKind.Unknown,
                };
                logicalIndex += count;
            }

            Cores = cores;
            PhysicalCoreCount = Cores.Length;
            LogicalToPhysical = BuildLogicalToPhysical(Cores, logicalCount);
            Detected = false;
        }

        private static PhysicalCoreInfo[]? DetectWindows()
        {
            PhysicalCoreInfo[]? cores = DetectWindowsCpuSets();
            cores ??= DetectWindowsProcessorCores();
            if (cores is not { Length: > 0 })
            {
                return null;
            }

            Dictionary<int, long> maxMhzPerLogical = GetWindowsRegistryMhz();
            //注册表 ~MHz 是每核真实默频，能区分体质核心；无差异或读取失败时回退
            if (maxMhzPerLogical.Count == 0 || maxMhzPerLogical.Values.Distinct().Count() <= 1)
            {
                Dictionary<int, long> powerMhz = GetWindowsProcessorMaxMhz();
                if (powerMhz.Count > 0)
                {
                    maxMhzPerLogical = powerMhz;
                }
            }
            Dictionary<int, (int GroupId, long Size)> l3PerLogical = GetWindowsL3CacheByLogicalId();

            for (int i = 0; i < cores.Length; i++)
            {
                long maxFreq = cores[i].LogicalIds
                    .Select(logicalId => maxMhzPerLogical.TryGetValue(logicalId, out long mhz) ? mhz * 1000 : 0)
                    .DefaultIfEmpty(0)
                    .Max();

                if (maxFreq > 0)
                {
                    cores[i].MaxFreqKHz = maxFreq;
                }

                foreach (int logicalId in cores[i].LogicalIds)
                {
                    if (l3PerLogical.TryGetValue(logicalId, out (int GroupId, long Size) l3))
                    {
                        cores[i].L3GroupId = l3.GroupId;
                        cores[i].L3CacheSize = l3.Size;
                        break;
                    }
                }
            }

            ClassifyByEfficiency(cores);
            MarkWindowsX3DCacheCores(cores);
            MarkPreferredPerformanceCores(cores);
            return cores;
        }

        [SupportedOSPlatform("windows")]
        private static PhysicalCoreInfo[]? DetectWindowsCpuSets()
        {
            try
            {
                if (!GetSystemCpuSetInformation(IntPtr.Zero, 0, out uint requiredLength, IntPtr.Zero, 0) && requiredLength == 0)
                {
                    return null;
                }

                IntPtr buffer = Marshal.AllocHGlobal((int)requiredLength);
                try
                {
                    if (!GetSystemCpuSetInformation(buffer, requiredLength, out requiredLength, IntPtr.Zero, 0))
                    {
                        return null;
                    }

                    Dictionary<(ushort Group, byte CoreIndex), List<WindowsCpuSet>> groups = new();
                    int offset = 0;
                    while (offset + 8 <= requiredLength)
                    {
                        IntPtr current = IntPtr.Add(buffer, offset);
                        uint size = (uint)Marshal.ReadInt32(current);
                        int type = Marshal.ReadInt32(current, 4);
                        if (size == 0 || offset + size > requiredLength)
                        {
                            break;
                        }

                        if (type == 0)
                        {
                            WindowsCpuSet cpuSet = ReadWindowsCpuSet(current);
                            groups.TryAdd((cpuSet.Group, cpuSet.CoreIndex), new List<WindowsCpuSet>());
                            groups[(cpuSet.Group, cpuSet.CoreIndex)].Add(cpuSet);
                        }

                        offset += (int)size;
                    }

                    if (groups.Count == 0)
                    {
                        return null;
                    }

                    return NormalizeCoreIds(groups.Values.Select(group =>
                    {
                        int[] logicalIds = group
                            .Select(static cpuSet => GetGlobalLogicalProcessorId(cpuSet.Group, cpuSet.LogicalProcessorIndex))
                            .Where(static id => id >= 0)
                            .Order()
                            .Distinct()
                            .ToArray();

                        return new PhysicalCoreInfo
                        {
                            LogicalIds = logicalIds,
                            HasSmt = logicalIds.Length > 1,
                            EfficiencyClass = group.Max(static cpuSet => cpuSet.EfficiencyClass),
                            SchedulingClass = group.Max(static cpuSet => cpuSet.SchedulingClass),
                            L3GroupId = group.Select(static cpuSet => (int)cpuSet.LastLevelCacheIndex).DefaultIfEmpty(-1).Max(),
                            Kind = CpuCoreKind.Unknown,
                        };
                    }));
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                return null;
            }
        }

        [SupportedOSPlatform("windows")]
        private static PhysicalCoreInfo[]? DetectWindowsProcessorCores()
        {
            try
            {
                if (!GetLogicalProcessorInformationEx(0, IntPtr.Zero, ref UnsafeZero) && Marshal.GetLastWin32Error() != 122)
                {
                    return null;
                }

                uint requiredLength = 0;
                GetLogicalProcessorInformationEx(0, IntPtr.Zero, ref requiredLength);
                if (requiredLength == 0)
                {
                    return null;
                }

                IntPtr buffer = Marshal.AllocHGlobal((int)requiredLength);
                try
                {
                    if (!GetLogicalProcessorInformationEx(0, buffer, ref requiredLength))
                    {
                        return null;
                    }

                    List<PhysicalCoreInfo> cores = new();
                    int offset = 0;
                    while (offset + 8 <= requiredLength)
                    {
                        IntPtr current = IntPtr.Add(buffer, offset);
                        int relationship = Marshal.ReadInt32(current);
                        int size = Marshal.ReadInt32(current, 4);
                        if (size <= 0 || offset + size > requiredLength)
                        {
                            break;
                        }

                        if (relationship == 0)
                        {
                            byte efficiencyClass = Marshal.ReadByte(current, 9);
                            ushort groupCount = (ushort)Marshal.ReadInt16(current, 30);
                            List<int> logicalIds = new();
                            IntPtr groupMaskPtr = IntPtr.Add(current, 32);
                            int groupAffinitySize = Marshal.SizeOf<WindowsGroupAffinity>();
                            for (int i = 0; i < groupCount; i++)
                            {
                                WindowsGroupAffinity groupAffinity = Marshal.PtrToStructure<WindowsGroupAffinity>(IntPtr.Add(groupMaskPtr, i * groupAffinitySize));
                                logicalIds.AddRange(EnumerateWindowsLogicalIds(groupAffinity.Group, groupAffinity.Mask));
                            }

                            if (logicalIds.Count > 0)
                            {
                                cores.Add(new PhysicalCoreInfo
                                {
                                    LogicalIds = logicalIds.Order().Distinct().ToArray(),
                                    HasSmt = logicalIds.Count > 1,
                                    EfficiencyClass = efficiencyClass,
                                    L3GroupId = -1,
                                    Kind = CpuCoreKind.Unknown,
                                });
                            }
                        }

                        offset += size;
                    }

                    return cores.Count > 0 ? NormalizeCoreIds(cores) : null;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                return null;
            }
        }

        private static uint UnsafeZero;

        private static void ClassifyByEfficiency(PhysicalCoreInfo[] cores)
        {
            int[] classes = cores.Select(static core => core.EfficiencyClass).Distinct().Order().ToArray();
            if (classes.Length <= 1)
            {
                for (int i = 0; i < cores.Length; i++)
                {
                    cores[i].Kind = CpuCoreKind.Performance;
                }
                return;
            }

            int min = classes[0];
            int max = classes[^1];
            for (int i = 0; i < cores.Length; i++)
            {
                if (cores[i].EfficiencyClass == max)
                {
                    cores[i].Kind = CpuCoreKind.Performance;
                }
                else if (cores[i].EfficiencyClass == min && classes.Length >= 3)
                {
                    cores[i].Kind = CpuCoreKind.LowPowerEfficiency;
                }
                else
                {
                    cores[i].Kind = CpuCoreKind.Efficiency;
                }
            }
        }

        private static void MarkWindowsX3DCacheCores(PhysicalCoreInfo[] cores)
        {
            if (GVariables.CpuVendor != CpuVendor.AMD)
            {
                return;
            }

            long[] l3Sizes = cores
                .Where(static core => core.L3CacheSize > 0)
                .Select(static core => core.L3CacheSize)
                .Distinct()
                .Order()
                .ToArray();
            if (l3Sizes.Length < 2)
            {
                return;
            }

            long min = l3Sizes[0];
            long max = l3Sizes[^1];
            if (min <= 0 || max < min * 2)
            {
                return;
            }

            for (int i = 0; i < cores.Length; i++)
            {
                if (cores[i].L3CacheSize == max)
                {
                    cores[i].Kind = CpuCoreKind.X3DCache;
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private static Dictionary<int, long> GetWindowsRegistryMhz()
        {
            Dictionary<int, long> result = new();
            try
            {
                using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor");
                if (root == null)
                {
                    return result;
                }

                foreach (string name in root.GetSubKeyNames())
                {
                    if (!int.TryParse(name, out int logicalId))
                    {
                        continue;
                    }

                    using var sub = root.OpenSubKey(name);
                    if (sub?.GetValue("~MHz") is int mhz && mhz > 0)
                    {
                        result[logicalId] = mhz;
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        [SupportedOSPlatform("windows")]
        private static Dictionary<int, long> GetWindowsProcessorMaxMhz()
        {
            Dictionary<int, long> result = new();
            try
            {
                int count = Environment.ProcessorCount;
                int size = Marshal.SizeOf<ProcessorPowerInformation>() * count;
                IntPtr buffer = Marshal.AllocHGlobal(size);
                try
                {
                    if (CallNtPowerInformation(11, IntPtr.Zero, 0, buffer, (uint)size) != 0)
                    {
                        return result;
                    }

                    int structSize = Marshal.SizeOf<ProcessorPowerInformation>();
                    for (int i = 0; i < count; i++)
                    {
                        ProcessorPowerInformation info = Marshal.PtrToStructure<ProcessorPowerInformation>(IntPtr.Add(buffer, i * structSize));
                        result[(int)info.Number] = info.MaxMhz;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
            }

            return result;
        }

        [SupportedOSPlatform("windows")]
        private static Dictionary<int, (int GroupId, long Size)> GetWindowsL3CacheByLogicalId()
        {
            Dictionary<int, (int GroupId, long Size)> result = new();
            try
            {
                uint requiredLength = 0;
                GetLogicalProcessorInformationEx(2, IntPtr.Zero, ref requiredLength);
                if (requiredLength == 0)
                {
                    return result;
                }

                IntPtr buffer = Marshal.AllocHGlobal((int)requiredLength);
                try
                {
                    if (!GetLogicalProcessorInformationEx(2, buffer, ref requiredLength))
                    {
                        return result;
                    }

                    int offset = 0;
                    int cacheGroupId = 0;
                    while (offset + 8 <= requiredLength)
                    {
                        IntPtr current = IntPtr.Add(buffer, offset);
                        int relationship = Marshal.ReadInt32(current);
                        int size = Marshal.ReadInt32(current, 4);
                        if (size <= 0 || offset + size > requiredLength)
                        {
                            break;
                        }

                        if (relationship == 2)
                        {
                            WindowsCacheRelationship cache = Marshal.PtrToStructure<WindowsCacheRelationship>(IntPtr.Add(current, 8));
                            if (cache.Level == 3)
                            {
                                foreach (int logicalId in EnumerateWindowsLogicalIds(cache.GroupMask.Group, cache.GroupMask.Mask))
                                {
                                    result[logicalId] = (cacheGroupId, cache.CacheSize);
                                }
                                cacheGroupId++;
                            }
                        }

                        offset += size;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
            }

            return result;
        }

        private static WindowsCpuSet ReadWindowsCpuSet(IntPtr ptr)
        {
            IntPtr cpuSet = IntPtr.Add(ptr, 8);
            return new WindowsCpuSet
            {
                Id = (uint)Marshal.ReadInt32(cpuSet),
                Group = (ushort)Marshal.ReadInt16(cpuSet, 4),
                LogicalProcessorIndex = Marshal.ReadByte(cpuSet, 6),
                CoreIndex = Marshal.ReadByte(cpuSet, 7),
                LastLevelCacheIndex = Marshal.ReadByte(cpuSet, 8),
                SchedulingClass = Marshal.ReadByte(cpuSet, 9),
                EfficiencyClass = Marshal.ReadByte(cpuSet, 10),
            };
        }

        private static IEnumerable<int> EnumerateWindowsLogicalIds(ushort group, UIntPtr mask)
        {
            ulong value = mask.ToUInt64();
            int groupBase = GetWindowsGroupBaseLogicalId(group);
            for (int bit = 0; bit < 64; bit++)
            {
                if (((value >> bit) & 1UL) != 0)
                {
                    yield return groupBase + bit;
                }
            }
        }

        private static int GetGlobalLogicalProcessorId(ushort group, byte groupLocalLogicalId)
        {
            return GetWindowsGroupBaseLogicalId(group) + groupLocalLogicalId;
        }

        [SupportedOSPlatform("windows")]
        private static int GetWindowsGroupBaseLogicalId(ushort group)
        {
            int id = 0;
            for (ushort i = 0; i < group; i++)
            {
                id += GetActiveProcessorCount(i);
            }
            return id;
        }

        private struct WindowsCpuSet
        {
            public uint Id;
            public ushort Group;
            public byte LogicalProcessorIndex;
            public byte CoreIndex;
            public byte LastLevelCacheIndex;
            public byte SchedulingClass;
            public byte EfficiencyClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsGroupAffinity
        {
            public UIntPtr Mask;
            public ushort Group;
            public ushort Reserved0;
            public ushort Reserved1;
            public ushort Reserved2;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsCacheRelationship
        {
            public byte Level;
            public byte Associativity;
            public ushort LineSize;
            public uint CacheSize;
            public int Type;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public ushort[] Reserved;
            public WindowsGroupAffinity GroupMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessorPowerInformation
        {
            public uint Number;
            public uint MaxMhz;
            public uint CurrentMhz;
            public uint MhzLimit;
            public uint MaxIdleState;
            public uint CurrentIdleState;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemCpuSetInformation(IntPtr information, uint bufferLength, out uint returnedLength, IntPtr process, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref uint returnedLength);

        [DllImport("kernel32.dll")]
        private static extern int GetActiveProcessorCount(ushort groupNumber);

        [DllImport("powrprof.dll")]
        private static extern uint CallNtPowerInformation(int informationLevel, IntPtr inputBuffer, uint inputBufferLength, IntPtr outputBuffer, uint outputBufferLength);

        private static PhysicalCoreInfo[]? DetectLinuxSysfs()
        {
            const string cpuRoot = "/sys/devices/system/cpu";
            if (!Directory.Exists(cpuRoot))
            {
                return null;
            }

            int[] onlineCpuIds = ReadLinuxOnlineCpuIds(cpuRoot);
            if (onlineCpuIds.Length == 0)
            {
                return null;
            }

            HashSet<int> onlineSet = onlineCpuIds.ToHashSet();
            Dictionary<(int PackageId, int CoreId), LinuxCoreBuilder> builders = new();

            foreach (int cpuId in onlineCpuIds)
            {
                string cpuPath = Path.Combine(cpuRoot, $"cpu{cpuId}");
                int packageId = ReadIntFile(Path.Combine(cpuPath, "topology", "physical_package_id"))
                    ?? ReadIntFile(Path.Combine(cpuPath, "topology", "package_id"))
                    ?? 0;
                int coreId = ReadIntFile(Path.Combine(cpuPath, "topology", "core_id")) ?? cpuId;
                (int PackageId, int CoreId) key = (packageId, coreId);

                if (!builders.TryGetValue(key, out LinuxCoreBuilder builder))
                {
                    builder = new LinuxCoreBuilder
                    {
                        PackageId = packageId,
                        CoreId = coreId,
                        LogicalIds = new SortedSet<int>(),
                        SiblingIds = new SortedSet<int>(),
                        L3GroupId = -1,
                    };
                    builders[key] = builder;
                }

                builder.LogicalIds.Add(cpuId);
                foreach (int siblingId in ReadCpuListFile(Path.Combine(cpuPath, "topology", "thread_siblings_list")))
                {
                    if (onlineSet.Contains(siblingId))
                    {
                        builder.SiblingIds.Add(siblingId);
                    }
                }

                builder.MaxFreqKHz = Math.Max(builder.MaxFreqKHz, ReadLongFile(Path.Combine(cpuPath, "cpufreq", "cpuinfo_max_freq")) ?? 0);
                builder.CpuCapacity = Math.Max(builder.CpuCapacity, ReadLongFile(Path.Combine(cpuPath, "cpu_capacity")) ?? 0);
                builder.CpuCapacity = Math.Max(builder.CpuCapacity, ReadLongFile(Path.Combine(cpuPath, "topology", "cpu_capacity")) ?? 0);

                LinuxCacheInfo? l3 = ReadLinuxL3CacheInfo(cpuPath, onlineSet);
                if (l3.HasValue)
                {
                    builder.L3GroupId = l3.Value.GroupId;
                    builder.L3CacheSize = l3.Value.SizeBytes;
                }
            }

            PhysicalCoreInfo[] cores = NormalizeCoreIds(builders.Values.Select(static builder =>
            {
                int[] logicalIds = builder.SiblingIds.Count > 0
                    ? builder.SiblingIds.ToArray()
                    : builder.LogicalIds.ToArray();

                return new PhysicalCoreInfo
                {
                    LogicalIds = logicalIds,
                    HasSmt = logicalIds.Length > 1,
                    EfficiencyClass = builder.CpuCapacity > 0 ? (int)Math.Min(int.MaxValue, builder.CpuCapacity) : 0,
                    MaxFreqKHz = builder.MaxFreqKHz,
                    L3GroupId = builder.L3GroupId,
                    L3CacheSize = builder.L3CacheSize,
                    Kind = CpuCoreKind.Unknown,
                };
            }));

            ClassifyLinuxCores(cores);
            MarkLinuxX3DCacheCores(cores);
            MarkPreferredPerformanceCores(cores);
            return cores.Length > 0 ? cores : null;
        }

        private static void ClassifyLinuxCores(PhysicalCoreInfo[] cores)
        {
            long[] capacities = cores.Select(static core => (long)core.EfficiencyClass).ToArray();
            long[] nonZeroCapacities = capacities.Where(static value => value > 0).Distinct().Order().ToArray();
            if (nonZeroCapacities.Length > 0 && nonZeroCapacities.Length <= cores.Length)
            {
                for (int i = 0; i < cores.Length; i++)
                {
                    long capacity = i < capacities.Length ? capacities[i] : 0;
                    cores[i].EfficiencyClass = capacity > 0 ? Array.IndexOf(nonZeroCapacities, capacity) : 0;
                    cores[i].Kind = MapClusterToKind(cores[i].EfficiencyClass, nonZeroCapacities.Length);
                }
                return;
            }

            long[] frequencies = cores.Where(static core => core.MaxFreqKHz > 0).Select(static core => core.MaxFreqKHz).Distinct().Order().ToArray();
            if (frequencies.Length == 0)
            {
                for (int i = 0; i < cores.Length; i++)
                {
                    cores[i].Kind = CpuCoreKind.Unknown;
                    cores[i].EfficiencyClass = -1;
                }
                return;
            }

            for (int i = 0; i < cores.Length; i++)
            {
                int cluster = cores[i].MaxFreqKHz > 0 ? Array.IndexOf(frequencies, cores[i].MaxFreqKHz) : 0;
                cores[i].EfficiencyClass = cluster;
                cores[i].Kind = MapClusterToKind(cluster, frequencies.Length);
            }
        }

        private static CpuCoreKind MapClusterToKind(int cluster, int clusterCount)
        {
            if (clusterCount <= 1)
            {
                return CpuCoreKind.Performance;
            }

            if (cluster == clusterCount - 1)
            {
                return CpuCoreKind.Performance;
            }

            if (cluster == 0 && clusterCount >= 3)
            {
                return CpuCoreKind.LowPowerEfficiency;
            }

            if (cluster == 0)
            {
                return CpuCoreKind.Efficiency;
            }

            return clusterCount >= 3 && cluster == 1 ? CpuCoreKind.Efficiency : CpuCoreKind.Performance;
        }

        private static void MarkLinuxX3DCacheCores(PhysicalCoreInfo[] cores)
        {
            string cpuInfo = ReadTextFile("/proc/cpuinfo")?.ToLowerInvariant() ?? string.Empty;
            bool amd = GVariables.CpuVendor == CpuVendor.AMD || cpuInfo.Contains("authenticamd") || cpuInfo.Contains("amd");
            if (!amd)
            {
                return;
            }

            long[] sizes = cores
                .Where(static core => core.L3CacheSize > 0)
                .Select(static core => core.L3CacheSize)
                .Distinct()
                .Order()
                .ToArray();
            if (sizes.Length < 2)
            {
                return;
            }

            long min = sizes[0];
            long max = sizes[^1];
            if (min <= 0 || max < min * 2)
            {
                return;
            }

            for (int i = 0; i < cores.Length; i++)
            {
                if (cores[i].L3CacheSize == max)
                {
                    cores[i].Kind = CpuCoreKind.X3DCache;
                }
            }
        }

        private static int[] ReadLinuxOnlineCpuIds(string cpuRoot)
        {
            int[] fromOnline = ReadCpuListFile(Path.Combine(cpuRoot, "online"));
            if (fromOnline.Length > 0)
            {
                return fromOnline;
            }

            return Directory.EnumerateDirectories(cpuRoot, "cpu*")
                .Select(static path => Path.GetFileName(path))
                .Where(static name => name is { Length: > 3 } && name[3..].All(char.IsDigit))
                .Select(static name => int.Parse(name![3..]))
                .Order()
                .ToArray();
        }

        private static LinuxCacheInfo? ReadLinuxL3CacheInfo(string cpuPath, HashSet<int> onlineCpuIds)
        {
            string cacheRoot = Path.Combine(cpuPath, "cache");
            if (!Directory.Exists(cacheRoot))
            {
                return null;
            }

            foreach (string indexPath in Directory.EnumerateDirectories(cacheRoot, "index*"))
            {
                int level = ReadIntFile(Path.Combine(indexPath, "level")) ?? -1;
                if (level != 3)
                {
                    continue;
                }

                int[] sharedCpuIds = ReadCpuListFile(Path.Combine(indexPath, "shared_cpu_list"))
                    .Where(onlineCpuIds.Contains)
                    .ToArray();
                long size = ParseLinuxCacheSize(ReadTextFile(Path.Combine(indexPath, "size")));
                return new LinuxCacheInfo
                {
                    GroupId = sharedCpuIds.Length > 0 ? sharedCpuIds.Min() : -1,
                    SizeBytes = size,
                };
            }

            return null;
        }

        private static int[] ReadCpuListFile(string path)
        {
            string? value = ReadTextFile(path);
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<int>();
            }

            SortedSet<int> ids = new();
            foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] range = part.Split('-', StringSplitOptions.TrimEntries);
                if (range.Length == 2 && int.TryParse(range[0], out int start) && int.TryParse(range[1], out int end))
                {
                    for (int id = start; id <= end; id++)
                    {
                        ids.Add(id);
                    }
                }
                else if (int.TryParse(part, out int id))
                {
                    ids.Add(id);
                }
            }

            return ids.ToArray();
        }

        private static int? ReadIntFile(string path)
        {
            return int.TryParse(ReadTextFile(path), out int value) ? value : null;
        }

        private static long? ReadLongFile(string path)
        {
            return long.TryParse(ReadTextFile(path), out long value) ? value : null;
        }

        private static string? ReadTextFile(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadLinuxCpuInfoBrandString()
        {
            string? cpuInfo = ReadTextFile("/proc/cpuinfo");
            if (string.IsNullOrWhiteSpace(cpuInfo))
            {
                return null;
            }

            string? modelName = null;
            foreach (string line in cpuInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int index = line.IndexOf(':');
                if (index <= 0 || index >= line.Length - 1)
                {
                    continue;
                }

                string key = line[..index].Trim();
                string value = line[(index + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (key.Equals("Hardware", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Processor", StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }

                if (modelName == null && key.Equals("model name", StringComparison.OrdinalIgnoreCase))
                {
                    modelName = value;
                }
            }

            return modelName;
        }

        private static long ParseLinuxCacheSize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            value = value.Trim();
            char suffix = char.ToUpperInvariant(value[^1]);
            long multiplier = suffix switch
            {
                'K' => 1024,
                'M' => 1024 * 1024,
                'G' => 1024L * 1024 * 1024,
                _ => 1,
            };
            string number = char.IsLetter(suffix) ? value[..^1] : value;
            return long.TryParse(number.Trim(), out long size) ? size * multiplier : 0;
        }

        private sealed class LinuxCoreBuilder
        {
            public int PackageId;
            public int CoreId;
            public SortedSet<int> LogicalIds = new();
            public SortedSet<int> SiblingIds = new();
            public long MaxFreqKHz;
            public long CpuCapacity;
            public int L3GroupId;
            public long L3CacheSize;
        }

        private struct LinuxCacheInfo
        {
            public int GroupId;
            public long SizeBytes;
        }

        private static PhysicalCoreInfo[]? DetectAppleSysctl()
        {
            int physicalCpu = ReadSysctlInt("hw.physicalcpu") ?? 0;
            int logicalCpu = ReadSysctlInt("hw.logicalcpu") ?? Environment.ProcessorCount;
            int perfLevels = ReadSysctlInt("hw.nperflevels") ?? 0;

            if (physicalCpu <= 0)
            {
                return null;
            }

            List<PhysicalCoreInfo> cores = new();
            int logicalId = 0;

            if (perfLevels > 1)
            {
                for (int level = 0; level < perfLevels; level++)
                {
                    int levelPhysical = ReadSysctlInt($"hw.perflevel{level}.physicalcpu") ?? 0;
                    int levelLogical = ReadSysctlInt($"hw.perflevel{level}.logicalcpu") ?? levelPhysical;
                    if (levelPhysical <= 0)
                    {
                        continue;
                    }

                    bool hasSmt = levelLogical > levelPhysical;
                    int logicalPerCore = hasSmt ? Math.Max(1, levelLogical / levelPhysical) : 1;
                    CpuCoreKind kind = level == 0 ? CpuCoreKind.Performance : CpuCoreKind.Efficiency;

                    for (int i = 0; i < levelPhysical; i++)
                    {
                        int count = Math.Min(logicalPerCore, Math.Max(1, logicalCpu - logicalId));
                        int[] logicalIds = Enumerable.Range(logicalId, count).Where(id => id < logicalCpu).ToArray();
                        if (logicalIds.Length == 0)
                        {
                            logicalIds = new[] { Math.Min(logicalId, Math.Max(0, logicalCpu - 1)) };
                        }

                        cores.Add(new PhysicalCoreInfo
                        {
                            LogicalIds = logicalIds,
                            HasSmt = hasSmt,
                            EfficiencyClass = perfLevels - level,
                            L3GroupId = level,
                            Kind = kind,
                        });
                        logicalId += count;
                    }
                }
            }

            if (cores.Count == 0)
            {
                bool hasSmt = logicalCpu > physicalCpu;
                int logicalPerCore = hasSmt ? Math.Max(1, logicalCpu / physicalCpu) : 1;
                for (int i = 0; i < physicalCpu; i++)
                {
                    int count = i == physicalCpu - 1 ? Math.Max(1, logicalCpu - logicalId) : logicalPerCore;
                    int[] logicalIds = Enumerable.Range(Math.Min(logicalId, Math.Max(0, logicalCpu - 1)), count)
                        .Where(id => id < logicalCpu)
                        .ToArray();
                    if (logicalIds.Length == 0)
                    {
                        logicalIds = new[] { Math.Min(i, Math.Max(0, logicalCpu - 1)) };
                    }

                    cores.Add(new PhysicalCoreInfo
                    {
                        LogicalIds = logicalIds,
                        HasSmt = hasSmt,
                        EfficiencyClass = 1,
                        L3GroupId = -1,
                        Kind = CpuCoreKind.Performance,
                    });
                    logicalId += count;
                }
            }

            return NormalizeCoreIds(cores);
        }

        private static int? ReadSysctlInt(string name)
        {
            try
            {
                UIntPtr length = (UIntPtr)sizeof(int);
                IntPtr buffer = Marshal.AllocHGlobal(sizeof(int));
                try
                {
                    if (sysctlbyname(name, buffer, ref length, IntPtr.Zero, UIntPtr.Zero) != 0 || length.ToUInt64() < sizeof(int))
                    {
                        return null;
                    }

                    return Marshal.ReadInt32(buffer);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadSysctlString(string name)
        {
            try
            {
                UIntPtr length = UIntPtr.Zero;
                if (sysctlbyname(name, IntPtr.Zero, ref length, IntPtr.Zero, UIntPtr.Zero) != 0 || length == UIntPtr.Zero)
                {
                    return null;
                }

                IntPtr buffer = Marshal.AllocHGlobal((int)length);
                try
                {
                    if (sysctlbyname(name, buffer, ref length, IntPtr.Zero, UIntPtr.Zero) != 0)
                    {
                        return null;
                    }

                    return Marshal.PtrToStringAnsi(buffer)?.TrimEnd('\0').Trim();
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadAndroidSystemProperty(string name)
        {
            try
            {
                IntPtr buffer = Marshal.AllocHGlobal(PropValueMax);
                try
                {
                    int length = __system_property_get(name, buffer);
                    if (length <= 0)
                    {
                        return null;
                    }

                    return Marshal.PtrToStringAnsi(buffer, length)?.Trim();
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                return null;
            }
        }

        [DllImport("libSystem.dylib", SetLastError = true)]
        private static extern int sysctlbyname([MarshalAs(UnmanagedType.LPStr)] string name, IntPtr oldp, ref UIntPtr oldlenp, IntPtr newp, UIntPtr newlen);

        private const int PropValueMax = 92;

        [DllImport("libc", EntryPoint = "__system_property_get", SetLastError = true)]
        private static extern int __system_property_get([MarshalAs(UnmanagedType.LPStr)] string name, IntPtr value);

        private static PhysicalCoreInfo[]? DetectPlayStation()
        {
            bool ps5 = IsProsperoRuntime();
            int logicalPerCore = ps5 ? 2 : 1;
            PhysicalCoreInfo[] cores = new PhysicalCoreInfo[8];
            for (int i = 0; i < cores.Length; i++)
            {
                int firstLogicalId = i * logicalPerCore;
                cores[i] = new PhysicalCoreInfo
                {
                    CoreId = i,
                    LogicalIds = Enumerable.Range(firstLogicalId, logicalPerCore).ToArray(),
                    HasSmt = logicalPerCore > 1,
                    EfficiencyClass = 1,
                    L3GroupId = 0,
                    Kind = CpuCoreKind.Performance,
                };
            }

            return cores;
        }

        private static bool IsProsperoRuntime()
        {
            string? platform = Environment.GetEnvironmentVariable("SCE_PLATFORM")
                ?? Environment.GetEnvironmentVariable("SCE_TARGET_PLATFORM")
                ?? Environment.GetEnvironmentVariable("ORBIS")
                ?? Environment.GetEnvironmentVariable("PROSPERO");

            if (!string.IsNullOrWhiteSpace(platform))
            {
                return platform.Contains("prospero", StringComparison.OrdinalIgnoreCase)
                    || platform.Contains("ps5", StringComparison.OrdinalIgnoreCase);
            }

            return Environment.ProcessorCount > 8;
        }
    }
}
