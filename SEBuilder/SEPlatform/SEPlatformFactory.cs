using System;
using SEBuilder.SECore;
using SEBuilder.SEPlatform.Windows;
using SEBuilder.SEPlatform.Mac;
using SEBuilder.SEPlatform.Linux;
using SEBuilder.SEPlatform.Android;
using SEBuilder.SEPlatform.IOS;

namespace SEBuilder.SEPlatform
{
    /// <summary>
    /// 平台工厂 — 根据目标平台创建对应的编译器实例
    /// </summary>
    public class SEPlatformFactory
    {
        public static SEPlatformBase CreatePlatform(SEPlatformInfo platformInfo)
        {
            return platformInfo.Platform switch
            {
                SETargetPlatform.Windows => new WinPlatform(platformInfo),
                SETargetPlatform.MacOS => new MacPlatform(platformInfo),
                SETargetPlatform.Linux => new LinuxPlatform(platformInfo),
                SETargetPlatform.Android => new AndroidPlatform(platformInfo),
                SETargetPlatform.IOS => new IOSPlatform(platformInfo),
                _ => throw new ArgumentException($"不支持的平台: {platformInfo.Platform}")
            };
        }

        /// <summary>
        /// 获取当前宿主平台的编译器
        /// </summary>
        public static SEPlatformBase CreateHostPlatform(SEArchitecture? arch = null, SEBuildConfig? config = null)
        {
            var platform = SEPlatformInfo.GetHostPlatform();
            var architecture = arch ?? SEPlatformInfo.GetHostArchitecture();
            var buildConfig = config ?? SEBuildConfig.Debug;

            var info = new SEPlatformInfo(platform, architecture, buildConfig);
            return CreatePlatform(info);
        }
    }
}

