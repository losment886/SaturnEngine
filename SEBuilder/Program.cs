using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SEBuilder.SECore;
using SEBuilder.SEPlatform;

namespace SEBuilder;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            if (args.Length < 1)
            {
                Console.WriteLine("SEBuilder - Simple Engine Build System");
                Console.WriteLine("用法: SEBuilder <命令> [选项]");
                Console.WriteLine("命令:");
                Console.WriteLine("  build      编译项目");
                Console.WriteLine("  package    打包项目");
                Console.WriteLine("  cook       烘焙资源");
                Console.WriteLine("  prerender   预渲染资源");
                Console.WriteLine("  compress    压缩资源");
                Console.WriteLine();
                Console.WriteLine("选项:");
                Console.WriteLine("  --project <路径>    指定项目文件 (.sln, .csproj, .vcxproj) 或项目目录");
                Console.WriteLine("  --platform <平台>   指定目标平台 (windows, linux, android, ios)");
                Console.WriteLine("  --configuration <配置> 指定构建配置 (Debug, Release)");
                Console.WriteLine("  --verbose           输出详细日志");
                Console.WriteLine("  --help              显示帮助信息");
                Console.WriteLine("  --version           显示版本信息");
                
                Console.WriteLine("进入命令行模式，请在下方输入编译指令");
                args = Console.ReadLine()?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                
            }
            
            
            // 解析命令行参数
            var cmd = SEBuildCommand.Parse(args);

            if (args.Length == 0)
                return;

            // 验证项目路径
            if (string.IsNullOrEmpty(cmd.ProjectPath) && string.IsNullOrEmpty(cmd.ProjectRoot))
            {
                Console.Error.WriteLine("[SEBuilder] 错误: 未指定项目路径或无法自动检测项目");
                return;
            }

            // 解析项目
            var projects = LoadProjects(cmd);
            if (projects.Count == 0)
            {
                Console.Error.WriteLine("[SEBuilder] 错误: 未找到任何可编译的项目");
                return;
            }

            // 创建平台
            var platformInfo = cmd.ToPlatformInfo();
            var platform = SEPlatformFactory.CreatePlatform(platformInfo);

            Console.WriteLine($"[SEBuilder] 平台: {platform.Name}");
            Console.WriteLine($"[SEBuilder] 配置: {platformInfo}");
            Console.WriteLine($"[SEBuilder] 找到项目数: {projects.Count}");
            Console.WriteLine();

            // 根据命令执行不同操作
            await ExecuteCommand(cmd, platform, projects);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SEBuilder] 发生异常: {ex.Message}");
            if (args.Any(a => a.Contains("verbose")))
                Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// 加载项目信息
    /// </summary>
    static List<SEProjectInfo> LoadProjects(SEBuildCommand cmd)
    {
        var projects = new List<SEProjectInfo>();

        // 如果指定了 .sln 文件
        if (!string.IsNullOrEmpty(cmd.ProjectPath) && cmd.ProjectPath.EndsWith(".sln"))
        {
            Console.WriteLine($"[SEBuilder] 解析解决方案: {cmd.ProjectPath}");
            projects = SEProjectResolver.ParseSolution(cmd.ProjectPath);
        }
        // 如果指定了 .csproj 文件
        else if (!string.IsNullOrEmpty(cmd.ProjectPath) && cmd.ProjectPath.EndsWith(".csproj"))
        {
            Console.WriteLine($"[SEBuilder] 解析项目: {cmd.ProjectPath}");
            var mainProject = SEProjectResolver.ParseCsprojFile(cmd.ProjectPath);
            projects.AddRange(CollectAllProjects(mainProject, new HashSet<string>()));
        }
        // 如果指定了 .vcxproj 文件
        else if (!string.IsNullOrEmpty(cmd.ProjectPath) && cmd.ProjectPath.EndsWith(".vcxproj"))
        {
            Console.WriteLine($"[SEBuilder] 解析项目: {cmd.ProjectPath}");
            projects.Add(SEProjectResolver.ParseVcxproj(cmd.ProjectPath));
        }
        // 如果指定了目录，查找 .sln 或 CMakeLists.txt
        else if (Directory.Exists(cmd.ProjectRoot))
        {
            Console.WriteLine($"[SEBuilder] 扫描项目目录: {cmd.ProjectRoot}");
            
            var slnFiles = Directory.GetFiles(cmd.ProjectRoot, "*.sln");
            if (slnFiles.Length > 0)
            {
                projects = SEProjectResolver.ParseSolution(slnFiles[0]);
            }
            else
            {
                // 查找 CMakeLists.txt
                var cmakeProject = SEProjectResolver.FindCMakeProject(cmd.ProjectRoot);
                if (cmakeProject != null)
                    projects.Add(cmakeProject);
            }
        }

        SEProjectResolver.SetDependencies(projects);

        return projects;
    }

    /// <summary>
    /// 执行编译命令
    /// </summary>
    static async Task ExecuteCommand(SEBuildCommand cmd, SEPlatformBase platform, List<SEProjectInfo> projects)
    {
        switch (cmd.Command)
        {
            case SECommandType.Build:
                await ExecuteBuild(cmd, platform, projects);
                break;
            case SECommandType.Package:
                await ExecutePackage(cmd, platform, projects);
                break;
            case SECommandType.Cook:
                ExecuteCook(cmd);
                break;
            case SECommandType.Prerender:
                ExecutePrerender(cmd);
                break;
            case SECommandType.Compress:
                ExecuteCompress(cmd);
                break;
            default:
                Console.Error.WriteLine($"[SEBuilder] 未知命令: {cmd.Command}");
                break;
        }
    }

    /// <summary>
    /// 执行编译
    /// </summary>
    static async Task ExecuteBuild(SEBuildCommand cmd, SEPlatformBase platform, List<SEProjectInfo> projects)
    {
        var manager = new SEBuildManager(cmd, platform, projects);

        if (!manager.ValidateEnvironment(out var error))
        {
            Console.Error.WriteLine($"[SEBuilder] 环境验证失败: {error}");
            return;
        }

        var success = await manager.Build();
        Environment.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// 执行打包
    /// </summary>
    static async Task ExecutePackage(SEBuildCommand cmd, SEPlatformBase platform, List<SEProjectInfo> projects)
    {
        Console.WriteLine("[SEBuilder] 执行打包功能（预留实现）");
        // TODO: 实现打包功能
        await Task.CompletedTask;
    }

    /// <summary>
    /// 执行烘焙
    /// </summary>
    static void ExecuteCook(SEBuildCommand cmd)
    {
        Console.WriteLine("[SEBuilder] 执行资源烘焙功能（预留实现）");
        // TODO: 实现烘焙功能
    }

    /// <summary>
    /// 执行预渲染
    /// </summary>
    static void ExecutePrerender(SEBuildCommand cmd)
    {
        Console.WriteLine("[SEBuilder] 执行预渲染功能（预留实现）");
        // TODO: 实现预渲染功能
    }

    /// <summary>
    /// 执行压缩
    /// </summary>
    static void ExecuteCompress(SEBuildCommand cmd)
    {
        Console.WriteLine("[SEBuilder] 执行压缩功能（预留实现）");
        // TODO: 实现压缩功能
    }

    /// <summary>
    /// 收集项目及其所有依赖项
    /// </summary>
    static List<SEProjectInfo> CollectAllProjects(SEProjectInfo project, HashSet<string> visited)
    {
        var result = new List<SEProjectInfo>();
        if (visited.Contains(project.FilePath))
            return result;

        visited.Add(project.FilePath);
        result.Add(project);

        foreach (var dep in project.Dependencies)
        {
            result.AddRange(CollectAllProjects(dep, visited));
        }

        return result;
    }
}
