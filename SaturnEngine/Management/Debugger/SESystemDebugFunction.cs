using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaturnEngine.Management.Debugger
{
    public class SESystemDebugFunction
    {
        /// <summary>
        /// 用来注册系统级的调试命令的函数
        /// </summary>
        /// <param name="v"></param>
        public static void Adder(List<KeyValuePair<string, Action<string[]>>> v)
        {
                v.Add(new KeyValuePair<string, Action<string[]>>("run", run));
                v.Add(new KeyValuePair<string, Action<string[]>>("runinline", runinline));
        }
        /// <summary>
        /// 运行外部程序，参数为：run [程序路径] [参数1] [参数2] ...
        /// </summary>
        /// <param name="args"></param>
        public static void run(string[] args)
        {
            string ag = "";
            for (int i = 1; i < args.Length; i++)
            {
                ag += args[i];
                if(i != args.Length - 1)
                {
                    ag += " ";
                }
            }
            try
            {
                Process.Start(args[0], ag);
            }
            catch (Exception ex) 
            {
                SELogger.Error("Failed to start process: " + ex.Message);
            }
        }
        /// <summary>
        /// 运行内部函数，参数为：runinline [类名] [函数名] [参数1],[参数2] ...，参数会按照object[]的形式传递给函数，函数需要自己进行类型转换，参数使用逗号分隔，参数个数不限，函数必须为静态函数，且必须在当前程序集中，否则无法找到函数并运行
        /// </summary>
        /// <param name="args"></param>
        public static void runinline(string[] args)
        {
            string ag = "";
            for (int i = 2; i < args.Length; i++)
            {
                ag += args[i];
                if (i != args.Length - 1)
                {
                    ag += " ";
                }
            }
            string className = args[0];
            string methodName = args[1];
            List<object?>? para = null;

            // 解析参数，参数使用逗号分隔
            string[] parameters = ag.Split(',');
            if (parameters.Length > 0)
            {
                para = new List<object?>();
            }
            for(int i = 0; i < parameters.Length; i++)
            {
                parameters[i] = parameters[i].Trim();
                if( int.TryParse(parameters[i].Trim(),out int intResult))
                {
                    para.Add(intResult);
                }
                else if (float.TryParse(parameters[i].Trim(), out float floatResult))
                {
                    para.Add(floatResult);
                }
                else if(double.TryParse(parameters[i].Trim(), out double doubleResult))
                {
                    para.Add(doubleResult);
                }
                else if(bool.TryParse(parameters[i].Trim(), out bool boolResult))
                {
                    para.Add(boolResult);
                }
                else if (long.TryParse(parameters[i].Trim(), out long longResult))
                {
                    para.Add(longResult);
                }
                else
                {
                    para.Add(parameters[i]);
                }
            }



            Type.GetType(className)?.GetMethod(methodName)?.Invoke(null, para?.ToArray());
        }
    }
}
