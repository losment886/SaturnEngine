using SaturnEngine.Asset;
using SaturnEngine.Management.SEMemory;
using SaturnEngine.Security;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using static SaturnEngine.SEMath.Helper;

namespace SaturnEngine.Management.IO
{
    public class LRStreamSlim : Stream
    {
        Stream bs;
        long lg = 0;//!limt下该项无效
        long npsoff = 0;
        long bsoff = 0;
        bool limt = true;
        public override bool CanRead => bs.CanRead;

        public override bool CanSeek => bs.CanSeek;

        public override bool CanWrite => bs.CanWrite;

        public override long Length => limt ? lg : bs.Length;

        public override long Position { get => npsoff; set { Seek(value, SeekOrigin.Begin); } }


        public byte[] ReadAllInBytes()
        {
            var p = bs as LRStreamEnc;
            if (p!= null)
            {
                return p.ReadAllInBytes();
            }
            bs.Seek(bsoff, SeekOrigin.Begin);
            byte[] buffer = new byte[lg];
            bs.ReadExactly(buffer, 0, (int)lg);
            return buffer;
        }
        public Stream ReadAllInStream()
        {
            SEMemoryStream sem = new SEMemoryStream(lg);
            bs.Seek(bsoff, SeekOrigin.Begin);
            bs.CopyTo(sem);
            sem.Seek(0, SeekOrigin.Begin);
            return sem;
        }

        ~LRStreamSlim()
        {
            bs.Dispose();
        }
        public LRStreamSlim(Stream bs, long bsoff, long lg, bool limt = true)
        {
            this.bs = bs;
            this.bsoff = bsoff;
            this.lg = lg;
            npsoff = 0;


            this.limt = limt;

        }


        public override void Flush()
        {
            bs.Flush();
        }
        public byte[] Read(int count, long offset = -1)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "数量必须大于0".GetInCurrLang());
            }
            if (offset >= 0)
            {
                Seek(offset, SeekOrigin.Begin);
            }
            byte[] buffer = new byte[count];
            int bytesRead = Read(buffer, 0, count);
            if (bytesRead < count)
            {
                Array.Resize(ref buffer, bytesRead);
            }
            return buffer;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            bs.Seek(bsoff + npsoff, SeekOrigin.Begin);
            if (limt)
            {
                int rd = 0;
                if (npsoff >= lg)
                {

                }
                else if (npsoff + count > lg)
                {
                    rd = (int)(lg - npsoff);
                    bs.ReadExactly(buffer, offset, rd);
                    npsoff += rd;
                }
                else
                {
                    rd = bs.Read(buffer, offset, count);
                    npsoff += rd;

                }
                return rd;
            }
            else
            {
                int rd = 0;
                rd = bs.Read(buffer, offset, count);
                npsoff += rd;
                return rd;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (limt)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        if (offset >= lg || offset < 0)
                        {
                            throw new ArgumentOutOfRangeException(nameof(offset));
                        }
                        else
                        {
                            npsoff = offset;
                        }
                        bs.Seek(bsoff + npsoff, SeekOrigin.Begin);
                        return offset;
                    case SeekOrigin.Current:
                        if (offset + npsoff >= lg || offset + npsoff < 0)
                        {
                            throw new ArgumentOutOfRangeException(nameof(offset));
                        }
                        else
                        {
                            npsoff += offset;
                        }
                        bs.Seek(bsoff + npsoff, SeekOrigin.Begin);
                        return offset;
                    case SeekOrigin.End:
                        if (-offset > lg || offset > 0 || lg + offset < 0)
                        {
                            throw new ArgumentOutOfRangeException(nameof(offset));
                        }
                        else
                        {
                            npsoff = offset + lg;
                        }
                        bs.Seek(bsoff + npsoff, SeekOrigin.Begin);
                        return offset;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(origin));
                }
            }
            else
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        npsoff = offset;
                        bs.Seek(bsoff + npsoff, SeekOrigin.Begin);
                        return offset;
                    case SeekOrigin.Current:
                        npsoff += offset;
                        bs.Seek(bsoff + npsoff, SeekOrigin.Begin);
                        return offset;
                    case SeekOrigin.End:
                        npsoff = offset + lg;
                        bs.Seek(bsoff + npsoff, SeekOrigin.Begin);
                        return offset;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(origin));
                }
            }
        }

        public override void SetLength(long value)
        {
            if (!limt)
                bs.SetLength(value);
            else
                throw new Exception();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            bs.Seek(bsoff + npsoff, SeekOrigin.Begin);
            if (limt)
            {
                int rd = 0;
                if (npsoff >= lg)
                {
                    throw new ArgumentOutOfRangeException(nameof(npsoff), "不可写超出固定流".GetInCurrLang());
                }
                else if (npsoff + count > lg)
                {
                    throw new ArgumentOutOfRangeException(nameof(npsoff), "不可写超出固定流".GetInCurrLang());
                }
                else
                {
                    rd = count;
                    bs.Write(buffer,offset,count);
                    npsoff += rd;

                }
            }
            else
            {
                int rd = 0;
                rd = count;
                bs.Write(buffer, offset, count);
                npsoff += rd;
            }
        }
    }
}
