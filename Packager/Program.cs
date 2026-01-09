using SaturnEngine.Asset;
using SaturnEngine.Security;
using System.Diagnostics;
using static SaturnEngine.SEMath.Helper;

namespace Packager
{
    internal class Program
    {
        static LRL l = null;
        static void Main(string[] args)
        {
            Console.WriteLine("Packaging Files!!!");
            string arg = "";
            for (int i = 0; i < args.Length; i++)
            {
                arg += args[i];
            }
            if (args.Length < 1)
            {
                Console.WriteLine("没有指令输入,请手动输入指令");
                arg = Console.ReadLine();
            }
            //flod -o savefp
            string[] agc = arg.Split(new string[] { "-o", "-p" }, StringSplitOptions.RemoveEmptyEntries);
            string ldp = agc[0].Trim();
            string sdp = agc[1].Trim();
            string ps = "";
            bool lc = false;
            l = new LRL();
            l.CreateNewFile(sdp);
            l.FLG |= LRL.LRLFlag.Allow_StreamLoad;
            if (agc.Length > 2)
            {
                ps = agc[2].Trim();
                lc = true;
                pas = ps;
                passtc = STCCode.GetSTC(ps);
                loc = true;
                l.FLG |= LRL.LRLFlag.Allow_Encrypt;
            }
            
            
            bac = ldp;
            //if(!ser.NewFile)
            //string[] fs = Directory.GetFiles(ldp);
            //在文件夹内会保留父系
            //C:\RES\
            //C:\RES\AAAC\BP.PNG
            //=>
            //  ./AAA/BP.PNG
            Console.WriteLine("创造前提条件成功，循环添加文件中");
            PPC(ldp);
            
            l.Save();
            l.Close();
            Console.WriteLine("添加完成！！");
        }
        static bool loc = false;
        static string pas = "";
        static ulong passtc = 0;
        static string bac = "";
        static void PPC(string bp)
        {
            Console.WriteLine("当前循环文件夹:" + bp);
            string[] f = Directory.GetFiles(bp);
            string[] fd = Directory.GetDirectories(bp);
            for (int i = 0; i < f.Length; i++)
            {
                string ppc = "." + f[i].Replace(bac, "").Replace("\\", "/").Trim();
                Console.WriteLine("已添加:" + ppc);
                if (loc)
                {
                    l.AddBox(File.OpenRead(f[i]), nm: ppc, bf: LRL.LRBKFlag.Encrypt, extdata: new KeyValuePair<LRL.LRBKExtDataType, byte[]>[1] { new KeyValuePair<LRL.LRBKExtDataType, byte[]>(LRL.LRBKExtDataType.Ext_Encrypt, new DataLayout(passtc).GetBytes()) }, leaveclose: true);
                }

                else
                    l.AddBox(File.OpenRead(f[i]), nm: ppc, leaveclose: true);
            }
            for (int i = 0; i < fd.Length; i++)
            {
                PPC(fd[i]);
            }
        }
    }
}
