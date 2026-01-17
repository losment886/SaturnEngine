using SaturnEngine.Management.SEMemory;
using SaturnEngine.Security;
using System.Text;

namespace SaturnEngine.Management.IO;
//LZ4改编算法实现，压缩流
public class SECompressStream : Stream
{
    public struct LzPkg
    {
        public int count;//字面长度
        public int index;//字面起始位置
        public int repeat;//重复长度，0是没有重复
        public int offset;//重复前移位置
    }
    Stream lrs;
    private long leng = 0;
    private long posi = 0;
    public override bool CanRead => false;

    public override bool CanSeek => lrs.CanSeek;

    public override bool CanWrite => lrs.CanWrite;

    public override long Length => leng;

    public override long Position { get => posi; set => posi = value; }


    public byte WindowSize = 8;

    
    public static byte[] DCmBt(byte[] d)
    {
        BinaryOperator bw = new BinaryOperator(new SEMemoryStream());
        BinaryOperator br = new BinaryOperator(new MemoryStream(d));
        //int curi = 0;
        while (br.Position < br.Length)
        {
            byte count = br.ReadUInt8();
            //int index = br.ReadInt32();
            int offset = br.ReadInt32();
            byte repeat = br.ReadUInt8();
            //bw.Write(br.ReadBytes(count));
            bw.Write(br.ReadBytes(count));
            if (repeat > 0)
            {
                long curp = bw.Position;
                bw.Seek(-offset, SeekOrigin.Current);
                byte[] repb = bw.ReadBytes(repeat);
                bw.Seek(curp, SeekOrigin.Begin);
                bw.Write(repb);
            }
        }
        LRStreamSlim l = new LRStreamSlim(bw,0,bw.Length);
        return l.ReadAllInBytes();
        //BinaryOperator bw = new BinaryOperator(new SEMemoryStream());
        //LRStreamSlim lrs = new LRStreamSlim(d,0,d.Length);
    }
    public static byte[] CmBt(byte[] d,int offset,int siz,byte WindowSize = 8)
    {
        Dictionary<ulong, int> stclist;
        //int szo = 0;
        int curindex = 0;
        List<LzPkg> list = new List<LzPkg>();
        stclist = new Dictionary<ulong,int>();
        int cci = 0;
        ulong stc = 0;
        while (true)
        {
            if (curindex + WindowSize > siz)
            {
                LzPkg lp = new LzPkg();
                lp.repeat = 0;
                lp.index = cci;
                lp.offset = 0;
                lp.count = siz - cci;
                list.Add(lp);
                break;
            }
            stc = STCCode.GetSTC(d, curindex + offset, WindowSize);
            if (stclist.TryGetValue(stc, out int repindex))
            {
                LzPkg lp = new LzPkg();
                lp.repeat = WindowSize;
                lp.index = cci;
                lp.offset = curindex - repindex;
                lp.count = curindex - cci;
                list.Add(lp);
                cci = curindex;
                curindex += WindowSize;
            }
            else
            {
                curindex++;
            }

        }
        //List<byte> res = new List<byte>();
        BinaryOperator bw = new BinaryOperator(new SEMemoryStream());
        
        LRStreamSlim l = new LRStreamSlim(bw,0,bw.Length);
        return l.ReadAllInBytes();
    }

    public SECompressStream(Stream lrs)
    {
        this.lrs = lrs;
        //stclist = new Dictionary<ulong, int>();
    }
    
    public override void Flush()
    {
        lrs.Flush();
    }
    public override int Read(byte[] buffer, int offset, int count)
    {
        return -1;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return 0;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        //lre.Write(buffer, offset, count);
    }
    public byte[] Read(int cou,long off = -1)
    {
        return [];
    }
}