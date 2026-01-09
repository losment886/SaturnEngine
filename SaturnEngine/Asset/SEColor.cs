using SaturnEngine.SEMath;
using System.Drawing;

namespace SaturnEngine.Asset
{
    public struct SEColor
    {

        public static readonly SEColor Black = (new Vector3D(0, 0, 0));
        public static readonly SEColor DarkBlue = (new Vector3D(0, 0, 0.5058823529411765));
        public static readonly SEColor DarkGreen = (new Vector3D(0, 0.5058823529411765, 0));
        public static readonly SEColor DarkCyan = (new Vector3D(0, 0.5058823529411765, 0.5058823529411765));
        public static readonly SEColor DarkRed = (new Vector3D(0.5058823529411765, 0, 0));
        public static readonly SEColor DarkMagenta = (new Vector3D(0.5058823529411765, 0, 0.5058823529411765));
        public static readonly SEColor DarkYellow = (new Vector3D(0, 0.5058823529411765, 0));
        public static readonly SEColor Gray = (new Vector3D(0.5019607843137255, 0.5019607843137255, 0.5019607843137255));
        public static readonly SEColor DarkGray = (new Vector3D(0.6627450980392157, 0.6627450980392157, 0.6627450980392157));
        public static readonly SEColor Blue = (new Vector3D(0, 0, 1));
        public static readonly SEColor Green = (new Vector3D(0, 1, 0));
        public static readonly SEColor Cyan = (new Vector3D(0, 1, 1));
        public static readonly SEColor Red = (new Vector3D(1, 0, 0));
        public static readonly SEColor Magenta = (new Vector3D(1, 0, 1));
        public static readonly SEColor Yellow = (new Vector3D(1, 1, 0));
        public static readonly SEColor White = (new Vector3D(1, 1, 1));










        /// <summary>
        /// Color Red, Range : [0,1]
        /// </summary>
        public double R;//[0,1]
        /// <summary>
        /// Color Green, Range : [0,1]
        /// </summary>
        public double G;
        /// <summary>
        /// Color Blue, Range : [0,1]
        /// </summary>
        public double B;
        /// <summary>
        /// Alpha Channel, Range : [0,1], default is 1
        /// </summary>
        public double A;
        /// <summary>
        /// Highlight rate,extern for hdr or higher, Range : [0,unlimited), default is 1
        /// </summary>
        public double HLR;//高亮倍率
        public static implicit operator SEColor(Vector3D v)
        {
            SEColor s = new SEColor(v.X, v.Y, v.Z);
            return s;
        }
        public static explicit operator Vector3D(SEColor s)
        {
            Vector3D v = new Vector3D(s.R, s.G, s.B);
            return v;
        }
        public SEColor()
        {
            R = 0;
            G = 0;
            B = 0;
            A = 1;
            HLR = 1;
        }
        public SEColor(double r = 0, double g = 0, double b = 0, double a = 1)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            HLR = 1;
        }
        public void SetHLR(double hlr = 1)
        {
            HLR = hlr;
        }
        public void FromRGBA8BIT(byte r, byte g, byte b, byte a)
        {
            R = r / 255.0;
            G = g / 255.0;
            B = b / 255.0;
            A = a / 255.0;
        }
        public void FromRGBA4BIT(byte r, byte g, byte b, byte a)
        {
            R = r / 127.0;
            G = g / 127.0;
            B = b / 127.0;
            A = a / 127.0;
        }
        public void FromRGB8BIT(byte r, byte g, byte b)
        {
            R = r / 255.0;
            G = g / 255.0;
            B = b / 255.0;
            A = 1;
        }
        public void FromRGB4BIT(byte r, byte g, byte b)
        {
            R = r / 127.0;
            G = g / 127.0;
            B = b / 127.0;
            A = 1;
        }
        public Color ToGDIColor()
        {
            int r = (int)(R * 255);
            int g = (int)(G * 255);
            int b = (int)(B * 255);
            int a = (int)(A * 255);
            if (r < 0) r = 0; else if (r > 255) r = 255;
            if (g < 0) g = 0; else if (g > 255) g = 255;
            if (b < 0) b = 0; else if (b > 255) b = 255;
            if (a < 0) a = 0; else if (a > 255) a = 255;
            return Color.FromArgb(a, r, g, b);
        }
        /// <summary>
        /// 预制类型很少，会强制转换;
        /// </summary>
        /// <returns></returns>
        public ConsoleColor GetConsoleColor()
        {
            Vector3D vb = new Vector3D(R, G, B);
            double dBlack = vb.GetDistance(Black);
            double dDarkBlue = vb.GetDistance(DarkBlue);
            double dDarkGreen = vb.GetDistance(DarkGreen);
            double dDarkCyan = vb.GetDistance(DarkCyan);
            double dDarkRed = vb.GetDistance(DarkRed);
            double dDarkMagenta = vb.GetDistance(DarkMagenta);
            double dDarkYellow = vb.GetDistance(DarkYellow);
            double dGray = vb.GetDistance(Gray);
            double dDarkGray = vb.GetDistance(DarkGray);
            double dBlue = vb.GetDistance(Blue);
            double dGreen = vb.GetDistance(Green);
            double dCyan = vb.GetDistance(Cyan);
            double dRed = vb.GetDistance(Red);
            double dMagenta = vb.GetDistance(Magenta);
            double dYellow = vb.GetDistance(Yellow);
            double dWhite = vb.GetDistance(White);
            double min = double.Min(dBlack, double.Min(dDarkBlue, double.Min(dDarkGreen, double.Min(dDarkCyan, double.Min(dDarkRed, double.Min(dDarkMagenta, double.Min(dDarkYellow, double.Min(dGray, double.Min(dDarkGray, double.Min(dBlue, double.Min(dGreen, double.Min(dCyan, double.Min(dRed, double.Min(dMagenta, double.Min(dYellow, dWhite)))))))))))))));
            if (min == dBlack) return ConsoleColor.Black;
            else if (min == dDarkBlue) return ConsoleColor.DarkBlue;
            else if (min == dDarkGreen) return ConsoleColor.DarkGreen;
            else if (min == dDarkCyan) return ConsoleColor.DarkCyan;
            else if (min == dDarkRed) return ConsoleColor.DarkRed;
            else if (min == dDarkMagenta) return ConsoleColor.DarkMagenta;
            else if (min == dDarkYellow) return ConsoleColor.DarkYellow;
            else if (min == dGray) return ConsoleColor.Gray;
            else if (min == dDarkGray) return ConsoleColor.DarkGray;
            else if (min == dBlue) return ConsoleColor.Blue;
            else if (min == dGreen) return ConsoleColor.Green;
            else if (min == dCyan) return ConsoleColor.Cyan;
            else if (min == dRed) return ConsoleColor.Red;
            else if (min == dMagenta) return ConsoleColor.Magenta;
            else if (min == dYellow) return ConsoleColor.Yellow;
            else return ConsoleColor.White;
        }
    }
}
