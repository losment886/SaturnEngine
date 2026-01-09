using SaturnEngine.Base;
using SaturnEngine.SEComponents;
using SaturnEngine.Security;

namespace SaturnEngine.Asset
{
    public class SEComponents : SEBase
    {
        List<SEComponent> components;
        List<ulong> nms;
        List<UUID> uuids;
        List<SEComponentType> componentTypes;
        GameObject owner;
        public SEComponents(GameObject go)
        {
            components = new List<SEComponent>();
            nms = new List<ulong>();
            uuids = new List<UUID>();
            componentTypes = new List<SEComponentType>();
            owner = go;
        }
        ~SEComponents()
        {
            components = null;
            nms = null;
            uuids = null;
            componentTypes = null;
        }
        public int Count()
        {
            return nms.Count;
        }

        public void Add(SEComponent component)
        {
            components.Add(component);
            nms.Add(component.STC);
            uuids.Add(component.Uuid);
            componentTypes.Add(component.CType);
        }
        public void Remove(SEComponent component)
        {
            int i = components.IndexOf(component);
            components.RemoveAt(i);
            uuids.RemoveAt(i);
            nms.RemoveAt(i);
            componentTypes.RemoveAt(i);
        }
        public void RemoveAt(int index)
        {
            components.RemoveAt(index);
            uuids.RemoveAt(index);
            nms.RemoveAt(index);
            componentTypes.RemoveAt(index);
        }
        public SEComponent? Search(SEComponentType tp)
        {
            try
            {
                return components[componentTypes.IndexOf(tp)];
            }
            catch
            {
                return null;
            }
        }
        public SEComponent[] Search(ulong nm, UUID? id = null)
        {
            List<SEComponent> l = new List<SEComponent>();
            for (int i = 0; i < nms.Count; i++)
            {
                if (nms[i] == nm)
                {
                    if (id.HasValue)
                    {
                        if (uuids[i] == id)
                        {
                            l.Add(components[i]);
                        }
                    }
                    else
                    {
                        l.Add(components[i]);
                    }
                }
            }
            return l.ToArray();
        }
        public SEComponent? SearchOne(ulong nm, UUID? id = null)
        {
            try
            {
                if (id.HasValue)
                {
                    return components[uuids.IndexOf(id.Value)];
                }
                else
                {
                    return components[nms.IndexOf(nm)];
                }
            }
            catch
            {
                return null;
            }

        }
        public SEComponent CreateAndAddComponent(SEComponentType tp)
        {
            SEComponent c = null;
            switch (tp)
            {
                case SEComponentType.Animator:
                    c = new Animator();
                    break;
                case SEComponentType.Script:
                    throw new Exception("无法创建该类型组件".GetInCurrLang());
                case SEComponentType.Model3D:
                    c = new Model3D();
                    break;
                case SEComponentType.Spirit2D:
                    c = new Spirit2D();
                    break;
                case SEComponentType.CollisionBox2D:
                    c = new CollisionBox2D();
                    break;
                case SEComponentType.CollisionBox3D:
                    //c = new CollisionBox3D();
                    throw new Exception("无法创建该类型组件".GetInCurrLang());
                case SEComponentType.Controller2D:
                    c = new Controller2D();
                    break;
                default:
                    throw new Exception("无法创建该类型组件".GetInCurrLang());
            }
            //c.Owner = owner;
            c.SetOwner(owner);
            Add(c);
            return c;
        }


    }
    public enum SEComponentType : int
    {
        None = 0,
        Animator = 1,
        Script = 2,//特指JS
        Model3D = 3,
        Spirit2D = 4,
        CollisionBox2D = 5,
        CollisionBox3D = 6,
        Controller2D = 7,
        Controller3D = 8,
    }
    public abstract class SEComponent : SEBase
    {
        public SEComponentType CType { get; protected set; }
        public GameObject Owner { get; protected set; }
        public SEComponent()
        {
            CType = SEComponentType.None;
            Owner = null;
        }
        public void SetOwner(GameObject go)
        {
            if (Owner == null)
            {
                Owner = go;
            }
        }

    }
}
