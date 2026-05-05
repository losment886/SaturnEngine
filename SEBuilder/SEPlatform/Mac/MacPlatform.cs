using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SEBuilder.SEPlatform.Mac
{
    public class MacPlatform : SECore.SEPlatformBase
    {
        public override string Name => "macOS";

        public MacPlatform(SECore.SEPlatformInfo info) : base(info) { }

        public override string[] GetDotNetBuildArgs()
        {
            var args = new List<string>
            {
                "-c", PlatformInfo.GetDotNetConfig(),
                $"-p:RuntimeIdentifier={PlatformInfo.DotNetRID}",
                "-p:UseAppHost=true"
            };

            if (PlatformInfo.Architecture == SECore.SEArchitecture.ARM64)
                args.Add("-p:PlatformTarget=ARM64");
            else
                args.Add("-p:PlatformTarget=x64");

            if (PlatformInfo.EnableAOT)
                args.Add("-p:PublishAot=true");

            if (PlatformInfo.Verbose)
                args.Add("-v:d");

            return args.ToArray();
        }

        public override string[] GetCMakeConfigureArgs()
        {
            var args = new List<string>
            {
                $"-DCMAKE_BUILD_TYPE={GetCMakeBuildType()}",
                "-DCMAKE_SYSTEM_NAME=Darwin"
            };

            if (PlatformInfo.Architecture == SECore.SEArchitecture.ARM64)
            {
                args.Add("-DCMAKE_OSX_ARCHITECTURES=arm64");
                args.Add("-DCMAKE_OSX_DEPLOYMENT_TARGET=11.0");
            }
            else
            {
                args.Add("-DCMAKE_OSX_ARCHITECTURES=x86_64");
                args.Add("-DCMAKE_OSX_DEPLOYMENT_TARGET=10.13");
            }

            return args.ToArray();
        }

        public override string[] GetCMakeBuildArgs()
        {
            return new[] { "--config", GetCMakeBuildType() };
        }

        public override Dictionary<string, string> GetNativeEnvironmentVariables()
        {
            var vars = new Dictionary<string, string>();
            
            // Xcode 工具链路径（可选）
            var xcodeSelectPath = "/opt/homebrew/opt/llvm/bin";
            if (System.IO.Directory.Exists(xcodeSelectPath))
            {
                vars["PATH"] = xcodeSelectPath + ":" + Environment.GetEnvironmentVariable("PATH");
            }

            return vars;
        }

        public override string GetOutputSubDir() => "Mac";

        public override bool ValidateToolchain(out string? errorMessage)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                errorMessage = "macOS 平台只能在 macOS 系统上进行编译。";
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

            errorMessage = "未找到 Xcode 工具链。请运行 'xcode-select --install' 安装。";
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

