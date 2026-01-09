using SaturnEngine.Asset;
using SaturnEngine.Management.SEMemory;
using SaturnEngine.Security;
using Silk.NET.OpenXR;
using System.Runtime.InteropServices;
using static SaturnEngine.SEMath.Helper;

namespace SaturnEngine.Management.IO
{
    public unsafe class LRStreamEnc : Stream
    {

        Stream bs;

        public override bool CanRead => bs.CanRead;

        public override bool CanSeek => bs.CanSeek;

        public override bool CanWrite => bs.CanWrite;

        public override long Length => bs.Length;

        public override long Position { get => bs.Position; set { Seek(value, SeekOrigin.Begin); } }


        ulong pswstc = 0;

        static byte[] ts = [100, 79, 23, 77, 225, 114, 22, 38];
        static DataLayout dlcf;
        static byte[] btcf;

        bool ndul = false;
        ulong pswstcstc;
        public byte[] ReadAllInBytes()
        {
            if (ORGS)
            {
                bs.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[Length];
                bs.ReadExactly(buffer, 0, (int)Length);
                return buffer;
            }
            else
            {
                bs.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[Length];
                bs.ReadExactly(buffer, 0, (int)Length);
                if (pswstc != 0)
                {
                    DC(buffer, 0, (int)Length, pswstc, 0);
                }
                return buffer;
            }
        }
        public Stream ReadAllInStream()
        {
            SEMemoryStream sem = new SEMemoryStream(Length);
            if (ORGS)
            {
                bs.Seek(0, SeekOrigin.Begin);
                bs.CopyTo(sem);
                sem.Seek(0, SeekOrigin.Begin);
                return sem;
            }
            else
            {
                bs.Seek(0, SeekOrigin.Begin);
                bs.CopyTo(sem);
                sem.Seek(0, SeekOrigin.Begin);
                if (pswstc != 0)
                {
                    byte[] buffer = new byte[Length];
                    sem.ReadExactly(buffer, 0, (int)Length);
                    DC(buffer, 0, (int)Length, pswstc, 0);
                    sem.Write(buffer, 0, (int)Length);
                    sem.Seek(0, SeekOrigin.Begin);
                }
                return sem;
            }
        }
        static void DC([Out] byte[] b, int o, int c, ulong psstc, long npsi)
        {
            dlcf = new DataLayout(psstc);
            btcf = dlcf.GetBytes();
            Parallel.For(0, c, i =>
            {
                long ppsi = npsi + i;
                int pi = o + i;
                int ns = (int)(ppsi % 8);
                //int ps = pi % 8;
                byte nv = b[pi];
                switch (ns)
                {
                    case 0:
                        nv += (byte)(btcf[ns] + ts[7] - ts[4]);
                        break;
                    case 1:
                        nv += (byte)(btcf[ns] + ts[3] - ts[5]);
                        break;
                    case 2:
                        nv += (byte)(btcf[ns] + ts[6] - ts[1]);
                        break;
                    case 3:
                        nv += (byte)(btcf[ns] + ts[1] - ts[7]);
                        break;
                    case 4:
                        nv += (byte)(btcf[ns] + ts[4] - ts[6]);
                        break;
                    case 5:
                        nv += (byte)(btcf[ns] + ts[2] - ts[5]);
                        break;
                    case 6:
                        nv += (byte)(btcf[ns] + ts[5] - ts[3]);
                        break;
                    case 7:
                        nv += (byte)(btcf[ns] + ts[4] - ts[7]);
                        break;

                }
                if (ppsi % 2 == 0)
                {
                    if (btcf[ns] % 2 == 0)
                    {
                        nv += 17;
                    }
                    else
                    {
                        nv += 1;
                    }

                }
                else
                {
                    nv -= 3;
                }
                b[pi] = nv;
                //b[o + i] = (byte)(b[o + i] ^ bt[i & 7] ^ ts[(npsi + (ulong)i) & 7]);
            });
        }
        static void EC([Out] byte[] b, int o, int c, ulong psstc, long npsi)
        {
            dlcf = new DataLayout(psstc);
            btcf = dlcf.GetBytes();
            Parallel.For(0, c, i =>
            {
                long ppsi = npsi + i;
                int pi = o + i;
                int ns = (int)(ppsi % 8);
                //int ps = pi % 8;
                byte nv = b[pi];
                switch (ns)
                {
                    case 0:
                        nv -= (byte)(btcf[ns] + ts[7] - ts[4]);
                        break;
                    case 1:
                        nv -= (byte)(btcf[ns] + ts[3] - ts[5]);
                        break;
                    case 2:
                        nv -= (byte)(btcf[ns] + ts[6] - ts[1]);
                        break;
                    case 3:
                        nv -= (byte)(btcf[ns] + ts[1] - ts[7]);
                        break;
                    case 4:
                        nv -= (byte)(btcf[ns] + ts[4] - ts[6]);
                        break;
                    case 5:
                        nv -= (byte)(btcf[ns] + ts[2] - ts[5]);
                        break;
                    case 6:
                        nv -= (byte)(btcf[ns] + ts[5] - ts[3]);
                        break;
                    case 7:
                        nv -= (byte)(btcf[ns] + ts[4] - ts[7]);
                        break;

                }
                if (ppsi % 2 == 0)
                {
                    if (btcf[ns] % 2 == 0)
                    {
                        nv -= 17;
                    }
                    else
                    {
                        nv -= 1;
                    }

                }
                else
                {
                    nv += 3;
                }
                b[pi] = nv;
                //b[o + i] = (byte)(b[o + i] ^ bt[i & 7] ^ ts[(npsi + (ulong)i) & 7]);
            });
        }

        bool ul = false;

        public bool IsDecrypt => ul;
        public bool NeedDecrypt => ndul;

        ~LRStreamEnc()
        {
            bs.Dispose();
        }
        public LRStreamEnc(Stream bs, bool NeedDecrypt = false, ulong psstcstc = 0)
        {
            this.bs = bs;

            ndul = NeedDecrypt;
            ul = false;
            pswstcstc = psstcstc;
        }
        public bool Decrypt(string psw)
        {
            pswstc = STCCode.GetSTC(psw);
            ulong pss = STCCode.GetSTC(pswstc);
            if (pss == pswstcstc)
            {
                ul = true;
                ORGS = false;

            }

            return ul;
        }
        public bool Decrypt(ulong pswstc)
        {
            ulong pss = STCCode.GetSTC(pswstc);
            this.pswstc = pswstc;
            if (pss == pswstcstc)
            {
                ul = true;
                ORGS = false;
            }
            return ul;
        }
        public void UseOrgData(bool v)
        {
            ORGS = v;
        }

        bool ORGS = true;
        public void CleanPassword()
        {
            pswstc = 0;
            ul = false;
            ORGS = true;
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
            int rd = 0;
            rd = bs.Read(buffer, offset, count);
            if (pswstc != 0 && ul && !ORGS)
            {
                DC(buffer, offset, rd, pswstc, Position - rd);
            }
            return rd;
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
            int rd = 0;
            rd = count;
            if (pswstc != 0 && ul && !ORGS)
            {
                EC(buffer, offset, rd, pswstc, Position);
            }
            bs.Write(buffer,offset,count);
        }
    }
}
