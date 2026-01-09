using System.Runtime.InteropServices;

namespace SaturnEngine.SEMath
{
    public class Helper
    {

        public static unsafe string PTRGetString(nint s)
        {
            byte[] b = new byte[256];
            Marshal.Copy(s, b, 0, 256);

            string v = "";

            for (int i = 0; i < b.Length; i++)
            {
                if (b[i] == '\0')
                {
                    break;
                }
                else
                {
                    v += (char)b[i];
                }
            }
            return v;
        }
        public static unsafe string PTRGetString(byte* s)
        {
            string v = "";

            for (int i = 0; i < 4096; i++)
            {
                if (s[i] == '\0')
                {
                    break;
                }
                else
                {
                    v += (char)s[i];
                }
            }
            return v;
        }


        [StructLayout(LayoutKind.Explicit)]
        //[MethodImpl(MethodImplOptions.NoOptimization)]
        public struct DataLayout
        {
            [FieldOffset(0)]
            public double D;
            [FieldOffset(0)]
            public float F0;
            [FieldOffset(4)]
            public float F1;
            [FieldOffset(0)]
            public ulong UL;
            [FieldOffset(0)]
            public long L;
            [FieldOffset(0)]
            public uint UI0;
            [FieldOffset(4)]
            public uint UI1;
            [FieldOffset(0)]
            public int I0;
            [FieldOffset(4)]
            public int I1;
            [FieldOffset(0)]
            public ushort US0;
            [FieldOffset(2)]
            public ushort US1;
            [FieldOffset(4)]
            public ushort US2;
            [FieldOffset(6)]
            public ushort US3;
            [FieldOffset(0)]
            public short S0;
            [FieldOffset(2)]
            public short S1;
            [FieldOffset(4)]
            public short S2;
            [FieldOffset(6)]
            public short S3;
            [FieldOffset(0)]
            public byte B0;
            [FieldOffset(1)]
            public byte B1;
            [FieldOffset(2)]
            public byte B2;
            [FieldOffset(3)]
            public byte B3;
            [FieldOffset(4)]
            public byte B4;
            [FieldOffset(5)]
            public byte B5;
            [FieldOffset(6)]
            public byte B6;
            [FieldOffset(7)]
            public byte B7;
            [FieldOffset(0)]
            public sbyte SB0;
            [FieldOffset(1)]
            public sbyte SB1;
            [FieldOffset(2)]
            public sbyte SB2;
            [FieldOffset(3)]
            public sbyte SB3;
            [FieldOffset(4)]
            public sbyte SB4;
            [FieldOffset(5)]
            public sbyte SB5;
            [FieldOffset(6)]
            public sbyte SB6;
            [FieldOffset(7)]
            public sbyte SB7;

            public DataLayout()
            {
                UL = 0;
            }
            public DataLayout(ulong v)
            {
                UL = v;
            }
            public byte[] GetBytes()
            {
                return [B0, B1, B2, B3, B4, B5, B6, B7];
            }
        }

        public static T LEToBE<T>(T b)
        {
            Type t = typeof(T);

            object v = b;
            DataLayout din = new DataLayout();
            DataLayout dou = new DataLayout();
            if (t == typeof(byte) || t == typeof(sbyte))
            {
                return b;
            }
            else if (t == typeof(short))
            {
                short h = (short)v;
                din.S0 = h;
                dou.B1 = din.B0;
                dou.B0 = din.B1;
                return (T)((object)dou.S0);
            }
            else if (t == typeof(int))
            {
                int h = (int)v;
                din.I0 = h;
                dou.B0 = din.B3;
                dou.B1 = din.B2;
                dou.B2 = din.B1;
                dou.B3 = din.B0;
                return (T)((object)dou.I0);
            }
            else if (t == typeof(long))
            {
                long h = (long)v;
                din.L = h;
                dou.B0 = din.B7;
                dou.B1 = din.B6;
                dou.B2 = din.B5;
                dou.B3 = din.B4;
                dou.B4 = din.B3;
                dou.B5 = din.B2;
                dou.B6 = din.B1;
                dou.B7 = din.B0;
                return (T)((object)dou.L);
            }
            else if (t == typeof(ushort))
            {
                ushort h = (ushort)v;
                din.US0 = h;
                dou.B1 = din.B0;
                dou.B0 = din.B1;
                return (T)((object)dou.US0);
            }
            else if (t == typeof(uint))
            {
                uint h = (uint)v;
                din.UI0 = h;
                dou.B0 = din.B3;
                dou.B1 = din.B2;
                dou.B2 = din.B1;
                dou.B3 = din.B0;
                return (T)((object)dou.UI0);
            }
            else if (t == typeof(ulong))
            {
                ulong h = (ulong)v;
                din.UL = h;
                dou.B0 = din.B7;
                dou.B1 = din.B6;
                dou.B2 = din.B5;
                dou.B3 = din.B4;
                dou.B4 = din.B3;
                dou.B5 = din.B2;
                dou.B6 = din.B1;
                dou.B7 = din.B0;
                return (T)((object)dou.UL);
            }
            else if (t == typeof(float))
            {
                float h = (float)v;
                din.F0 = h;
                dou.B0 = din.B3;
                dou.B1 = din.B2;
                dou.B2 = din.B1;
                dou.B3 = din.B0;


                return (T)((object)dou.F0);
            }
            else if (t == typeof(double))
            {
                double h = (double)v;
                din.D = h;
                dou.B0 = din.B7;
                dou.B1 = din.B6;
                dou.B2 = din.B5;
                dou.B3 = din.B4;
                dou.B4 = din.B3;
                dou.B5 = din.B2;
                dou.B6 = din.B1;
                dou.B7 = din.B0;
                return (T)((object)dou.D);
            }
            else
            {
                return b;
            }
        }

        public static T BEToLE<T>(T b)
        {
            Type t = typeof(T);

            object v = b;
            DataLayout din = new DataLayout();
            DataLayout dou = new DataLayout();
            if (t == typeof(byte) || t == typeof(sbyte))
            {
                return b;
            }
            else if (t == typeof(short))
            {
                short h = (short)v;
                din.S0 = h;
                dou.B1 = din.B0;
                dou.B0 = din.B1;
                return (T)((object)dou.S0);
            }
            else if (t == typeof(int))
            {
                int h = (int)v;
                din.I0 = h;
                dou.B0 = din.B3;
                dou.B1 = din.B2;
                dou.B2 = din.B1;
                dou.B3 = din.B0;
                return (T)((object)dou.I0);
            }
            else if (t == typeof(long))
            {
                long h = (long)v;
                din.L = h;
                dou.B0 = din.B7;
                dou.B1 = din.B6;
                dou.B2 = din.B5;
                dou.B3 = din.B4;
                dou.B4 = din.B3;
                dou.B5 = din.B2;
                dou.B6 = din.B1;
                dou.B7 = din.B0;
                return (T)((object)dou.L);
            }
            else if (t == typeof(ushort))
            {
                ushort h = (ushort)v;
                din.US0 = h;
                dou.B1 = din.B0;
                dou.B0 = din.B1;
                return (T)((object)dou.US0);
            }
            else if (t == typeof(uint))
            {
                uint h = (uint)v;
                din.UI0 = h;
                dou.B0 = din.B3;
                dou.B1 = din.B2;
                dou.B2 = din.B1;
                dou.B3 = din.B0;
                return (T)((object)dou.UI0);
            }
            else if (t == typeof(ulong))
            {
                ulong h = (ulong)v;
                din.UL = h;
                dou.B0 = din.B7;
                dou.B1 = din.B6;
                dou.B2 = din.B5;
                dou.B3 = din.B4;
                dou.B4 = din.B3;
                dou.B5 = din.B2;
                dou.B6 = din.B1;
                dou.B7 = din.B0;
                return (T)((object)dou.UL);
            }
            else if (t == typeof(float))
            {
                float h = (float)v;
                din.F0 = h;
                dou.B0 = din.B3;
                dou.B1 = din.B2;
                dou.B2 = din.B1;
                dou.B3 = din.B0;


                return (T)((object)dou.F0);
            }
            else if (t == typeof(double))
            {
                double h = (double)v;
                din.D = h;
                dou.B0 = din.B7;
                dou.B1 = din.B6;
                dou.B2 = din.B5;
                dou.B3 = din.B4;
                dou.B4 = din.B3;
                dou.B5 = din.B2;
                dou.B6 = din.B1;
                dou.B7 = din.B0;
                return (T)((object)dou.D);
            }
            else
            {
                return b;
            }
        }

        public static double GetAngleByRadians(double d)
        {
            return d * 180d / double.Pi;
        }
        public static double GetRadiansByAngle(double a)
        {
            return a * double.Pi / 180d;
        }
    }
}
