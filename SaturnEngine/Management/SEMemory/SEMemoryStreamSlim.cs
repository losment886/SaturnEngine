using SaturnEngine.Management.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaturnEngine.Management.SEMemory
{
    public  class SEMemoryStreamSlim : Stream
    {
        //MemoryStream ms;
        Stream bs;

        public override bool CanRead => bs.CanRead;

        public override bool CanSeek => bs.CanSeek;

        public override bool CanWrite => bs.CanWrite;

        public override long Length => bs.Length;

        public override long Position { get => bs.Position; set => bs.Position = value; }
        public bool CacheMode {  get; private set; }
        string svp = "";
        public override void Close()
        {
            base.Close();
            bs.Close();
            if (File.Exists(svp))
                File.Delete(svp);
        }
        ~SEMemoryStreamSlim()
        {
            Close();
        }
        public byte[] ToArray()
        {
            LRStreamSlim l = new LRStreamSlim(bs, 0, 0, false);
            return l.ReadAllInBytes();
        }
        public SEMemoryStreamSlim(long leng)
        {
            MemoryStream ms = new MemoryStream(new byte[leng]);
            bs = ms;
        }
        public void Cache(bool enable)
        {
            if(enable)
            {
                if (!CacheMode)
                {
                    //内存转储存
                    svp = Path.GetTempFileName();
                    Stream s = new FileStream(svp, FileMode.OpenOrCreate);
                    bs.Seek(0, SeekOrigin.Begin);
                    bs.CopyTo(s);
                    bs.Close();
                    bs = s;
                    bs.Seek(0,SeekOrigin.Begin);
                    bs.Flush();
                }
            }
            else
            {
                if (CacheMode)
                {
                    //储存转内存
                    MemoryStream s = new MemoryStream(new byte[bs.Length]);
                    bs.Seek(0, SeekOrigin.Begin);
                    bs.CopyTo(s);
                    bs.Close();
                    bs = s;
                    bs.Seek(0, SeekOrigin.Begin);
                    bs.Flush();
                    if(File.Exists(svp))
                        File.Delete(svp);
                }
            }
        }
        public override void Flush()
        {
            bs.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return bs.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return bs.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            bs.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            bs.Write(buffer, offset, count);
        }
    }
}
