using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SEBuilder.SECore
{
    /// <summary>
    /// 平台编译基类 — 定义平台特定的编译规则
    /// </summary>
    public abstract class SEPlatformBase
    {
        protected SEPlatformInfo PlatformInfo { get; set; }

        public abstract string Name { get; }

        protected SEPlatformBase(SEPlatformInfo info)
        {
            PlatformInfo = info;
        }

        /// <summary>
        /// 获取 .NET 编译参数
        /// </summary>
        public abstract string[] GetDotNetBuildArgs();

        /// <summary>
        /// 获取 CMake 配置参数
        /// </summary>
        public abstract string[] GetCMakeConfigureArgs();

        /// <summary>
        /// 获取 CMake 构建参数
        /// </summary>
        public abstract string[] GetCMakeBuildArgs();

        /// <summary>
        /// 获取原生编译环境变量
        /// </summary>
        public abstract Dictionary<string, string> GetNativeEnvironmentVariables();

        /// <summary>
        /// 获取输出子目录名
        /// </summary>
        public abstract string GetOutputSubDir();

        /// <summary>
        /// 验证工具链是否可用
        /// </summary>
        public abstract bool ValidateToolchain(out string? errorMessage);

        /// <summary>
        /// 执行 .NET 项目编译
        /// </summary>
        public virtual async Task<bool> BuildDotNetProject(string projectPath, string outputDir, bool verbose = false)
        {
            var args = GetDotNetBuildArgs();
            var argList = new List<string> { "build", projectPath };
            argList.AddRange(args);
            
            if (!string.IsNullOrEmpty(outputDir))
            {
                argList.Add("-o");
                argList.Add(outputDir);
            }

            return await RunCommand("dotnet", string.Join(" ", argList), verbose);
        }

        /// <summary>
        /// 执行 .NET 项目发布
        /// </summary>
        public virtual async Task<bool> PublishDotNetProject(string projectPath, string outputDir, bool verbose = false)
        {
            var args = GetDotNetBuildArgs();
            var argList = new List<string> { "publish", projectPath };
            argList.AddRange(args);
            
            if (!string.IsNullOrEmpty(outputDir))
            {
                argList.Add("-o");
                argList.Add(outputDir);
            }

            return await RunCommand("dotnet", string.Join(" ", argList), verbose);
        }

        /// <summary>
        /// 执行 CMake 配置
        /// </summary>
        public virtual async Task<bool> ConfigureCMake(string sourceDir, string buildDir, bool verbose = false)
        {
            var args = GetCMakeConfigureArgs();
            
            if (verbose)
                args = args.Append("-DCMAKE_VERBOSE_MAKEFILE=ON").ToArray();

            var argList = new List<string> { "-S", sourceDir, "-B", buildDir };
            argList.AddRange(args);

            return await RunCommand("cmake", string.Join(" ", argList), verbose);
        }

        /// <summary>
        /// 执行 CMake 构建
        /// </summary>
        public virtual async Task<bool> BuildCMake(string buildDir, bool verbose = false)
        {
            var args = GetCMakeBuildArgs();
            var argList = new List<string> { "--build", buildDir };
            argList.AddRange(args);

            if (verbose)
                argList.Add("--verbose");

            return await RunCommand("cmake", string.Join(" ", argList), verbose);
        }

        /// <summary>
        /// 执行 MSBuild 编译（仅 Windows）
        /// </summary>
        public virtual async Task<bool> BuildWithMSBuild(string projectPath, string? outputDir, bool verbose = false)
        {
            throw new NotSupportedException($"MSBuild is not supported on {Name}");
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        protected virtual async Task<bool> RunCommand(string command, string arguments, bool verbose = false)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = verbose,
                    RedirectStandardError = true,
                    CreateNoWindow = !verbose
                };

                // 添加平台特定的环境变量
                var envVars = GetNativeEnvironmentVariables();
                foreach (var kvp in envVars)
                {
                    psi.EnvironmentVariables[kvp.Key] = kvp.Value;
                }

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return false;

                    if (verbose)
                    {
                        var output = await process.StandardOutput.ReadToEndAsync();
                        if (!string.IsNullOrEmpty(output))
                            Console.WriteLine(output);
                    }

                    var error = await process.StandardError.ReadToEndAsync();
                    if (!string.IsNullOrEmpty(error))
                        Console.Error.WriteLine(error);

                    await Task.Run(() => process.WaitForExit());
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"执行命令失败: {command} {arguments}");
                Console.Error.WriteLine($"错误: {ex.Message}");
                return false;
            }
        }

        public override string ToString() => $"{Name} ({PlatformInfo})";
    }
}

