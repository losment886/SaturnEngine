using Hexa.NET.SDL3;
using Konscious.Security.Cryptography;
using SaturnEngine.Base;
using SaturnEngine.Management.IO;
using SaturnEngine.Management.SEMemory;
using SaturnEngine.Security;
using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using static SaturnEngine.SEMath.Helper;

namespace SaturnEngine.Asset
{
    //2代逻辑与1代完全不同。
    public class LRLV2 : SEBase
    {
        public class LRLExtDataLists
        {
            [StructLayout(LayoutKind.Explicit, Size = 128)]
            public unsafe struct LRL_Ext_Def
            {
                [FieldOffset(0)]
                public LRLExtDataType t;
                [FieldOffset(4)]
                public uint Length;
                [FieldOffset(8)]
                public fixed byte Data[120];

                [FieldOffset(0)]
                public fixed byte RawData[128];
                public byte[] RawData_RCH
                {
                    get
                    {
                        byte[] bts = new byte[128];
                        fixed (byte* p = RawData)
                        {
                            Marshal.Copy((IntPtr)p, bts, 0, 128);
                        }
                        return bts;
                    }
                    set
                    {
                        if (value.Length != 128)
                        {
                            throw new ArgumentException("RawData must be 128 bytes long.");
                        }
                        fixed (byte* p = RawData)
                        {
                            Marshal.Copy(value, 0, (IntPtr)p, 128);
                        }
                    }
                }
                public byte[] Data_RCH
                {
                    get
                    {
                        byte[] bts = new byte[120];
                        fixed (byte* p = Data)
                        {
                            Marshal.Copy((IntPtr)p, bts, 0, 120);
                        }
                        return bts;
                    }
                    set
                    {
                        if (value.Length != 120)
                        {
                            throw new ArgumentException("Data must be 120 bytes long.");
                        }
                        fixed (byte* p = Data)
                        {
                            Marshal.Copy(value, 0, (IntPtr)p, 120);
                        }
                    }
                }
            }
            public struct LRLExt_Creator
            {
                public LRL_Ext_Def Def;
                public string Name;
                public LRLExt_Creator(LRL_Ext_Def def)
                {
                    this.Def = def;
                    Name = System.Text.Encoding.UTF8.GetString(def.Data_RCH).TrimEnd('\0');
                }
                public LRLExt_Creator(string name)
                {
                    Name = name;
                    Def = new LRL_Ext_Def();
                    Def.t = LRLExtDataType.Creator;
                    byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
                    if (nameBytes.Length > 120)
                    {
                        throw new ArgumentException("Name is too long. It must be 120 bytes or less when encoded in UTF-8.");
                    }
                    Def.Length = (uint)nameBytes.Length;
                    Array.Clear(Def.Data_RCH, 0, 120);
                    Array.Copy(nameBytes, Def.Data_RCH, nameBytes.Length);
                }
            }
        }

        public const string LOSF = "LOSF"; // LosResourcesLib File
        public readonly byte[] LOSF_B = [0x4C, 0x4F, 0x53, 0x46]; // LosResourcesLib File
        public const string BK = "LRBK"; // Box
        public readonly byte[] BK_B = [0x4C, 0x52, 0x42, 0x4B]; // Box
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
        public readonly VERSION LRLVersion = new VERSION(new Version(2, 0, 0, 4)); // LosResourcesLib Version
        public readonly byte[] TREE_B = [0x4C, 0x52, 0x54, 0x52]; // LRTR
        public readonly byte[] TREE_PAGE_B = [0x4C, 0x52, 0x50, 0x47]; // LRPG

        public bool Unicode = false; // 是否使用Unicode编码(仅针对于内部的一些字符串数据)

        public enum LRLExtDataType : uint
        {
            None = 0,
            Creator = 1,
        }
        public enum LRLBlockSize : uint
        {
            Small = 1024,
            Default = 2048,
            Large = 4096,
        }

        public enum LRLFeatureFlags : uint
        {
            None = 0,
            Allow_Encrypt = 1,
            Allow_Compress = 2,
            Allow_ExtendFile = 4,
            Allow_StreamLoad = 8,
            Allow_ParallelOperations = 16,
            Allow_AdvancedAttributes = 32,
        }

        public enum LRLFeatureEBFlags : uint
        {
            None = 0,
            Encrypt = 1,
            Compress = 2,
            StreamLoad = 4,
            ParallelOperations = 8,
            AdvancedAttributes = 16,
            ReadOnly = 32,
            Hide = 64,
            UseUnicode = 128,
        }
        public enum LRLCacheFlags : uint
        {
            None = 0,
        }
        public enum LRLTreeFlags : uint
        {
            None = 0,
        }
        public enum LRLTransPoolFlags : uint
        {
            None = 0,
        }
        public enum LRLHeadPoolFlags : uint
        {
            None = 0,
        }


        [StructLayout(LayoutKind.Explicit, Size = 128)]
        public unsafe struct LRLFileHead
        {
            [StructLayout(LayoutKind.Explicit,Size=72)]
            public unsafe struct VarInfo
            {
                [FieldOffset(0)]
                public fixed byte FileName[32];
                [FieldOffset(32)]
                public fixed byte FileExtension[8];
                [FieldOffset(40)]
                public ulong StartPosition;
                [FieldOffset(48)]
                public ulong OriginalSize;
                [FieldOffset(56)]
                public ulong StoredSize;
                [FieldOffset(64)]
                public ulong Reserved;
                [FieldOffset(0)]
                public fixed byte ExtenFilePath[72];

                [FieldOffset(0)]
                public fixed byte RawData[72];
                public byte[] FileName_RCH
                {
                    get
                    {
                        byte[] bts = new byte[32];
                        fixed (byte* p = FileName)
                        {
                            Marshal.Copy((IntPtr)p, bts, 0, 32);
                        }
                        return bts;
                    }
                    set
                    {
                        if (value.Length != 32)
                        {
                            throw new ArgumentException("FileName must be 32 bytes long.");
                        }
                        fixed (byte* p = FileName)
                        {
                            Marshal.Copy(value, 0, (IntPtr)p, 32);
                        }
                    }
                }
                public byte[] FileExtension_RCH
                {
                    get
                    {
                        byte[] bts = new byte[8];
                        fixed (byte* p = FileExtension)
                        {
                            Marshal.Copy((IntPtr)p, bts, 0, 8);
                        }
                        return bts;
                    }
                    set
                    {
                        if (value.Length != 8)
                        {
                            throw new ArgumentException("FileExtension must be 8 bytes long.");
                        }
                        fixed (byte* p = FileExtension)
                        {
                            Marshal.Copy(value, 0, (IntPtr)p, 8);
                        }
                    }
                }
                public byte[] ExtenFilePath_RCH
                {
                    get
                    {
                        byte[] bts = new byte[72];
                        fixed (byte* p = ExtenFilePath)
                        {
                            Marshal.Copy((IntPtr)p, bts, 0, 72);
                        }
                        return bts;
                    }
                    set
                    {
                        if (value.Length != 72)
                        {
                            throw new ArgumentException("ExtenFilePath must be 72 bytes long.");
                        }
                        fixed (byte* p = ExtenFilePath)
                        {
                            Marshal.Copy(value, 0, (IntPtr)p, 72);
                        }
                    }
                }
                public byte[] RawData_RCH
                {
                    get {
                        byte[] bts = new byte[72];
                        fixed (byte* p = RawData)
                        {
                            Marshal.Copy((IntPtr)p, bts, 0, 72);
                        }
                        return bts;
                    }
                    set
                    {
                        if (value.Length != 72)
                        {
                            throw new ArgumentException("RawData must be 72 bytes long.");
                        }
                        fixed (byte* p = RawData)
                        {
                            Marshal.Copy(value, 0, (IntPtr)p, 72);
                        }
                    }
                }
                public VarInfo()
                {

                }
            }
            [FieldOffset(0)]
            public uint LRBK;
            [FieldOffset(4)]
            public uint FileAttribute;
            [FieldOffset(8)]
            public VarInfo VariableFileInfo;
            [FieldOffset(80)]
            public TIME Date;
            [FieldOffset(88)]
            public fixed byte PasswordEncryptedEncryptKey[32];
            [FieldOffset(120)]
            public ulong STCCode;
            [FieldOffset(0)]
            public fixed byte RawData[128];
            [FieldOffset(0)]
            public fixed byte STCData[120];
            public byte[] PasswordEncryptedEncryptKey_RCH
            {
                get
                {
                    byte[] bts = new byte[32];
                    fixed (byte* p = PasswordEncryptedEncryptKey)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 32);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 32)
                    {
                        throw new ArgumentException("PasswordEncryptedEncryptKey must be 32 bytes long.");
                    }
                    fixed (byte* p = PasswordEncryptedEncryptKey)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 32);
                    }
                }
            }
            public byte[] RawData_RCH
            {
                get
                {
                    byte[] bts = new byte[128];
                    fixed (byte* p = RawData)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 128);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 128)
                    {
                        throw new ArgumentException("RawData must be 128 bytes long.");
                    }
                    fixed (byte* p = RawData)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 128);
                    }
                }
            }
            public byte[] STCData_RCH
            {
                get
                {
                    byte[] bts = new byte[120];
                    fixed (byte* p = STCData)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 120);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 120)
                    {
                        throw new ArgumentException("STCData must be 120 bytes long.");
                    }
                    fixed (byte* p = STCData)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 120);
                    }
                }
            }
            public LRLFileHead()
            {

            }
        }
        [StructLayout(LayoutKind.Explicit,Size=256)]
        public unsafe struct LRLHead
        {
            [FieldOffset(0)]
            public fixed byte LOSF[4];
            [FieldOffset(4)]
            public VERSION FileVersion;
            [FieldOffset(8)]
            public TIME CreateTime;
            [FieldOffset(16)]
            public LRLFeatureFlags featureflags;
            [FieldOffset(20)]
            public LRLFeatureEBFlags banflags;
            [FieldOffset(24)]
            public LRLFeatureEBFlags enabledflags;
            [FieldOffset(28)]
            public fixed byte PasswordEncryptedEncryptKey[32];
            [FieldOffset(60)]
            public LRLBlockSize blocksize;
            [FieldOffset(64)]
            public ulong cachesize;
            [FieldOffset(72)]
            public ulong cacheposition;
            [FieldOffset(80)]
            public ulong treesize;
            [FieldOffset(88)]
            public ulong treeposition;
            [FieldOffset(96)]
            public ulong transpoolsize;
            [FieldOffset(104)]
            public ulong transpoolposition;
            [FieldOffset(112)]
            public ulong headpoolsize;
            [FieldOffset(120)]
            public ulong headpoolposition;
            [FieldOffset(128)]
            public ulong datasize;
            [FieldOffset(136)]
            public ulong dataposition;
            [FieldOffset(144)]
            public LRLCacheFlags cacheflags;
            [FieldOffset(148)]
            public LRLTreeFlags treeflags;
            [FieldOffset(152)]
            public LRLTransPoolFlags transpflags;
            [FieldOffset(156)]
            public LRLHeadPoolFlags headpflags;
            [FieldOffset(160)]
            public ulong ExternalDataCount;
            [FieldOffset(168)]
            public ulong TotalSize;
            [FieldOffset(176)]
            public fixed byte TAG[16];
            [FieldOffset(192)]
            public fixed byte Nonce[12];
            [FieldOffset(204)]
            public fixed byte KeyTag[16];
            [FieldOffset(220)]
            public fixed byte KeyNonce[12];

            [FieldOffset(232)]
            public fixed byte NOP[16];
            [FieldOffset(248)]
            public ulong HeadSTCCode;

            [FieldOffset(0)]
            public fixed byte RawData[256];

            [FieldOffset(0)]
            public fixed byte STCData[248];


            public byte[] KeyNonce_RCH
            {
                get
                {
                    byte[] bts = new byte[12];
                    fixed (byte* p = KeyNonce)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 12);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 12)
                    {
                        throw new ArgumentException("KeyNonce must be 12 bytes long.");
                    }
                    fixed (byte* p = KeyNonce)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 12);
                    }
                }
            }

            public byte[] KeyTag_RCH
            {
                get
                {
                    byte[] bts = new byte[16];
                    fixed (byte* p = KeyTag)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 16);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 16)
                    {
                        throw new ArgumentException("KeyTag must be 16 bytes long.");
                    }
                    fixed (byte* p = KeyTag)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 16);
                    }
                }
            }

            public byte[] Nonce_RCH
            {
                get
                {
                    byte[] bts = new byte[12];
                    fixed (byte* p = Nonce)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 12);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 12)
                    {
                        throw new ArgumentException("Nonce must be 12 bytes long.");
                    }
                    fixed (byte* p = Nonce)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 12);
                    }
                }
            }

            public byte[] TAG_RCH
            {
                get
                {
                    byte[] bts = new byte[16];
                    fixed (byte* p = TAG)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 16);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 16)
                    {
                        throw new ArgumentException("TAG must be 16 bytes long.");
                    }
                    fixed (byte* p = TAG)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 16);
                    }
                }
            }

            public byte[] LOSF_RCH
            {
                get
                {
                    byte[] bts = new byte[4];
                    fixed (byte* p = LOSF)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 4);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 4)
                    {
                        throw new ArgumentException("LOSF must be 4 bytes long.");
                    }
                    fixed (byte* p = LOSF)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 4);
                    }
                }
            }


            public byte[] RawData_RCH
            {
                get
                {
                    byte[] bts = new byte[256];
                    fixed (byte* p = RawData)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 256);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 256)
                    {
                        throw new ArgumentException("RawData must be 256 bytes long.");
                    }
                    fixed (byte* p = RawData)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 256);
                    }
                }
            }


            public byte[] PasswordEncryptedEncryptKey_RCH
            {
                get
                {
                    byte[] bts = new byte[32];
                    fixed (byte* p = PasswordEncryptedEncryptKey)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 32);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 32)
                    {
                        throw new ArgumentException("PasswordEncryptedEncryptKey must be 32 bytes long.");
                    }
                    fixed (byte* p = PasswordEncryptedEncryptKey)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 32);
                    }
                }
            }


            public byte[] NOP_RCH
            {
                get
                {
                    byte[] bts = new byte[16];
                    fixed (byte* p = NOP)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 16);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 16)
                    {
                        throw new ArgumentException("NOP must be 16 bytes long.");
                    }
                    fixed (byte* p = NOP)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 16);
                    }
                }
            }

            public byte[] STCData_RCH
            {
                get
                {
                    byte[] bts = new byte[248];
                    fixed (byte* p = STCData)
                    {
                        Marshal.Copy((IntPtr)p, bts, 0, 248);
                    }
                    return bts;
                }
                set
                {
                    if (value.Length != 248)
                    {
                        throw new ArgumentException("STCData must be 248 bytes long.");
                    }
                    fixed (byte* p = STCData)
                    {
                        Marshal.Copy(value, 0, (IntPtr)p, 248);
                    }
                }
            }

            public LRLHead()
            {
            }
        }
        bool fromStream = false;


        Stream? dts;
        string? FP;
        string BSDir = "./";
        long extstreamoffset = 0;
        public bool Changed { get; private set; } = false;
        public bool Loaded { get; private set; } = false;
        public bool Encrypted { get; private set; } = false;
        public bool Compressed { get; private set; } = false;
        public bool StreamLoad { get; private set; } = false;
        public bool UnLocked { get; private set; } = false;


        LRLHead Head;
        byte[]? exts;
        /// <summary>
        /// 扩展数据列表，存放LRL文件的扩展数据
        /// </summary>
        List<LRLExtDataLists.LRL_Ext_Def> ExtDataList = new List<LRLExtDataLists.LRL_Ext_Def>();
        /// <summary>
        /// 是否正在进行半加载，半加载时，LRL文件已读取头部，但未读取扩展数据，调用ContinueLoading()可继续加载扩展数据
        /// </summary>
        bool loadhalf = false;


        /// <summary>
        /// 文件树，存放LRL文件的文件结构
        /// </summary>
        LRLBPlusTree filetree;
        /// <summary>
        /// 目录树，存放LRL文件的目录结构
        /// </summary>
        LRLBPlusTree diectorytree;

        public void LoadFromStream(Stream s, long stoffset = 0, string? Password = null)
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
            Head = new LRLHead();
            bo.Seek(stoffset, SeekOrigin.Begin);
            byte[] headbytes = bo.ReadBytes(Marshal.SizeOf<LRLHead>());
            Head.RawData_RCH = headbytes;
            // 检查版本兼容性，大版本和中版本需要匹配。
            if (Head.FileVersion.Major != LRLVersion.Major || Head.FileVersion.Minor != LRLVersion.Minor || Head.FileVersion.Build > LRLVersion.Build || Head.FileVersion.Revision > LRLVersion.Revision)
            {
                throw new InvalidDataException("LRL版本不兼容".GetInCurrLang());
            }
            if (Head.STCData_RCH.ToSTC() != Head.HeadSTCCode)
            {
                throw new InvalidDataException("LRL头损坏".GetInCurrLang());
            }
            ProcessFlags();
            fromStream = true;
            loadhalf = true;
            UnLock(Password);
            LoadExt();
            loadhalf = false;
            Loaded = true;
        }

        public void LoadFromFile(string fp, string? Password = null)
        {
            if (File.Exists(fp))
            {
                FileStream s = File.Open(fp, FileMode.Open);
                dts = s;
                s.Seek(0, SeekOrigin.Begin);
                extstreamoffset = 0;
                BinaryOperator bo = new BinaryOperator(s);
                var headbts = bo.ReadBytes(4);
                if (!headbts.SequenceEqual(LOSF_B))
                {
                    throw new InvalidDataException("LRL头损坏".GetInCurrLang());
                }
                Head = new LRLHead();
                bo.Seek(0, SeekOrigin.Begin);
                byte[] headbytes = bo.ReadBytes(Marshal.SizeOf<LRLHead>());
                Head.RawData_RCH = headbytes;
                exts = bo.ReadBytes((int)((int)Head.blocksize - Marshal.SizeOf<LRLHead>()));
                // 检查版本兼容性，大版本和中版本需要匹配。
                if (Head.FileVersion.Major != LRLVersion.Major || Head.FileVersion.Minor != LRLVersion.Minor || Head.FileVersion.Build > LRLVersion.Build || Head.FileVersion.Revision > LRLVersion.Revision)
                {
                    throw new InvalidDataException("LRL版本不兼容".GetInCurrLang());
                }
                if (Head.STCData_RCH.ToSTC() != Head.HeadSTCCode)
                {
                    throw new InvalidDataException("LRL头损坏".GetInCurrLang());
                }
                ProcessFlags();
                fromStream = false;
                loadhalf = true;
                UnLock(Password);
                LoadExt();
                loadhalf = false;
                Loaded = true;
            }
            else 
            {
                throw new FileNotFoundException("未找到文件:".GetInCurrLang() + fp);
            }

        }


        public void ContinueLoading()
        {
            if(loadhalf)
            {
                CheckUnLock();
                LoadExt();
                loadhalf = false;
            }
        }


        void UnLock(string? Password = null)
        {
            if (Encrypted && !UnLocked)
            {
                if (Password == null)
                {
                    throw new InvalidOperationException("LRL文件已加密，未解锁，无法操作".GetInCurrLang());
                }
                else
                {
                    bool isPasswordCorrect = VerifyPassword(Password);
                    if (!isPasswordCorrect)
                    {
                        throw new UnauthorizedAccessException("密码错误，无法解锁LRL文件".GetInCurrLang());
                    }
                    UnLocked = true;
                }
            }
        }
        SEMSV? sv = null;
        //保证安全性的代码有点难写，暂时先明文存放，后期完善
        byte[]? svb = null;
        bool VerifyPassword(string password)
        {
            //在此是默认是加载且加密的，如果在没加密时调用此方法，可能会有奇怪的输出
            Argon2id ag2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password));
            byte[] bn = ag2.GetBytes(32);
            AesGcm ag = new AesGcm(bn, 0);
            byte[] ob = new byte[32];
            try
            {
                ag.Decrypt(Head.KeyNonce_RCH, Head.PasswordEncryptedEncryptKey_RCH, Head.KeyTag_RCH, ob);
            }
            catch
            {
                return false;
            }

            AesGcm fil = new AesGcm(ob, 16);
            byte[] svc = new byte[exts != null ? exts.Length : 0];
            try
            {
                ag.Decrypt(Head.Nonce_RCH, exts ?? [], Head.TAG_RCH, svc);
            }
            catch
            {
                return false;
            }
            svb = new byte[svc.Length];
            svc.CopyTo(svb, 0);
            sv = new SEMSV();
            powerToken.Value = sv.Init(svc);

            return true;
        }
        int tryCount = 0;
        public bool TryUnLock(string? Password = null)
        {
            tryCount++;
            if(tryCount > 5)
            {
                throw new InvalidOperationException("尝试解锁次数过多，可能存在安全风险".GetInCurrLang());
            }
            if (Encrypted && !UnLocked)
            {
                if (Password == null)
                {
                    return false;
                }
                else
                {
                    bool isPasswordCorrect = VerifyPassword(Password);
                    if (!isPasswordCorrect)
                    {
                        return false;
                    }
                    UnLocked = true;
                    return true;
                }
            }
            return true; // 如果未加密或已解锁，返回true
        }
        void ProcessFlags()
        {
            if (LRL.HasFlag(Head.enabledflags, LRLFeatureEBFlags.Encrypt))
            {
                Encrypted = true;
                UnLocked = fromStream;
            }
            if (LRL.HasFlag(Head.enabledflags, LRLFeatureEBFlags.Compress))
            {
                Compressed = true;
            }
            if (LRL.HasFlag(Head.enabledflags, LRLFeatureEBFlags.StreamLoad))
            {
                StreamLoad = true;
            }
        }

        void CheckUnLock()
        {
            if(Encrypted && !UnLocked)
            {
                throw new InvalidOperationException("LRL文件已加密，未解锁，无法操作".GetInCurrLang());
            }
        }


        void LoadExt()
        {
            if (Head.ExternalDataCount > 0)
            {
                CheckUnLock();
                for(uint i = 0;i < Head.ExternalDataCount; i++)
                {
                    dts.Seek(extstreamoffset + Marshal.SizeOf<LRLHead>() + i * Marshal.SizeOf<LRLExtDataLists.LRL_Ext_Def>(), SeekOrigin.Begin);
                    BinaryOperator bo = new BinaryOperator(dts);
                    var extbts = bo.ReadBytes(Marshal.SizeOf<LRLExtDataLists.LRL_Ext_Def>());
                    LRLExtDataLists.LRL_Ext_Def extdef = new LRLExtDataLists.LRL_Ext_Def();
                    extdef.RawData_RCH = extbts;
                    ExtDataList.Add(extdef);
                }
            }
            else
            {
                ExtDataList.Clear();
            }
        }


        public LRLV2()
        {

        }

        ThreadLocal<ulong> powerToken = new ThreadLocal<ulong>(() => 0);

        /// <summary>
        /// 获取访问令牌，储存在内部。
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void CreateAccessToken()
        {
            if (!Encrypted)
                return;

            CheckUnLock();
            if (sv == null)
            {
                throw new InvalidOperationException("未初始化，无法创建访问令牌".GetInCurrLang());
            }

            //ulong tk = 0;
            if(powerToken.Value == 0)
            {
                //not administrator

                //向创建线程申请临时访问。

            }


            
        }
        
        



    }


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
        public readonly VERSION LRLVersion = new VERSION(new Version(1, 27, 7, 10)); // LosResourcesLib Version



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
            Allow_Compress = 0x0020, // 允许压缩(v1.27)，压缩默认Lz4
            
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
        private static byte[] ReadFixedBytes(Stream stream, long length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (length == 0)
            {
                return [];
            }

            SEMemoryStream ms = new SEMemoryStream();
            byte[] buffer = new byte[40960000];
            long remaining = length;
            while (remaining > 0)
            {
                int readSize = (int)Math.Min(buffer.Length, remaining);
                int readLength = stream.Read(buffer, 0, readSize);
                if (readLength <= 0)
                {
                    throw new EndOfStreamException("压缩数据不完整".GetInCurrLang());
                }
                ms.Write(buffer, 0, readLength);
                remaining -= readLength;
            }

            ms.Seek(0, SeekOrigin.Begin);
            return new LRStreamSlim(ms, 0, ms.Length).ReadAllInBytes();
        }
        private static LRStream CreateCompressedBoxStream(Stream stream, long length, bool needEncrypt = false, ulong passwordstc = 0)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (stream.CanSeek && SECompressStream.IsChunkedStream(stream))
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"SaturnEngine.LRL.{Guid.NewGuid():N}.tmp");
                FileStream tempStream = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
                SECompressStream.DecompressChunkedStream(stream, tempStream, length);
                tempStream.Seek(0, SeekOrigin.Begin);
                return new LRStream(tempStream, 0, 0, false, needEncrypt, passwordstc);
            }

            byte[] compressed = ReadFixedBytes(stream, length);
            byte[] decompressed = SECompressStream.DecompressWithSize(compressed);
            SEMemoryStream ms = new SEMemoryStream();
            ms.Write(decompressed, 0, decompressed.Length);
            ms.Seek(0, SeekOrigin.Begin);
            return new LRStream(ms, 0, 0, false, needEncrypt, passwordstc);
        }
        private static long WriteStoredStream(LRStream stream, Stream destination, bool compress)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(destination);

            stream.Flush();
            stream.UseOrgData(true);
            stream.Seek(0, SeekOrigin.Begin);

            if (compress)
            {
                return SECompressStream.CompressToChunkedStream(stream, destination);
            }

            byte[] buffer = new byte[40960000];
            long totalWritten = 0;
            int readLength = 0;
            while ((readLength = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                destination.Write(buffer, 0, readLength);
                totalWritten += readLength;
            }
            return totalWritten;
        }
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
                                using Stream bs = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open);
                                BKs[i].data = CreateCompressedBoxStream(bs, (long)BKs[i].Length, true, ssss);
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
                                BKs[i].data = CreateCompressedBoxStream(bo, (long)BKs[i].Length, true, ssss);
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
                                using Stream bs = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open);
                                BKs[i].data = CreateCompressedBoxStream(bs, (long)BKs[i].Length);
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
                                BKs[i].data = CreateCompressedBoxStream(bo, (long)BKs[i].Length);
                            }
                            else
                            {
                                BKs[i].data = new LRStream(bo, bo.Position, (long)BKs[i].Length);
                                //BKs[i].data = new LRStream(bo, bo.Position, (long)BKs[i].Length);
                            }
                        }
                    }
                    if (!HasFlag(BKs[i].flg, LRBKFlag.CrossFile) && !HasFlag(BKs[i].flg, LRBKFlag.Compress))
                    {
                        bo.Seek((long)BKs[i].Length, SeekOrigin.Current);
                    }
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
                            using Stream bs = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open);
                            BKs[i].data = CreateCompressedBoxStream(bs, (long)BKs[i].Length, true, ssss);
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
                            BKs[i].data = CreateCompressedBoxStream(bo, (long)BKs[i].Length, true, ssss);
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
                            using Stream bs = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Open);
                            BKs[i].data = CreateCompressedBoxStream(bs, (long)BKs[i].Length);
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
                            BKs[i].data = CreateCompressedBoxStream(bo, (long)BKs[i].Length);
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

            if (HasFlag(bf, LRBKFlag.Compress))
            {
                if (!HasFlag(FLG, LRLFlag.Allow_Compress))
                    throw new NotSupportedException("文件未允许压缩".GetInCurrLang());

                bk.alc = true;
                if (HasFlag(bf, LRBKFlag.Encrypt))
                {
                    bk.Encrypt = true;
                    bk.data = new LRStream(new SEMemoryStream(), 0, 0, false, true, ssss);
                    if (data != null)
                    {
                        bk.data.Decrypt(passwordstc);
                        data.Seek(offset, SeekOrigin.Begin);
                        if (length == -1)
                        {
                            data.CopyTo(bk.data);
                        }
                        else
                        {
                            byte[] raw = ReadFixedBytes(data, length);
                            bk.data.Write(raw, 0, raw.Length);
                        }
                        bk.data.Seek(0, SeekOrigin.Begin);
                        bk.data.CleanPassword();
                    }
                }
                else
                {
                    bk.data = new LRStream(new SEMemoryStream(), 0, 0, false);
                    if (data != null)
                    {
                        data.Seek(offset, SeekOrigin.Begin);
                        if (length == -1)
                        {
                            data.CopyTo(bk.data);
                        }
                        else
                        {
                            byte[] raw = ReadFixedBytes(data, length);
                            bk.data.Write(raw, 0, raw.Length);
                        }
                        bk.data.Seek(0, SeekOrigin.Begin);
                    }
                }

                bk.NameSTC = nmstc;
                BKs = BKs.Append(bk).ToArray();
                nmstcs.Add(nmstc);
                offsets.Add(-1);
                return BoxCount++;
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
                        bk.data = new LRStream(s, offset, length, false, true, ssss);
                        
                        data.Seek(offset, SeekOrigin.Begin);
                        bk.data.Decrypt(passwordstc);
                        bk.data.Seek(0, SeekOrigin.Begin);

                        data.CopyTo(bk.data);
                        bk.data.Seek(0, SeekOrigin.Begin);
                        bk.data.CleanPassword();
                    }
                    else
                    {
                        bk.data = new LRStream(s, 0, 0, false, true, ssss);
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
                    else
                    {
                        SEMemoryStream ms = new SEMemoryStream();
                        bk.data = new LRStream(ms, 0, 0, false, true, ssss);
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
                        bk.data = new LRStream(s, offset, length, false);
                    }
                    else
                    {
                        bk.data = new LRStream(s, 0, 0, false);
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
                        bk.data = new LRStream(data, offset, length, false);
                    }
                    else
                    {
                        SEMemoryStream ms = new SEMemoryStream();
                        bk.data = new LRStream(ms, 0, 0, false);
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
                        if (HasFlag(BKs[i].flg, LRBKFlag.Compress))
                        {
                            if (HasFlag(BKs[i].flg, LRBKFlag.CrossFile))
                            {
                                using Stream s = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Create);
                                long storedLength = WriteStoredStream(BKs[i].data, s, true);
                                BKs[i].Length = (ulong)storedLength;
                                bo.Write((ulong)storedLength);
                                s.Flush();
                            }
                            else
                            {
                                long lengthPosition = bo.Position;
                                bo.Write((ulong)0);
                                long storedLength = WriteStoredStream(BKs[i].data, bo, true);
                                BKs[i].Length = (ulong)storedLength;
                                long endPosition = bo.Position;
                                bo.Seek(lengthPosition, SeekOrigin.Begin);
                                bo.Write((ulong)storedLength);
                                bo.Seek(endPosition, SeekOrigin.Begin);
                            }
                        }
                        else
                        {
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
                        BKs[i].data.UseOrgData(true);
                        BKs[i].data.Seek(0, SeekOrigin.Begin);
                        if (HasFlag(BKs[i].flg, LRBKFlag.Compress))
                        {
                            if (HasFlag(BKs[i].flg, LRBKFlag.CrossFile))
                            {
                                using Stream s = File.Open(string.Format(PT_Ext_PTH, BSDir, BKs[i].NameSTC), FileMode.Create);
                                long storedLength = WriteStoredStream(BKs[i].data, s, true);
                                BKs[i].Length = (ulong)storedLength;
                                bo.Write((ulong)storedLength);
                                s.Flush();
                            }
                            else
                            {
                                long lengthPosition = bo.Position;
                                bo.Write((ulong)0);
                                long storedLength = WriteStoredStream(BKs[i].data, bo, true);
                                BKs[i].Length = (ulong)storedLength;
                                long endPosition = bo.Position;
                                bo.Seek(lengthPosition, SeekOrigin.Begin);
                                bo.Write((ulong)storedLength);
                                bo.Seek(endPosition, SeekOrigin.Begin);
                            }
                        }
                        else if (HasFlag(BKs[i].flg, LRBKFlag.CrossFile))
                        {
                            if (BKs[i].alc)
                            {
                                bo.Write(BKs[i].data.Length);
                            }
                            else
                            {
                                bo.Write(BKs[i].Length);
                            }
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
                            if (BKs[i].alc)
                            {
                                bo.Write(BKs[i].data.Length);
                            }
                            else
                            {
                                bo.Write(BKs[i].Length);
                            }
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
         * LRL V1 RULES:
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
