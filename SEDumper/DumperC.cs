using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SEDumper
{
    public class DumperC
    {

        [DllImport("dbghelp.dll")]
        static extern bool MiniDumpWriteDump(
       IntPtr hProcess,
       uint processId,
       SafeHandle hFile,
       MINIDUMP_TYPE dumpType,
       IntPtr exceptionParam,
       IntPtr userStreamParam,
       IntPtr callbackParam);

        [Flags]
        enum MINIDUMP_TYPE : uint
        {
            MiniDumpNormal = 0x00000000,
            MiniDumpWithDataSegs = 0x00000001,
            MiniDumpWithFullMemory = 0x00000002,
            MiniDumpWithHandleData = 0x00000004
        }






        public Process TargetProcess;
        public string SavePath;
        public string NM = "SED";
        Timer t;

        public DumperC(Process targetProcess, string savePath)
        {
            TargetProcess = targetProcess;
            SavePath = savePath;
            TargetProcess.EnableRaisingEvents = true;
            TargetProcess.Exited += TargetProcess_Exited;
        }

        private void TargetProcess_Exited(object? sender, EventArgs e)
        {
            //throw new NotImplementedException();
            if (TargetProcess.ExitCode != 0)
            {
                CreateDump();
            }
        }

        event TimerCallback PPIN;
        public void StartMonitor()
        {
            if(PPIN == null)
                PPIN += DumperC_PPIN;
            t = new Timer(PPIN);
            t.Change(0, 200);

            TargetProcess.WaitForExit();
            
        }
        public void StopMonitor()
        {
            t.Dispose();
        }
        private void DumperC_PPIN(object? state)
        {
            //throw new NotImplementedException();
        }

        public void CreateDump(string nm = "UNG")
        {
            string dumpFile = System.IO.Path.Combine(SavePath, $"{NM}_dump_{nm}_{TargetProcess.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.dmp");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CreateWindowsDump(dumpFile);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                CreateLinuxDump(dumpFile);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                CreateMacDump(dumpFile);
            }
            else
            {
                throw new PlatformNotSupportedException("不支持的操作系统平台");
            }
        }
        private void CreateWindowsDump(string dumpFile)
        {
            // 使用 Windows 原生 API 或 procdump
            try
            {

                using (var fs = new FileStream(dumpFile, FileMode.Create))
                {
                    bool success = MiniDumpWriteDump(
                        TargetProcess.Handle,
                        (uint)TargetProcess.Id,
                        fs.SafeFileHandle,
                        MINIDUMP_TYPE.MiniDumpWithFullMemory,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero);

                    if (success)
                    {
                        Console.WriteLine($"内存转储已创建: {dumpFile}");
                    }
                    else
                    {
                        Console.WriteLine("内存转储创建失败");
                        throw new Exception();//跳转
                    }
                }


                
            }
            catch
            {
                try
                {
                    var procdump = new ProcessStartInfo
                    {
                        FileName = "procdump",
                        Arguments = $"-ma {TargetProcess.Id} \"{dumpFile}\"",
                        //UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    Process.Start(procdump)?.WaitForExit(30000);
                }
                catch 
                {
                    Console.WriteLine("请安装 procdump 工具");
                }
                
            }
        }

        private void CreateLinuxDump(string dumpFile)
        {
            try
            {
                // 方法1: 使用 gcore
                var gcoreProcess = new ProcessStartInfo
                {
                    FileName = "gcore",
                    Arguments = $"-o \"{dumpFile}\" {TargetProcess.Id}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(gcoreProcess)?.WaitForExit(30000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"gcore 失败: {ex.Message}");

                // 方法2: 使用 .NET createdump 工具
                try
                {
                    var createdumpProcess = new ProcessStartInfo
                    {
                        FileName = "createdump",
                        Arguments = $"-f \"{dumpFile}\" {TargetProcess.Id}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    Process.Start(createdumpProcess)?.WaitForExit(30000);
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"createdump 失败: {ex2.Message}");
                }
            }
        }

        private void CreateMacDump(string dumpFile)
        {
            try
            {
                // macOS 可以使用 lldb 或 createdump
                var lldbProcess = new ProcessStartInfo
                {
                    FileName = "lldb",
                    Arguments = $"-p {TargetProcess.Id} -b -o \"process save-core \\\"{dumpFile}\\\"\" -o quit",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(lldbProcess)?.WaitForExit(30000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"macOS 转储失败: {ex.Message}");
            }
        }

    }
}
