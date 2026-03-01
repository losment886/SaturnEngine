using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Global;
using SaturnEngine.Management;
using SaturnEngine.Management.IO;
using SaturnEngine.Security;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;

namespace SaturnEngine.ScriptEngine
{
    public class SEScriptEngine : SEBase, IDisposable
    {
        private V8ScriptEngine? JSEngine;
        public bool EnableJS { get; private set; } = false;
        public bool EnableLua { get; private set; } = false;
        public bool EnableCSharp { get; private set; } = false;
        public bool EnablePython { get; private set; } = false;

        List<string> csdep;
        Dictionary<ulong, V8Script> jscripts;
        Dictionary<ulong, Assembly> csscripts;
        Dictionary<ulong, ScriptType> scriptslib;
        static List<MetadataReference> DefalutReferences = new List<MetadataReference>();
        public enum EnableScriptType
        {
            CSharp = 1,
            JavaScript = 2,
            Lua = 4,
            Python = 8,

            All = CSharp | JavaScript | Lua | Python,
        }
        public enum ScriptType
        {
            CSharp = 0,
            SEUI = 1,
            JavaScript = 2,
            Lua = 3,
            Python = 4,

            Unknown = 7
        }
        public SEScriptEngine()
        {
            SELogger.Log("脚本引擎类初始化");
            csdep = new List<string>();
            jscripts = new Dictionary<ulong, V8Script>();
            csscripts = new Dictionary<ulong, Assembly>();
            scriptslib = new Dictionary<ulong, ScriptType>();
        }

        public dynamic Run(string nm, string oclass, string funcname, object?[]? para) => Run(STCCode.GetSTC(nm), oclass, funcname, para);

        /// <summary>
        /// 运行特殊函数，方法必须是public且static
        /// </summary>
        /// <param name="nm"></param>
        /// <param name="oclass"></param>
        /// <param name="funcname"></param>
        /// <returns></returns>
        public dynamic Run(ulong nm, string oclass, string funcname, object?[]? para)
        {
            if (scriptslib.TryGetValue(nm, out var t))
            {
                switch (t)
                {
                    case ScriptType.CSharp:
                        if (EnableCSharp && csscripts.TryGetValue(nm, out var asm))
                        {
                            Type? programType = asm.GetType(oclass);
                            if (programType != null)
                            {
                                MethodInfo? mainMethod = programType.GetMethod(funcname, BindingFlags.Public | BindingFlags.Static);
                                if (mainMethod != null)
                                {
                                    return mainMethod.Invoke(Activator.CreateInstance(programType), para);
                                }
                            }
                        }
                        break;
                    case ScriptType.JavaScript:

                        //不支持类方法调用
                        break;
                }
            }
            return null;
        }
        public bool Exists(ulong nm)
        {
            return scriptslib.TryGetValue(nm, out _);
        }
        public bool Exists(string nm) => Exists(STCCode.GetSTC(nm));
        public dynamic RunMain(string nm) => RunMain(STCCode.GetSTC(nm));
        public dynamic? Get(string nm) => Get(STCCode.GetSTC(nm));
        public dynamic? Get(ulong nm)
        {
            if( scriptslib.TryGetValue(nm,out var t))
            {
                switch(t)
                {
                    case ScriptType.CSharp:
                        if (EnableCSharp && csscripts.TryGetValue(nm, out var asm))
                            { return asm; }
                        break;
                    case ScriptType.JavaScript:
                        if (EnableJS && jscripts.TryGetValue(nm,out var vl))
                        {
                            JSEngine.Execute(vl);
                            return JSEngine.Script;
                        }
                        break;
                }
            }
            return null;
        }

        /// <summary>
        /// 适用于全体脚本类型的主函数运行
        /// </summary>
        /// <param name="nm"></param>
        /// <returns></returns>
        public dynamic? RunMain(ulong nm)
        {
            if (scriptslib.TryGetValue(nm, out var t))
            {
                switch (t)
                {
                    case ScriptType.CSharp:
                        if (EnableCSharp && csscripts.TryGetValue(nm, out var asm))
                        {
                            Type? programType = asm.GetType("Program");
                            if (programType != null)
                            {
                                MethodInfo? mainMethod = programType.GetMethod("Main", BindingFlags.Public | BindingFlags.Static);
                                if (mainMethod != null)
                                {
                                    return mainMethod.Invoke(Activator.CreateInstance(programType), null);
                                }
                                else
                                {
                                    SELogger.Error("未搜索到Main函数，函数STC:".GetInCurrLang() + nm);
                                }
                            }
                            else
                            {
                                SELogger.Error("未搜索到Program类，函数STC:".GetInCurrLang() + nm);
                            }
                        }
                        else
                        {
                            SELogger.Error("未搜索到程序集，函数STC:".GetInCurrLang() + nm);
                        }

                        break;
                    case ScriptType.JavaScript:
                        if (EnableJS && JSEngine != null)
                        {
                            JSEngine.Execute(jscripts[nm]);
                            return JSEngine.Script.Main();
                        }
                        break;
                }
            }
            else
            {
                SELogger.Error("未搜索到函数，函数STC:".GetInCurrLang() + nm);
                foreach (var p in scriptslib)
                    SELogger.Log("已加载函数:" + p.Key + " 类型:" + p.Value);
            }
            SELogger.Error("运行失败，函数STC:".GetInCurrLang() + nm);
            return null;
        }
        public ulong LoadSEBin(string pth, string nm)
        {
            byte[] b = File.ReadAllBytes(pth);
            byte[] c = new byte[b.LongLength - 8];
            ulong sc = STCCode.GetSTC(nm);
            Parallel.For(0, c.LongLength, (i) =>
            {
                c[i] = b[i + 8];
            });
            MemoryStream ms = new MemoryStream(b, false);
            ms.Seek(0, SeekOrigin.Begin);
            BinaryOperator bo = new BinaryOperator(ms);
            ulong ostc = bo.ReadUInt64();
            ms.Dispose();
            Assembly asm = Assembly.Load(c);
            if (asm != null)
            {
                csscripts.Add(sc, asm);
                scriptslib.Add(sc, ScriptType.CSharp);
                return sc;
            }
            c = null;
            b = null;
            return sc;
        }
        public static byte[] CompileCodeCS(string code, string nm = "CSharp Code", bool independentDependFile = false, string[]? libpath = null)
        {
            SyntaxTree st = CSharpSyntaxTree.ParseText(code);

            List<MetadataReference> references = new List<MetadataReference>();

            references.AddRange(DefalutReferences);

            if (independentDependFile)
            {
                if (libpath != null)
                {
                    foreach (string lp in libpath)
                    {
                        references.Add(MetadataReference.CreateFromFile(lp));
                    }
                }
            }

            var compilation = CSharpCompilation.Create(
                nm,
                new[] { st },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: GVariables.DebugMode ? OptimizationLevel.Debug : OptimizationLevel.Release, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable));
            using (var ms = new MemoryStream())
            {
                var rs = compilation.Emit(ms);
                if (!rs.Success)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var diagnostic in rs.Diagnostics)
                    {
                        sb.AppendLine(diagnostic.ToString());
                    }
                    SELogger.Error("CS脚本编译失败".GetInCurrLang() + sb.ToString());
                }
                else
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    byte[] assemblyBytes = ms.ToArray();


                    return assemblyBytes;
                }
            }


            return [];
        }
        public ulong CompileCode(string code, ScriptType t, bool independentDependFile = false, string[]? libpath = null)
        {
            ulong stc = STCCode.GetSTC(code);
            string nm = "Script_" + stc;
            string fp = (GVariables.SystemTempDir ?? ".") + "/" + nm + ".tps";
            //SELogger.Log("编译脚本到缓存文件:".GetInCurrLang() + fp);
            File.WriteAllText(fp, code);
            ulong v = CompileCodeFromFile(fp, t, independentDependFile, libpath);
            File.Delete(fp);
            return v;
        }
        public void CompileCodeFromFiles(string[] fp, ScriptType t, bool independentDependFile = false, string[]? libpath = null)
        {
            for (int i = 0; i < fp.Length; i++)
            {
                CompileCodeFromFile(fp[i], t, independentDependFile, libpath);
            }
        }
        public ulong CompileCodeFromFile(string fp, ScriptType t, bool independentDependFile = false, string[]? libpath = null)
        {
            string nm = System.IO.Path.GetFileName(fp);
            ulong sc = STCCode.GetSTC(nm);
            if (scriptslib.TryGetValue(sc, out _))
                return sc;
            switch (t)
            {
                case ScriptType.CSharp:
                    //有优化，第一次编译完后会缓存编译结果，后续相同文件不会重复编译，除非文件STC值不同
                    string bsp = System.IO.Path.GetDirectoryName(fp) ?? "./";


                    string cachepath = (GVariables.SystemTempDir ?? ".") + "/" + sc + ".sebin";
                    ulong stc = STCCode.GetSTCFromFile(fp);

                    if (File.Exists(cachepath))
                    {

                        byte[] b = File.ReadAllBytes(cachepath);
                        byte[] c = new byte[b.LongLength - 8];
                        Parallel.For(0, c.LongLength, (i) =>
                        {
                            c[i] = b[i + 8];
                        });
                        MemoryStream ms = new MemoryStream(b, false);
                        ms.Seek(0, SeekOrigin.Begin);
                        BinaryOperator bo = new BinaryOperator(ms);
                        ulong ostc = bo.ReadUInt64();
                        ms.Dispose();
                        if (ostc == stc)
                        {
                            //加载缓存的二进制文件
                            Assembly asm = Assembly.Load(c);
                            if (asm != null)
                            {
                                csscripts.Add(sc, asm);
                                scriptslib.Add(sc, ScriptType.CSharp);
                                return sc;
                            }
                        }
                        c = null;
                        b = null;
                    }
                    SyntaxTree st = CSharpSyntaxTree.ParseText(File.ReadAllText(fp));

                    List<MetadataReference> references = new List<MetadataReference>();
                    references.AddRange(DefalutReferences);


                    if (independentDependFile)
                    {
                        if (libpath != null)
                        {
                            foreach (string lp in libpath)
                            {
                                references.Add(MetadataReference.CreateFromFile(lp));
                            }
                        }
                    }

                    for (int i = 0; i < csdep.Count; i++)
                    {
                        references.Add(MetadataReference.CreateFromFile(csdep[i]));
                    }

                    var compilation = CSharpCompilation.Create(
                        nm,
                        new[] { st },
                        references: references,
                        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: GVariables.DebugMode ? OptimizationLevel.Debug : OptimizationLevel.Release, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable));
                    using (var ms = new MemoryStream())
                    {
                        var rs = compilation.Emit(ms);
                        if (!rs.Success)
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (var diagnostic in rs.Diagnostics)
                            {
                                sb.AppendLine(diagnostic.ToString());
                            }
                            SELogger.Error("CS脚本编译失败".GetInCurrLang() + sb.ToString());
                        }
                        else
                        {
                            ms.Seek(0, SeekOrigin.Begin);
                            byte[] assemblyBytes = ms.ToArray();
                            //缓存编译结果
                            using (var cms = new MemoryStream())
                            {
                                BinaryOperator bo = new BinaryOperator(cms);
                                bo.Write(stc);
                                cms.Write(assemblyBytes, 0, assemblyBytes.Length);
                                File.WriteAllBytes(cachepath, cms.ToArray());
                            }
                            Assembly assembly = Assembly.Load(assemblyBytes);
                            csscripts.Add(sc, assembly);
                            scriptslib.Add(sc, ScriptType.CSharp);
                            return sc;
                        }
                    }

                    break;
                case ScriptType.SEUI:
                    string bspu = System.IO.Path.GetDirectoryName(fp) ?? "./";


                    string cachepathu = (GVariables.SystemTempDir ?? ".") + "/" + sc + ".sebin";
                    ulong stcu = STCCode.GetSTCFromFile(fp);

                    if (File.Exists(cachepathu))
                    {

                        byte[] b = File.ReadAllBytes(cachepathu);
                        byte[] c = new byte[b.LongLength - 8];
                        Parallel.For(0, c.LongLength, (i) =>
                        {
                            c[i] = b[i + 8];
                        });
                        MemoryStream ms = new MemoryStream(b, false);
                        ms.Seek(0, SeekOrigin.Begin);
                        BinaryOperator bo = new BinaryOperator(ms);
                        ulong ostc = bo.ReadUInt64();
                        ms.Dispose();
                        if (ostc == stcu)
                        {
                            //SELogger.Log("Loading cached SEUI binary:" + cachepathu);
                            GVariables.ShareMemory.AllocateMemory(c.Length, nm);
                            GVariables.ShareMemory.GetMemoryBlock(nm).Write(c);
                            return sc;
                        }
                        c = null;
                        b = null;
                    }
                    SyntaxTree stu = CSharpSyntaxTree.ParseText(File.ReadAllText(fp));

                    List<MetadataReference> referencesu = new List<MetadataReference>();

                    //referencesu.Add(MetadataReference.CreateFromFile(typeof(SEBase).Assembly.Location));
                    referencesu.AddRange(DefalutReferences);

                    if (independentDependFile)
                    {
                        if (libpath != null)
                        {
                            foreach (string lp in libpath)
                            {
                                referencesu.Add(MetadataReference.CreateFromFile(lp));
                            }
                        }
                    }

                    for (int i = 0; i < csdep.Count; i++)
                    {
                        referencesu.Add(MetadataReference.CreateFromFile(csdep[i]));
                    }
                    var compilationu = CSharpCompilation.Create(
                        nm,
                        new[] { stu },
                        references: referencesu,
                        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: GVariables.DebugMode ? OptimizationLevel.Debug : OptimizationLevel.Release, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable));
                    using (var msu = new MemoryStream())
                    {
                        var rs = compilationu.Emit(msu);
                        if (!rs.Success)
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (var diagnostic in rs.Diagnostics)
                            {
                                sb.AppendLine(diagnostic.ToString());
                            }
                            SELogger.Error("SEUI脚本编译失败".GetInCurrLang() + sb.ToString());
                        }
                        else
                        {
                            msu.Seek(0, SeekOrigin.Begin);
                            byte[] assemblyBytes = msu.ToArray();
                            //缓存编译结果
                            using (var cms = new MemoryStream())
                            {
                                BinaryOperator bo = new BinaryOperator(cms);
                                bo.Write(stcu);
                                cms.Write(assemblyBytes, 0, assemblyBytes.Length);
                                File.WriteAllBytes(cachepathu, cms.ToArray());
                            }
                            //SELogger.Log("SEUI script compiled, allocating shared memory:" + nm);
                            GVariables.ShareMemory.AllocateMemory(msu.Length, nm);
                            GVariables.ShareMemory.GetMemoryBlock(nm).Write(assemblyBytes);
                            //MemoryStream ms = new MemoryStream(assemblyBytes);
                            //Assembly assembly = Assembly.Load(assemblyBytes);
                            //csscripts.Add(sc, assembly);
                            //scriptslib.Add(sc, ScriptType.CSharp);
                            return sc;
                        }
                    }

                    break;
                case ScriptType.JavaScript:
                    if (EnableJS && JSEngine != null)
                    {
                        //string n = Path.GetFileNameWithoutExtension(fp);

                        jscripts.Add(sc, JSEngine.CompileDocument(fp));
                        scriptslib.Add(sc, ScriptType.JavaScript);
                        return sc;
                    }
                    break;
                case ScriptType.Lua:
                    break;
                case ScriptType.Python:
                    break;
            }
            return 0;
        }

        public void AddDepending(Type type)
        {
            AddDepending(type.Name, type);
        }
        public void AddDepending(string nm, Type type)
        {
            if (EnableJS && JSEngine != null)
            {
                JSEngine.AddHostType(nm, type);
                //SELogger.Log("Added JS depending type:" + type.FullName);
            }
            if (EnableCSharp)
            {
                csdep.Add(type.Assembly.Location);
            }
        }

        public void Init(EnableScriptType enabletype = EnableScriptType.All)
        {
            SELogger.Log("Step1");
            if ((enabletype & EnableScriptType.CSharp) == EnableScriptType.CSharp)
            {
                EnableCSharp = true;
            }
            if ((enabletype & EnableScriptType.JavaScript) == EnableScriptType.JavaScript)
            {
                if (GVariables.DebugMode)
                {
                    SELogger.Log("Step21");
                    JSEngine = new V8ScriptEngine(V8ScriptEngineFlags.EnableDebugging);
                    JSEngine.DocumentSettings.AccessFlags = Microsoft.ClearScript.DocumentAccessFlags.EnableAllLoading;
                    JSEngine.DefaultAccess = ScriptAccess.Full;
                }
                else
                {
                    SELogger.Log("Step22");
                    JSEngine = new V8ScriptEngine();
                    JSEngine.DocumentSettings.AccessFlags = Microsoft.ClearScript.DocumentAccessFlags.EnableAllLoading;
                    JSEngine.DefaultAccess = ScriptAccess.Full;
                }
                EnableJS = true;
            }
            if ((enabletype & EnableScriptType.Lua) == EnableScriptType.Lua)
            {
                EnableLua = true;
            }
            if ((enabletype & EnableScriptType.Python) == EnableScriptType.Python)
            {
                EnablePython = true;
            }
            SELogger.Log("Step3");
            DefalutReferences.Add(MetadataReference.CreateFromFile(typeof(SEBase).Assembly.Location));
            string dllp = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location) ?? "./";
            Directory.GetFiles(dllp, "*.dll").ToList().ForEach((f) =>
            {
                try
                {
                    if (Path.GetFileNameWithoutExtension(f).StartsWith("System") && f.IndexOf("Native") < 0)
                    {

                        DefalutReferences.Add(MetadataReference.CreateFromFile(f));
                    }
                }
                catch
                {
                    //忽略无法加载的DLL
                }
            });
            SELogger.Log("Step4");
            dllp = System.IO.Path.GetDirectoryName(this.GetType().Assembly.Location) ?? "./";
            Directory.GetFiles(dllp, "*.dll").ToList().ForEach((f) =>
            {
                try
                {
                    var a = Assembly.LoadFile(f);
                    if (a!=null)
                    {
                        
                        DefalutReferences.Add(MetadataReference.CreateFromFile(f));

                    }
                    GC.Collect();
                }
                catch
                {
                    //忽略无法加载的DLL
                }
            });
            DefalutReferences.Add(MetadataReference.CreateFromFile(typeof(System.Dynamic.DynamicObject).Assembly.Location));
            try
            {
                var csharpAssembly = Assembly.Load("Microsoft.CSharp, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                DefalutReferences.Add(MetadataReference.CreateFromFile(csharpAssembly.Location));
            }
            catch
            {
                // 如果无法加载，尝试其他方式
                try
                {
                    var csharpAssemblyPath = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "Microsoft.CSharp.dll");
                    if (File.Exists(csharpAssemblyPath))
                    {
                        DefalutReferences.Add(MetadataReference.CreateFromFile(csharpAssemblyPath));
                    }
                }
                catch
                {
                    SELogger.Warn("警告: 无法加载 Microsoft.CSharp.dll，dynamic 功能可能受限".GetInCurrLang());
                }
            }
            //SELogger.Log("Script engine initialized. With Flag:" + enabletype);
        }

        public void Dispose()
        {
            foreach (var s in jscripts)
            {
                s.Value.Dispose();
            }
            jscripts.Clear();
            if (JSEngine != null)
            {
                JSEngine.Dispose();
                JSEngine = null;
            }

            csscripts.Clear();
            csdep.Clear();
            scriptslib.Clear();
        }
    }
}
