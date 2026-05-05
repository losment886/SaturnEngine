using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SEBuilder.SEPlatform.Windows
{
    public class WinPlatform : SECore.SEPlatformBase
    {
        public override string Name => "Windows";

        public WinPlatform(SECore.SEPlatformInfo info) : base(info) { }

        public override string[] GetDotNetBuildArgs()
        {
            var args = new List<string>
            {
                "-c", PlatformInfo.GetDotNetConfig(),
                "-p:Platform=" + GetMsBuildPlatform(),
                $"-p:PlatformTarget={GetPlatformTarget()}",
                $"-p:RuntimeIdentifier={PlatformInfo.DotNetRID}",
                "-p:WindowsTargetPlatformVersion=10.0"
            };

            if (!PlatformInfo.EnableAOT)
                args.Add("-p:PublishAot=false");

            if (PlatformInfo.Verbose)
                args.Add("-v:d");

            return args.ToArray();
        }

        public override string[] GetCMakeConfigureArgs()
        {
            return new[]
            {
                $"-DCMAKE_BUILD_TYPE={PlatformInfo.GetDotNetConfig()}",
                "-DCMAKE_C_COMPILER=cl.exe",
                "-DCMAKE_CXX_COMPILER=cl.exe",
                $"-A={GetMsBuildPlatform()}",
                "-DCMAKE_SYSTEM_NAME=Windows"
            };
        }

        public override string[] GetCMakeBuildArgs()
        {
            return new[] { "--config", PlatformInfo.GetDotNetConfig() };
        }

        public override Dictionary<string, string> GetNativeEnvironmentVariables()
        {
            return new Dictionary<string, string>();
        }

        public override string GetOutputSubDir() => "Win";

        public override bool ValidateToolchain(out string? errorMessage)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                errorMessage = "Windows 平台只能在 Windows 系统上进行编译。";
                return false;
            }

            var msbuildPaths = new[]
            {
                @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
                @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            };

            foreach (var path in msbuildPaths)
            {
                if (File.Exists(path)) { errorMessage = null; return true; }
            }

            try
            {
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc != null)
                {
                    proc.WaitForExit(5000);
                    if (proc.ExitCode == 0) { errorMessage = null; return true; }
                }
            }
            catch { } // Ignore if dotnet is not available

            try
            {
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmake",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc != null)
                {
                    proc.WaitForExit(5000);
                    if (proc.ExitCode == 0) { errorMessage = null; return true; }
                }
            }
            catch { } // Ignore if cmake is not available

            errorMessage = "未找到 MSBuild、dotnet CLI 或 CMake。请安装 Visual Studio 2022、.NET SDK 或 CMake。";
            return false;
        }

        public override async Task<bool> BuildWithMSBuild(string projectPath, string? outputDir, bool verbose = false)
        {
            var args = new List<string>
            {
                projectPath,
                $"/p:Configuration={PlatformInfo.MSBuildConfiguration}",
                $"/p:Platform={GetMsBuildPlatform()}"
            };

            if (!string.IsNullOrEmpty(outputDir))
                args.Add($"/p:OutDir={outputDir}");

            if (verbose)
                args.Add("/v:d");
            else
                args.Add("/v:m");

            return await RunCommand("MSBuild", string.Join(" ", args), verbose);
        }

        private string GetMsBuildPlatform() => PlatformInfo.Architecture switch
        {
            SECore.SEArchitecture.x86 => "Win32",
            SECore.SEArchitecture.x64 => "x64",
            SECore.SEArchitecture.ARM64 => "ARM64",
            _ => "x64"
        };

        private string GetPlatformTarget() => PlatformInfo.Architecture switch
        {
            SECore.SEArchitecture.x86 => "x86",
            SECore.SEArchitecture.x64 => "x64",
            SECore.SEArchitecture.ARM64 => "ARM64",
            _ => "AnyCPU"
        };
    }
}
