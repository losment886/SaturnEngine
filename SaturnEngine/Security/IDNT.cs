using SaturnEngine.SEMath;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static SaturnEngine.SEMath.Helper;

namespace SaturnEngine.Security
{
    /// <summary>
    /// 全局唯一ID
    /// </summary>
    public struct UUID
    {
        private static ulong idc = 1;
        private static Stack<ulong> Usage = new Stack<ulong>();
        ulong id;

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        public override string ToString()
        {
            return "[UUID:"+id.ToString() + "]";
        }


        public ulong ID { get { return id; } }


        public UUID()
        {
            id = 0;
            if (Usage.Count > 0)
            {
                id = Usage.Pop();
            }
            else
            {
                id = idc;
                idc++;
            }

        }
        public void Delete()
        {
            Usage.Push(id);
            id = 0;
        }

        public static bool operator ==(UUID left, UUID right)
        {
            return left.id == right.id;
        }
        public static bool operator !=(UUID left, UUID right)
        {
            return left.id != right.id;
        }

        public Guid GetGUID()
        {
            Helper.DataLayout dl = new Helper.DataLayout();
            dl.UL = id;
            return new Guid(dl.GetBytes());
        }
    }
    /// <summary>
    /// 类似HASH的码，版本V2
    /// </summary>
    public static class STCCode
    {
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this ulong v)
        {
            return GetSTC(v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this long v)
        {
            return GetSTC((ulong)v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this uint v)
        {
            return GetSTC(v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this int v)
        {
            return GetSTC((ulong)v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this ushort v)
        {
            return GetSTC((ulong)v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this short v)
        {
            return GetSTC((ulong)v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this byte v)
        {
            return GetSTC((ulong)v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this sbyte v)
        {
            return GetSTC((ulong)v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this char v)
        {
            return GetSTC((ulong)v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this double v)
        {
            return GetSTC(v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this float v)
        {
            return GetSTC(v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this byte[] v)
        {
            return GetSTC(v);
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this object v)
        {
            return v.GetHashCode().ToSTC();
        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong ToSTC(this string v)
        {
            return GetSTC(v);
        }


        private static byte[] CharConvertTable = new byte[256] { 0, 127, 126, 125, 124, 123, 122, 121, 120, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 77, 78, 79, 80, 81, 188, 187, 186, 185, 82, 83, 90, 200, 210, 38, 193, 192, 191, 189, 39, 202, 203, 204, 40, 41, 42, 43, 128, 2, 3, 6, 5, 10, 19, 59, 49, 44, 45, 46, 47, 48, 55, 54, 53, 52, 51, 58, 60, 11, 12, 13, 14, 15, 16, 17, 18, 20, 22, 24, 26, 28, 21, 23, 25, 27, 29, 220, 221, 226, 227, 228, 229, 230, 255, 254, 253, 252, 251, 250, 249, 248, 247, 223, 224, 246, 245, 244, 243, 242, 241, 240, 30, 31, 32, 33, 34, 35, 36, 37, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 84, 85, 86, 87, 88, 89, 91, 92, 93, 94, 95, 96, 97, 98, 99, 110, 111, 112, 113, 114, 115, 116, 117, 150, 170, 190, 239, 149, 148, 238, 237, 236, 235, 234, 233, 232, 4, 1, 7, 9, 8, 231, 219, 218, 217, 216, 215, 214, 213, 212, 211, 209, 208, 207, 206, 50, 56, 57, 205, 199, 198, 197, 196, 195, 194, 222, 225, 184, 183, 118, 119, 129, 130, 182, 181, 180, 177, 169, 168, 167, 176, 175, 174, 173, 172, 171, 166, 165, 164, 163, 162, 161, 160, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 143, 145, 147, 146, 144, 142, 159, 179, 178, 158, 201, 152, 151, 157, 156, 155, 154, 153 };


        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong GetSTC(ulong v)
        {
            DataLayout dl = new DataLayout(v);
            return GetSTC(dl.GetBytes());
        }

        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong GetSTC(double v)
        {
            DataLayout dl = new DataLayout();
            dl.D = v;
            return GetSTC(dl.GetBytes());
        }
        /// <summary>
        /// 获取文件内容的STC码
        /// </summary>
        /// <param name="fp">文件</param>
        /// <returns>STC码</returns>
        public static ulong GetSTCFromFile(string fp)
        {
            ulong bv = 0x1145;//(ulong)HashCode.Combine(v)/3;
            ulong i = 0;
            using (var s = File.OpenRead(fp))
            {
                for (; i < (ulong)s.Length; i++)
                {
                    int b = s.ReadByte();
                    if (b == -1) break;
                    bv += ((uint)CharConvertTable[(byte)b] * (i + 1) * bv);
                    bv -= 2;
                }
            }
            return bv;
        }
        /// <summary>
        /// 获取字符串的STC码
        /// </summary>
        /// <param name="str">值</param>
        /// <param name="unicode">标记是否为Unicode编码</param>
        /// <returns>STC码</returns>
        public static ulong GetSTC(string str, bool unicode = false)
        {
            if (unicode)
            {
                return GetSTC(Encoding.Unicode.GetBytes(str));
            }
            else
            {
                return GetSTC(Encoding.UTF8.GetBytes(str));
            }
        }
        /// <summary>
        /// 获取字节数组的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <param name="index">开始序列</param>
        /// <param name="count">数量</param>
        /// <returns>STC码</returns>
        public static ulong GetSTC(byte[] v, int index, uint count)
        {
            ulong bv = 0x1145;//(ulong)HashCode.Combine(v)/3;
            for (uint i = (uint)index; i < count; i++)
            {
                bv += ((uint)CharConvertTable[v[i]] * (i - (uint)index + 1) * bv);
                bv -= 2;
            }
            return bv;

        }
        /// <summary>
        /// 获取当前值的STC码
        /// </summary>
        /// <param name="v">值</param>
        /// <returns>STC码</returns>
        public static ulong GetSTC(byte[] v)
        {
            ulong bv = 0x1145;//(ulong)HashCode.Combine(v)/3;
            for (uint i = 0; i < v.Length; i++)
            {
                bv += ((uint)CharConvertTable[v[i]] * (i + 1) * bv);
                bv -= 2;
            }
            return bv;
        }
    }
}
