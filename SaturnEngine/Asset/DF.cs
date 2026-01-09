using System.Runtime.InteropServices;
using static SaturnEngine.SEMath.Helper;

namespace SaturnEngine.Asset
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TIME
    {
        public ushort YEAR;
        public byte MONTH;
        public byte DAY;
        public byte HOUR;
        public byte MINUTE;
        public byte SECOND;
        public byte MILLISECOND;
        public TIME(ulong CAT)
        {
            DataLayout dl = new DataLayout();
            dl.UL = CAT;
            YEAR = dl.US0;
            MONTH = dl.B2;
            DAY = dl.B3;
            HOUR = dl.B4;
            MINUTE = dl.B5;
            SECOND = dl.B6;
            MILLISECOND = dl.B7;
        }
        public TIME(DateTime DT)
        {
            YEAR = (ushort)DT.Year;
            MONTH = (byte)DT.Month;
            DAY = (byte)DT.Day;
            HOUR = (byte)DT.Hour;
            MINUTE = (byte)DT.Minute;
            SECOND = (byte)DT.Second;
            MILLISECOND = (byte)DT.Millisecond;
        }
        public DateTime GetDateTime()
        {
            try
            {
                return new DateTime(YEAR, MONTH, DAY, HOUR, MINUTE, SECOND, MILLISECOND);
            }
            catch
            {
                return new DateTime();
            }
        }
        public ulong GetTimeCode()
        {
            DataLayout dl = new DataLayout();
            dl.US0 = YEAR;
            dl.B2 = MONTH;
            dl.B3 = DAY;
            dl.B4 = HOUR;
            dl.B5 = MINUTE;
            dl.B6 = SECOND;
            dl.B7 = MILLISECOND;
            return dl.UL;
        }
    }
    public struct VERSION
    {
        public byte Major;
        public byte Minor;
        public byte Build;
        public byte Revision;


        public VERSION(Version v)
        {
            Major = (byte)v.Major;
            Minor = (byte)v.Minor;
            Build = (byte)v.Build;
            Revision = (byte)v.Revision;
        }
        public VERSION(uint v)
        {
            DataLayout dl = new DataLayout();
            dl.UI0 = v;
            Major = dl.B0;
            Minor = dl.B1;
            Build = dl.B2;
            Revision = dl.B3;
        }

        public Version GetVersion()
        {
            return new Version(Major, Minor, Build, Revision);
        }

        public uint GetVersionCode()
        {
            DataLayout dl = new DataLayout();

            dl.B0 = Major;
            dl.B1 = Minor;
            dl.B2 = Build;
            dl.B3 = Revision;
            return dl.UI0;
        }

    }
}
