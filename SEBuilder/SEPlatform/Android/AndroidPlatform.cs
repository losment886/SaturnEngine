using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SEBuilder.SEPlatform.Android
{
    public class AndroidPlatform : SECore.SEPlatformBase
    {
        public override string Name => "Android";

        public AndroidPlatform(SECore.SEPlatformInfo info) : base(info) { }

        public override string[] GetDotNetBuildArgs()
        {
            var args = new List<string>
            {
                "-c", PlatformInfo.GetDotNetConfig(),
                $"-p:RuntimeIdentifier={PlatformInfo.DotNetRID}",
                "-p:UseAppHost=false",
                "-p:PublishAsBundle=true"
            };

            if (PlatformInfo.EnableAOT)
                args.Add("-p:PublishAot=true");

            if (PlatformInfo.Verbose)
                args.Add("-v:d");

            return args.ToArray();
        }

        public override string[] GetCMakeConfigureArgs()
        {
            var ndk = Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT");
            if (string.IsNullOrEmpty(ndk))
                ndk = Environment.GetEnvironmentVariable("ANDROID_NDK");

            var args = new List<string>
            {
                $"-DCMAKE_BUILD_TYPE={GetCMakeBuildType()}",
                "-DCMAKE_SYSTEM_NAME=Android",
                $"-DCMAKE_SYSTEM_VERSION=21"
            };

            if (!string.IsNullOrEmpty(ndk))
            {
                args.Add($"-DCMAKE_ANDROID_NDK={ndk}");
                args.Add($"-DCMAKE_ANDROID_ABI={GetAndroidABI()}");
                args.Add("-DCMAKE_ANDROID_PLATFORM=android-21");
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
            
            var ndk = Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT");
            if (string.IsNullOrEmpty(ndk))
                ndk = Environment.GetEnvironmentVariable("ANDROID_NDK");
            
            if (!string.IsNullOrEmpty(ndk))
                vars["ANDROID_NDK"] = ndk;

            return vars;
        }

        public override string GetOutputSubDir() => "Android";

        public override bool ValidateToolchain(out string? errorMessage)
        {
            var ndk = Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT");
            if (string.IsNullOrEmpty(ndk))
                ndk = Environment.GetEnvironmentVariable("ANDROID_NDK");

            if (string.IsNullOrEmpty(ndk) || !System.IO.Directory.Exists(ndk))
            {
                errorMessage = "未找到 Android NDK。请设置 ANDROID_NDK_ROOT 或 ANDROID_NDK 环境变量。";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private string GetAndroidABI() => PlatformInfo.Architecture switch
        {
            SECore.SEArchitecture.ARM64 => "arm64-v8a",
            SECore.SEArchitecture.x64 => "x86_64",
            SECore.SEArchitecture.x86 => "x86",
            _ => "arm64-v8a"
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

