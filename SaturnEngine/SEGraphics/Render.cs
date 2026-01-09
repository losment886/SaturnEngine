using SaturnEngine.Base;

namespace SaturnEngine.SEGraphics
{
    public abstract class Render : SEBase
    {
        public Render(string nm = "Render", string desc = "NULL")
            : base(nm, desc)
        {
        }
        public enum Feature : int
        {
            Sync = 1,
            HDR = 2,
            DolbyVision = 3,
        }
        public abstract string[] GetDeviceNames();
        public abstract void Initialize();
        public abstract bool CreateDevice(int index = 0);
        public abstract void DestroyDevice();
        public abstract void RenderFrame(double deltaTime);
        public abstract void PrepareFrame(double deltaTime);
        public abstract void SetSize(int width, int height);
        public abstract void SetPosition(int x, int y);
        public abstract void SetScene(int index);
        public abstract void Close();
        public abstract bool CheckSupport(Feature f);
        public abstract void SetFeature(Feature f, bool enable);
        public abstract void SetUIScene(bool enable);//启用UI场景，当UI存在时渲染，UIscene不存在是即使TRUE也不渲染


    }
}
