using SaturnEngine.Asset;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace SaturnEngine.SENet.SEUdp
{
    public struct SEUPack
    {
        public ulong MagicNumber;//8
        public SEUPackInfo Info;//12
    }
    public struct SEUPackInfo
    {
        public TIME PackedTime;//8
        public VERSION PackVersion;//4

    }
}
