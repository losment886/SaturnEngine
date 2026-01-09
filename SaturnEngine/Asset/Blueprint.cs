using SaturnEngine.Base;

namespace SaturnEngine.Asset
{
    public class CONST
    {
        public const ulong NULL = 0;
        /// <summary>
        /// 模型
        /// </summary>
        public const ulong MODEL = 1371285712849;
        /// <summary>
        /// 模型贴图
        /// </summary>
        public const ulong TEXTURE = 4370398836310950110;
        /// <summary>
        /// 2D精灵
        /// </summary>
        public const ulong SPIRIT = 420727632039048;
        /// <summary>
        /// 动画脚本
        /// </summary>
        public const ulong ANIMATION = 7643591982128930642;
        /// <summary>
        /// 声音
        /// </summary>
        public const ulong SOUND = 29872175726066;
        /// <summary>
        /// 脚本
        /// </summary>
        public const ulong SCRIPT = 276952731793848;
        /// <summary>
        /// 字体
        /// </summary>
        public const ulong FONT = 34937051641;
        /// <summary>
        /// 内置效果器（粒子光照等）
        /// </summary>
        public const ulong INLINEEFFECT = 1260916029145854812;
    }
    public class IMPORTINFO
    {
        public string Name { get; set; }
        public ulong NameSTC { get; set; }
        public string Description { get; set; }
        public string FilePath { get; set; } //文件路径或内联名
        public ulong Type { get; set; } //类型，可能是模型、纹理、动画等，统一名称以常量存在，类型STC码
    }
    /// <summary>
    /// 描述地图场景（最初始状态）
    /// </summary>
    public unsafe class Blueprint : SEBase
    {
        //Silk.NET.Assimp.Scene*[] ScenesPtr;
        public Blueprint(string serfilepath)
        {
            /*
            SEResource s = new SEResource(serfilepath);
            var ap = Silk.NET.Assimp.Assimp.GetApi();
            //Silk.NET.Assimp.Scene* ss = ap.ImportFile("", (uint)(PostProcessSteps.Triangulate | PostProcessSteps.FlipUVs));
            Name = Encoding.UTF8.GetString(s.ReadAll(s.GetBlock("NAME")).V1);
            Description = Encoding.UTF8.GetString(s.ReadAll(s.GetBlock("DESCRIPTION")).V1);
            //读取蓝图
            DataLayout dl = new DataLayout();
            var ccp = s.ReadAll(s.GetBlock("IMPORT_COUNT")).V1;//包括内部的内容和外部的内容,大小为ulong,对于导入的内容，用json描述
            dl.B0 = ccp[0];
            dl.B1 = ccp[1];
            dl.B2 = ccp[2];
            dl.B3 = ccp[3];
            dl.B4 = ccp[4];
            dl.B5 = ccp[5];
            dl.B6 = ccp[6];
            dl.B7 = ccp[7];
            ulong ic = dl.UL;
            for (ulong i = 0; i < ic; i++)
            {
                string NIM = "IC:" + i;
                s.ReadAll(s.GetBlock(NIM));
                IMPORTINFO io = (IMPORTINFO)JsonSerializer.Deserialize(s.ReadAll(s.GetBlock(NIM)).V1, typeof(IMPORTINFO));
                Console.WriteLine($"Load Object:{io.Name} Type:{io.Type}");
                switch (io.Type)
                {
                    case CONST.TEXTURE:
                        break;
                    case CONST.ANIMATION:
                        break;
                    case CONST.MODEL:
                        if (System.IO.File.Exists(io.FilePath))
                        {

                        }
                        else
                        {

                        }
                        break;
                    case CONST.INLINEEFFECT:
                        break;
                    case CONST.FONT:
                        break;
                    case CONST.SOUND:
                        break;
                    case CONST.SCRIPT:
                        break;
                }
            }
            */
        }
    }
}
