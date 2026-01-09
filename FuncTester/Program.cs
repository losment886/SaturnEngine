using SaturnEngine.Asset;
using SaturnEngine.Security;
using System.Text;
using static SaturnEngine.SEMath.Helper;

namespace FuncTester
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            LRL l = new LRL();
            l.CreateNewFile("./test.lrl",LRL.LRLFlag.Allow_Encrypt);
            l.AddBox(new MemoryStream(Encoding.UTF8.GetBytes("Helloworld!!!!!!!!")), bf: LRL.LRBKFlag.Encrypt, extdata: [new KeyValuePair<LRL.LRBKExtDataType, byte[]>(LRL.LRBKExtDataType.Ext_Encrypt, new DataLayout("passw".ToSTC()).GetBytes())]);
            l.Save();
        }
    }
}
