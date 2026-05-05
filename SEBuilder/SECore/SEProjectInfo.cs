using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SEBuilder.SECore
{
    /// <summary>
    /// 子项目类型
    /// </summary>
    public enum SEProjectType
    {
        Unknown,
        /// <summary>.NET C# 项目 (.csproj)</summary>
        DotNet,
        /// <summary>原生 C/C++ MSBuild 项目 (.vcxproj)</summary>
        NativeMSBuild,
        /// <summary>CMake 项目 (CMakeLists.txt)</summary>
        NativeCMake,
        /// <summary>解决方案 (.sln)</summary>
        Solution
    }

    /// <summary>
    /// 已解析的项目信息
    /// </summary>
    public class SEProjectInfo
    {
        /// <summary>项目名称</summary>
        public string Name { get; set; } = "";
        /// <summary>项目文件完整路径</summary>
        public string FilePath { get; set; } = "";
        /// <summary>项目类型</summary>
        public SEProjectType Type { get; set; } = SEProjectType.Unknown;
        /// <summary>项目目录</summary>
        public string Directory => Path.GetDirectoryName(FilePath) ?? "";
        /// <summary>输出类型（Library / Exe / WinExe）</summary>
        public string? OutputType { get; set; }
        /// <summary>目标框架（.NET 项目）</summary>
        public string? TargetFramework { get; set; }
        /// <summary>是否启用 AOT</summary>
        public bool PublishAot { get; set; }
        /// <summary>平台配置</summary>
        public List<string> Platforms { get; set; } = new();
        /// <summary>依赖的其他项目</summary>
        public List<SEProjectInfo> Dependencies { get; set; } = new();

        public override string ToString() => $"{Name} [{Type}]: {FilePath}";
    }

    /// <summary>
    /// 解决方案/项目解析器
    /// </summary>
    public class SEProjectResolver
    {
        /// <summary>
        /// 解析 .sln 文件中的所有项目
        /// </summary>
        public static List<SEProjectInfo> ParseSolution(string slnPath)
        {
            var result = new List<SEProjectInfo>();
            var slnDir = Path.GetDirectoryName(Path.GetFullPath(slnPath))!;

            if (!File.Exists(slnPath))
                return result;

            var lines = File.ReadAllLines(slnPath);
            // 正则匹配 .sln 中的 Project 行
            // Project("{GUID}") = "ProjectName", "ProjectPath.csproj", "{GUID}"
            var regex = new Regex(@"Project\(""[^""]+""\)\s*=\s*""([^""]+)""\s*,\s*""([^""]+)""", RegexOptions.Compiled);

            foreach (var line in lines)
            {
                var match = regex.Match(line.Trim());
                if (match.Success)
                {
                    var projName = match.Groups[1].Value;
                    var projRelativePath = match.Groups[2].Value.Replace('\\', Path.DirectorySeparatorChar);
                    var projFullPath = Path.GetFullPath(Path.Combine(slnDir, projRelativePath));

                    var info = new SEProjectInfo
                    {
                        Name = projName,
                        FilePath = projFullPath,
                        Type = DetectProjectType(projFullPath)
                    };

                    // 如果是 .csproj，解析更多细节
                    if (info.Type == SEProjectType.DotNet)
                        ParseCsproj(info);

                    result.Add(info);
                }
            }

            return result;
        }

        /// <summary>
        /// 解析单个 .csproj 文件
        /// </summary>
        public static SEProjectInfo ParseCsprojFile(string csprojPath)
        {
            var info = new SEProjectInfo
            {
                Name = Path.GetFileNameWithoutExtension(csprojPath),
                FilePath = Path.GetFullPath(csprojPath),
                Type = SEProjectType.DotNet
            };
            ParseCsproj(info);
            return info;
        }

        /// <summary>
        /// 解析 .vcxproj 文件
        /// </summary>
        public static SEProjectInfo ParseVcxproj(string vcxprojPath)
        {
            var info = new SEProjectInfo
            {
                Name = Path.GetFileNameWithoutExtension(vcxprojPath),
                FilePath = Path.GetFullPath(vcxprojPath),
                Type = SEProjectType.NativeMSBuild
            };

            // 如果目录中有 CMakeLists.txt，优先使用 CMake
            var dir = Path.GetDirectoryName(vcxprojPath);
            if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "CMakeLists.txt")))
            {
                info.Type = SEProjectType.NativeCMake;
                info.FilePath = Path.Combine(dir, "CMakeLists.txt");
            }

            return info;
        }

        /// <summary>
        /// 检测项目类型
        /// </summary>
        private static SEProjectType DetectProjectType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLower();
            return ext switch
            {
                ".csproj" => SEProjectType.DotNet,
                ".vcxproj" => SEProjectType.NativeMSBuild,
                ".sln" => SEProjectType.Solution,
                _ when Path.GetFileName(filePath).Equals("CMakeLists.txt", StringComparison.OrdinalIgnoreCase)
                    => SEProjectType.NativeCMake,
                _ => SEProjectType.Unknown
            };
        }

        /// <summary>
        /// 解析 .csproj 的 XML 内容
        /// </summary>
        private static void ParseCsproj(SEProjectInfo info)
        {
            if (!File.Exists(info.FilePath))
                return;

            try
            {
                var doc = XDocument.Load(info.FilePath);
                var ns = doc.Root?.GetDefaultNamespace();

                if (ns == null)
                    return;

                // PropertyGroup 中的属性
                var propertyGroups = doc.Descendants(ns + "PropertyGroup");
                foreach (var pg in propertyGroups)
                {
                    var outputType = pg.Element(ns + "OutputType")?.Value;
                    if (!string.IsNullOrEmpty(outputType))
                        info.OutputType = outputType;

                    var tf = pg.Element(ns + "TargetFramework")?.Value;
                    if (!string.IsNullOrEmpty(tf))
                        info.TargetFramework = tf;

                    var platforms = pg.Element(ns + "Platforms")?.Value;
                    if (!string.IsNullOrEmpty(platforms))
                        info.Platforms = platforms.Split(';').Select(p => p.Trim()).ToList();

                    var publishAot = pg.Element(ns + "PublishAot")?.Value;
                    if (bool.TryParse(publishAot, out var aot))
                        info.PublishAot = aot;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SEProjectInfo] 解析失败: {info.FilePath} - {ex.Message}");
            }
        }

        /// <summary>
        /// 查找根 CMakeLists.txt 以获取原生项目信息
        /// </summary>
        public static SEProjectInfo? FindCMakeProject(string rootDir)
        {
            var cmakePath = Path.Combine(rootDir, "CMakeLists.txt");
            if (!File.Exists(cmakePath))
            {
                cmakePath = Path.Combine(rootDir, "SENativeRenderer", "CMakeLists.txt");
                if (!File.Exists(cmakePath))
                    return null;
            }

            return new SEProjectInfo
            {
                Name = Path.GetFileName(Path.GetDirectoryName(cmakePath) ?? "SENativeRenderer"),
                FilePath = cmakePath,
                Type = SEProjectType.NativeCMake
            };
        }

        /// <summary>
        /// 设置项目依赖关系
        /// </summary>
        public static void SetDependencies(List<SEProjectInfo> projects)
        {
            foreach (var project in projects.Where(p => p.Type == SEProjectType.DotNet))
            {
                SetProjectDependencies(project, projects);
            }
        }

        /// <summary>
        /// 设置单个项目的依赖关系
        /// </summary>
        private static void SetProjectDependencies(SEProjectInfo info, List<SEProjectInfo> allProjects)
        {
            if (!File.Exists(info.FilePath))
                return;

            try
            {
                var doc = XDocument.Load(info.FilePath);
                var ns = doc.Root?.GetDefaultNamespace();

                if (ns == null)
                    return;

                var projectReferences = doc.Descendants(ns + "ProjectReference");
                foreach (var pr in projectReferences)
                {
                    var include = pr.Attribute("Include")?.Value;
                    if (!string.IsNullOrEmpty(include))
                    {
                        var depPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(info.FilePath)!, include.Replace('\\', Path.DirectorySeparatorChar)));
                        var depName = Path.GetFileNameWithoutExtension(depPath);
                        var depProject = allProjects.FirstOrDefault(p => p.Name == depName);
                        if (depProject != null)
                        {
                            info.Dependencies.Add(depProject);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SEProjectInfo] 解析依赖失败: {info.FilePath} - {ex.Message}");
            }
        }
    }
}
