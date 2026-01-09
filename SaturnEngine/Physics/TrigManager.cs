using SaturnEngine.Asset;
using SaturnEngine.Base;

namespace SaturnEngine.Physics
{
    public class TrigManager : SEBase
    {
        public List<GameObject> gos;
        public TrigManager()
        {
            gos = new List<GameObject>();
        }
        ~TrigManager()
        {
            gos.Clear();
            gos = null;
        }
        public void Add(GameObject go)
        {
            
            if (!gos.Contains(go))
                gos.Add(go);
        }
        public void Remove(GameObject go)
        {
            if (gos.Contains(go))
                gos.Remove(go);
        }

    }
}
