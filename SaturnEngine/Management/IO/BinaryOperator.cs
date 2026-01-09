using System.Text;
using static SaturnEngine.SEMath.Helper;

namespace SaturnEngine.Management.IO
{
    /// <summary>
    /// 适合<see cref="DataLayout"/>操作习惯的二进制操作流
    /// </summary>
    public class BinaryOperator : Stream
    {
        Stream st;
        public override bool CanRead => st.CanRead;

        public override bool CanSeek => st.CanSeek;

        public override bool CanWrite => st.CanWrite;

        public override long Length => st.Length;

        public override long Position { get => st.Position; set => st.Position = value; }

        public override void Flush()
        {
            st.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return st.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return st.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            st.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            st.Write(buffer, offset, count);
        }
        public void Write(string s, bool unicode = false)
        {
            if (unicode)
            {
                Write(Encoding.Unicode.GetBytes(s));
            }
            else
            {
                Write(Encoding.UTF8.GetBytes(s));
            }
        }
        public void Write(byte b)
        {
            st.WriteByte(b);
        }
        public void Write(sbyte b)
        {
            st.WriteByte((byte)b);
        }
        public void Write(char b)
        {
            st.WriteByte((byte)b);
        }
        public void Write(ushort b)
        {
            DataLayout dl = new DataLayout();
            dl.US0 = b;
            Write(dl.B0);
            Write(dl.B1);
        }
        public void Write(short b)
        {
            DataLayout dl = new DataLayout();
            dl.S0 = b;
            Write(dl.B0);
            Write(dl.B1);
        }
        public void Write(uint b)
        {
            DataLayout dl = new DataLayout();
            dl.UI0 = b;
            Write(dl.B0);
            Write(dl.B1);
            Write(dl.B2);
            Write(dl.B3);
        }
        public void Write(int b)
        {
            DataLayout dl = new DataLayout();
            dl.I0 = b;
            Write(dl.B0);
            Write(dl.B1);
            Write(dl.B2);
            Write(dl.B3);
        }
        public void Write(float b)
        {
            DataLayout dl = new DataLayout();
            dl.F0 = b;
            Write(dl.B0);
            Write(dl.B1);
            Write(dl.B2);
            Write(dl.B3);
        }
        public void Write(ulong b)
        {
            DataLayout dl = new DataLayout();
            dl.UL = b;
            Write(dl.B0);
            Write(dl.B1);
            Write(dl.B2);
            Write(dl.B3);
            Write(dl.B4);
            Write(dl.B5);
            Write(dl.B6);
            Write(dl.B7);
        }
        public void Write(long b)
        {
            DataLayout dl = new DataLayout();
            dl.L = b;
            Write(dl.B0);
            Write(dl.B1);
            Write(dl.B2);
            Write(dl.B3);
            Write(dl.B4);
            Write(dl.B5);
            Write(dl.B6);
            Write(dl.B7);
        }
        public void Write(double b)
        {
            DataLayout dl = new DataLayout();
            dl.D = b;
            Write(dl.B0);
            Write(dl.B1);
            Write(dl.B2);
            Write(dl.B3);
            Write(dl.B4);
            Write(dl.B5);
            Write(dl.B6);
            Write(dl.B7);
        }
        public ulong ReadUInt64()
        {
            DataLayout dl = new DataLayout();
            byte[] bytes = new byte[8];
            st.ReadExactly(bytes);
            dl.B0 = bytes[0];
            dl.B1 = bytes[1];
            dl.B2 = bytes[2];
            dl.B3 = bytes[3];
            dl.B4 = bytes[4];
            dl.B5 = bytes[5];
            dl.B6 = bytes[6];
            dl.B7 = bytes[7];
            return dl.UL;
        }
        public long ReadInt64()
        {
            DataLayout dl = new DataLayout();
            byte[] bytes = new byte[8];
            st.ReadExactly(bytes);
            dl.B0 = bytes[0];
            dl.B1 = bytes[1];
            dl.B2 = bytes[2];
            dl.B3 = bytes[3];
            dl.B4 = bytes[4];
            dl.B5 = bytes[5];
            dl.B6 = bytes[6];
            dl.B7 = bytes[7];
            return dl.L;
        }
        public double ReadDouble()
        {
            DataLayout dl = new DataLayout();
            byte[] bytes = new byte[8];
            st.ReadExactly(bytes);
            dl.B0 = bytes[0];
            dl.B1 = bytes[1];
            dl.B2 = bytes[2];
            dl.B3 = bytes[3];
            dl.B4 = bytes[4];
            dl.B5 = bytes[5];
            dl.B6 = bytes[6];
            dl.B7 = bytes[7];
            return dl.D;
        }
        public uint ReadUInt32()
        {
            DataLayout dl = new DataLayout();
            byte[] bytes = new byte[4];
            st.ReadExactly(bytes);
            dl.B0 = bytes[0];
            dl.B1 = bytes[1];
            dl.B2 = bytes[2];
            dl.B3 = bytes[3];

            return dl.UI0;
        }
        public int ReadInt32()
        {
            DataLayout dl = new DataLayout();
            byte[] bytes = new byte[4];
            st.ReadExactly(bytes);
            dl.B0 = bytes[0];
            dl.B1 = bytes[1];
            dl.B2 = bytes[2];
            dl.B3 = bytes[3];

            return dl.I0;
        }
        public float ReadFloat()
        {
            DataLayout dl = new DataLayout();
            byte[] bytes = new byte[4];
            st.ReadExactly(bytes);
            dl.B0 = bytes[0];
            dl.B1 = bytes[1];
            dl.B2 = bytes[2];
            dl.B3 = bytes[3];

            return dl.F0;
        }
        public ushort ReadUInt16()
        {
            DataLayout dl = new DataLayout();
            byte[] bytes = new byte[2];
            st.ReadExactly(bytes);
            dl.B0 = bytes[0];
            dl.B1 = bytes[1];

            return dl.US0;
        }
        public short ReadInt16()
        {
            DataLayout dl = new DataLayout();
            byte[] bytes = new byte[2];
            st.ReadExactly(bytes);
            dl.B0 = bytes[0];
            dl.B1 = bytes[1];

            return dl.S0;
        }
        public byte ReadUInt8()
        {
            return (byte)ReadByte();
        }
        public sbyte ReadInt8()
        {
            return (sbyte)ReadByte();
        }
        public char ReadChar()
        {
            return (char)ReadByte();
        }
        public string ReadWchar()
        {
            string s;
            byte[] bytes = new byte[2];
            s = Encoding.Unicode.GetString(bytes);
            return s;
        }
        public byte[] ReadBytes(int length)
        {
            byte[] b = new byte[length];
            st.ReadExactly(b);
            return b;
        }
        public byte[] ReadBytes(long length)
        {
            byte[] b = new byte[length];
            MemoryStream ms = new MemoryStream(b);
            //st.ReadExactly(b);
            byte[] buf = new byte[1024000];
            long co = length / buf.LongLength;
            int last = (int)(length % buf.LongLength);
            for (long i = 0; i < co; i++)
            {
                st.ReadExactly(buf);
                ms.Write(buf);
            }
            st.ReadExactly(buf, 0, last);
            ms.Write(buf, 0, last);
            ms.Flush();
            return b;
        }
        public BinaryOperator(Stream stream)
        {
            st = stream;
        }
    }
}
