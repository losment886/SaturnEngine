using System;
using System.Collections.Generic;

namespace SEBuilder.SECore
{
    /// <summary>
    /// SEBuilder 命令类型
    /// </summary>
    public enum SECommandType
    {
        Build,
        Package,
        Cook,
        Prerender,
        Compress
    }

    /// <summary>
    /// 命令行解析结果
    /// </summary>
    public class SEBuildCommand
    {
        public SECommandType Command { get; set; } = SECommandType.Build;
        public SETargetPlatform Platform { get; set; } = SETargetPlatform.Windows;
        public SEArchitecture Architecture { get; set; } = SEArchitecture.x64;
        public SEBuildConfig Configuration { get; set; } = SEBuildConfig.Debug;
        public string? ProjectPath { get; set; }
        public string ProjectRoot { get; set; } = "";
        public List<string> TargetProjects { get; set; } = new();
        public bool EnableAOT { get; set; } = false;
        public string? OutputDir { get; set; }
        public string? PackageOutputDir { get; set; }
        public bool IncludePdb { get; set; } = false;

        // Cook (预留)
        public string? CookSourceDir { get; set; }
        public string? CookOutputDir { get; set; }

        // Prerender (预留)
        public string? PrerenderSource { get; set; }
        public string? PrerenderOutput { get; set; }

        // Compress (预留)
        public string? CompressSource { get; set; }
        public string? CompressOutput { get; set; }

        public bool Verbose { get; set; } = false;

        public SEPlatformInfo ToPlatformInfo()
        {
            return new SEPlatformInfo(Platform, Architecture, Configuration)
            {
                EnableAOT = EnableAOT,
                OutputDir = OutputDir,
                Verbose = Verbose
            };
        }

        public override string ToString()
        {
            var targets = TargetProjects.Count > 0 ? string.Join(", ", TargetProjects) : "(all)";
            return $"{Command} [{Platform}-{Architecture}-{Configuration}] Project={ProjectPath} Targets={targets} AOT={EnableAOT}";
        }

        /// <summary>
        /// 从命令行参数解析命令
        /// </summary>
        public static SEBuildCommand Parse(string[] args)
        {
            var cmd = new SEBuildCommand();
            if (args.Length == 0)
            {
                PrintUsage();
                return cmd;
            }

            // 第一个参数是命令
            var commandStr = args[0].ToLower();
            cmd.Command = commandStr switch
            {
                "build" => SECommandType.Build,
                "package" => SECommandType.Package,
                "cook" => SECommandType.Cook,
                "prerender" => SECommandType.Prerender,
                "compress" => SECommandType.Compress,
                _ => SECommandType.Build
            };

            // 解析选项
            for (int i = 1; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.StartsWith("-"))
                {
                    var key = arg.TrimStart('-').ToLower();
                    string? value = null;

                    if (arg.Contains('='))
                    {
                        var split = arg.Split('=', 2);
                        key = split[0].TrimStart('-').ToLower();
                        value = split[1];
                        // 移除双引号（如果存在）
                        if (!string.IsNullOrEmpty(value) && value.StartsWith("\"") && value.EndsWith("\""))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }
                    }
                    else if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        value = args[++i];
                        // 移除双引号（如果存在）
                        if (!string.IsNullOrEmpty(value) && value.StartsWith("\"") && value.EndsWith("\""))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }
                    }

                    switch (key)
                    {
                        case "project":
                        case "p":
                            cmd.ProjectPath = value;
                            break;
                        case "target":
                        case "t":
                            if (!string.IsNullOrEmpty(value))
                                cmd.TargetProjects.Add(value);
                            break;
                        case "platform":
                        case "plt":
                            cmd.Platform = value?.ToLower() switch
                            {
                                "win" or "windows" => SETargetPlatform.Windows,
                                "mac" or "macos" => SETargetPlatform.MacOS,
                                "linux" => SETargetPlatform.Linux,
                                "android" => SETargetPlatform.Android,
                                "ios" => SETargetPlatform.IOS,
                                _ => cmd.Platform
                            };
                            break;
                        case "arch":
                        case "architecture":
                        case "a":
                            cmd.Architecture = value?.ToLower() switch
                            {
                                "x86" or "x86_64" => SEArchitecture.x86,
                                "x64" or "amd64" => SEArchitecture.x64,
                                "arm64" or "aarch64" => SEArchitecture.ARM64,
                                _ => cmd.Architecture
                            };
                            break;
                        case "config":
                        case "configuration":
                        case "c":
                            cmd.Configuration = value?.ToLower() switch
                            {
                                "debug" => SEBuildConfig.Debug,
                                "release" => SEBuildConfig.Release,
                                "shipping" => SEBuildConfig.Shipping,
                                _ => cmd.Configuration
                            };
                            break;
                        case "aot":
                            cmd.EnableAOT = true;
                            break;
                        case "output":
                        case "o":
                            cmd.OutputDir = value;
                            break;
                        case "packageoutput":
                        case "po":
                            cmd.PackageOutputDir = value;
                            break;
                        case "includepdb":
                            cmd.IncludePdb = true;
                            break;
                        case "cooksourcedir":
                        case "csd":
                            cmd.CookSourceDir = value;
                            break;
                        case "cookoutputdir":
                        case "cod":
                            cmd.CookOutputDir = value;
                            break;
                        case "prerendersource":
                        case "prs":
                            cmd.PrerenderSource = value;
                            break;
                        case "prerenderoutput":
                        case "pro":
                            cmd.PrerenderOutput = value;
                            break;
                        case "compressionsource":
                        case "cms":
                            cmd.CompressSource = value;
                            break;
                        case "compressionoutput":
                        case "cmo":
                            cmd.CompressOutput = value;
                            break;
                        case "verbose":
                        case "v":
                            cmd.Verbose = true;
                            break;
                        case "help":
                        case "h":
                            PrintUsage();
                            break;
                    }
                }
            }

            // 自动检测项目根目录
            cmd.ResolveProjectRoot();

            return cmd;
        }

        private void ResolveProjectRoot()
        {
            if (!string.IsNullOrEmpty(ProjectPath))
            {
                ProjectRoot = Path.GetDirectoryName(Path.GetFullPath(ProjectPath))!;
            }
            else
            {
                // 自动搜索当前目录或父目录中的 .sln 文件
                var dir = Directory.GetCurrentDirectory();
                var sln = Directory.GetFiles(dir, "*.sln").FirstOrDefault();
                if (sln != null)
                {
                    ProjectPath = sln;
                    ProjectRoot = dir;
                }
                else
                {
                    ProjectRoot = dir;
                }
            }

            if (!string.IsNullOrEmpty(ProjectPath))
                ProjectPath = Path.GetFullPath(ProjectPath);
            ProjectRoot = Path.GetFullPath(ProjectRoot);
        }

        public static void PrintUsage()
        {
            Console.WriteLine(@"SEBuilder - SaturnEngine Build Tool

Usage:
  SEBuilder <Command> [Options]

Commands:
  Build             编译项目
  Package           打包编译产物
  Cook              烘焙资源（预留）
  Prerender         预渲染（预留）
  Compress          压缩文件（预留）

Build Options:
  -Project, -P        <path>      .sln 或 .csproj 文件路径
  -Target, -T         <name>      要编译的子项目名（可多次指定）
  -Platform, -Plt     <name>      目标平台: Win, Mac, Linux, Android, IOS
  -Arch, -Architecture, -A <arch> 架构: x64, ARM64, x86
  -Config, -Configuration, -C <config> 配置: Debug, Release, Shipping
  -AOT                           启用 AOT 发布
  -Output, -O         <path>      输出目录
  -Verbose, -V                   详细输出

Package Options:
  -Project, -P        <path>      .sln 或 .csproj 文件路径
  -Platform, -Plt     <name>      目标平台
  -Arch, -A           <arch>      架构
  -Config, -C         <config>    配置
  -Output, -O         <path>      输出目录
  -PackageOutput, -PO <path>      打包输出目录
  -IncludePdb                    包含 PDB 调试符号

Cook Options (预留):
  -CookSourceDir, -CSD <path>     资源源目录
  -CookOutputDir, -COD <path>     资源输出目录

Prerender Options (预留):
  -PrerenderSource, -PRS <path>   预渲染源文件
  -PrerenderOutput, -PRO <path>   预渲染输出文件

Compress Options (预留):
  -CompressionSource, -CMS <path> 压缩源文件/目录
  -CompressionOutput, -CMO <path> 压缩输出文件

Examples:
  SEBuilder Build -Plt=Win -A=x64 -C=Debug
  SEBuilder Build -Plt=Mac -A=ARM64 -C=Release -AOT -T=Windows_Test_Project
  SEBuilder Build -Plt=Android -A=ARM64 -C=Release -T=SaturnEngine
  SEBuilder Package -Plt=Win -A=x64 -C=Release -O=./BuildOutput -IncludePdb
  SEBuilder Cook -CSD=./Assets -COD=./CookedAssets
");
        }
    }
}
