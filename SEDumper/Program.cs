using System.Diagnostics;

namespace SEDumper
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SEDumper");
            string ag = "";
            for (int i = 0; i < args.Length; i++) 
            {
                ag += args[i];
                if(i != args.Length - 1) 
                {
                    ag += " ";
                }
            }
            Console.WriteLine(ag);
            if(!string.IsNullOrEmpty(ag))
            {
                //%NM%|%PID%|%PATH%
                string[] v = ag.Split('|');
                string nm = v[0];
                int pid = int.Parse(v[1]);
                string path = v[2];
                var p = Process.GetProcessById(pid);
                DumperC dc = new DumperC(p, path);
                dc.NM = nm;
                Console.WriteLine("开始监视");
                dc.StartMonitor();
            }
        }
    }
    public class SEDumperFunction
    {
        public static void StartDumper()
        {
            var p = Process.GetCurrentProcess();
            Process.Start(".\\SEDumper.exe",$"{p.ProcessName}|{p.Id}|.\\");
        }
    }
}
