namespace SaturnEngine.Asset
{
    public class BitEditor
    {
        public static byte[] BitsToBytes(Bits b)
        {
            int tt = 0, i = 0;
            byte[] bt = new byte[0];
            byte c = 0;
            byte bbs = 0b00000001;
            for (i = 0; i < b.bts.Count; i++, tt++)
            {
                if (tt == 8)
                {
                    tt = 0;
                    bt = bt.Append(c).ToArray();
                    c = 0;
                }


                if (b.bts[i])
                {
                    byte ccb = (byte)(bbs << (7 - tt));
                    c |= ccb;
                }
            }
            bt = bt.Append(c).ToArray();
            return bt;
        }
        public static Bits BytesToBits(byte[] bytes)
        {
            Bits bits = new Bits();
            foreach (byte b in bytes)
            {
                for (int i = 7; i >= 0; i--)
                    bits.Add((b & (1 << i)) != 0 ? 1 : 0);
            }
            return bits;
        }
    }

    public class Bits
    {
        public List<bool> bts;
        public Bits()
        {
            bts = new List<bool>();
        }
        public void Add(int num)
        {
            if (num == 0)
            {
                bts.Add(false);
            }
            else
            {
                bts.Add(true);
            }
        }
        public void Add(Bits bt)
        {
            bts.AddRange(bt.bts);
        }
        public void Change(int bitposi, int val)
        {
            if (val == 0)
            {
                bts[bitposi] = false;
            }
            else
            {
                bts[bitposi] = true;
            }
        }
        public override string ToString()
        {
            string v = "";
            for (int i = 0; i < bts.Count; i++)
            {
                v += bts[i] ? 1 : 0;
            }

            return v;
        }
    }
}
