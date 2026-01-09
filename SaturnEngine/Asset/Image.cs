using SaturnEngine.Base;
using SaturnEngine.Global;
using SaturnEngine.Management.IO;
using SaturnEngine.Security;
using SaturnEngine.SEMath;
using Silk.NET.Core;
using SixLabors.ImageSharp;
using System.IO.Compression;
using System.Runtime.InteropServices;
using static SaturnEngine.SEMath.Helper;


namespace SaturnEngine.Asset
{
    public class SEImageFile : SEBase
    {
        SixLabors.ImageSharp.Image? im;
        public Vector2D Size;
        public Image? BaseImage { get =>im; set=>im=value; }
        public bool IsLoaded
        {
            get
            {
                return im != null;
            }
        }
        ~SEImageFile()
        {
            if (im != null)
            {
                im.Dispose();
                im = null;
            }
        }
        public SEImageFile()
        {
            //im = SixLabors.ImageSharp.Image.Load("");
            //im.CloneAs()
        }
        public void CreateEmpty(int width, int height)
        {
            im = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
            Size = new Vector2D(im.Width, im.Height);
        }
        public void CreateEmpty(Vector2D siz)
        {
            im = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>((int)Math.Ceiling(siz.X), (int)Math.Ceiling(siz.Y));
            Size = new Vector2D(im.Width, im.Height);
        }
        public void DisposeImage()
        {
            if (im != null)
            {
                im.Dispose();
                im = null;
            }
        }
        public void SaveImageToPNGFile(string path)
        {
            if (im != null)
            {
                im.SaveAsPng(path);
            }
        }
        public void CreateWithColor(Vector2D siz, SEColor clo)
        {
            var gc = clo.ToGDIColor();
            im = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>((int)Math.Ceiling(siz.X), (int)Math.Ceiling(siz.Y), new SixLabors.ImageSharp.PixelFormats.Rgba32(gc.R, gc.G, gc.B, gc.A));

            Size = new Vector2D(im.Width, im.Height);
        }
        public void CreateWithColor(int width, int height, SEColor clo)
        {
            var gc = clo.ToGDIColor();
            im = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height, new SixLabors.ImageSharp.PixelFormats.Rgba32(gc.R, gc.G, gc.B, gc.A));

            Size = new Vector2D(im.Width, im.Height);
        }
        public void LoadImageFromLRL(LRL l, ulong nmstc, ulong psstc = 0)
        {

            try
            {
                int id = l.SearchByName(nmstc);
                if (psstc != 0 && l.BKs[id].data.NeedDecrypt)
                {
                    l.BKs[id].data.Decrypt(psstc);
                }
                im = SixLabors.ImageSharp.Image.Load(l.BKs[id].data.ReadAllInBytes());
                Size = new Vector2D(im.Width, im.Height);
            }
            catch (Exception ex)
            {
                if (GVariables.GlobalResources.TryGetValue(GVariables.DefaultEngineResources, out LRL lc))
                    LoadImageFromLRL(lc, STCCode.GetSTC("./icon.png"), GVariables.DefaultEngineResourcesPassword);
            }
        }
        public void LoadImageFromLRLEx(LRL l, ulong nmstc, ulong psstc = 0, int count = 0)
        {


            try
            {
                int id = l.SearchByName(nmstc);
                if (psstc != 0 && l.BKs[id].data.NeedDecrypt)
                {
                    l.BKs[id].data.Decrypt(psstc);
                }
                im = SixLabors.ImageSharp.Image.Load(l.BKs[id].data.Read(count, 0));
                //SixLabors.ImageSharp.Image.Load();
                Size = new Vector2D(im.Width, im.Height);
            }
            catch
            {
                if (GVariables.GlobalResources.TryGetValue(GVariables.DefaultEngineResources, out LRL lc))
                    LoadImageFromLRL(lc, STCCode.GetSTC("./icon.png"), GVariables.DefaultEngineResourcesPassword);
            }
        }
        public void LoadImageFromLRLExC(LRL l, ulong nmstc, ulong psstc = 0, int count = 0, int offset = 0)
        {
            try
            {
                int id = l.SearchByName(nmstc);
                if (psstc != 0 && l.BKs[id].data.NeedDecrypt)
                {
                    l.BKs[id].data.Decrypt(psstc);
                }
                List<byte> ccb = l.BKs[id].data.Read(count).ToList();
                ccb.RemoveRange(0, offset);
                im = SixLabors.ImageSharp.Image.Load(ccb.ToArray());
                //SixLabors.ImageSharp.Image.Load();
                Size = new Vector2D(im.Width, im.Height);
            }
            catch
            {
                if (GVariables.GlobalResources.TryGetValue(GVariables.DefaultEngineResources, out LRL lc))
                    LoadImageFromLRL(lc, STCCode.GetSTC("./icon.png"), GVariables.DefaultEngineResourcesPassword);
            }
        }
        /// <summary>
        /// 引擎资源与孪生程序资源
        /// </summary>
        /// <param name="nmstc"></param>
        /// <param name="psstc"></param>
        public void LoadImageFromGlobalResource(ulong nmstc, ulong psstc = 0)
        {
            if (GVariables.GlobalResources.TryGetValue(GVariables.DefaultEngineResources, out LRL l))
            {
                LoadImageFromLRL(l, nmstc, psstc);
            }

        }
        public void LoadImageFromGlobalResourceEx(ulong nmstc, ulong psstc = 0, int count = 0)
        {
            if (GVariables.GlobalResources.TryGetValue(GVariables.DefaultEngineResources, out LRL l))
            {
                LoadImageFromLRLEx(l, nmstc, psstc, count);
            }

        }
        public void LoadImageFromGlobalResourceExC(ulong nmstc, ulong psstc = 0, int count = 0, int offset = 0)
        {
            if (GVariables.GlobalResources.TryGetValue(GVariables.DefaultEngineResources, out LRL l))
            {
                LoadImageFromLRLExC(l, nmstc, psstc, count, offset);
            }

        }
        public void LoadImageFromFile(string path)
        {
            try
            {
                im = SixLabors.ImageSharp.Image.Load(path);
                Size = new Vector2D(im.Width, im.Height);
            }
            catch
            {
                if (GVariables.GlobalResources.TryGetValue(GVariables.DefaultEngineResources, out LRL lc))
                    LoadImageFromLRL(lc, STCCode.GetSTC("./icon.png"), GVariables.DefaultEngineResourcesPassword);
            }
        }
        public SixLabors.ImageSharp.Image? GetImage()
        {
            return im;
        }
    }
    [Obsolete]
    public unsafe class PNG
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct PLTE_IndexColor
        {
            public byte R;
            public byte G;
            public byte B;
            public byte A;
            public bool UsingA;
            public PLTE_IndexColor(byte r = 0, byte g = 0, byte b = 0)
            {
                R = r; G = g; B = b;
                A = 0;
                UsingA = false;
            }
        }
        public Silk.NET.GLFW.Image GetGLFLImage()
        {
            Silk.NET.GLFW.Image i = new Silk.NET.GLFW.Image();
            fixed (byte* b = PIXCODE)
            {
                i.Pixels = b;
            }
            i.Height = (int)Y;
            i.Width = (int)X;
            return new Silk.NET.GLFW.Image();
        }
        public RawImage GetRawImage()
        {
            RawImage ri = new RawImage((int)X, (int)Y, new Memory<byte>(RawFile, 0, RawFile.Length));
            return ri;
        }
        public static PNG LoadFromFile(string path)
        {
            //不会搞，不搞
            PNG p = new PNG();
            BinaryOperator br = new BinaryOperator(File.OpenRead(path));
            p.FilePath = path;

            MemoryStream msf = new MemoryStream();

            byte[] rdf = new byte[16384];
            int rt = br.Read(rdf, 0, rdf.Length);
            while (rt > 0)
            {
                msf.Write(rdf, 0, rt);
                rt = br.Read(rdf, 0, rdf.Length);
            }
            br.Seek(0, SeekOrigin.Begin);
            rdf = null;
            p.RawFile = new byte[msf.Length];
            for (int i = 0; i < p.RawFile.Length; i++)
            {
                p.RawFile[i] = (byte)msf.ReadByte();
            }
            msf.Close();
            ulong st = br.ReadUInt64();
            if (st != 727905341920923785)
            {
                //Not PNG File
                throw new Exception("Not PNG File");
            }
            //IHDR
            int len = Helper.BEToLE(br.ReadInt32());


            byte[] b;
            b = br.ReadBytes(len + 4);
            Console.WriteLine(br.CanSeek);
            br.Seek(12, SeekOrigin.Begin);

            char IHDR_I = br.ReadChar();
            char IHDR_H = br.ReadChar();
            char IHDR_D = br.ReadChar();
            char IHDR_R = br.ReadChar();
            /*
            DataLayout dl = new DataLayout();
            dl.B0 = b[4];
            dl.B1 = b[5];
            dl.B2 = b[6];
            dl.B3 = b[7];
            dl.B4 = b[8];
            dl.B5 = b[9];
            dl.B6 = b[10];
            dl.B7 = b[11];
            b[4] = dl.B3;
            b[5] = dl.B2;
            b[6] = dl.B1;
            b[7] = dl.B0;
            b[8] = dl.B7;
            b[9] = dl.B6;
            b[10] = dl.B5;
            b[11] = dl.B4;*/

            p.X = Helper.BEToLE(br.ReadInt32());
            p.Y = Helper.BEToLE(br.ReadInt32());
            p.Depth = br.ReadUInt8();
            p.ColorType = br.ReadUInt8();
            p.ComdivssionMethod = br.ReadUInt8();
            p.FilterMethod = br.ReadUInt8();
            p.InterlaceMethod = br.ReadUInt8();

            uint crc = /*LEToBE*/(br.ReadUInt32());
            /*一堆错，不判断CRC*/
            /*
            uint g = CRC32.GetCRC32(b, 0, b.LongLength);
            if(g != crc)
            {
                throw new Exception("数据损坏");
                
            }
            else
            {
                Console.WriteLine("OK");
            }*/
            string idn = "";
            MemoryStream ms = new MemoryStream();
        Loop:
            idn = "";
            len = Helper.BEToLE(br.ReadInt32());
            idn += br.ReadChar();
            idn += br.ReadChar();
            idn += br.ReadChar();
            idn += br.ReadChar();
            if (idn == "PLTE")
            {
                if (len % 3 != 0)
                {
                    throw new Exception("PLTE长度不正确");
                }
                for (int i = 0; i < len / 3; i++)
                {
                    p.IndexColors.Add(new PLTE_IndexColor(br.ReadUInt8(), br.ReadUInt8(), br.ReadUInt8()));
                }
                crc = br.ReadUInt32();
                goto Loop;
            }
            else if (idn == "tRNS")
            {
                byte a = 0;
                for (int i = 0; i < len; i++)
                {
                    a = br.ReadUInt8();
                    PLTE_IndexColor pi = p.IndexColors[i];
                    pi.A = a;
                    pi.UsingA = true;
                    p.IndexColors[i] = pi;
                }
            }
            else if (idn == "pHYs")
            {


                p.PPUX = LEToBE(br.ReadUInt32());
                p.PPUY = LEToBE(br.ReadUInt32());
                p.UnitSpecifier = br.ReadUInt8();
                crc = br.ReadUInt32();
                goto Loop;
            }
            else if (idn == "IDAT")
            {
                //byte[] data2 = new byte[len];
                //br.Read(data2, 0, len);
                for (int i = 0; i < len; i++)
                {
                    ms.WriteByte(br.ReadUInt8());
                }
                crc = br.ReadUInt32();
                goto Loop;
            }
            else if (idn == "IEND")
            {
                int bytesPerPixel = 4; //argb only 32bit
                int scanlineLength = (int)(p.X * bytesPerPixel);
                int totalBytes = scanlineLength * (int)p.Y;

                byte[] decompressedData = new byte[totalBytes];
                ms.Seek(0, SeekOrigin.Begin);

                using (ZLibStream ds = new ZLibStream(ms, CompressionMode.Decompress))
                {
                    ds.Read(decompressedData, 0, decompressedData.Length);
                }

                byte[] reconstructedData = new byte[totalBytes];

                for (int y = 0; y < p.Y; y++)
                {
                    int scanlineOffset = y * scanlineLength;
                    byte filterType = decompressedData[scanlineOffset];

                    // The first byte of each scanline is the filter type
                    for (int x = 1; x < scanlineLength; x++)
                    {
                        int currentPos = scanlineOffset + x;
                        byte currentByte = decompressedData[currentPos];

                        switch (filterType)
                        {
                            case 0: // None
                                reconstructedData[currentPos] = currentByte;
                                break;

                            case 1: // Sub
                                if (x > bytesPerPixel)
                                {
                                    reconstructedData[currentPos] = (byte)(currentByte + reconstructedData[currentPos - bytesPerPixel]);
                                }
                                else
                                {
                                    reconstructedData[currentPos] = currentByte;
                                }
                                break;

                            case 2: // Up
                                if (y > 0)
                                {
                                    reconstructedData[currentPos] = (byte)(currentByte + reconstructedData[currentPos - scanlineLength]);
                                }
                                else
                                {
                                    reconstructedData[currentPos] = currentByte;
                                }
                                break;

                            case 3: // Average
                                int left = (x > bytesPerPixel) ? reconstructedData[currentPos - bytesPerPixel] : 0;
                                int above = (y > 0) ? reconstructedData[currentPos - scanlineLength] : 0;
                                reconstructedData[currentPos] = (byte)(currentByte + ((left + above) / 2));
                                break;

                            case 4: // Paeth
                                byte a = (x > bytesPerPixel) ? reconstructedData[currentPos - bytesPerPixel] : (byte)0;
                                byte b1 = (y > 0) ? reconstructedData[currentPos - scanlineLength] : (byte)0;
                                byte c = (x > bytesPerPixel && y > 0) ? reconstructedData[currentPos - scanlineLength - bytesPerPixel] : (byte)0;
                                reconstructedData[currentPos] = (byte)(currentByte + PaethPredictor(a, b1, c));
                                break;

                            default:
                                throw new Exception($"Unknown filter type: {filterType}");
                        }
                    }
                }

                p.PIXCODE = reconstructedData;
                Console.WriteLine($"Filter method {p.FilterMethod}, INFO: IMAGE:X:{p.X}|Y:{p.Y}|PPUX:{p.PPUX}|PPUY:{p.PPUY}");
            }
            else
            {
                br.ReadBytes(len);
                crc = br.ReadUInt32();
                goto Loop;
            }
            br.Close();
            return p;
        }
        //by ds
        private static byte PaethPredictor(byte a, byte b, byte c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);

            if (pa <= pb && pa <= pc)
            {
                return a;
            }
            else if (pb <= pc)
            {
                return b;
            }
            else
            {
                return c;
            }
        }
        public string FilePath { get; private set; }
        public long X { get; private set; }
        public long Y { get; private set; }
        public byte Depth { get; private set; }
        public byte ColorType { get; private set; }
        public byte ComdivssionMethod { get; private set; }
        public byte FilterMethod { get; private set; }
        public byte InterlaceMethod { get; private set; }
        public uint PPUX { get; private set; }
        public uint PPUY { get; private set; }
        public byte UnitSpecifier { get; private set; }

        public byte[] PIXCODE;

        public byte[] RawFile;
        public List<PLTE_IndexColor> IndexColors { get; private set; }

        public byte[] GetRGBABytes()
        {
            //argb to rgba
            byte[] rgba = new byte[PIXCODE.Length];
            /*
            for (int i = 0; i < PIXCODE.Length; i += 4)
            {
                rgba[i] = PIXCODE[i + 1];     // R
                rgba[i + 1] = PIXCODE[i + 2]; // G
                rgba[i + 2] = PIXCODE[i + 3];     // B
                rgba[i + 3] = PIXCODE[i]; // A
            }
            */
            Parallel.For(0, PIXCODE.LongLength / 4, (i) =>
            {
                rgba[i * 4] = PIXCODE[i * 4 + 1];     // R
                rgba[i * 4 + 1] = PIXCODE[i * 4 + 2]; // G
                rgba[i * 4 + 2] = PIXCODE[i * 4 + 3];     // B
                rgba[i * 4 + 3] = PIXCODE[i * 4]; // A
            });
            return rgba;
        }
        public byte[] GetARGBBytes()
        {
            return PIXCODE;
        }
        public PNG()
        {
            IndexColors = new List<PLTE_IndexColor>();
            PIXCODE = new byte[0];
            FilePath = "";
            RawFile = new byte[0];
        }
    }
}
