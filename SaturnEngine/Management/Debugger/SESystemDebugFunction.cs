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
        public static void ls(string[] args)
        {

        }
    }
}
