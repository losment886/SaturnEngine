using Microsoft.ClearScript.JavaScript;
using SaturnEngine.Asset;
using SaturnEngine.SEMath;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageProcesser
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            string arg = "";
            args.All((v) =>
            {
                arg += v + " ";
                return true;
            });
            arg.Trim();
            if (File.Exists(arg))
            {
                //byte[] bytes = File.ReadAllBytes(arg);
                SaturnEngine.Asset.SEImageFile i = new SaturnEngine.Asset.SEImageFile();
                i.LoadImageFromFile(arg);
                var ic = i.GetImage();
                var vc = ic.CloneAs<Rgba32>();
                byte[] b = new byte[vc.Width * vc.Height * 4];
                SEComplex[] s = new SEComplex[vc.Width * vc.Height * 4];
                Parallel.For(0L, vc.Width * vc.Height * 4, (i) =>
                {
                    s[i] = new SEComplex(b[i], 0);
                });
                s[0] = new SEComplex();
                vc.CopyPixelDataTo(b);
                SaturnEngine.SEMath.SEFFT.FFT(false, 4, ref s);
                string vcap = Path.GetDirectoryName(arg) + "/";
                string cac1 = vcap + $"{Path.GetFileNameWithoutExtension(arg)}_FFT.png";
                SEImageFile cac = new SEImageFile();
                cac.CreateEmpty(vc.Width * 2, vc.Height * 2);
                var cacima = cac.GetImage().CloneAs<Rgba32>();
                byte[] cacb = new byte[vc.Width * vc.Height * 4 * 2];
                Parallel.For(0L, vc.Width * vc.Height * 4 , (i) =>
                {
                    //s[i] = new SEComplex(b[i], 0);
                    SEColor c = new SEColor(s[i].X, s[i].Y);
                    cacb[i] = (byte)c.ToGDIColor().R;
                    cacb[i + 1] = (byte)c.ToGDIColor().G;
                });
                for (int x = 0; x < vc.Width * 2; x++)
                {
                    for (int y = 0; y < vc.Height * 2; y++)
                    {
                        int ind = x +y * vc.Width * 2;
                        cacima[x, y] = new Rgba32(cacb[ind], cacb[ind + 1], cacb[ind + 2], cacb[ind + 3]);
                    }
                }
                cacima.SaveAsPng(cac1);
            }
        }
    }
}
