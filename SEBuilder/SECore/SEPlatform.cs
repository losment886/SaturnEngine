using System;
using System.Runtime.InteropServices;

namespace SEBuilder.SECore
{
    /// <summary>
    /// 目标平台枚举
    /// </summary>
    public enum SETargetPlatform
    {
        Windows,
        MacOS,
        Linux,
        Android,
        IOS
    }

    /// <summary>
    /// CPU 架构枚举
    /// </summary>
    public enum SEArchitecture
    {
        x86,
        x64,
        ARM64
    }

    /// <summary>
    /// 构建配置枚举
    /// </summary>
    public enum SEBuildConfig
    {
        Debug,
        Release,
        Shipping
    }

    /// <summary>
    /// 平台信息类 — 描述一个目标平台的全部属性
    /// </summary>
    public class SEPlatformInfo
    {
        public SETargetPlatform Platform { get; set; }
        public SEArchitecture Architecture { get; set; }
        public SEBuildConfig Configuration { get; set; }
        public bool EnableAOT { get; set; }
        public string? OutputDir { get; set; }
        public bool Verbose { get; set; }

        public string DotNetRID => GetDotNetRID();
        public string? CMakePresetName => GetCMakePresetName();
        public string NativeLibPrefix => Platform == SETargetPlatform.Windows ? "" : "lib";
        public string NativeLibExtension => Platform switch
        {
            SETargetPlatform.Windows => ".dll",
            SETargetPlatform.MacOS => ".dylib",
            SETargetPlatform.Linux => ".so",
            SETargetPlatform.Android => ".so",
            SETargetPlatform.IOS => ".dylib",
            _ => ".dll"
        };
        public string NativeExeExtension => Platform == SETargetPlatform.Windows ? ".exe" : "";
        public string MSBuildPlatform => Architecture switch
        {
            SEArchitecture.x86 => "Win32",
            SEArchitecture.x64 => "x64",
            SEArchitecture.ARM64 => "ARM64",
            _ => "x64"
        };
        public string MSBuildConfiguration => Configuration switch
        {
            SEBuildConfig.Debug => "Debug",
            SEBuildConfig.Release => "Release",
            SEBuildConfig.Shipping => "Release",
            _ => "Debug"
        };

        public SEPlatformInfo(SETargetPlatform platform, SEArchitecture arch, SEBuildConfig config)
        {
            Platform = platform;
            Architecture = arch;
            Configuration = config;
        }

        public string GetDotNetConfig() => Configuration switch
        {
            SEBuildConfig.Debug => "Debug",
            SEBuildConfig.Release => "Release",
            SEBuildConfig.Shipping => "Release",
            _ => "Debug"
        };

        private string GetDotNetRID()
        {
            string osPart = Platform switch
            {
                SETargetPlatform.Windows => "win",
                SETargetPlatform.MacOS => "osx",
                SETargetPlatform.Linux => "linux",
                SETargetPlatform.Android => "android",
                SETargetPlatform.IOS => "ios",
                _ => "win"
            };
            string archPart = Architecture switch
            {
                SEArchitecture.x86 => "x86",
                SEArchitecture.x64 => "x64",
                SEArchitecture.ARM64 => "arm64",
                _ => "x64"
            };
            return $"{osPart}-{archPart}";
        }

        private string? GetCMakePresetName()
        {
            var config = Configuration == SEBuildConfig.Debug ? "debug" : "release";
            if (Platform == SETargetPlatform.Windows)
            {
                return Architecture switch
                {
                    SEArchitecture.x64 => $"x64-{config}",
                    SEArchitecture.x86 => $"x86-{config}",
                    _ => $"x64-{config}"
                };
            }
            else if (Platform == SETargetPlatform.MacOS)
            {
                return Architecture switch
                {
                    SEArchitecture.ARM64 => $"macos-arm64-{config}",
                    _ => $"macos-{config}"
                };
            }
            return null;
        }

        public static SETargetPlatform GetHostPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return SETargetPlatform.Windows;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return SETargetPlatform.MacOS;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return SETargetPlatform.Linux;
            return SETargetPlatform.Windows;
        }

        public static SEArchitecture GetHostArchitecture()
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.X86 => SEArchitecture.x86,
                System.Runtime.InteropServices.Architecture.X64 => SEArchitecture.x64,
                System.Runtime.InteropServices.Architecture.Arm64 => SEArchitecture.ARM64,
                _ => SEArchitecture.x64
            };
        }

        public bool RequiresCrossCompile()
        {
            var host = GetHostPlatform();
            var hostArch = GetHostArchitecture();
            if (host != Platform) return true;
            if (hostArch != Architecture) return true;
            return false;
        }

        public override string ToString() => $"{Platform}-{Architecture}-{Configuration}";
    }
}
