using SaturnEngine.Base;
using SaturnEngine.SEUI;

namespace SaturnEngine.Asset
{
    public class UIScene : SEBase, IUpdateLoop
    {
        public SEControls Controls = new SEControls();
        public SEUIAssembly UIA;
        public SEUILL UILL;
        public void Update(double deltaTime)
        {

            //throw new NotImplementedException();
            Controls.Update(deltaTime);
        }

        public void LoadUICode(string code)
        {
            UILL = new SEUILL();
            UILL.LoadFromString(code);
            UIA = UILL.CompileCode();
            if (UIA != null)
            {
                UIA.Load();
                var controls = UIA.RunMain();
                Controls.Controls = controls;
                Controls.Init();
            }
        }
        public void LoadUIFromFile(string path)
        {
            UILL = new SEUILL();
            UILL.LoadFromFile(path);
            UIA = UILL.CompileCode();
            if (UIA != null)
            {
                UIA.Load();
                var controls = UIA.RunMain();
                Controls.Controls = controls;
                Controls.Init();
            }

        }

    }
}
