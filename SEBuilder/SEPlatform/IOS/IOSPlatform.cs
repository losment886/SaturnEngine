using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SEBuilder.SEPlatform.IOS
{
    public class IOSPlatform : SECore.SEPlatformBase
    {
        public override string Name => "iOS";

        public IOSPlatform(SECore.SEPlatformInfo info) : base(info) { }

        public override string[] GetDotNetBuildArgs()
        {
            var args = new List<string>
            {
                "-c", PlatformInfo.GetDotNetConfig(),
                $"-p:RuntimeIdentifier={PlatformInfo.DotNetRID}",
                "-p:UseAppHost=false"
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
                "-DCMAKE_SYSTEM_NAME=iOS",
                "-DCMAKE_SYSTEM_VERSION=12.0",
                $"-DCMAKE_OSX_ARCHITECTURES={GetIOSArchitecture()}",
                "-GXcode"
            };
        }

        public override string[] GetCMakeBuildArgs()
        {
            return new[] { "--config", GetCMakeBuildType() };
        }

        public override Dictionary<string, string> GetNativeEnvironmentVariables()
        {
            return new Dictionary<string, string>();
        }

        public override string GetOutputSubDir() => "IOS";

        public override bool ValidateToolchain(out string? errorMessage)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                errorMessage = "iOS 平台只能在 macOS 系统上进行编译。";
                return false;
            }

            try
            {
                var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "xcode-select",
                    Arguments = "-p",
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

            errorMessage = "未找到 Xcode 工具链。请在 macOS 系统上运行 'xcode-select --install'。";
            return false;
        }

        private string GetIOSArchitecture() => PlatformInfo.Architecture switch
        {
            SECore.SEArchitecture.ARM64 => "arm64",
            _ => "arm64"
        };

        private string GetCMakeBuildType() => PlatformInfo.Configuration switch
        {
            SECore.SEBuildConfig.Debug => "Debug",
            SECore.SEBuildConfig.Release => "Release",
            SECore.SEBuildConfig.Shipping => "MinSizeRel",
            _ => "Release"
        };
    }
}

