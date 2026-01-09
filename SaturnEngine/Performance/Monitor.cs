using SaturnEngine.Asset;
using SaturnEngine.Global;
using SaturnEngine.Management;
using System.Diagnostics;

namespace SaturnEngine.Performance
{
    public static class SEMonitor
    {
        static SEThread LGT;
        static Process cacp;


        public static ulong GCSize;
        public static ulong MemoryUsed;
        public static ulong UnPagedUsed;
        public static ulong PagedUsed;
        public static ulong VirtualMemoryUsed;
        public static ulong GlobalMemoryUsed;
        public static ulong GlobalVirtualMemoryUsed;
        public static ulong TotalMemory;
        public static ulong TotalVirtualMemory;
        public static TimeSpan UserTime;
        public static TimeSpan TotalTime;
        public static float Upload;
        public static float Download;
        public static float NetTotal;
        public static ulong CPUUsage;
        public static bool AllowLog = false;
        public static Stopwatch StopwatchGlobal;

        public static void Init()
        {
            SELogger.Log("生成监视线程".GetInCurrLang());
            LGT = Dispatcher.CreateThreadORG(Recer, ThreadPriority.Lowest); //new SEThread(Recer);
            //LGT.Priority = ThreadPriority.Lowest;
            LGT.Start();
        }
        static void Recer()
        {
            //SELogger.Log("监视线程hajime");
            var hi = new Hardware.Info.HardwareInfo();
            hi.RefreshMemoryStatus();
            TotalMemory = hi.MemoryStatus.TotalPhysical;
            TotalVirtualMemory = hi.MemoryStatus.TotalVirtual;
            StopwatchGlobal = new Stopwatch();
            StopwatchGlobal.Start();
            //double ttc = 0;
            //double fac = 0;
            LGT.SetFPS(1);
            //SELogger.Log("监视线程启动");
            while (GVariables.EngineRunning)
            {
                cacp = Process.GetCurrentProcess();
                GCSize = (ulong)GC.GetTotalAllocatedBytes();
                MemoryUsed = (ulong)cacp.WorkingSet64;//?
                UnPagedUsed = (ulong)cacp.NonpagedSystemMemorySize64;
                PagedUsed = (ulong)cacp.PagedMemorySize64;
                VirtualMemoryUsed = (ulong)cacp.VirtualMemorySize64;
                //TotalTime = cacp.TotalProcessorTime;
                TotalTime = StopwatchGlobal.Elapsed;
                GVariables.TotalRunningTimeInSecond = TotalTime.TotalSeconds;
                UserTime = cacp.UserProcessorTime;
                //hi.RefreshCPUList();
                //
                hi.RefreshBatteryList();
                //hi.RefreshNetworkAdapterList();


                //CPUUsage = hi.CpuList[0].PercentProcessorTime;
                //GlobalMemoryUsed = hi.MemoryStatus.TotalPhysical - hi.MemoryStatus.AvailablePhysical;
                //GlobalVirtualMemoryUsed = hi.MemoryStatus.TotalVirtual - hi.MemoryStatus.AvailableVirtual;

                if (hi.BatteryList.Count > 0)
                {
                    var p = hi.BatteryList[0];
                    GVariables.BatteryPercent = p.EstimatedChargeRemaining;
                    GVariables.BatteryFullCapacity = p.FullChargeCapacity;
                    GVariables.BatteryCurrentCapacity = GVariables.BatteryFullCapacity * GVariables.BatteryPercent;
                    if (AllowLog)
                        Console.WriteLine($"{GVariables.BatteryPercent}% btr {GVariables.BatteryCurrentCapacity}/{GVariables.BatteryFullCapacity}");
                }
                /*
                if(hi.NetworkAdapterList.Count > 0)
                {
                    var n = hi.NetworkAdapterList[0];
                    Console.WriteLine($"NAME:{n.Name}  TYPE:{n.AdapterType}  {n.BytesSentPersec}↑{n.BytesReceivedPersec}↓  DESC:{n.Caption}  MANU:{n.Manufacturer}  PNAME{n.ProductName}  DSC:{n.Description}");
                }
                */
                if (AllowLog)
                    Console.WriteLine("GC total allocated bytes:" + GCSize + " | ProcessWorkSet:" + MemoryUsed + " | UnPaged:" + UnPagedUsed + " | Paged:" + PagedUsed + " | Virtual:" + VirtualMemoryUsed + " || UserTime:" + UserTime + " || TotalTime:" + TotalTime + " || CPUUsage:" + CPUUsage + " || GlobalMemoryUsed:" + GlobalMemoryUsed + " || SEMemoryUsed:" + GVariables.ShareMemory.TotalUsed);
                //LGT.WaitForFPS();
                LGT.Sleep(0.4);
                //
                //Thread.Sleep();
            }
            StopwatchGlobal.Stop();
            //SELogger.Log("监视线程结束");
            /*
            int prt = 0;
            DateTime st = DateTime.Now;
            Computer cmp = new Computer()
            {
                IsBatteryEnabled = true,
                IsNetworkEnabled = true,
            };
            cmp.Open();

            IHardware bat = null;
            IHardware[] nat = new IHardware[0];
            float[] upm = new float[0];
            float[] dwm = new float[0];
            float[] ntm = new float[0];
            IHardware mnat = null;
            Console.WriteLine("CCP");
            int nti = 0;
            foreach (var hd in cmp.Hardware)
            {
                if (hd.HardwareType == HardwareType.Network)
                {
                    nat = nat.Append(hd).ToArray();
                    Console.WriteLine("NAT:" + nat[nti].Name);

                    foreach (var sensor in nat[nti].Sensors)
                    {
                        Console.WriteLine(sensor.Name);
                        switch (sensor.SensorType)
                        {
                            case SensorType.Data when sensor.Name.Contains("Download"):
                                dwm = dwm.Append(sensor.V2 ?? 0.0f).ToArray();
                                break;
                            case SensorType.Data when sensor.Name.Contains("Upload"):
                                upm = upm.Append(sensor.V2 ?? 0.0f).ToArray();
                                break;
                            case SensorType.Data when (sensor.Name.Contains("Throughput") || sensor.Name.Contains("Network Utilization")):
                                ntm = ntm.Append(sensor.V2 ?? 0.0f).ToArray();
                                break;
                        }
                    }
                    nti++;
                }
                else if (hd.HardwareType == HardwareType.Battery)
                {
                    Console.WriteLine("BAT");
                    bat = hd;
                    Console.WriteLine(bat.Name);
                    Console.WriteLine(bat.Identifier);

                    Console.WriteLine();
                }
            }
            //cmp.Close();
            Thread.Sleep(100);
            for (int i = 0; i < nat.Length; i++)
            {
                nat[i].Update();
                bool ym = false;
                foreach (var sensor in nat[i].Sensors)
                {
                    switch (sensor.SensorType)
                    {
                        case SensorType.Data when sensor.Name.Contains("Download"):
                            if (dwm[i] != (sensor.V2 ?? 0.0f))
                            {
                                ym = true;
                            }
                            break;
                        case SensorType.Data when sensor.Name.Contains("Upload"):
                            if (upm[i] != (sensor.V2 ?? 0.0f))
                            {
                                ym = true;
                            }
                            break;
                        case SensorType.Data when (sensor.Name.Contains("Throughput") || sensor.Name.Contains("Network Utilization")):
                            if (ntm[i] != (sensor.V2 ?? 0.0f))
                            {
                                ym = true;
                            }
                            break;
                    }
                }
                if (ym)
                {
                    mnat = nat[i];
                    break;
                }
            }

            while (GVariables.EngineRunning)
            {
                cacp = Process.GetCurrentProcess();
                GCSize = (ulong)GC.GetTotalAllocatedBytes();
                MemoryUsed = (ulong)cacp.WorkingSet64;//?
                UnPagedUsed = (ulong)cacp.NonpagedSystemMemorySize64;
                PagedUsed = (ulong)cacp.PagedMemorySize64;
                VirtualMemoryUsed = (ulong)cacp.VirtualMemorySize64;
                //TotalTime = cacp.TotalProcessorTime;
                UserTime = cacp.UserProcessorTime;
                Console.WriteLine("GC total allocated bytes:" + GCSize + " | ProcessWorkSet:" + MemoryUsed + " | UnPaged:" + UnPagedUsed + " | Paged:" + PagedUsed + " | Virtual:" + VirtualMemoryUsed + " || UserTime:" + UserTime + " || TotalTime:" + TotalTime);
                prt++;

                if (bat != null)
                {
                    bat.Update();
                    foreach (var s in bat.Sensors)
                    {
                        switch (s.SensorType)
                        {
                            case SensorType.Level:
                                GVariables.Bretty = s.V2 ?? 0.0f; // 剩余电量百分比
                                break;
                            case SensorType.Voltage:
                                GVariables.Voltage = s.V2 ?? 0.0f; // 当前电压(mV)
                                break;
                            case SensorType.Power:
                                GVariables.Power = s.V2 ?? 0.0f;
                                break;

                        }
                    }
                    GVariables.Charging = bat.Name.Contains("Discharging") ? false :
                         bat.Name.Contains("Charging") ? true : false;
                    GVariables.Charging = bat.Name.Contains("Discharging") ? false :
                         bat.Name.Contains("Charging") ? false : true;
                    Console.WriteLine($"BrettyLevel:{GVariables.Bretty}|Voltage:{GVariables.Voltage}|Power:{GVariables.Power}|IsCharging:{GVariables.Charging}|Full:{GVariables.Full}");

                }




                if (mnat != null)
                {
                    mnat.Update();
                    foreach (var sensor in mnat.Sensors)
                    {
                        switch (sensor.SensorType)
                        {
                            case SensorType.Data when sensor.Name.Contains("Download"):
                                Download = sensor.V2 ?? 0.0f; // MB/s
                                break;
                            case SensorType.Data when sensor.Name.Contains("Upload"):
                                Upload = sensor.V2 ?? 0.0f; // MB/s
                                break;
                            case SensorType.Data when (sensor.Name.Contains("Throughput") || sensor.Name.Contains("Network Utilization")):
                                NetTotal = sensor.V2 ?? 0.0f;
                                break;
                        }
                    }
                    if (GVariables.OS == OS.Windows)
                    {
                        //Console.WriteLine(mnat.Name);
                        //Console.WriteLine(mnat.GetReport());
                        if (mnat.Name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) || mnat.Name.Contains("WLAN", StringComparison.OrdinalIgnoreCase))
                            GVariables.InternetMethod = InternetMethod.WiFi;
                        if (mnat.Name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase) || mnat.Name.Contains("RJ45", StringComparison.OrdinalIgnoreCase) || mnat.Name.Contains("rj45", StringComparison.OrdinalIgnoreCase))
                            GVariables.InternetMethod = InternetMethod.LAN;
                    }
                    else if (GVariables.OS == OS.Linux)
                    {
                        try
                        {
                            // 通过nmcli检测
                            var process = new System.Diagnostics.Process
                            {
                                StartInfo = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "nmcli",
                                    Arguments = "-t -f DEVICE,TYPE dev",
                                    RedirectStandardOutput = true,
                                    UseShellExecute = false
                                }
                            };
                            process.Start();
                            var output = process.StandardOutput.ReadToEnd();

                            // 解析输出示例：eth0:ethernet\nwlan0:wifi
                            var lines = output.Split('\n');
                            foreach (var line in lines)
                            {
                                if (line.StartsWith(mnat?.Name.Split(':')[0]))
                                {
                                    string v = line.Split(':')[1] switch
                                    {
                                        "wifi" => "Wi-Fi",
                                        "ethernet" => "Ethernet",
                                        _ => line.Split(':')[1]
                                    };
                                    if (v == "Wi-Fi")
                                    {
                                        GVariables.InternetMethod = InternetMethod.WiFi;
                                    }
                                    else if (v == "Ethernet")
                                    {
                                        GVariables.InternetMethod = InternetMethod.LAN;
                                    }
                                }
                            }
                        }
                        catch
                        { }

                        // 备用方案：检查/sys/class/net/
                        var netPath = $"/sys/class/net/{mnat?.Name}/wireless";
                        GVariables.InternetMethod = File.Exists(netPath) ? InternetMethod.WiFi : InternetMethod.LAN;
                    }
                    else if (GVariables.OS == OS.MacOS)
                    {
                        try
                        {
                            var process = new System.Diagnostics.Process
                            {
                                StartInfo = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "/System/Library/PrivateFrameworks/Apple80211.framework/Versions/Current/Resources/airport",
                                    Arguments = "-I",
                                    RedirectStandardOutput = true,
                                    UseShellExecute = false
                                }
                            };
                            process.Start();
                            var output = process.StandardOutput.ReadToEnd();
                            GVariables.InternetMethod = output.Contains("SSID:") ? InternetMethod.WiFi : InternetMethod.LAN;
                        }
                        catch
                        {
                            // 备用方案：通过networksetup
                            var process = new System.Diagnostics.Process
                            {
                                StartInfo = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "networksetup",
                                    Arguments = "-listallhardwareports",
                                    RedirectStandardOutput = true,
                                    UseShellExecute = false
                                }
                            };
                            process.Start();
                            var output = process.StandardOutput.ReadToEnd();
                            GVariables.InternetMethod = output.Contains("Wi-Fi") ? InternetMethod.WiFi : InternetMethod.LAN;
                        }
                    }
                    Console.WriteLine($"{Upload}MB/S↑ {Download}MB/S↓ T{NetTotal}IO/UYSAGE↑↓ Type:{GVariables.InternetMethod}");



                }

                Thread.Sleep(800);

                //TotalTime += new TimeSpan(0, 0, 0, 0, 205);
                if (prt >= 4)
                {
                    prt = 0;
                    TotalTime = DateTime.Now - st;
                    if ((int)(TotalTime.TotalSeconds % 30) == 0)
                    {
                        Console.WriteLine("Reflush Network Hardware");
                        cmp = new Computer()
                        {
                            IsNetworkEnabled = true,
                        };
                        cmp.Open();

                        nti = 0;
                        foreach (var hd in cmp.Hardware)
                        {
                            if (hd.HardwareType == HardwareType.Network)
                            {
                                nat = nat.Append(hd).ToArray();
                                Console.WriteLine("NAT:" + nat[nti].Name);

                                foreach (var sensor in nat[nti].Sensors)
                                {
                                    Console.WriteLine(sensor.Name);
                                    switch (sensor.SensorType)
                                    {
                                        case SensorType.Data when sensor.Name.Contains("Download"):
                                            dwm = dwm.Append(sensor.V2 ?? 0.0f).ToArray();
                                            break;
                                        case SensorType.Data when sensor.Name.Contains("Upload"):
                                            upm = upm.Append(sensor.V2 ?? 0.0f).ToArray();
                                            break;
                                        case SensorType.Data when (sensor.Name.Contains("Throughput") || sensor.Name.Contains("Network Utilization")):
                                            ntm = ntm.Append(sensor.V2 ?? 0.0f).ToArray();
                                            break;
                                    }
                                }
                                nti++;
                            }
                            else if (hd.HardwareType == HardwareType.Battery)
                            {
                                Console.WriteLine("BAT");
                                bat = hd;
                                Console.WriteLine(bat.Name);
                                Console.WriteLine(bat.Identifier);

                                Console.WriteLine();
                            }
                        }
                        cmp.Close();
                        Thread.Sleep(100);
                        for (int i = 0; i < nat.Length; i++)
                        {
                            nat[i].Update();
                            bool ym = false;
                            foreach (var sensor in nat[i].Sensors)
                            {
                                switch (sensor.SensorType)
                                {
                                    case SensorType.Data when sensor.Name.Contains("Download"):
                                        if (dwm[i] != (sensor.V2 ?? 0.0f))
                                        {
                                            ym = true;
                                        }
                                        break;
                                    case SensorType.Data when sensor.Name.Contains("Upload"):
                                        if (upm[i] != (sensor.V2 ?? 0.0f))
                                        {
                                            ym = true;
                                        }
                                        break;
                                    case SensorType.Data when (sensor.Name.Contains("Throughput") || sensor.Name.Contains("Network Utilization")):
                                        if (ntm[i] != (sensor.V2 ?? 0.0f))
                                        {
                                            ym = true;
                                        }
                                        break;
                                }
                            }
                            if (ym)
                            {
                                mnat = nat[i];
                                break;
                            }
                        }
                        GC.Collect();
                    }
                }
            }
            */
        }


    }
}
