using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Global;
using SaturnEngine.Management.SEMemory;
using SaturnEngine.Security;
using SaturnEngine.SEMath;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SaturnEngine.SEUI
{
    public class SEUIAssembly
    {
        //程序二进制文件，不会超过2G
        public SEMemoryStream Binary;
        public ulong Key;
        public SEUIAssembly() { }

        Assembly a;
        public void Load()
        {
            //MemoryStream ms = new MemoryStream((int)Binary.Length);
            //Binary.CopyTo(ms);
            //a = Assembly.Load(ms.ToArray());
            
        }
        public List<SEControl> RunMain()
        {
            /*
            if (a == null)
            {
                throw new Exception("Assembly not loaded. Call Load() first.");
            }
            Type program = a.GetType("Program");
            MethodInfo mi = program.GetMethod("Main", BindingFlags.Public | BindingFlags.Static);
            */
            //var res = mi.Invoke(null, null);
            var res = GVariables.ScriptEngineGlobal.RunMain(Key);
            return (List<SEControl>)res;
        }
    }
    /// <summary>
    /// SE UI layout language
    /// 
    /// </summary>
    public class SEUILL : SEBase
    {
        public string Code;
        public string BasePath;
        //public static MethodInfo miosss = GVariables.ThisGame.UIScene.GetType().GetMethod(funcname);
        public SEUILL()
        {

        }

        public void LoadFromFile(string path)
        {
            LoadFromString(System.IO.File.ReadAllText(path, Encoding.UTF8));
            BasePath = System.IO.Path.GetDirectoryName(path) ?? "./";
        }
        public void LoadFromString(string code)
        {
            Code = code;

        }
        public SEUIAssembly? CompileCode()
        {
            SEUIParser p = new SEUIParser();
            var ele = p.Parse(Code);
            //p.PrintUITree(ele);
            //using System.Net;
            string cscode = "class Program{\r\npublic static List<SEControl> Main(){\r\n";
            int currvars = 0;
            string inicode = "";
            string jscode = "";
            string jscacnm = "TMPJSScript_" + STCCode.GetSTC(Uuid.ID) + ".js";
            string jscacpath = GVariables.SystemTempDir + "/" + jscacnm;
            List<string> JsScripts = new List<string>();
            string GetVarName()
            {
                currvars++;
                return $"ini_var_{currvars}";
            }
            string GenOne(string? fname, string flstnm, string nm, string tagnm, string sz, SEMargin bor, SEAnchor bin, bool AllowHorl, SEUIElement e)
            {
                
                
                if (e.TagName.ToLower() == "script")
                {
                    switch (e.Script.Language)
                    {
                        case ScriptEngine.SEScriptEngine.ScriptType.CSharp:
                            inicode += e.Script.Code + "\r\n";
                            break;
                        case ScriptEngine.SEScriptEngine.ScriptType.JavaScript:
                            jscode += e.Script.Code + "\r\n";
                            break;
                    }
                }
                else
                {
                    string varname = nm;
                    string cs = $"{tagnm} {varname} = new {tagnm}();\r\n";
                    cs += $"{varname}.Size = new Vector2D({sz});\r\n";
                    cs += $"{varname}.Border = new SEMargin({bor.Left},{bor.Top},{bor.Right},{bor.Bottom});\r\n";
                    cs += $"{varname}.Bind = SEAnchor.{bin};\r\n";

                    
                    if (fname != null)
                    {
                        cs += $"{varname}.Parent = {fname};\r\n";
                        cs += $"{flstnm}.Add({varname});\r\n";
                    }
                    if (AllowHorl)
                    {
                        cs += $"{varname}.AllowHorizontalLayout = true;\r\n";
                    }
                    foreach (var tg in e.Attributes)
                    {
                        switch (tg.Key)
                        {
                            case "OnInit":
                                if(e.Attributes.ContainsKey("Class"))
                                {
                                    string cls = e.Attributes["Class"];
                                    cs += $"{varname}.{tg.Key} += (k) => {{ {GetTrueFuncCodeEXC(tg.Value,cls)} }};\r\n";//支持私有域（当前块的全局）,仅JS与CS脚本有效
                                }
                                else
                                {
                                    cs += $"{varname}.{tg.Key} += (k) => {{ {GetTrueFuncCodeEX(tg.Value)} }};\r\n";
                                }
                                break;
                            case "OnMouseEnter":
                            case "OnMouseLeave":
                            case "OnMouseMove":
                            case "OnMouseWheel":
                            case "OnMouseUp":
                            case "OnMouseDown":
                            case "OnClick":
                            case "OnMouseUpRight":
                            case "OnMouseDownRight":
                            case "OnClickRight":
                                if (e.Attributes.ContainsKey("Class"))
                                {
                                    string cls = e.Attributes["Class"];
                                    cs += $"{varname}.{tg.Key} += () => {{ {GetTrueFuncCodeC(tg.Value,cls)} }};\r\n";
                                }
                                else
                                {
                                    cs += $"{varname}.{tg.Key} += () => {{ {GetTrueFuncCode(tg.Value)} }};\r\n";
                                }
                               
                                break;
                            case "OnKeyDown":
                            case "OnKeyUp":
                            case "OnKeyPress":
                                //cs += $"{varname}.{tg.Key} += (k) => {{ {GetTrueFuncCodeEX(tg.Value)} }};\r\n";
                                if (e.Attributes.ContainsKey("Class"))
                                {
                                    string cls = e.Attributes["Class"];
                                    cs += $"{varname}.{tg.Key} += (k) => {{ {GetTrueFuncCodeEXC(tg.Value, cls)} }};\r\n";//支持私有域（当前块的全局）,仅JS与CS脚本有效
                                }
                                else
                                {
                                    cs += $"{varname}.{tg.Key} += (k) => {{ {GetTrueFuncCodeEX(tg.Value)} }};\r\n";
                                }
                                break;
                        }
                    }
                    return cs;
                }
                return "";
            }
            string GetTrueFuncCodeEXC(string code,string cls)
            {
                //Keys s
                code = code.Trim();
                string cs = "";
                if (code.StartsWith('&'))
                {
                    //该项表示引用外部函数，在UIScene中，用反射调用
                    string funcname = code[1..].Trim();
                    //会放在全局中
                    string assename = GetVarName();
                    inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.UIScene.GetType().GetMethod({funcname});";
                    cs = $"{assename}.Invoke(null,[k]);";
                    //Assembly a = GVariables.ThisGame.UIScene.GetType().Assembly;
                    //MethodInfo mi = GVariables.ThisGame.UIScene.GetType().GetMethod(funcname);
                    //mi.Invoke(null, []);
                }
                else if (code.StartsWith('#'))
                {
                    //该项表示引用外部函数，在Scene中，用反射调用，#后面加数字可以指定Scene
                    string funcname = code[1..].Trim();
                    string assename = GetVarName();
                    if (funcname[0] >= '0' && funcname[0] <= '9')
                    {
                        string id = Regex.Match(funcname, @"\d+").Value;
                        inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.ThisScenes[{id}].GetType().GetMethod({funcname},BindingFlags.Public | BindingFlags.Static);";
                    }
                    else
                    {
                        inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.ThisScenes[GVariables.ThisGame.CurrentSceneIndex].GetType().GetMethod({funcname},BindingFlags.Public | BindingFlags.Static);";
                    }
                    //会放在全局中


                    cs = $"{assename}.Invoke(null,[k]);";
                    //Assembly a = GVariables.ThisGame.ThisScenes[GVariables.ThisGame.CurrentSceneIndex].GetType().Assembly;
                    //MethodInfo mi = GVariables.ThisGame.UIScene.GetType().GetMethod(funcname);
                    //mi.Invoke(null, null);
                }
                else if (code.StartsWith('*'))
                {
                    //JS 函数
                    //当有<>时，中间是所在JS脚本的名字，需要提前Import
                    //没有就会默认存在在当前的JS脚本块
                    //名字为文件名包括后缀
                    code = code[1..].Trim();
                    int ist = code.IndexOf('<');
                    if (ist < 0)
                    {
                        int end = code.IndexOf('>', ist);
                        string nam = code.Substring(ist + 1, end).Trim();
                        string funcname = code.Substring(end + 1).Trim();
                        ulong cd = STCCode.GetSTC(nam);
                        cs = $"GVariables.ScriptEngineGlobal.Get({cd}).{cls}.{funcname}(k);";
                    }
                    else
                    {
                        string nam = jscacnm;
                        string funcname = code.Trim();
                        ulong cd = STCCode.GetSTC(nam);
                        cs = $"GVariables.ScriptEngineGlobal.Get({cd}).{cls}.{funcname}(k);";
                    }
                }
                else
                {
                    //否则默认C#代码块的
                    cs = $"{cls}.{code}(k);";
                }
                return cs;
            }
            string GetTrueFuncCodeEX(string code)
            {
                //Keys s
                code = code.Trim();
                string cs = "";
                if (code.StartsWith('&'))
                {
                    //该项表示引用外部函数，在UIScene中，用反射调用
                    string funcname = code[1..].Trim();
                    //会放在全局中
                    string assename = GetVarName();
                    inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.UIScene.GetType().GetMethod({funcname});";
                    cs = $"{assename}.Invoke(null,[k]);";
                    //Assembly a = GVariables.ThisGame.UIScene.GetType().Assembly;
                    //MethodInfo mi = GVariables.ThisGame.UIScene.GetType().GetMethod(funcname);
                    //mi.Invoke(null, []);
                }
                else if (code.StartsWith('#'))
                {
                    //该项表示引用外部函数，在Scene中，用反射调用，#后面加数字可以指定Scene
                    string funcname = code[1..].Trim();
                    string assename = GetVarName();
                    if (funcname[0] >= '0' && funcname[0] <= '9')
                    {
                        string id = Regex.Match(funcname, @"\d+").Value;
                        inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.ThisScenes[{id}].GetType().GetMethod({funcname},BindingFlags.Public | BindingFlags.Static);";
                    }
                    else
                    {
                        inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.ThisScenes[GVariables.ThisGame.CurrentSceneIndex].GetType().GetMethod({funcname},BindingFlags.Public | BindingFlags.Static);";
                    }
                    //会放在全局中


                    cs = $"{assename}.Invoke(null,[k]);";
                    //Assembly a = GVariables.ThisGame.ThisScenes[GVariables.ThisGame.CurrentSceneIndex].GetType().Assembly;
                    //MethodInfo mi = GVariables.ThisGame.UIScene.GetType().GetMethod(funcname);
                    //mi.Invoke(null, null);
                }
                else if (code.StartsWith('*'))
                {
                    //JS 函数
                    //当有<>时，中间是所在JS脚本的名字，需要提前Import
                    //没有就会默认存在在当前的JS脚本块
                    //名字为文件名包括后缀
                    code = code[1..].Trim();
                    int ist = code.IndexOf('<');
                    if (ist < 0)
                    {
                        int end = code.IndexOf('>', ist);
                        string nam = code.Substring(ist + 1, end).Trim();
                        string funcname = code.Substring(end + 1).Trim();
                        ulong cd = STCCode.GetSTC(nam);
                        cs = $"GVariables.ScriptEngineGlobal.Get({cd}).{funcname}(k);";
                    }
                    else
                    {
                        string nam = jscacnm;
                        string funcname = code.Trim();
                        ulong cd = STCCode.GetSTC(nam);
                        cs = $"GVariables.ScriptEngineGlobal.Get({cd}).{funcname}(k);";
                    }
                }
                else
                {
                    //否则默认C#代码块的
                    cs = $"{code}(k);";
                }
                return cs;
            }
            string GetTrueFuncCodeC(string code,string cls)
            {
                //ref inicode;
                code = code.Trim();
                string cs = "";
                if (code.StartsWith('&'))
                {
                    //该项表示引用外部函数，在UIScene中，用反射调用
                    string funcname = code[1..].Trim();
                    //会放在全局中
                    string assename = GetVarName();
                    inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.UIScene.GetType().GetMethod({funcname});";
                    cs = $"{assename}.Invoke(null,null);";
                    //Assembly a = GVariables.ThisGame.UIScene.GetType().Assembly;
                    //MethodInfo mi = GVariables.ThisGame.UIScene.GetType().GetMethod(funcname);
                    //mi.Invoke(null, null);
                }
                else if (code.StartsWith('#'))
                {
                    //该项表示引用外部函数，在Scene中，用反射调用，#后面加数字可以指定Scene
                    string funcname = code[1..].Trim();
                    string assename = GetVarName();
                    if (funcname[0] >= '0' && funcname[0] <= '9')
                    {
                        string id = Regex.Match(funcname, @"\d+").Value;
                        inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.ThisScenes[{id}].GetType().GetMethod({funcname},BindingFlags.Public | BindingFlags.Static);";
                    }
                    else
                    {
                        inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.ThisScenes[GVariables.ThisGame.CurrentSceneIndex].GetType().GetMethod({funcname},BindingFlags.Public | BindingFlags.Static);";
                    }
                    //会放在全局中


                    cs = $"{assename}.Invoke(null,null);";
                    //Assembly a = GVariables.ThisGame.ThisScenes[GVariables.ThisGame.CurrentSceneIndex].GetType().Assembly;
                    //MethodInfo mi = GVariables.ThisGame.UIScene.GetType().GetMethod(funcname, BindingFlags.Public | BindingFlags.Static);
                    //mi.Invoke(null, null);
                }
                else if (code.StartsWith('*'))
                {
                    //JS 函数
                    //当有<>时，中间是所在JS脚本的名字，需要提前Import
                    //没有就会默认存在在当前的JS脚本块
                    //名字为文件名包括后缀
                    code = code[1..].Trim();
                    int ist = code.IndexOf('<');
                    if (ist >= 0)
                    {
                        int end = code.IndexOf('>', ist);
                        string nam = code.Substring(ist + 1, end).Trim();
                        string funcname = code.Substring(end + 1).Trim();
                        ulong cd = STCCode.GetSTC(nam);
                        cs = $"GVariables.ScriptEngineGlobal.Get({cd}).{cls}.{funcname}();";
                    }
                    else
                    {
                        string nam = jscacnm;
                        string funcname = code.Trim();
                        ulong cd = STCCode.GetSTC(nam);
                        cs = $"GVariables.ScriptEngineGlobal.Get({cd}).{cls}.{funcname}();";
                    }
                }
                else
                {
                    //否则默认C#代码块的
                    cs = $"{cls}.{code}();";
                }
                return cs;
            }
            string GetTrueFuncCode(string code)
            {
                //ref inicode;
                code = code.Trim();
                string cs = "";
                if (code.StartsWith('&'))
                {
                    //该项表示引用外部函数，在UIScene中，用反射调用
                    string funcname = code[1..].Trim();
                    //会放在全局中
                    string assename = GetVarName();
                    inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.UIScene.GetType().GetMethod({funcname});";
                    cs = $"{assename}.Invoke(null,null);";
                    //Assembly a = GVariables.ThisGame.UIScene.GetType().Assembly;
                    //MethodInfo mi = GVariables.ThisGame.UIScene.GetType().GetMethod(funcname);
                    //mi.Invoke(null, null);
                }
                else if (code.StartsWith('#'))
                {
                    //该项表示引用外部函数，在Scene中，用反射调用，#后面加数字可以指定Scene
                    string funcname = code[1..].Trim();
                    string assename = GetVarName();
                    if (funcname[0] >= '0' && funcname[0] <= '9')
                    {
                        string id = Regex.Match(funcname, @"\d+").Value;
                        inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.ThisScenes[{id}].GetType().GetMethod({funcname},BindingFlags.Public | BindingFlags.Static);";
                    }
                    else
                    {
                        inicode += $"public static MethodInfo {assename} = GVariables.ThisGame.ThisScenes[GVariables.ThisGame.CurrentSceneIndex].GetType().GetMethod({funcname},BindingFlags.Public | BindingFlags.Static);";
                    }
                    //会放在全局中


                    cs = $"{assename}.Invoke(null,null);";
                    //Assembly a = GVariables.ThisGame.ThisScenes[GVariables.ThisGame.CurrentSceneIndex].GetType().Assembly;
                    //MethodInfo mi = GVariables.ThisGame.UIScene.GetType().GetMethod(funcname, BindingFlags.Public | BindingFlags.Static);
                    //mi.Invoke(null, null);
                }
                else if (code.StartsWith('*'))
                {
                    //JS 函数
                    //当有<>时，中间是所在JS脚本的名字，需要提前Import
                    //没有就会默认存在在当前的JS脚本块
                    //名字为文件名包括后缀
                    code = code[1..].Trim();
                    int ist = code.IndexOf('<');
                    if(ist >= 0)
                    {
                        int end = code.IndexOf('>', ist);
                        string nam = code.Substring(ist + 1, end).Trim();
                        string funcname = code.Substring(end + 1).Trim();
                        ulong cd = STCCode.GetSTC(nam);
                        cs = $"GVariables.ScriptEngineGlobal.Get({cd}).{funcname}();";
                    }
                    else
                    {
                        string nam = jscacnm;
                        string funcname = code.Trim();
                        ulong cd = STCCode.GetSTC(nam);
                        cs = $"GVariables.ScriptEngineGlobal.Get({cd}).{funcname}();";
                    }
                }
                else
                {
                    //否则默认C#代码块的
                    cs = $"{code}();";
                }
                return cs;
            }
            string GenC(string fname, List<SEUIElement> chils)
            {
                string lst = GetVarName();
                string cs = $"List<SEControl> {lst} = new List<SEControl>();\r\n";
                foreach (var chil in chils)
                {
                    //string tagnm = "SEControl";
                    //bool AllowHorl = true;
                    //tip: sz => x,y
                    string currnm = GetVarName();
                    cs += GenOne(fname, lst, currnm, chil.TagName, (chil.Attributes.TryGetValue("Size", out _) ? (chil.Attributes["Size"]) : "100,100"), chil.Margin, chil.Anchor, (chil.Attributes.TryGetValue("AllowHorizontalLayout", out _) ? (chil.Attributes["AllowHorizontalLayout"].ToLower() == "true") : false),chil);
                    if (chil.Children.Count > 0)
                    {
                        cs += GenC(currnm, chil.Children);
                    }
                }
                return cs;
            }
            string mainlst = GetVarName();
            cscode += $"List<SEControl> {mainlst} = new List<SEControl>();\r\n";
            List<string> lbs = new List<string>();
            List<string> nmspcs = new List<string>();
            
            nmspcs.AddRange(["System", "System.IO", "SaturnEngine.Asset", "SaturnEngine.Base", "SaturnEngine.Global", "SaturnEngine.Management", "SaturnEngine.Management.Event", "SaturnEngine.Management.IO", "SaturnEngine.Management.SEMemory", "SaturnEngine.Performance", "SaturnEngine.Physics", "SaturnEngine.Platform", "SaturnEngine.SEAudio", "SaturnEngine.SEComponents", "SaturnEngine.Security", "SaturnEngine.SEErrors", "SaturnEngine.SEGraphics", "SaturnEngine.SEInput", "SaturnEngine.SEMath", "SaturnEngine.SEUI", "SaturnEngine.SEUIControls", "System.Threading", "System.Net", "SaturnEngine.ScriptEngine", "System.Collections.Generic", "System.Collections", "System.Reflection", "System.Globalization", "SaturnEngine.SEFont", "SixLabors.Fonts"]);
            foreach (var child in ele.Children)
            {
                if(child.TagName.ToLower() == "import")
                {
                    if (child.Attributes.TryGetValue("Type",out _)&&child.Attributes["Type"] == "JavaScript")
                    {
                        //JS脚本
                        JsScripts.Add(child.Attributes["Path"].Trim());
                    }
                    else
                    {
                        lbs.Add(child.Attributes["Path"].Trim());
                        nmspcs.Add(child.Attributes["Namespace"].Trim());
                    }
                    //import标签大括号内没有内容
                }
                else
                {
                    string currnm = GetVarName();
                    cscode += GenOne(null, "", currnm, child.TagName, (child.Attributes.TryGetValue("Size", out _) ? (child.Attributes["Size"]) : "100,100"), child.Margin, child.Anchor, (child.Attributes.TryGetValue("AllowHorizontalLayout", out _) ? (child.Attributes["AllowHorizontalLayout"].ToLower() == "true") : false), child);
                    if (child.Children.Count > 0)
                    {
                        cscode += GenC(currnm, child.Children);
                    }
                    cscode += $"{mainlst}.Add({currnm});\r\n";
                }
            }
            cscode += $"return {mainlst};\r\n";
            string fncode = "";
            foreach (var nspc in nmspcs)
            {
                fncode += $"using {nspc};\r\n";
            }
            cscode = fncode + cscode + "\r\n}\r\n" + inicode;

            cscode += "\r\n}\r\n";
            File.WriteAllText(GVariables.SystemTempDir+"/PPC.CS", cscode);
            ulong nm = GVariables.ScriptEngineGlobal.CompileCode(cscode, ScriptEngine.SEScriptEngine.ScriptType.CSharp, true, lbs.ToArray());
            SEUIAssembly sa = new SEUIAssembly();
            //sa.Binary = GVariables.ShareMemory.GetMemoryBlock(nm);
            sa.Key = nm;
            if (!string.IsNullOrWhiteSpace(jscode))
            {
                File.WriteAllText(jscacpath, jscode);
                ulong id = GVariables.ScriptEngineGlobal.CompileCodeFromFile(jscacpath, ScriptEngine.SEScriptEngine.ScriptType.JavaScript);
                File.Delete(jscacpath);
                if(id != STCCode.GetSTC(jscacnm))
                {
                    throw new Exception();
                }
            }
            GVariables.ScriptEngineGlobal.CompileCodeFromFiles(JsScripts.ToArray(), ScriptEngine.SEScriptEngine.ScriptType.JavaScript);

            return sa;
        }
    }
}
