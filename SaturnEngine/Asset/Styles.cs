using SaturnEngine.Base;
using SaturnEngine.Global;
using SaturnEngine.Security;
using SaturnEngine.SEMath;

namespace SaturnEngine.Asset
{
    public class WindowStyle : SEBase
    {
        public string Title;
        public SEImageFile ICON;
        public SEImageFile Cursor;
        public Vector2D PointSet;
        public WindowStyle() { }
        public static WindowStyle? GetDefault()
        {
            if (GVariables.GlobalResources.TryGetValue(GVariables.DefaultEngineResources, out LRL l))
            {
                WindowStyle ws = new WindowStyle();
                ws.Title = GVariables.ProgramName ?? "Saturn Engine";
                ws.ICON = new SEImageFile();
                ws.ICON.LoadImageFromLRL(l, STCCode.GetSTC("./icon.png"), GVariables.DefaultEngineResourcesPassword);
                ws.Cursor = new SEImageFile();
                ws.Cursor.LoadImageFromLRL(l, STCCode.GetSTC("./cursor.png"), GVariables.DefaultEngineResourcesPassword);
                ws.PointSet = new Vector2D(16, 16);
                return ws;
            }
            return null;
        }
    }
}
