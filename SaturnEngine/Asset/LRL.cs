using SaturnEngine.Base;
using SaturnEngine.Management.IO;
using SaturnEngine.Management.SEMemory;
using SaturnEngine.Security;
using System.IO.Compression;
using static SaturnEngine.SEMath.Helper;

namespace SaturnEngine.Asset
{
    public class LRL : SEBase
    {

        public LRL(bool StreamingLoad = true)
        {
            StreamLoad = StreamingLoad;
        }

        public const string LOSF = "LOSF"; // LosResourcesLib File
        public readonly byte[] LOSF_B = [0x4C, 0x4F, 0x53, 0x46]; // LosResourcesLib File
        public const string BK = "BK"; // Box
        public readonly byte[] BK_B = [0x42, 0x4B]; // Box
        public const string Ext = ".lrl";
        public const string PT_Ext = ".lrl.ext";
        public const string ExtFilter = "*.lrl";
        public const string PT_ExtFilter = "*.lrl.ext";
        public const string ExtSFDFilter = "(lrl文件)|*.lrl";
        public const string PT_ExtSFDFilter = "(LRL分卷文件)|*.lrl.ext";
        public const string Ext_R = "{0}.lrl";
        public const string PT_Ext_R = "{0}.lrl.ext";
        public const string Ext_PTH = "{0}/{1}.lrl";
        public const string PT_Ext_PTH = "{0}/{1}.lrl.ext";
        public const string LRL_Default_Box_Name = "LRL.Box";
        public const string LRL_Default_Box_Name_R = "LRL.Box{0}";
        public readonly VERSION LRLVersion = new VERSION(new Version(1, 24, 3, 9)); // LosResourcesLib Version



        public bool StreamLoad = true; // 是否流式加载
        //选择流式加载时，若文件改动，保存时，需要全部读取后重新写入，且性能受限于磁盘性能
        public bool ExitFile = false;
        public bool OnlyStream = false;
        [Flags]
        public enum LRLFlag : ushort
        {
            None = 0x0000,
            Allow_CrossFile = 0x0001, // 允许跨文件合并(v1.15)，简称分卷（1.15仅支持分卷单独盒子，可多个分卷）
            Allow_MultiBox = 0x0002, // 允许多盒子合并(v1.17)，就在本体的盒子与分卷的盒子合并（分卷仅能容纳一个盒子或一个盒子的分身）
            Allow_StreamLoad = 0x0004,  //允许流式加载(v1.9)
            Allow_Encrypt = 0x0008, // 允许加密(v1.16)
            Allow_ExtendEXT = 0x0010, // 允许扩展其他项目类型 eg. LRL_IMG(利用LRL结构的全新图片格式) LRL_PNG(将PNG块拆分存LRL中)等 ，注，仅限单个文件，不可数据混用(v2.1)
            Allow_Compress = 0x0020, // 允许压缩(v1.27)，压缩默认GZip
            
            Allow_All = 0xffff
        }
        [Flags]
        public enum LRLExtDataType : ushort
        {
            None = 0x0000,
            Ext_CrossFilePaths = 0x0001, // 跨文件合并路径列表（名字），在此仅为声明，以供提前加载，默认UTF8编码，而且分卷必须在同一目录下
            Ext_Description = 0x0002, // 描述
            Ext_StreamingBoxesList = 0x0004  // 在流式加载下指示数据盒子名字与偏移量
        }
        [Flags]
        public enum LRBKFlag : ushort
        {
            None = 0x0000,
            CrossFile = 0x0001, // 允许跨文件合并，在此选项下，长度BK中LENGTH无效，由分卷决定
            MultiBox = 0x0002, // 允许多盒子合并
            Encrypt = 0x0004, // 允许加密
            Compress = 0x0008 // 允许压缩
        }
        [Flags]
        public enum LRBKExtDataType : ushort
        {
            None = 0x0000,
            Ext_CrossFileNames = 0x0001, // 跨文件合并文件名列表以及合并的盒子名字STC（盒子STC为扩展数据）
            Ext_BoxName = 0x0002, // 盒子名字STC，对于没有名字的盒子，则会按照顺序编号(转换为STC)（并不会有此项） eg LRL.Box1  LRL.Box2
            Ext_Encrypt = 0x0004, // 加密扩展数据，加密密码STC的STC
            Ext_Description = 0x0008, // 描述UTF8
            Ext_BoxNameString = 0x0010 // 盒子名字字符串，UTF8编码的字符串
        }
        public static bool HasFlag(object m, object flag)
        {
            return ((ushort)m & (ushort)flag) == (ushort)flag;
        }

        public class ExtDT
        {
            public byte[] dt = new byte[0];
            public LRLExtDataType t;
        }

        public class LRBK
        {
            public class BKExtDT
            {
                public byte[] dt = new byte[0];
                public LRBKExtDataType t;
            }
            public ulong NameSTC;
            public LRBKFlag flg;
            public ushort ExtDataCount;
            public BKExtDT[] Exts = new BKExtDT[0];
            public ulong Length;
            public LRStream data;
            public bool alc = false;//扩展
            public bool Loaded = false;

            public bool Encrypt = false; // 是否加密

        }

        public Stream dts;
        public string? FP;
        public string BSDir = "./";
        public ulong DEV;
        public VERSION vi;
        public TIME ti;
        public LRLFlag FLG;
        public ushort ExtDataCount;
        public ExtDT[] Exts = new ExtDT[0];
        public ulong HDSTC;
        public uint BoxCount;
        public LRBK[] BKs = new LRBK[0];
        public List<ulong> nmstcs = new List<ulong>();
        public List<long> offsets = new List<long>();
        public bool usnmlst = false;

        public bool Changed = false;
        public void UnLockStream(uint id)
        {
            if (!BKs[id].alc)
            {
                BKs[id].alc = true;
                SEMemoryStream ms = new SEMemoryStream();
                byte[] buf = new byte[40960000];
                int rd = 0;
                while ((rd = BKs[id].data.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, rd);
                }
                Changed = true;
                if (BKs[id].Encrypt)
                {
                    ulong ssss = 0;
                    DataLayout dl = new DataLayout();
                    for (uint i = 0; i < BKs[id].ExtDataCount; i++)

                    {
                        if (BKs[id].Exts[i].t == LRBKExtDataType.Ext_Encrypt)
                        {
                            dl.B0 = BKs[id].Exts[i].dt[0];
                            dl.B1 = BKs[id].Exts[i].dt[1];
                            dl.B2 = BKs[id].Exts[i].dt[2];
                            dl.B3 = BKs[id].Exts[i].dt[3];
                            dl.B4 = BKs[id].Exts[i].dt[4];
                            dl.B5 = BKs[id].Exts[i].dt[5];
                            dl.B6 = BKs[id].Exts[i].dt[6];
                            dl.B7 = BKs[id].Exts[i].dt[7];
                            ssss = dl.UL;
                        }
                    }
                    BKs[id].data = new LRStream(ms, 0, 0, false, true, ssss);
                }
                else
                {
                    BKs[id].data = new LRStream(ms, 0, 0, false);
                }
            }

        }
        public void CreateNewFile(string fp, LRLFlag fg = LRLFlag.None)
        {
            if (StreamLoad)
            {
                FP = fp;
                BSDir = Path.GetDirectoryName(fp);
                dts = File.Open(fp, FileMode.Create);
                ExitFile = false;
                OnlyStream = false;
            }
            else
            {

                FP = fp;
                BSDir = Path.GetDirectoryName(fp);
                dts = new SEMemoryStream();
                ExitFile = true;
                OnlyStream = false;
            }
            vi = LRLVersion;
            ti = new TIME(DateTime.Now);
            FLG = fg;
            ExtDataCount = 0;
            Exts = new ExtDT[0];
            HDSTC = 0;
            BoxCount = 0;
            BKs = new LRBK[0];
            Changed = true;
        }
        public void CreateNewStream(Stream s, LRLFlag fg = LRLFlag.None)
        {
            dts = s;
            OnlyStream = true;
            ExitFile = true;
            vi = LRLVersion;
            ti = new TIME(DateTime.Now);
            FLG = fg;
            ExtDataCount = 0;
            Exts = new ExtDT[0];
            HDSTC = 0;
            BoxCount = 0;
            BKs = new LRBK[0];
            Changed = true;
        }
        public void LoadFromFile(string fp)
        {
            if (File.Exists(fp))
            {
                if (StreamLoad)
                {
                    FP = fp;
                    BSDir = Path.GetDirectoryName(fp);

                    ExitFile = false;
                    OnlyStream = false;
                    LoadFromStream(File.Open(fp, FileMode.Open));
                }
                else
                {
                    FP = fp;
                    BSDir = Path.GetDirectoryName(fp);
                    OnlyStream = false;
                    ExitFile = true;
                    SEMemoryStream ms = new SEMemoryStream();
                    using (FileStream fs = File.Open(fp, FileMode.Open))
                    {
                        byte[] bf = new byte[10240000];
                        int rd = 0;
                        while ((rd = fs.Read(bf, 0, bf.Length)) > 0)
                        {
                            ms.Write(bf, 0, rd);
                        }
                    }
                    LoadFromStream(ms);
                }
            }
            else
            {
                throw new FileNotFoundException("File not found", fp);
            }
        }
        public bool TryGet(string name, out LRBK box, bool unicode = false)
        {
            int id = SearchByName(name, unicode);
            if (id >= 0)
            {
                box = BKs[id];
                return true;
            }
            box = null;
            return false;
        }
        public bool TryGet(ulong stc, out LRBK box)
        {
            int id = SearchByName(stc);
            if (id >= 0)
            {
                box = BKs[id];
                return true;
            }
            box = null;
            return false;
        }
        public LRBK Get(string name, bool unicode = false)
        {
            int id = SearchByName(name, unicode);
            if (id >= 0)
            {
                return BKs[id];
            }
            throw new KeyNotFoundException("Box not found: " + name);
        }
        public LRBK Get(ulong stc)
        {
            int id = SearchByName(stc);
            if (id >= 0)
            {
                return BKs[id];
            }
            throw new KeyNotFoundException("Box not found: " + stc);
        }
        public int SearchByName(string name, bool unicode = false)
        {
            ulong stc = STCCode.GetSTC(name, unicode);
            int rs = nmstcs.IndexOf(stc);
            if (rs < 0)
            {
                for (uint i = 0; i < BoxCount; i++)
                {
                    if (BKs[i].NameSTC == stc)
                    {
                        return (int)i;
                    }
                }
            }
            return rs;
        }
        public int SearchByName(ulong stc)
        {

            int rs = nmstcs.IndexOf(stc);
            if (rs < 0)
            {
                for (uint i = 0; i < BoxCount; i++)
                {
                    if (BKs[i].NameSTC == stc)
                    {
                        return (int)i;
                    }
                }
            }
            return rs;
        }
        public long extstreamoffset = 0;
        public void LoadFromStream(Stream s, long stoffset = 0)
        {
            dts = s;
            s.Seek(stoffset, SeekOrigin.Begin);
            extstreamoffset = stoffset;
            BinaryOperator bo = new BinaryOperator(s);
            var headbts = bo.ReadBytes(4);
            if (!headbts.SequenceEqual(LOSF_B))
            {
                throw new InvalidDataException("LRL头损坏".GetInCurrLang());
            }
            vi = new VERSION(bo.ReadUInt32());
            ti = new TIME(bo.ReadUInt64());
            DEV = bo.ReadUInt64();
            FLG = (LRLFlag)bo.ReadUInt16();
            ExtDataCount = bo.ReadUInt16();
            Exts = new ExtDT[ExtDataCount];
            for (int i = 0; i < ExtDataCount; i++)
            {
                Exts[i] = new ExtDT();
                uint leg = bo.ReadUInt32();//并不会超过1GB，超过1GB的扩展信息那还的了？？？
                Exts[i].t = (LRLExtDataType)bo.ReadUInt16();
                Exts[i].dt = bo.ReadBytes(leg);
                if (Exts[i].t == LRLExtDataType.Ext_StreamingBoxesList && HasFlag(FLG, LRLFlag.Allow_StreamLoad) && StreamLoad)
                {
                    usnmlst = true;
                    long co = Exts[i].dt.LongLength / 8 / 2;
                    bo.Seek(-(co * 8 * 2), SeekOrigin.Current);
                    for (long io = 0; io < co; io++)
                    {
                        nmstcs.Add(bo.ReadUInt64());
                        offsets.Add(bo.ReadInt64());
                    }
                }
            }
            BoxCount = bo.ReadUInt32();
            long bts = bo.Position - stoffset;

            HDSTC = bo.ReadUInt64();
            long ps = bo.Position;
            bo.Seek(stoffset, SeekOrigin.Begin);

            ulong stcchk = STCCode.GetSTC(bo.ReadBytes(bts));
            bo.Seek(ps, SeekOrigin.Begin);
            if (stcchk != HDSTC)
            {
                throw new InvalidDataException("LRL头损坏".GetInCurrLang());
            }
            BKs = new LRBK[BoxCount];
            if (!usnmlst)
            {
                uint unnmd = 0;
                for (uint i = 0; i < BoxCount; i++)
                {
                    offsets.Add(bo.Position);
                    if (!BK_B.SequenceEqual(bo.ReadBytes(2)))
                    {
                        throw new InvalidDataException("盒子头损坏".GetInCurrLang());
                    }
                    BKs[i] = new LRBK()
                    {
                        flg = (LRBKFlag)bo.ReadUInt16(),
                        ExtDataCount = bo.ReadUInt16(),

                    };
                    BKs[i].Exts = new LRBK.BKExtDT[BKs[i].ExtDataCount];
                    bool nmd = false;
                    ulong ssss = 0;
                    for (int ip = 0; ip < BKs[i].ExtDataCount; ip++)
                    {
                        BKs[i].Exts[ip] = new LRBK.BKExtDT();
                        uint dtc = bo.ReadUInt32();
                        BKs[i].Exts[ip].t = (LRBKExtDataType)bo.ReadUInt16();

                        BKs[i].Exts[ip].dt = bo.ReadBytes(dtc);
                        if (BKs[i].Exts[ip].t == LRBKExtDataType.Ext_BoxName)
                        {
                            nmd = true;
                            DataLayout dl = new DataLayout();
                            dl.B0 = BKs[i].Exts[ip].dt[0];
                            dl.B1 = BKs[i].Exts[ip].dt[1];
                            dl.B2 = BKs[i].Exts[ip].dt[2];
                            dl.B3 = BKs[i].Exts[ip].dt[3];
                            dl.B4 = BKs[i].Exts[ip].dt[4];
                            dl.B5 = BKs[i].Exts[ip].dt[5];
                            dl.B6 = BKs[i].Exts[ip].dt[6];
                            dl.B7 = BKs[i].Exts[ip].dt[7];
                            BKs[i].NameSTC = dl.UL;
                        }
                        if (BKs[i].Exts[ip].t == LRBKExtDataType.Ext_Encrypt)
                        {
                            DataLayout dl = new DataLayout();
                            dl.B0 = BKs[i].Exts[ip].dt[0];
                            dl.B1 = BKs[i].Exts[ip].dt[1];
                            dl.B2 = BKs[i].Exts[ip].dt[2];
                            dl.B3 = BKs[i].Exts[ip].dt[3];
                            dl.B4 = BKs[i].Exts[ip].dt[4];
                            dl.B5 = BKs[i].Exts[ip].dt[5];
                            dl.B6 = BKs[i].Exts[ip].dt[6];
                            dl.B7 = BKs[i].Exts[ip].dt[7];
                            ssss = dl.UL;
                        }
                    }
                    if (!nmd)
                    {
                        BKs[i].NameSTC = STCCode.GetSTC($"{LRL_Default_Box_Name}{unnmd}");
                        unnmd++;
                    }
                    nmstcs.Add(BKs[i].NameSTC);
                    BKs[i].Length = bo.ReadUInt64();
                    if (HasFlag(BKs[i].flg, LRBKFlag.Encrypt))
                    {
                        BKs[i].Encrypt = true;
                        if (HasFlag(BKs[i].flg, LRBKFlag.CrossFile))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_CrossFile))
                                throw new NotSupportedException("文件未允许".GetInCurrLang());
                            if (!File.Exists(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC)))
                            {
                                throw new FileNotFoundException(BKs[i].NameSTC + "的一部分分卷文件未找到".GetInCurrLang());
                            }
                            if (HasFlag(BKs[i].flg, LRBKFlag.Compress))
                            {
                                if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                    throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                                BKs[i].data = new LRStream(new GZipStream(File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open), CompressionLevel.SmallestSize), 0, 0, false, true, ssss);
                            }
                            else
                            {
                                BKs[i].data = new LRStream(File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open), 0, 0, false, true, ssss);
                            }

                        }
                        else
                        {
                            if (HasFlag(BKs[i].flg, LRBKFlag.Compress))
                            {
                                if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                    throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                                BKs[i].data = new LRStream(new GZipStream(bo, CompressionLevel.Fastest), bo.Position, (long)BKs[i].Length, false, true, ssss);
                            }
                            else
                            {
                                BKs[i].data = new LRStream(bo, bo.Position, (long)BKs[i].Length, true, true, ssss);
                            }
                        }
                    }
                    else
                    {
                        if (HasFlag(BKs[i].flg, LRBKFlag.CrossFile))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_CrossFile))
                                throw new NotSupportedException("文件未允许".GetInCurrLang());
                            if (!File.Exists(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC)))
                            {
                                throw new FileNotFoundException(BKs[i].NameSTC + "的一部分分卷文件未找到".GetInCurrLang());
                            }
                            if (HasFlag(BKs[i].flg, LRBKFlag.Compress))
                            {
                                if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                    throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                                BKs[i].data = new LRStream(new GZipStream(File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open), CompressionLevel.SmallestSize), 0, 0, false);
                            }
                            else
                            {
                                //BKs[i].data = new LRStream(File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open), 0, 0, false);
                                BKs[i].data = new LRStream(File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open), 0, 0, false);
                            }
                        }
                        else
                        {
                            if (HasFlag(BKs[i].flg, LRBKFlag.Compress))
                            {
                                if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                    throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                                BKs[i].data = new LRStream(new GZipStream(bo, CompressionLevel.Fastest), bo.Position, (long)BKs[i].Length);
                            }
                            else
                            {
                                BKs[i].data = new LRStream(bo, bo.Position, (long)BKs[i].Length);
                                //BKs[i].data = new LRStream(bo, bo.Position, (long)BKs[i].Length);
                            }
                        }
                    }
                    bo.Seek((long)BKs[i].Length, SeekOrigin.Current);
                    BKs[i].Loaded = true;
                }
            }
            else
            {
                for (uint i = 0; i < BoxCount; i++)
                {
                    BKs[i] = new LRBK();
                }
            }
        }
        public void LoadBox(int i)
        {
            //var BKs[i];// = BKs[i];
            BinaryOperator bo = new BinaryOperator(dts);
            if (!BKs[i].Loaded)
            {
                if (offsets[i] != -1)
                {
                    bo.Seek(offsets[i], SeekOrigin.Begin);
                }
                else
                    throw new Exception();

                if (BK_B.SequenceEqual(bo.ReadBytes(2)))
                {
                    throw new InvalidDataException("盒子头损坏".GetInCurrLang());
                }
                BKs[i].flg = (LRBKFlag)bo.ReadUInt16();
                BKs[i].ExtDataCount = bo.ReadUInt16();
                BKs[i].Exts = new LRBK.BKExtDT[BKs[i].ExtDataCount];
                bool nmd = false;
                ulong ssss = 0;
                for (int ip = 0; ip < BKs[i].ExtDataCount; ip++)
                {
                    BKs[i].Exts[ip] = new LRBK.BKExtDT();
                    uint dtc = bo.ReadUInt32();
                    BKs[i].Exts[ip].t = (LRBKExtDataType)bo.ReadUInt16();

                    BKs[i].Exts[ip].dt = bo.ReadBytes(dtc);

                    if (BKs[i].Exts[ip].t == LRBKExtDataType.Ext_BoxName)
                    {
                        nmd = true;
                        DataLayout dl = new DataLayout();
                        dl.B0 = BKs[i].Exts[ip].dt[0];
                        dl.B1 = BKs[i].Exts[ip].dt[1];
                        dl.B2 = BKs[i].Exts[ip].dt[2];
                        dl.B3 = BKs[i].Exts[ip].dt[3];
                        dl.B4 = BKs[i].Exts[ip].dt[4];
                        dl.B5 = BKs[i].Exts[ip].dt[5];
                        dl.B6 = BKs[i].Exts[ip].dt[6];
                        dl.B7 = BKs[i].Exts[ip].dt[7];
                        BKs[i].NameSTC = dl.UL;
                    }
                    if (BKs[i].Exts[ip].t == LRBKExtDataType.Ext_Encrypt)
                    {
                        DataLayout dl = new DataLayout();
                        dl.B0 = BKs[i].Exts[ip].dt[0];
                        dl.B1 = BKs[i].Exts[ip].dt[1];
                        dl.B2 = BKs[i].Exts[ip].dt[2];
                        dl.B3 = BKs[i].Exts[ip].dt[3];
                        dl.B4 = BKs[i].Exts[ip].dt[4];
                        dl.B5 = BKs[i].Exts[ip].dt[5];
                        dl.B6 = BKs[i].Exts[ip].dt[6];
                        dl.B7 = BKs[i].Exts[ip].dt[7];
                        ssss = dl.UL;
                    }
                }
                BKs[i].Length = bo.ReadUInt64();
                if (!nmd)
                {
                    BKs[i].NameSTC = nmstcs[i];
                }
                else
                {
                    if (BKs[i].NameSTC != nmstcs[i])
                    {
                        throw new Exception("名字冲突！可能是文件被篡改！".GetInCurrLang() + $"{BKs[i].NameSTC}!={nmstcs[i]}");
                    }
                }
                if (HasFlag(BKs[i].flg, LRBKFlag.Encrypt))
                {
                    if (HasFlag(BKs[i].flg, LRBKFlag.CrossFile))
                    {
                        if (!HasFlag(FLG, LRLFlag.Allow_CrossFile))
                            throw new NotSupportedException("文件未允许".GetInCurrLang());
                        if (!File.Exists(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC)))
                        {
                            throw new FileNotFoundException(BKs[i].NameSTC + "的一部分分卷文件未找到".GetInCurrLang());
                        }
                        //BKs[i].data = new LRStream(File.Open(string.Format(PT_Ext_PTH, BKs[i].NameSTC), FileMode.Open), 0, 0, false, true, ssss);
                        if (HasFlag(BKs[i].flg, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            BKs[i].data = new LRStream(new GZipStream(File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open), CompressionLevel.SmallestSize), 0, 0, false, true, ssss);
                        }
                        else
                        {
                            BKs[i].data = new LRStream(File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open), 0, 0, false, true, ssss);
                        }
                    }
                    else
                    {
                        //BKs[i].data = new LRStream(bo, bo.Position, (long)BKs[i].Length, true, true, ssss);
                        if (HasFlag(BKs[i].flg, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            BKs[i].data = new LRStream(new GZipStream(bo, CompressionLevel.Fastest), bo.Position, (long)BKs[i].Length, false, true, ssss);
                        }
                        else
                        {
                            BKs[i].data = new LRStream(bo, bo.Position, (long)BKs[i].Length, true, true, ssss);
                        }
                    }
                }
                else
                {
                    if (HasFlag(BKs[i].flg, LRBKFlag.CrossFile))
                    {
                        if (!HasFlag(FLG, LRLFlag.Allow_CrossFile))
                            throw new NotSupportedException("文件未允许".GetInCurrLang());
                        if (!File.Exists(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC)))
                        {
                            throw new FileNotFoundException(BKs[i].NameSTC + "的一部分分卷文件未找到".GetInCurrLang());
                        }
                        //BKs[i].data = new LRStream(File.Open(string.Format(PT_Ext_PTH, BKs[i].NameSTC), FileMode.Open), 0, 0, false);
                        if (HasFlag(BKs[i].flg, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            BKs[i].data = new LRStream(new GZipStream(File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open), CompressionLevel.SmallestSize), 0, 0, false);
                        }
                        else
                        {
                            //BKs[i].data = new LRStream(File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open), 0, 0, false);
                            BKs[i].data = new LRStream(File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open), 0, 0, false);
                        }
                    }
                    else
                    {
                        //BKs[i].data = new LRStream(bo, bo.Position, (long)BKs[i].Length);
                        if (HasFlag(BKs[i].flg, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            BKs[i].data = new LRStream(new GZipStream(bo, CompressionLevel.Fastest), bo.Position, (long)BKs[i].Length);
                        }
                        else
                        {
                            BKs[i].data = new LRStream(bo, bo.Position, (long)BKs[i].Length);
                            //BKs[i].data = new LRStream(bo, bo.Position, (long)BKs[i].Length);
                        }
                    }
                }
                //bo.Seek((long)BKs[i].Length, SeekOrigin.Current);
                BKs[i].Loaded = true;
            }
        }
        public uint AddBox(Stream? data = null, long offset = 0, long length = -1, string? nm = null, LRBKFlag bf = LRBKFlag.None, KeyValuePair<LRBKExtDataType, byte[]>[] extdata = null, bool leaveclose = false,ulong passwordstc = 0)
        {
            Changed = true;

            LRBK bk = new LRBK();

            bk.flg = bf;
            string nam = nm == null ? $"{LRL_Default_Box_Name}{BKs.Length - 1}" : nm;
            ulong nmstc = STCCode.GetSTC(nam);
            if (leaveclose)
            {
                if (data != null)
                {
                    SEMemoryStream sem = new SEMemoryStream();
                    data.CopyTo(sem);
                    data.Close();
                    data = sem;
                }
            }
            if (nm != null)
            {
                if (extdata != null)
                {
                    bool hs = false;
                    for (int i = 0; i < extdata.Length; i++)
                    {
                        if (extdata[i].Key == LRBKExtDataType.Ext_BoxName)
                        {
                            hs = true;
                            break;
                        }
                    }
                    if (!hs)
                    {
                        extdata = extdata.Append(new KeyValuePair<LRBKExtDataType, byte[]>(LRBKExtDataType.Ext_BoxName, new DataLayout(nmstc).GetBytes())).ToArray();
                    }
                }
                else
                {
                    extdata = new KeyValuePair<LRBKExtDataType, byte[]>[0];
                    extdata = extdata.Append(new KeyValuePair<LRBKExtDataType, byte[]>(LRBKExtDataType.Ext_BoxName, new DataLayout(nmstc).GetBytes())).ToArray();
                }


            }
            bk.ExtDataCount = extdata == null ? (ushort)0 : (ushort)extdata.Length;
            bk.Exts = new LRBK.BKExtDT[bk.ExtDataCount];
            ulong ssss = 0;
            DataLayout dl = new DataLayout();
            for (int i = 0; i < bk.Exts.Length; i++)
            {
                if (extdata[i].Key == LRBKExtDataType.Ext_Encrypt)
                {
                    dl.B0 = extdata[i].Value[0];
                    dl.B1 = extdata[i].Value[1];
                    dl.B2 = extdata[i].Value[2];
                    dl.B3 = extdata[i].Value[3];
                    dl.B4 = extdata[i].Value[4];
                    dl.B5 = extdata[i].Value[5];
                    dl.B6 = extdata[i].Value[6];
                    dl.B7 = extdata[i].Value[7];
                    ssss = dl.UL;
                }
                bk.Exts[i] = new LRBK.BKExtDT()
                {
                    t = extdata[i].Key,
                    dt = extdata[i].Value
                };
            }


            if (HasFlag(bf, LRBKFlag.Encrypt))
            {
                bk.Encrypt = true;
                if (HasFlag(bf, LRBKFlag.CrossFile))
                {
                    if (!HasFlag(FLG, LRLFlag.Allow_CrossFile))
                        throw new NotSupportedException("文件未允许".GetInCurrLang());
                    Stream s = File.Create(string.Format(PT_Ext_PTH, BSDir, nmstc));
                    if (data != null)
                    {
                        

                        if (HasFlag(bf, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            bk.data = new LRStream(new GZipStream(s, CompressionLevel.SmallestSize), offset, length, false, true, ssss);
                        }
                        else
                        {
                            bk.data = new LRStream(s, offset, length, false, true, ssss);
                        }
                        
                        data.Seek(offset, SeekOrigin.Begin);
                        bk.data.Decrypt(passwordstc);
                        bk.data.Seek(0, SeekOrigin.Begin);

                        data.CopyTo(bk.data);
                        bk.data.Seek(0, SeekOrigin.Begin);
                        bk.data.CleanPassword();
                    }
                    else
                    {

                        if (HasFlag(bf, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            bk.data = new LRStream(new GZipStream(s, CompressionLevel.SmallestSize), 0, 0, false, true, ssss);
                        }
                        else
                        {
                            bk.data = new LRStream(s, 0, 0, false, true, ssss);
                        }
                    }
                }
                else
                {
                    bk.alc = true;

                    if (data != null)
                    {
                        if (length == -1)
                        {
                            length = data.Length - offset;
                        }
                        data.Seek(offset, SeekOrigin.Begin);
                        //MemoryStream ms = new MemoryStream();
                        //data.CopyTo(ms);
                        SEMemoryStream ms = new SEMemoryStream();
                        if (HasFlag(bf, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            bk.data = new LRStream(new GZipStream(ms, CompressionLevel.SmallestSize), offset, length, false, true, ssss);
                            bk.data.Decrypt(passwordstc);
                            bk.data.Seek(0, SeekOrigin.Begin);
                            
                            data.Seek(offset, SeekOrigin.Begin);
                            
                            data.CopyTo(bk.data);
                            bk.data.Seek(0, SeekOrigin.Begin);
                            bk.data.CleanPassword();
                        }
                        else
                        {
                            bk.data = new LRStream(ms, offset, length, false, true, ssss);
                            bk.data.Decrypt(passwordstc);
                            bk.data.Seek(0, SeekOrigin.Begin);
                            
                            data.Seek(offset, SeekOrigin.Begin);
                            
                            data.CopyTo(bk.data);
                            bk.data.Seek(0,SeekOrigin.Begin);
                            bk.data.CleanPassword();
                            //byte[] b = bk.data.ReadAllInBytes();
                            
                            //bk.data.UseOrgData(true);
                            //byte[] b = bk.data.ReadAllInBytes();
                        }
                    }
                    else
                    {
                        SEMemoryStream ms = new SEMemoryStream();

                        if (HasFlag(bf, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            bk.data = new LRStream(new GZipStream(ms, CompressionLevel.SmallestSize), 0, 0, false, true, ssss);
                        }
                        else
                        {
                            bk.data = new LRStream(ms, 0, 0, false, true, ssss);
                        }
                    }
                }
            }
            else
            {
                
                if (HasFlag(bf, LRBKFlag.CrossFile))
                {
                    if (!HasFlag(FLG, LRLFlag.Allow_CrossFile))
                        throw new NotSupportedException("文件未允许".GetInCurrLang());
                    Stream s = File.Create(string.Format(PT_Ext_PTH, BSDir, nmstc));
                    if (data != null)
                    {
                        byte[] buf = new byte[40960000];
                        int rd = 0;
                        while ((rd = data.Read(buf, 0, buf.Length)) > 0)
                        {
                            s.Write(buf, 0, rd);
                        }
                        s.Seek(0, SeekOrigin.Begin);

                        if (HasFlag(bf, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            bk.data = new LRStream(new GZipStream(s, CompressionLevel.SmallestSize), offset, length, false);
                        }
                        else
                        {
                            bk.data = new LRStream(s, offset, length, false);
                        }
                    }
                    else
                    {

                        if (HasFlag(bf, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            bk.data = new LRStream(new GZipStream(s, CompressionLevel.SmallestSize), 0, 0, false);
                        }
                        else
                        {
                            bk.data = new LRStream(s, 0, 0, false);
                        }
                    }
                }
                else
                {
                    bk.alc = true;

                    if (data != null)
                    {
                        if (length == -1)
                        {
                            length = data.Length - offset;
                        }
                        data.Seek(offset, SeekOrigin.Begin);
                        //MemoryStream ms = new MemoryStream();
                        //data.CopyTo(ms);

                        if (HasFlag(bf, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            bk.data = new LRStream(new GZipStream(data, CompressionLevel.SmallestSize), offset, length, false);
                        }
                        else
                        {
                            bk.data = new LRStream(data, offset, length, false);
                        }
                    }
                    else
                    {
                        SEMemoryStream ms = new SEMemoryStream();

                        if (HasFlag(bf, LRBKFlag.Compress))
                        {
                            if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                                throw new NotSupportedException("文件未允许压缩".GetInCurrLang());
                            bk.data = new LRStream(new GZipStream(ms, CompressionLevel.SmallestSize), 0, 0, false);
                        }
                        else
                        {
                            bk.data = new LRStream(ms, 0, 0, false);
                        }
                    }
                }
            }
            bk.NameSTC = nmstc;
            BKs = BKs.Append(bk).ToArray();
            nmstcs.Add(nmstc);
            offsets.Add(-1);
            return BoxCount++;
        }

        public void RemoveBox(int index)
        {
            Changed = true;
            var l = BKs.ToList();
            LRBK bk = l[index];
            if (HasFlag(bk.flg, LRBKFlag.CrossFile))
            {
                if (File.Exists(string.Format(PT_Ext_PTH, BSDir, bk.NameSTC)))
                {
                    bk.data.Close();
                    File.Delete(string.Format(PT_Ext_PTH, BSDir, bk.NameSTC));
                }
            }
            l.RemoveAt(index);
            BKs = l.ToArray();
            nmstcs.RemoveAt(index);
            offsets.RemoveAt(index);
        }
        public void Close()
        {
            if (StreamLoad)
            {
                if (dts != null)
                {
                    dts.Close();
                }
                for (uint i = 0; i < BoxCount; i++)
                {
                    if (BKs[i].data != null && BKs[i].alc)
                    {
                        BKs[i].data.Close();
                    }
                }
            }
            else
            {
                if (dts != null)
                {
                    dts.Close();
                }
                for (uint i = 0; i < BoxCount; i++)
                {
                    if (BKs[i].data != null && BKs[i].alc)
                    {
                        BKs[i].data.Close();
                    }
                }
            }
        }
        public void LoadToMemory(uint id)
        {
            LRBK b = BKs[id];
            if (StreamLoad || HasFlag(b.flg, LRBKFlag.CrossFile))
            {
                b.alc = true;
                SEMemoryStream ms = new SEMemoryStream();
                if (HasFlag(b.flg, LRBKFlag.Encrypt))
                {
                    ulong ssss = 0;
                    for (uint ip = 0; ip < b.ExtDataCount; ip++)
                    {
                        if (b.Exts[ip].t == LRBKExtDataType.Ext_Encrypt)
                        {
                            DataLayout dl = new DataLayout();
                            dl.B0 = b.Exts[ip].dt[0];
                            dl.B1 = b.Exts[ip].dt[1];
                            dl.B2 = b.Exts[ip].dt[2];
                            dl.B3 = b.Exts[ip].dt[3];
                            dl.B4 = b.Exts[ip].dt[4];
                            dl.B5 = b.Exts[ip].dt[5];
                            dl.B6 = b.Exts[ip].dt[6];
                            dl.B7 = b.Exts[ip].dt[7];
                            ssss = dl.UL;
                            break;
                        }
                    }
                    if (HasFlag(b.flg, LRBKFlag.CrossFile))
                    {
                        byte[] buf = new byte[40960000];
                        int rd = 0;
                        //Stream s = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open);
                        while ((rd = b.data.Read(buf, 0, buf.Length)) > 0)
                        {
                            ms.Write(buf, 0, rd);
                        }
                        ms.Flush();
                        b.data.Close();
                        b.data = new LRStream(ms, 0, 0, false, true, ssss);
                    }
                    else
                    {
                        byte[] buf = new byte[40960000];
                        int rd = 0;
                        //Stream s = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open);
                        while ((rd = b.data.Read(buf, 0, buf.Length)) > 0)
                        {
                            ms.Write(buf, 0, rd);
                        }
                        ms.Flush();
                        b.data = new LRStream(ms, 0, 0, false, true, ssss);
                    }
                }
                else
                {
                    if (HasFlag(b.flg, LRBKFlag.CrossFile))
                    {
                        byte[] buf = new byte[40960000];
                        int rd = 0;
                        //Stream s = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open);
                        while ((rd = b.data.Read(buf, 0, buf.Length)) > 0)
                        {
                            ms.Write(buf, 0, rd);
                        }
                        ms.Flush();
                        b.data.Close();
                        b.data = new LRStream(ms, 0, 0, false);
                    }
                    else
                    {
                        byte[] buf = new byte[40960000];
                        int rd = 0;
                        //Stream s = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open);
                        while ((rd = b.data.Read(buf, 0, buf.Length)) > 0)
                        {
                            ms.Write(buf, 0, rd);
                        }
                        ms.Flush();
                        b.data = new LRStream(ms, 0, 0, false);
                    }
                }
            }
        }
        public void Save()
        {
            if (Changed)
            {
                if (StreamLoad)
                {
                    dts.Seek(extstreamoffset, SeekOrigin.Begin);
                    BinaryOperator bo = new BinaryOperator(dts);
                    bo.Write(LOSF_B);
                    bo.Write(vi.GetVersionCode());
                    bo.Write(ti.GetTimeCode());
                    bo.Write(DEV);
                    bo.Write((ushort)FLG);
                    bo.Write(ExtDataCount);
                    for (int i = 0; i < ExtDataCount; i++)
                    {
                        bo.Write((uint)Exts[i].dt.Length);
                        bo.Write((ushort)Exts[i].t);
                        bo.Write(Exts[i].dt);
                    }
                    bo.Write(BoxCount);
                    long bts = bo.Position - extstreamoffset;



                    long ps = bo.Position;
                    bo.Seek(extstreamoffset, SeekOrigin.Begin);

                    ulong stcchk = STCCode.GetSTC(bo.ReadBytes(bts));
                    bo.Seek(ps, SeekOrigin.Begin);
                    //
                    bo.Write(stcchk);

                    for (int i = 0; i < BoxCount; i++)
                    {
                        bo.Write(BK_B);
                        bo.Write((ushort)BKs[i].flg);
                        bo.Write(BKs[i].ExtDataCount);
                        for (int ip = 0; ip < BKs[i].ExtDataCount; ip++)
                        {
                            bo.Write((uint)BKs[i].Exts[ip].dt.Length);
                            bo.Write((ushort)BKs[i].Exts[ip].t);
                            bo.Write(BKs[i].Exts[ip].dt);
                        }
                        BKs[i].data.UseOrgData(true);
                        if (BKs[i].alc)
                        {
                            bo.Write(BKs[i].data.Length);

                            if (HasFlag(BKs[i].flg, LRBKFlag.CrossFile))
                            {
                                BKs[i].data.Flush();
                                BKs[i].data.Seek(0, SeekOrigin.Begin);
                                byte[] buf = new byte[40960000];
                                int rd = 0;
                                Stream s = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open);
                                while ((rd = BKs[i].data.Read(buf, 0, buf.Length)) > 0)
                                {
                                    s.Write(buf, 0, rd);
                                }
                                s.Flush();
                                s.Close();
                            }
                            else
                            {
                                BKs[i].data.Seek(0, SeekOrigin.Begin);
                                byte[] buf = new byte[40960000];
                                int rd = 0;
                                while ((rd = BKs[i].data.Read(buf, 0, buf.Length)) > 0)
                                {
                                    bo.Write(buf, 0, rd);
                                }
                            }
                        }
                        else
                        {
                            bo.Write(BKs[i].Length);

                            if (HasFlag(BKs[i].flg, LRBKFlag.CrossFile))
                            {
                                BKs[i].data.Flush();
                            }
                            else
                            {
                                BKs[i].data.Seek(0, SeekOrigin.Begin);
                                byte[] buf = new byte[40960000];
                                int rd = 0;
                                while ((rd = BKs[i].data.Read(buf, 0, buf.Length)) > 0)
                                {
                                    bo.Write(buf, 0, rd);
                                }
                            }
                        }
                        BKs[i].data.UseOrgData(false);
                    }
                    bo.Flush();
                }
                else
                {
                    dts.Seek(extstreamoffset, SeekOrigin.Begin);
                    BinaryOperator bo = new BinaryOperator(File.Open(FP, FileMode.OpenOrCreate));
                    //BinaryOperator bow = new BinaryOperator(dts);
                    bo.Write(LOSF_B);
                    bo.Write(vi.GetVersionCode());
                    bo.Write(ti.GetTimeCode());
                    bo.Write(DEV);
                    bo.Write((ushort)FLG);
                    bo.Write(ExtDataCount);
                    for (int i = 0; i < ExtDataCount; i++)
                    {
                        bo.Write((uint)Exts[i].dt.Length);
                        bo.Write((ushort)Exts[i].t);
                        bo.Write(Exts[i].dt);
                    }
                    bo.Write(BoxCount);
                    long bts = bo.Position - extstreamoffset;



                    long ps = bo.Position;
                    bo.Seek(extstreamoffset, SeekOrigin.Begin);

                    ulong stcchk = STCCode.GetSTC(bo.ReadBytes(bts));
                    bo.Seek(ps, SeekOrigin.Begin);
                    //
                    bo.Write(stcchk);

                    for (int i = 0; i < BoxCount; i++)
                    {
                        bo.Write(BK_B);
                        bo.Write((ushort)BKs[i].flg);
                        bo.Write(BKs[i].ExtDataCount);
                        for (int ip = 0; ip < BKs[i].ExtDataCount; ip++)
                        {
                            bo.Write((uint)BKs[i].Exts[ip].dt.Length);
                            bo.Write((ushort)BKs[i].Exts[ip].t);
                            bo.Write(BKs[i].Exts[ip].dt);
                        }
                        if (BKs[i].alc)
                        {
                            bo.Write(BKs[i].data.Length);
                        }
                        else
                        {
                            bo.Write(BKs[i].Length);
                        }
                        BKs[i].data.UseOrgData(true);
                        BKs[i].data.Seek(0, SeekOrigin.Begin);
                        if (HasFlag(BKs[i].flg, LRBKFlag.CrossFile))
                        {
                            if (BKs[i].alc)
                            {
                                BKs[i].data.Flush();
                                BKs[i].data.Seek(0, SeekOrigin.Begin);
                                byte[] buf = new byte[40960000];
                                int rd = 0;
                                Stream s = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open);
                                while ((rd = BKs[i].data.Read(buf, 0, buf.Length)) > 0)
                                {
                                    s.Write(buf, 0, rd);
                                }
                                s.Flush();
                                s.Close();
                            }
                            else
                            {
                                BKs[i].data.Flush();
                            }
                        }
                        else
                        {
                            byte[] buf = new byte[40960000];
                            int rd = 0;
                            while ((rd = BKs[i].data.Read(buf, 0, buf.Length)) > 0)
                            {
                                bo.Write(buf, 0, rd);
                            }
                        }
                        BKs[i].data.UseOrgData(false);
                    }
                    bo.Flush();
                    bo.Close();
                }
            }
            else
            {
                if (StreamLoad && (!ExitFile || OnlyStream))
                {
                    dts.Flush();
                }
                else
                {
                    BinaryOperator bo = new BinaryOperator(File.Open(FP, FileMode.Open));
                    int rd = 0;
                    byte[] buf = new byte[40960000];
                    dts.Seek(extstreamoffset, SeekOrigin.Begin);
                    while ((rd = dts.Read(buf, 0, buf.Length)) > 0)
                    {
                        bo.Write(buf, 0, buf.Length);
                    }
                    bo.Flush();
                    bo.Close();
                }
            }
        }
        /*
         * LRL RULES:
         * Ext:*.lrl
         * HEAD
         * offset       name        size            value          desc
         * 0            开始          4             LOSF           LosResourcesLib文件头
         * 4            版本          4             1.0.0.0        LosResourcesLib版本号（struct:VERSION）
         * 8            日期          8             ?              LosResourcesLib创建日期 (struct:TIME)
         * 16           作者STC       8             ?              LosResourcesLib作者 (class:STC)
         * 24           功能          2             ?              LosResourcesLib功能 (enum:LRLFlag)
         * 26           扩展数据数量  2             ?              LosResourcesLib扩展数据数量 (ushort)
         * lp（case 扩展数据数量>0）:
         * 28+?        扩展数据长度   4             ?              LosResourcesLib扩展数据长度（不得超过1GB） (uint)
         * 32+?        扩展数据类型   2             ?              LosResourcesLib扩展数据类型（也就是父系数据对象）（enum:LRLExtDataType）
         * 34+?        扩展数据       ?             ?              LosResourcesLib扩展数据
         *  
         * 28+??        数据块数量    4             ?              LosResourcesLib数据块数量 (uint)
         * 32+??        头部STC       8             ?              LosResourcesLib头部STC（不包括STC字段） (class:STC)
         * 
         * 
         * 
         * BOX（作为装载数据的容器，看FLG支持多BOX合并或跨文件多BOX合并）
         * offset            name               size                   value                 desc
         * 0                 开始                 2                    BK                    LosResourcesLib文件盒子头
         * 2                 功能                 2                    ?                     LosResourcesLib文件盒子功能（enum:LRBKFlag）
         * 4                 扩展数据数量         2                    ?                     LosResourcesLib文件盒子扩展数据数量 (ushort)
         * lp（case 扩展数据数量>0）:
         * 6+?               扩展数据长度         4                    ?                     LosResourcesLib文件盒子扩展数据长度（不得超过1GB） (uint)
         * 10+?              扩展数据类型         2                    ?                     LosResourcesLib文件盒子扩展数据类型（也就是父系数据对象）（enum:LRBKExtDataType）
         * 12+?              扩展数据             ?                    ?                     LosResourcesLib文件盒子扩展数据
         * 
         * 6+??              当前数据长度         8                    ?                     LosResourcesLib当前数据长度（不包括LRBK字段） (ulong)
         * 14+??             数据                 ?                    ?                     LosResourcesLib文件盒子存储数据
         */
    }
}
