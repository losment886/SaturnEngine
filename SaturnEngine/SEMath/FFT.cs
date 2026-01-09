namespace SaturnEngine.SEMath
{
    public class SEFFT
    {
        public static void FFT(bool forward, long count, ref SEComplex[] data)
        {
            //Thanks to NAudio
            int num = 1;
            for (long i = 0; i < count; i++)
                num *= 2;
            //Math.Exp();
            int num2 = num >> 1;
            int num3 = 0;
            for (int i = 0; i < num - 1; i++)
            {
                if (i < num3)
                {

                    double x = data[i].X;
                    double y = data[i].Y;
                    data[i].X = data[num3].X;
                    data[i].Y = data[num3].Y;
                    data[num3].X = x;
                    data[num3].Y = y;
                }

                int num4;
                for (num4 = num2; num4 <= num3; num4 >>= 1)
                {
                    num3 -= num4;
                }

                num3 += num4;
            }

            double num5 = -1f;
            double num6 = 0f;
            int num7 = 1;
            for (int j = 0; j < count; j++)
            {
                int num8 = num7;
                num7 <<= 1;
                double num9 = 1f;
                double num10 = 0f;
                for (num3 = 0; num3 < num8; num3++)
                {
                    for (int i = num3; i < num; i += num7)
                    {
                        int num11 = i + num8;
                        double num12 = num9 * data[num11].X - num10 * data[num11].Y;
                        double num13 = num9 * data[num11].Y + num10 * data[num11].X;
                        data[num11].X = data[i].X - num12;
                        data[num11].Y = data[i].Y - num13;
                        data[i].X += num12;
                        data[i].Y += num13;
                    }

                    double num14 = num9 * num5 - num10 * num6;
                    num10 = num9 * num6 + num10 * num5;
                    num9 = num14;
                }

                num6 = Math.Sqrt((1f - num5) / 2f);
                if (forward)
                {
                    num6 = 0f - num6;
                }

                num5 = Math.Sqrt((1f + num5) / 2f);
            }

            if (forward)
            {
                for (int i = 0; i < num; i++)
                {
                    data[i].X /= num;
                    data[i].Y /= num;
                }
            }
        }
    }
}
