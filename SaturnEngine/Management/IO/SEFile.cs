using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SaturnEngine.Global;
using SaturnEngine.Security;
using SaturnEngine.Asset;

namespace SaturnEngine.Management.IO
{
    
    /// <summary>
    /// 提供跨平台内外部文件访问
    /// </summary>
    /// <remarks>
    /// <para>
    /// "asset:/%PATH%" 表示为访问程序内置资源文件，路径为相对于资源根目录的路径，后面第一个为资源包文件名，后面为资源包内路径
    /// </para>
    /// <para>
    /// "file:/%PATH%" 表示为访问外部文件，路径为绝对路径
    /// </para>
    /// 
    /// 在输入时，如果路径以"asset:/"开头，则会被解析为访问内置资源文件；如果路径以"file:/"开头，则会被解析为访问外部文件。
    /// 如果传入相对路径，不以"asset:/"或"file:/"开头，则会被默认解析为访问外部文件，
    /// 资源文件路径生成时不允许相对路径操作符，在切换路径时可以使用相对路径操作符，但最终生成的路径必须是绝对路径。
    /// </remarks>
    public class SEFile
    {
        public class SEFileStream : Stream
        {
            private Stream innerStream;
            private SEFile Owner;
            public SEFileStream(Stream stream, SEFile current)
            {
                this.innerStream = stream;
                Owner = current;
            }
            public override bool CanRead => innerStream.CanRead;
            public override bool CanSeek => innerStream.CanSeek;
            public override bool CanWrite => innerStream.CanWrite;
            public override long Length => innerStream.Length;
            public override long Position { get => innerStream.Position; set => innerStream.Position = value; }
            public override void Flush()
            {
                innerStream.Flush();
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                return innerStream.Read(buffer, offset, count);
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                return innerStream.Seek(offset, origin);
            }
            public override void SetLength(long value)
            {
                innerStream.SetLength(value);
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                innerStream.Write(buffer, offset, count);
            }
            public override void Close()
            {
                base.Close();
                Owner.InternalClose();
                
            }
        }

        string path;
        bool isOpen;
        SEFileStream? tempstream;
        public string FullPath => path;
        public bool Opened => isOpen;
        public SEFile(string path)
        {
            this.path = path;
            //this.FullPath = Path.GetFullPath(path);
            this.isOpen = false;
            this.path = ProcessPath(path);
        }
        internal void InternalClose()
        {
            isOpen = false;
            tempstream = null;
        }
        public void Close()
        {
            isOpen = false;
            tempstream?.Close();
        }
        public SEFileStream Open()
        {
            if (isOpen)
                throw new InvalidOperationException("File is already opened.");
            Stream stream;
            if (path.StartsWith("asset:/", StringComparison.OrdinalIgnoreCase))
            {
                string filePath = path.Substring(7);
                string pakname = filePath.Split(":::")[0];
                filePath = "." + filePath.Substring(0, pakname.Length);
                ulong key = pakname.ToSTC();
                string filename = "";
                if (pakname.StartsWith('#'))
                {
                    if (GVariables.GlobalResources.TryGetValue(key, out LRL l))
                    {
                        //stream = GVariables.GlobalResources[pakname].Open(filePath);
                        if (l.TryGet(filename, out LRL.LRBK b))
                        {

                            l.UnLockStream((uint)l.SearchByName(filename));
                            stream = b.data;
                        }
                        else
                        {
                            throw new FileNotFoundException($"Asset file '{filename}' not fount.");
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException($"Asset package '{pakname}' not found.");
                    }
                }
                else
                {
                    LRL l = new LRL();
                    l.LoadFromFile(filePath);
                    //stream = GVariables.GlobalResources[pakname].Open(filePath);
                    if (l.TryGet(filename, out LRL.LRBK b))
                    {

                        l.UnLockStream((uint)l.SearchByName(filename));
                        stream = b.data;
                    }
                    else
                    {
                        throw new FileNotFoundException($"Asset file '{filename}' not fount.");
                    }
                }

            }
            else if (path.StartsWith("file:/", StringComparison.OrdinalIgnoreCase))
            {
                string filePath = path.Substring(6); // 去掉 "file:/" 前缀
                stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);
            }
            else
            {
                throw new InvalidOperationException("Invalid file path format.");
            }
            isOpen = true;
            tempstream = new SEFileStream(stream, this);
            return tempstream;
        }
        /// <summary>
        /// 跳转文件或目录，传入路径为以“.”开头的相对路径，如果不是以“.”开头，则会被当作绝对路径处理
        /// </summary>
        /// <remarks>
        /// 正在使用文件时不可跳转
        /// </remarks>
        /// <param name="ofpath">路径</param>
        public void Seek(string ofpath)
        {
            if (string.IsNullOrEmpty(ofpath))
                return;

            if (ofpath.StartsWith('.'))
            {
                // 相对路径：基于当前 path 的目录进行解析
                if (path.StartsWith("asset:/", StringComparison.OrdinalIgnoreCase))
                {
                    // 解析当前 asset 包与内部路径
                    string p = path.Substring(7);
                    int sepIndex = p.IndexOf(":::", StringComparison.Ordinal);
                    string pkgSpec = sepIndex >= 0 ? p.Substring(0, sepIndex) : p;
                    string innerPath = sepIndex >= 0 ? p.Substring(sepIndex + 3) : string.Empty;

                    innerPath = innerPath.TrimStart('/');
                    // 计算基目录（去掉最后一段文件名）
                    string baseDir;
                    if (string.IsNullOrEmpty(innerPath))
                    {
                        baseDir = "/"; // 包根
                    }
                    else
                    {
                        var parts = innerPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (innerPath.EndsWith('/'))
                        {
                            baseDir = "/" + string.Join("/", parts);
                        }
                        else if (parts.Length <= 1)
                        {
                            baseDir = "/"; // 只有文件名，回到包根
                        }
                        else
                        {
                            baseDir = "/" + string.Join("/", parts.Take(parts.Length - 1));
                        }
                    }

                    // 组合基目录与相对路径，然后交给 ProcessPath 规范化
                    string combinedInner = baseDir.TrimEnd('/') + "/" + ofpath;
                    string newPath = "asset:/" + pkgSpec + ":::" + combinedInner;
                    this.path = ProcessPath(newPath);
                }
                else if (path.StartsWith("file:/", StringComparison.OrdinalIgnoreCase))
                {
                    // 基于当前外部文件的目录来解析相对路径
                    string filePath = path.Substring(6);
                    // 使用平台路径分隔符以便 Path API 正确工作
                    string fsPath = filePath.Replace('/', Path.DirectorySeparatorChar);
                    string dir = Path.GetDirectoryName(fsPath) ?? fsPath;
                    string combined = Path.GetFullPath(Path.Combine(dir, ofpath));
                    combined = combined.Replace('\\', '/');
                    this.path = ProcessPath("file:/" + combined);
                }
                else
                {
                    // 无前缀时，按原有逻辑交给 ProcessPath（会以 ProgramDataDir 为根）
                    this.path = ProcessPath(ofpath);
                }
            }
            else
            {
                this.path = ProcessPath(ofpath);
            }
        }
        string ProcessPath(string p)
        {
            if (string.IsNullOrEmpty(p))
                return p;

            // 更新规则：
            // asset:/(FLAG)%PACKAGENAME%:::%PATH% 访问资源文件（内置与外部扩展资源文件），其中 %PACKAGENAME% 为资源包文件名，%PATH% 为资源包内路径
            // 如
            // asset:/#base:::/textures/tex.png 访问内置资源包 base.lrl 中的 textures/tex.png 文件，内置资源包文件名以 '#' 开头
            // asset:/./data/model1:::/model.obj 访问外部资源包 {GVariables.ProgramDataDir}/data/model1.lrl 中的 model.obj 文件
            // asset:/C:/path/to/assetpak:::/file.txt 访问外部资源包 C:/path/to/assetpak.lrl 中的 file.txt 文件
            // 所有相对路径以{GVariables.ProgramDataDir}为根目录进行解析

            // 统一分隔符
            p = p.Replace('\\', '/');
            string prefix = string.Empty;
            if (p.StartsWith("asset:/", StringComparison.OrdinalIgnoreCase))
            {
                prefix = "asset:/";
                p = p.Substring(7);

                // 找到包名与内部路径的分隔符 ":::":
                int sepIndex = p.IndexOf(":::", StringComparison.Ordinal);
                string pkgSpec;
                string innerPath;
                if (sepIndex >= 0)
                {
                    pkgSpec = p.Substring(0, sepIndex);
                    innerPath = p.Substring(sepIndex + 3);
                }
                else
                {
                    // 如果没有分隔符，则整个剩余视为包名，内部路径为空
                    pkgSpec = p;
                    innerPath = string.Empty;
                }

                pkgSpec = pkgSpec.Trim();
                innerPath = innerPath.Trim();

                string resolvedPkg;
                if (pkgSpec.StartsWith("#"))
                {
                    // 内置包，直接保留（不添加扩展名）
                    resolvedPkg = pkgSpec;
                }
                else if (pkgSpec.StartsWith(".") || !Path.IsPathRooted(pkgSpec))
                {
                    // 相对路径：以 GVariables.ProgramDataDir 为根
                    string rel = pkgSpec;
                    // 去掉开头的 ./ 或 .\\
                    if (rel.StartsWith("./")) rel = rel.Substring(2);
                    else if (rel.StartsWith(".") && rel.Length == 1) rel = string.Empty;
                    else if (rel.StartsWith(".")) rel = rel.TrimStart('.', '/');

                    string combined = Path.Combine(GVariables.ProgramDataDir ?? string.Empty, rel);
                    string full = Path.GetFullPath(combined);
                    if (!full.EndsWith(".lrl", StringComparison.OrdinalIgnoreCase))
                        full = full + ".lrl";
                    resolvedPkg = full.Replace('\\', '/');
                }
                else
                {
                    // 绝对路径（例如 C:/... ）
                    string full = pkgSpec;
                    if (!full.EndsWith(".lrl", StringComparison.OrdinalIgnoreCase))
                        full = full + ".lrl";
                    resolvedPkg = Path.GetFullPath(full).Replace('\\', '/');
                }

                // 规范化内部路径（作为包内路径）
                // 移除前导 '/' 以便统一处理
                innerPath = innerPath.TrimStart('/');
                var parts = innerPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var stack = new Stack<string>();
                foreach (var part in parts)
                {
                    if (part == ".") continue;
                    if (part == "..")
                    {
                        if (stack.Count > 0) stack.Pop();
                        continue;
                    }
                    stack.Push(part);
                }
                var segs = stack.Reverse().ToArray();
                string normInner = string.Join("/", segs);
                // 保证以 '/' 开头表示包内绝对路径
                if (!string.IsNullOrEmpty(normInner))
                    normInner = "/" + normInner;

                return prefix + resolvedPkg + ":::" + normInner;
            }
            else if (p.StartsWith("file:/", StringComparison.OrdinalIgnoreCase))
            {
                prefix = "file:/";
                p = p.Substring(6);
                // 如果以 '.' 开头，按 GVariables.ProgramDataDir 解析；否则按路径解析
                if (p.StartsWith(".") || !Path.IsPathRooted(p))
                {
                    string rel = p;
                    if (rel.StartsWith("./")) rel = rel.Substring(2);
                    else if (rel.StartsWith(".")) rel = rel.TrimStart('.');
                    string combined = Path.Combine(GVariables.ProgramDataDir ?? string.Empty, rel);
                    string full = Path.GetFullPath(combined).Replace('\\', '/');
                    // 去掉开头多余 '/'
                    if (full.StartsWith("/")) full = full.TrimStart('/');
                    return "file:/" + full;
                }
                else
                {
                    string full = Path.GetFullPath(p).Replace('\\', '/');
                    if (full.StartsWith("/")) full = full.TrimStart('/');
                    return "file:/" + full;
                }
            }
            else
            {
                // 无前缀：按外部文件处理，相对路径以 GVariables.ProgramDataDir 为根
                string combined = Path.Combine(GVariables.ProgramDataDir ?? string.Empty, p);
                string full = Path.GetFullPath(combined).Replace('\\', '/');
                if (full.StartsWith("/")) full = full.TrimStart('/');
                return "file:/" + full;
            }
        }
    }
}
