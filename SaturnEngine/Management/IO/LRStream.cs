using SaturnEngine.Asset;
using SaturnEngine.Management.SEMemory;
using SaturnEngine.Security;
using Silk.NET.OpenXR;
using System.Runtime.InteropServices;
using static SaturnEngine.SEMath.Helper;

namespace SaturnEngine.Management.IO
{

    public class LRStream : Stream
    {
        LRStreamSlim lrs;
        LRStreamEnc lre;

        public LRStream(Stream bs,long offset = 0, long length = 0,bool limt = true,bool NeedEnc = false,ulong pswstcstc = 0)
        {
            lrs = new LRStreamSlim(bs, offset, length, limt);
            lre = new LRStreamEnc(lrs, NeedEnc, pswstcstc);
            
        }

        ~LRStream()
        {
            lrs.Dispose();
        }

        public bool NeedDecrypt => lre.NeedDecrypt;
        public bool IsDecrypt => lre.IsDecrypt;

        public override bool CanRead => lrs.CanRead;

        public override bool CanSeek => lrs.CanSeek;

        public override bool CanWrite => lrs.CanWrite;

        public override long Length => lrs.Length;

        public override long Position { get => lrs.Position; set => lrs.Position = value; }

        public bool Decrypt(string psw)
        {
            return lre.Decrypt(psw);
        }
        public bool Decrypt(ulong pswstc)
        {
            return lre.Decrypt(pswstc);
        }
        public void UseOrgData(bool useOrgData)
        {
            lre.UseOrgData(useOrgData);
        }
        public void CleanPassword()
        {
            lre.CleanPassword(); 
        }
        public override void Flush()
        {
            lrs.Flush();
        }
        public byte[] ReadAllInBytes()
        {
            return lre.ReadAllInBytes(); 
        }
        public Stream ReadAllInStream()
        {
            return lre.ReadAllInStream(); 
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            return lre.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return lre.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            lre.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lre.Write(buffer, offset, count);
        }
        public byte[] Read(int cou,long off = -1)
        {
            return lre.Read(cou,off);
        }
    }

}