using SaturnEngine.Asset;
using SaturnEngine.SEMath;
using System.Text;
using System.Text.RegularExpressions;

namespace SaturnEngine.SEUI
{
    /// <summary>
    /// SEUI元素类，表示UI描述脚本中的一个标签
    /// </summary>
    public class SEUIElement
    {
        public string TagName { get; set; }
        public SEAnchor Anchor { get; set; }
        public SEMargin Margin { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
        public ScriptBlock Script { get; set; }
        public List<SEUIElement> Children { get; set; }
        public SEUIElement Parent { get; set; }

        public SEUIElement()
        {
            Attributes = new Dictionary<string, string>();
            Children = new List<SEUIElement>();
            Margin = new SEMargin(0, 0, 0, 0); // LEFT, TOP, RIGHT, BOTTOM
        }

        public override string ToString()
        {
            return $"{TagName} (Anchor: {Anchor}, Children: {Children.Count})";
        }
    }

    /// <summary>
    /// 脚本块类，表示UI元素中的脚本代码
    /// </summary>
    public class ScriptBlock
    {
        public SaturnEngine.ScriptEngine.SEScriptEngine.ScriptType Language { get; set; }
        public string Code { get; set; }

        public ScriptBlock(SaturnEngine.ScriptEngine.SEScriptEngine.ScriptType language, string code)
        {
            Language = language;
            Code = code;
        }

        public override string ToString()
        {
            return $"{Language} Script: {Code.Substring(0, Math.Min(Code.Length, 50))}...";
        }
    }

    /// <summary>
    /// SEUI脚本解析器
    /// </summary>
    public class SEUIParser
    {
        private string _script;
        private int _position;
        private SEUIElement _root;

        /// <summary>
        /// 解析SEUI脚本
        /// </summary>
        /// <param name="script">脚本内容</param>
        /// <returns>解析后的UI元素树</returns>
        public SEUIElement Parse(string script)
        {
            // 移除注释
            string cleanedScript = RemoveComments(script);
            int pos = 0;
            SEUIElement root = new SEUIElement { TagName = "Root" };

            ParseElement(root, cleanedScript, ref pos);

            return root;
        }
        /// <summary>
        /// 移除注释
        /// </summary>
        private string RemoveComments(string script)
        {
            // 移除单行注释
            string pattern = @"//.*";
            return Regex.Replace(script, pattern, "");
        }

        /// <summary>
        /// 移除注释和多余空白
        /// </summary>
        private string RemoveCommentsAndTrim(string script)
        {
            // 移除单行注释
            string pattern = @"//.*";
            string result = Regex.Replace(script, pattern, "");

            // 移除多余空白和换行
            result = Regex.Replace(result, @"\s+", " ");
            result = Regex.Replace(result, @"\s*{\s*", "{");
            result = Regex.Replace(result, @"\s*}\s*", "}");
            result = Regex.Replace(result, @"\s*;\s*", ";");

            return result.Trim();
        }

        /// <summary>
        /// 解析UI元素
        /// </summary>
        private void ParseElement(SEUIElement parent, string script, ref int pos)
        {
            while (pos < script.Length)
            {
                SkipWhitespace(script, ref pos);
                if (pos >= script.Length) break;

                // 检查是否结束
                if (script[pos] == '}')
                {
                    pos++;
                    return;
                }

                // 解析标签名称
                string tagName = ParseTagName(script, ref pos);
                if (string.IsNullOrEmpty(tagName)) break;

                SEUIElement element = new SEUIElement
                {
                    TagName = tagName,
                    Parent = parent
                };

                // 解析边界绑定 <>
                SkipWhitespace(script, ref pos);
                if (pos < script.Length && script[pos] == '<')
                {
                    element.Anchor = ParseAnchor(script, ref pos);
                }

                // 解析边界间距 []
                SkipWhitespace(script, ref pos);
                if (pos < script.Length && script[pos] == '[')
                {
                    element.Margin = ParseMargin(script, ref pos);
                }

                // 解析自定义属性 ()
                SkipWhitespace(script, ref pos);
                if (pos < script.Length && script[pos] == '(')
                {
                    element.Attributes = ParseAttributes(script, ref pos);
                }

                // 解析脚本 @
                SkipWhitespace(script, ref pos);
                if (pos < script.Length && script[pos] == '@')
                {
                    element.Script = ParseScript(script, ref pos);
                }

                // 解析子元素
                SkipWhitespace(script, ref pos);
                if (pos < script.Length && script[pos] == '{')
                {
                    pos++; // 跳过 {
                    ParseElement(element, script, ref pos); // 递归解析子元素
                }
                else if (pos < script.Length && script[pos] == ';')
                {
                    pos++; // 跳过 ;
                }

                parent.Children.Add(element);
            }
        }

        /// <summary>
        /// 解析标签名称
        /// </summary>
        private string ParseTagName(string script, ref int pos)
        {
            StringBuilder sb = new StringBuilder();
            while (pos < script.Length && !IsSpecialCharacter(script[pos]))
            {
                sb.Append(script[pos]);
                pos++;
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// 解析边界绑定
        /// </summary>
        private SEAnchor ParseAnchor(string script, ref int pos)
        {
            pos++; // 跳过 <
            StringBuilder sb = new StringBuilder();
            while (pos < script.Length && script[pos] != '>')
            {
                sb.Append(script[pos]);
                pos++;
            }
            pos++; // 跳过 >
            string v = sb.ToString().Trim();
            switch (v)
            {
                default:
                case "LT":
                case "LeftTop":
                    return SEAnchor.LeftTop;
                case "RT":
                case "RightTop":
                    return SEAnchor.RightTop;
                case "LB":
                case "LeftBottom":
                    return SEAnchor.LeftBottom;
                case "RB":
                case "RightBottom":
                    return SEAnchor.RightBottom;

            }

        }

        /// <summary>
        /// 解析边界间距
        /// </summary>
        private SEMargin ParseMargin(string script, ref int pos)
        {
            pos++; // 跳过 [
            StringBuilder sb = new StringBuilder();
            while (pos < script.Length && script[pos] != ']')
            {
                sb.Append(script[pos]);
                pos++;
            }
            pos++; // 跳过 ]

            string marginStr = sb.ToString().Trim();
            string[] parts = marginStr.Split(',');

            int[] margin = new int[4] { 0, 0, 0, 0 };
            for (int i = 0; i < parts.Length && i < 4; i++)
            {
                if (int.TryParse(parts[i].Trim(), out int value))
                {
                    margin[i] = value;
                }
            }

            return new SEMargin(margin[0], margin[1], margin[2], margin[3]);
        }

        /// <summary>
        /// 解析自定义属性
        /// </summary>
        private Dictionary<string, string> ParseAttributes(string script, ref int pos)
        {
            pos++; // 跳过 (
            Dictionary<string, string> attributes = new Dictionary<string, string>();

            StringBuilder sb = new StringBuilder();
            int parenthesisCount = 1;
            bool inQuotes = false;
            char quoteChar = '\0';

            while (pos < script.Length && parenthesisCount > 0)
            {
                char currentChar = script[pos];

                // 处理引号
                if ((currentChar == '"' || currentChar == '\'') && !inQuotes)
                {
                    inQuotes = true;
                    quoteChar = currentChar;
                }
                else if (currentChar == quoteChar && inQuotes)
                {
                    inQuotes = false;
                }

                // 处理括号嵌套
                if (currentChar == '(' && !inQuotes)
                    parenthesisCount++;
                else if (currentChar == ')' && !inQuotes)
                    parenthesisCount--;

                if (parenthesisCount > 0)
                {
                    sb.Append(currentChar);
                }

                pos++;
            }

            string attributesStr = sb.ToString().Trim();

            // 使用改进的方法解析属性
            attributes = ParseAttributesString(attributesStr);

            return attributes;
        }
        /// <summary>
        /// 解析属性字符串，正确处理值中的逗号
        /// </summary>
        private Dictionary<string, string> ParseAttributesString(string attributesStr)
        {
            Dictionary<string, string> attributes = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(attributesStr))
                return attributes;

            int pos = 0;
            while (pos < attributesStr.Length)
            {
                // 跳过空白
                while (pos < attributesStr.Length && char.IsWhiteSpace(attributesStr[pos]))
                    pos++;

                if (pos >= attributesStr.Length) break;

                // 解析键
                string key = ParseAttributeKey(attributesStr, ref pos);
                if (string.IsNullOrEmpty(key)) break;

                // 跳过等号
                while (pos < attributesStr.Length && attributesStr[pos] != '=')
                    pos++;
                if (pos < attributesStr.Length) pos++; // 跳过 =

                // 解析值
                string value = ParseAttributeValue(attributesStr, ref pos);

                if (!string.IsNullOrEmpty(key))
                {
                    attributes[key] = value;
                }

                // 跳过逗号
                while (pos < attributesStr.Length && attributesStr[pos] != ',')
                    pos++;
                if (pos < attributesStr.Length) pos++; // 跳过 ,
            }

            return attributes;
        }
        /// <summary>
        /// 解析属性键
        /// </summary>
        private string ParseAttributeKey(string str, ref int pos)
        {
            StringBuilder sb = new StringBuilder();
            while (pos < str.Length && !char.IsWhiteSpace(str[pos]) && str[pos] != '=')
            {
                sb.Append(str[pos]);
                pos++;
            }
            return sb.ToString().Trim();
        }
        /// <summary>
        /// 解析属性值，正确处理引号和逗号
        /// </summary>
        private string ParseAttributeValue(string str, ref int pos)
        {
            // 跳过空白
            while (pos < str.Length && char.IsWhiteSpace(str[pos]))
                pos++;

            if (pos >= str.Length) return string.Empty;

            StringBuilder sb = new StringBuilder();

            if (str[pos] == '"' || str[pos] == '\'')
            {
                // 引号包围的值
                char quoteChar = str[pos];
                pos++; // 跳过开始引号

                bool escaped = false;
                while (pos < str.Length)
                {
                    if (str[pos] == '\\' && !escaped)
                    {
                        escaped = true;
                        pos++;
                        continue;
                    }

                    if (str[pos] == quoteChar && !escaped)
                    {
                        pos++; // 跳过结束引号
                        break;
                    }

                    if (escaped)
                    {
                        // 处理转义字符
                        sb.Append(ProcessEscapeChar(str[pos]));
                        escaped = false;
                    }
                    else
                    {
                        sb.Append(str[pos]);
                    }

                    pos++;
                }
            }
            else
            {
                // 非引号值（遇到逗号或结尾结束）
                while (pos < str.Length && str[pos] != ',')
                {
                    sb.Append(str[pos]);
                    pos++;
                }
            }

            return sb.ToString().Trim();
        }
        /// <summary>
        /// 处理转义字符
        /// </summary>
        private char ProcessEscapeChar(char c)
        {
            return c switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '\\' => '\\',
                '"' => '"',
                '\'' => '\'',
                _ => c
            };
        }
        /// <summary>
        /// 解析脚本 - 修复版本，正确读取大括号内的代码
        /// </summary>
        private ScriptBlock ParseScript(string script, ref int pos)
        {
            pos++; // 跳过 @

            // 解析脚本语言
            if (pos >= script.Length) return null;

            char languageChar = script[pos];
            var language = GetLanguageFromChar(languageChar);
            pos++; // 跳过语言字符

            SkipWhitespace(script, ref pos);

            // 解析脚本代码块
            if (pos < script.Length && script[pos] == '{')
            {
                pos++; // 跳过 {
                string code = ExtractBlockContent(script, ref pos);
                return new ScriptBlock(language, code);
            }

            return null;
        }
        /// <summary>
        /// 跳过空白字符
        /// </summary>
        private void SkipWhitespace(string script, ref int pos)
        {
            while (pos < script.Length && char.IsWhiteSpace(script[pos]))
                pos++;
        }

        /// <summary>
        /// 提取块内容 - 专门用于脚本代码块
        /// </summary>
        private string ExtractBlockContent(string script, ref int pos)
        {
            StringBuilder sb = new StringBuilder();
            int braceCount = 1; // 已经从第一个 { 开始

            while (pos < script.Length && braceCount > 0)
            {
                char currentChar = script[pos];

                if (currentChar == '{')
                    braceCount++;
                else if (currentChar == '}')
                    braceCount--;

                if (braceCount > 0)
                {
                    sb.Append(currentChar);
                }

                pos++;
            }

            return sb.ToString().Trim();
        }


        /// <summary>
        /// 解析块内容
        /// </summary>
        private string ParseBlockContent(string script, ref int pos, char openChar, char closeChar)
        {
            StringBuilder sb = new StringBuilder();
            int braceCount = 1;

            while (pos < script.Length && braceCount > 0)
            {
                if (script[pos] == openChar)
                    braceCount++;
                else if (script[pos] == closeChar)
                    braceCount--;

                if (braceCount > 0)
                {
                    sb.Append(script[pos]);
                    pos++;
                }
            }

            if (braceCount == 0)
            {
                pos++; // 跳过结束符号
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// 根据字符获取脚本语言
        /// </summary>
        private SaturnEngine.ScriptEngine.SEScriptEngine.ScriptType GetLanguageFromChar(char c)
        {
            return c switch
            {
                'J' => SaturnEngine.ScriptEngine.SEScriptEngine.ScriptType.JavaScript,
                'C' => SaturnEngine.ScriptEngine.SEScriptEngine.ScriptType.CSharp,
                'L' => SaturnEngine.ScriptEngine.SEScriptEngine.ScriptType.Lua,
                'P' => SaturnEngine.ScriptEngine.SEScriptEngine.ScriptType.Python,
                _ => SaturnEngine.ScriptEngine.SEScriptEngine.ScriptType.Unknown
            };
        }

        /// <summary>
        /// 检查是否为特殊字符
        /// </summary>
        private bool IsSpecialCharacter(char c)
        {
            return c == '<' || c == '[' || c == '(' || c == '@' || c == '{' || c == '}' || c == ';' || char.IsWhiteSpace(c);
        }

        /// <summary>
        /// 打印UI树结构（用于调试）
        /// </summary>
        public void PrintUITree(SEUIElement element, int indent = 0)
        {
            string indentStr = new string(' ', indent * 2);
            Console.WriteLine($"{indentStr}{element}");

            if (element.Script != null)
            {
                Console.WriteLine($"{indentStr}  Script: {element.Script}");
            }

            if (element.Attributes.Count > 0)
            {
                Console.WriteLine($"{indentStr}  Attributes:");
                foreach (var attr in element.Attributes)
                {
                    Console.WriteLine($"{indentStr}    {attr.Key} = {attr.Value}");
                }
            }

            foreach (var child in element.Children)
            {
                PrintUITree(child, indent + 1);
            }
        }
    }
}
