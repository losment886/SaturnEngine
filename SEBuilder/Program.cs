using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEBuilder;

/// <summary>
/// 单个脚本的范围
/// </summary>
public static class SEBuilder
{
    public static string ScriptPath { get; set; } = "";
    public static string BasePath { get; set; } = "";
    public static Dictionary<string, string> Macros { get; set; } = new Dictionary<string, string>();
}
/// <summary>
/// 全局范围
/// </summary>
public static class SEBuilderGlobal
{
    public static Dictionary<string, string> Macros_Global { get; set; } = new Dictionary<string, string>();
}

public class Program
{
    public static Version curr_version = new Version(1, 1, 0, 7);

    public static string[] StrSplit(string v)
    {
        bool isInQuotes = false;
        List<string> result = new List<string>();
        for (int i = 0; i < v.Length; i++)
        {
            if (v[i] == '"')
            {
                if( i > 0 && v[i - 1] == '\\') // Check for escaped quote
                {
                    continue; // Skip this quote as it's escaped
                }
                isInQuotes = !isInQuotes;
            }
            else if (v[i] == ' ' && !isInQuotes)
            {
                result.Add(v.Substring(0, i));
                v = v.Substring(i + 1);
                i = -1;
            }
        }
        result.Add(v);
        return result.ToArray();
    }
    public static byte[] CompileCodeCS(string code, string nm = "CSharp Code", bool independentDependFile = false, string[]? libpath = null)
    {
        SyntaxTree st = CSharpSyntaxTree.ParseText(code);

        List<MetadataReference> references = new List<MetadataReference>();
        string dllp = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location) ?? "./";
        Directory.GetFiles(dllp, "*.dll").ToList().ForEach((f) =>
        {
            try
            {
                if (Path.GetFileNameWithoutExtension(f).StartsWith("System") && f.IndexOf("Native") < 0)
                {

                    references.Add(MetadataReference.CreateFromFile(f));
                }
            }
            catch
            {
                //忽略无法加载的DLL
            }
        });
        references.Add(MetadataReference.CreateFromFile(typeof(SEBuilder).Assembly.Location));
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
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable));
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
                Console.WriteLine("CS脚本编译失败" + sb.ToString());
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

    
    public static void ExecuteScript(string code)
    {
        byte[] assemblyBytes = CompileCodeCS(code);
        if (assemblyBytes.Length > 0)
        {
            var assembly = System.Reflection.Assembly.Load(assemblyBytes);
            var entryType = assembly.GetType("Program");
            if (entryType != null)
            {
                var entryMethod = entryType.GetMethod("Main");
                if (entryMethod != null)
                {
                    entryMethod.Invoke(null, null);
                }
                else
                {
                    Console.WriteLine("Entry.Main method not found.");
                }
            }
            else
            {
                Console.WriteLine("SEBuilderScript.Entry type not found.");
            }
        }
    }

    public static void AddScript()
    {
        
    }
    
    public static void Main(string[] args)
    {

        Console.WriteLine("SEBuilder " + curr_version);

        if(args.Length == 0)
        {
            Console.WriteLine("Usage: SEBuilder <sebuilder_script> (option) ");
            return;
        }
        else
        {
            string wholearg = "";
            for(int i = 0; i < args.Length; i++) {
                wholearg += args[i] + " ";
            }
            wholearg = wholearg.Trim();
            Console.WriteLine(wholearg);
            string[] splitArgs = StrSplit(wholearg);
            foreach (string arg in splitArgs)
            {
                Console.WriteLine(arg);
            }
            string scriptPath = splitArgs[0];
            Console.WriteLine("Script Path: " + scriptPath);
            scriptPath = Path.GetFullPath(scriptPath);
            Console.WriteLine("Full Script Path: " + scriptPath);
            if (File.Exists(scriptPath))
            {
                string code = File.ReadAllText(scriptPath);
                if(code.StartsWith("[SEBuilder Script]"))
                {
                    // Process the script
                    Console.WriteLine("Processing script...");

                    code = code.Substring("[SEBuilder Script]".Length).Trim(); // Remove the marker from the code

                    ExecuteScript(code);

                }
                else
                {
                    Console.WriteLine("Invalid script file.");
                }
            }
            else
            {
                Console.WriteLine("Script file not found.");
            }
        }

    }
}
