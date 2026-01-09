using SaturnEngine.Base;

namespace SaturnEngine.Asset
{
    public class SESpirit : SEBase
    {
        public SEImageFile? BaseImage { get; private set; } = null;
        public bool IsLoaded { get { return BaseImage?.IsLoaded ?? false; } }
        public SESpirit()
        {

        }
        public void Load(SEImageFile sif)
        {
            BaseImage = sif;
        }
    }
}
