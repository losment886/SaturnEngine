using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SEBuilder.SECore
{
    /// <summary>
    /// 编译管理器 — 协调整个编译过程
    /// </summary>
    public class SEBuildManager
    {
        private readonly SEBuildCommand _command;
        private readonly SEPlatformBase _platform;
        private readonly List<SEProjectInfo> _projects;

        public SEBuildManager(SEBuildCommand command, SEPlatformBase platform, List<SEProjectInfo> projects)
        {
            _command = command;
            _platform = platform;
            _projects = projects;
        }

        /// <summary>
        /// 验证构建环境
        /// </summary>
        public bool ValidateEnvironment(out string? errorMessage)
        {
            if (!_platform.ValidateToolchain(out var toolchainError))
            {
                errorMessage = $"平台工具链验证失败: {toolchainError}";
                return false;
            }

            if (_projects.Count == 0)
            {
                errorMessage = "未找到任何要编译的项目";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 执行编译
        /// </summary>
        public async Task<bool> Build()
        {
            Console.WriteLine($"[SEBuilder] 开始编译: {_platform.Name}");
            Console.WriteLine($"[SEBuilder] 平台配置: {_command.ToPlatformInfo()}");
            Console.WriteLine($"[SEBuilder] 项目数量: {_projects.Count}");
            Console.WriteLine();

            string? outputDir = _command.OutputDir;
            if (string.IsNullOrEmpty(outputDir))
            {
                outputDir = Path.Combine(_command.ProjectRoot, "BuildOutput", _platform.GetOutputSubDir(), _command.Configuration.ToString());
            }

            Directory.CreateDirectory(outputDir);

            var projectsToCompile = GetProjectsToCompile();
            if (projectsToCompile.Count == 0)
            {
                Console.Error.WriteLine("[SEBuilder] 未找到要编译的项目");
                return false;
            }

            Console.WriteLine($"[SEBuilder] 将编译 {projectsToCompile.Count} 个项目");
            foreach (var proj in projectsToCompile)
            {
                Console.WriteLine($"  - {proj.Name} ({proj.Type})");
            }
            Console.WriteLine();

            bool success = true;
            int projectIndex = 1;

            foreach (var project in projectsToCompile)
            {
                Console.WriteLine($"[{projectIndex}/{projectsToCompile.Count}] 编译项目: {project.Name}");
                Console.WriteLine($"  类型: {project.Type}");
                Console.WriteLine($"  路径: {project.FilePath}");

                var projectOutputDir = Path.Combine(outputDir, project.Name);
                Directory.CreateDirectory(projectOutputDir);

                bool projectSuccess = await CompileProject(project, projectOutputDir);
                
                if (!projectSuccess)
                {
                    Console.Error.WriteLine($"[SEBuilder] 项目编译失败: {project.Name}");
                    success = false;
                    
                    if (!_command.Verbose)
                    {
                        Console.WriteLine("提示: 使用 --verbose 选项查看详细输出");
                    }
                }
                else
                {
                    Console.WriteLine($"[SEBuilder] 项目编译成功: {project.Name}");
                }
                
                Console.WriteLine();
                projectIndex++;
            }

            if (success)
            {
                Console.WriteLine($"[SEBuilder] 所有项目编译完成");
                Console.WriteLine($"[SEBuilder] 输出目录: {outputDir}");
            }
            else
            {
                Console.Error.WriteLine("[SEBuilder] 编译过程中出现错误");
            }

            return success;
        }

        /// <summary>
        /// 获取需要编译的项目列表（拓扑排序）
        /// </summary>
        private List<SEProjectInfo> GetProjectsToCompile()
        {
            var candidates = _projects.Where(p => p.Type != SEProjectType.Solution).ToList();

            if (_command.TargetProjects.Count > 0)
            {
                var result = new List<SEProjectInfo>();
                foreach (var targetName in _command.TargetProjects)
                {
                    var proj = candidates.FirstOrDefault(p => p.Name == targetName);
                    if (proj != null)
                        result.Add(proj);
                }
                return TopologicalSort(result);
            }

            // 默认编译所有项目，按依赖顺序
            return TopologicalSort(candidates);
        }

        /// <summary>
        /// 拓扑排序项目（依赖项优先）
        /// </summary>
        private List<SEProjectInfo> TopologicalSort(List<SEProjectInfo> projects)
        {
            var result = new List<SEProjectInfo>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            void Visit(SEProjectInfo project)
            {
                if (visited.Contains(project.FilePath))
                    return;

                if (visiting.Contains(project.FilePath))
                    throw new InvalidOperationException($"检测到循环依赖: {project.Name}");

                visiting.Add(project.FilePath);

                foreach (var dep in project.Dependencies)
                {
                    Visit(dep);
                }

                visiting.Remove(project.FilePath);
                visited.Add(project.FilePath);
                result.Add(project);
            }

            foreach (var project in projects)
            {
                Visit(project);
            }

            return result;
        }

        /// <summary>
        /// 编译单个项目
        /// </summary>
        private async Task<bool> CompileProject(SEProjectInfo project, string outputDir)
        {
            return project.Type switch
            {
                SEProjectType.DotNet => await CompileDotNetProject(project, outputDir),
                SEProjectType.NativeMSBuild => await CompileNativeMsBuildProject(project, outputDir),
                SEProjectType.NativeCMake => await CompileNativeCMakeProject(project, outputDir),
                _ => false
            };
        }

        /// <summary>
        /// 编译 .NET 项目
        /// </summary>
        private async Task<bool> CompileDotNetProject(SEProjectInfo project, string outputDir)
        {
            try
            {
                bool usePublish = project.OutputType == "Exe" || project.OutputType == "Console" || project.PublishAot;
                
                if (usePublish)
                {
                    Console.WriteLine("  使用 publish 进行编译（启用独立发布）");
                    return await _platform.PublishDotNetProject(project.FilePath, outputDir, _command.Verbose);
                }
                else
                {
                    return await _platform.BuildDotNetProject(project.FilePath, outputDir, _command.Verbose);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  编译失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 编译原生 MSBuild 项目
        /// </summary>
        private async Task<bool> CompileNativeMsBuildProject(SEProjectInfo project, string outputDir)
        {
            try
            {
                return await _platform.BuildWithMSBuild(project.FilePath, outputDir, _command.Verbose);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  编译失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 编译 CMake 项目
        /// </summary>
        private async Task<bool> CompileNativeCMakeProject(SEProjectInfo project, string outputDir)
        {
            try
            {
                var sourceDir = project.Directory;
                var buildDir = Path.Combine(outputDir, "build");
                Directory.CreateDirectory(buildDir);

                Console.WriteLine($"  CMake 源目录: {sourceDir}");
                Console.WriteLine($"  CMake 构建目录: {buildDir}");

                // 执行 CMake 配置
                Console.WriteLine("  Configuring CMake...");
                if (!await _platform.ConfigureCMake(sourceDir, buildDir, _command.Verbose))
                {
                    Console.Error.WriteLine("  CMake 配置失败");
                    return false;
                }

                // 执行 CMake 构建
                Console.WriteLine("  Building with CMake...");
                if (!await _platform.BuildCMake(buildDir, _command.Verbose))
                {
                    Console.Error.WriteLine("  CMake 构建失败");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  编译失败: {ex.Message}");
                return false;
            }
        }
    }
}

