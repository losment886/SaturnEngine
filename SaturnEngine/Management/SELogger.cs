using SaturnEngine.Asset;
using SaturnEngine.Global;

namespace SaturnEngine.Management
{
    public struct Str
    {
        public struct StrStyle
        {
            public SEColor Color;
            //public SEFont Font;
            //public double Size;
            public StrStyle()
            {
                Color = new SEColor(1, 1, 1, 1);
                //Font = new SEFont();
                //Size = 12;
            }
            public StrStyle(SEColor color)
            {
                Color = color;
                //Font = new SEFont();
                //Size = 12;
            }

        }

        public struct Pair<T1, T2>
        {
            public T1 V1;
            public T2 V2;
            public Pair(T1 key, T2 value)
            {
                V1 = key;
                V2 = value;
            }
        }
        public string RawString
        {
            get
            {
                string s = "";
                for (int i = 0; i < Sts.Count; i++)
                {
                    s += Sts[i].V1;
                }
                return s;
            }
        }
        public int Length
        {
            get
            {
                int l = 0;
                for (int i = 0; i < Sts.Count; i++)
                {
                    l += Sts[i].V1.Length;
                }
                return l;
            }
        }
        public bool IsEmpty
        {
            get
            {
                return Length == 0;
            }
        }
        public int FromRawStringIndexGetListIndex(int index)
        {

            int nbox = 0;
            int lg = 0;
            for (; nbox < Sts.Count; nbox++)
            {
                lg += Sts[nbox].V1.Length;
                if (index > lg)
                {
                    return nbox - 1;
                }
            }
            return -1;
        }
        public void RemoveBox(int index)
        {
            if (index < 0 || index >= Sts.Count)
                return;
            Sts.RemoveAt(index);
        }
        public List<Pair<string, StrStyle>> Sts;
        public Str()
        {
            Sts = new List<Pair<string, StrStyle>>();
        }
        public Str(string s, StrStyle style)
        {
            Sts = new List<Pair<string, StrStyle>>();

            Sts.Add(new Pair<string, StrStyle>(s, style));
        }
        public Str(Str[] v)
        {
            Sts = new List<Pair<string, StrStyle>>();
            for (int i = 0; i < v.Length; i++)
            {
                for (int j = 0; j < v[i].Sts.Count; j++)
                {
                    Sts.Add(v[i].Sts[j]);
                }
            }

        }

        public void Add(string s, StrStyle style)
        {
            Sts.Add(new Pair<string, StrStyle>(s, style));
        }
        public void Add(Str[] s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                for (int j = 0; j < s[i].Sts.Count; j++)
                {
                    Sts.Add(s[i].Sts[j]);
                }
            }
        }
        public override string ToString()
        {
            return RawString;
        }
        public static implicit operator Str(string s)
        {
            var st = new Str();
            var sty = new StrStyle();
            //sty.Color = new SEColor(1, 1, 1, 1);
            st.Sts.Add(new Pair<string, StrStyle>(s, sty));

            return st;
        }
        public static explicit operator string(Str s)
        {
            return s.ToString();
        }
        public static Str operator +(Str a, Str b)
        {
            for (int i = 0; i < b.Sts.Count; i++)
            {
                a.Sts.Add(b.Sts[i]);
            }
            return a;
        }
        public static Str operator +(Str a, string b)
        {
            var sty = new StrStyle();
            //sty.Color = new SEColor(1, 1, 1, 1);
            a.Sts.Add(new Pair<string, StrStyle>(b, sty));
            return a;
        }
    }

    public class SELogger
    {
        public static string? Input()
        {
            return Console.ReadLine();
        }
        public static void Log(Str message) => Log(message, "Saturn Engine");
        public static void Log(Str message,string sender)
        {
            if (!GVariables.AllowConsoleOutput) return;
            var pr = Console.ForegroundColor;
            Console.Write($"[");
            Console.ForegroundColor = ConsoleColor.Blue;
            //Console.Write(part.V1);
            Console.Write(sender);
            Console.ForegroundColor = pr;
            Console.Write("]<");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("LOG");
            Console.ForegroundColor = pr;
            Console.Write("(");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($"{DateTime.Now}");
            Console.ForegroundColor = pr;

            //]<LOG({DateTime.Now})>
            Console.Write($")>");
            for (int i = 0; i < message.Sts.Count; i++)
            {
                var part = message.Sts[i];
                var prevColor = Console.ForegroundColor;
                Console.ForegroundColor = part.V2.Color.GetConsoleColor();
                Console.Write(part.V1);
                Console.ForegroundColor = prevColor;
            }
            Console.WriteLine();
        }
        public static void Warn(Str message) => Warn(message, "Saturn Engine");
        public static void Warn(Str message, string sender )
        {
            if (!GVariables.AllowConsoleOutput) return;
            var pr = Console.ForegroundColor;
            Console.Write($"[");
            Console.ForegroundColor = ConsoleColor.Blue;
            //Console.Write(part.V1);
            Console.Write(sender);
            Console.ForegroundColor = pr;
            Console.Write("]<");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("WARN");
            Console.ForegroundColor = pr;
            Console.Write("(");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($"{DateTime.Now}");
            Console.ForegroundColor = pr;

            //]<LOG({DateTime.Now})>
            Console.Write($")>");
            for (int i = 0; i < message.Sts.Count; i++)
            {
                var part = message.Sts[i];
                var prevColor = Console.ForegroundColor;
                Console.ForegroundColor = part.V2.Color.GetConsoleColor();
                Console.Write(part.V1);
                Console.ForegroundColor = prevColor;
            }
            Console.WriteLine();
        }
        public static void Error(Str message) => Error(message, "Saturn Engine");
        public static void Error(Str message, string sender)
        {
            if (!GVariables.AllowConsoleOutput) return;
            var pr = Console.ForegroundColor;
            Console.Write($"[");
            Console.ForegroundColor = ConsoleColor.Blue;
            //Console.Write(part.V1);
            Console.Write(sender);
            Console.ForegroundColor = pr;
            Console.Write("]<");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("ERROR");
            Console.ForegroundColor = pr;
            Console.Write("(");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($"{DateTime.Now}");
            Console.ForegroundColor = pr;

            //]<LOG({DateTime.Now})>
            Console.Write($")>");
            for (int i = 0; i < message.Sts.Count; i++)
            {
                var part = message.Sts[i];
                var prevColor = Console.ForegroundColor;
                Console.ForegroundColor = part.V2.Color.GetConsoleColor();
                Console.Write(part.V1);
                Console.ForegroundColor = prevColor;
            }
            Console.WriteLine();
        }
    }
}
