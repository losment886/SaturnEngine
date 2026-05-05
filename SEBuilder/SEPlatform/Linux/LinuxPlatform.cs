using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SEBuilder.SEPlatform.Linux
{
    public class LinuxPlatform : SECore.SEPlatformBase
    {
        public override string Name => "Linux";

        public LinuxPlatform(SECore.SEPlatformInfo info) : base(info) { }

        public override string[] GetDotNetBuildArgs()
        {
            var args = new List<string>
            {
                "-c", PlatformInfo.GetDotNetConfig(),
                $"-p:RuntimeIdentifier={PlatformInfo.DotNetRID}",
                "-p:UseAppHost=true"
            };

            if (PlatformInfo.EnableAOT)
                args.Add("-p:PublishAot=true");

            if (PlatformInfo.Verbose)
                args.Add("-v:d");

            return args.ToArray();
        }

        public override string[] GetCMakeConfigureArgs()
        {
            return new[]
            {
                $"-DCMAKE_BUILD_TYPE={GetCMakeBuildType()}",
                "-DCMAKE_SYSTEM_NAME=Linux",
                "-DCMAKE_C_COMPILER=gcc",
                "-DCMAKE_CXX_COMPILER=g++"
            };
        }

        public override string[] GetCMakeBuildArgs()
        {
            return new[] { "--config", GetCMakeBuildType(), "--", "-j4" };
        }

        public override Dictionary<string, string> GetNativeEnvironmentVariables()
        {
            return new Dictionary<string, string>();
        }

        public override string GetOutputSubDir() => "Linux";

        public override bool ValidateToolchain(out string? errorMessage)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                errorMessage = "Linux 平台只能在 Linux 系统上进行编译。";
                return false;
            }

            try
            {
                var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "gcc",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc != null && proc.WaitForExit(5000) && proc.ExitCode == 0)
                {
                    errorMessage = null;
                    return true;
                }
            }
            catch { }

            errorMessage = "未找到 GCC。请通过包管理器安装必要的构建工具（build-essential）。";
            return false;
        }

        private string GetCMakeBuildType() => PlatformInfo.Configuration switch
        {
            SECore.SEBuildConfig.Debug => "Debug",
            SECore.SEBuildConfig.Release => "Release",
            SECore.SEBuildConfig.Shipping => "MinSizeRel",
            _ => "Release"
        };
    }
}

