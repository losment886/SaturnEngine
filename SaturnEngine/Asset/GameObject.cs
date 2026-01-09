using SaturnEngine.Base;
using SaturnEngine.SEMath;

namespace SaturnEngine.Asset
{
    public class GameObject : SEBase, ITransform, IComponent
    {
        //注，GameObject大小取决于最大可渲染组件的大小，例如2D中就是Spirit的大小。
        Transform ITransform.Transform { get; set; }//操作角色坐标到空间坐标
        SEComponents IComponent.Components { get; set; }

        public Scene OwnerScene { get; private set; }

        public GameObject(Scene s, string? nm = null, string? desc = null)
            : base(nm ?? "UnknowName", desc ?? "NULL")
        {
            ((IComponent)this).Components = new SEComponents(this);
            ((ITransform)this).Transform = new Transform();
            OwnerScene = s;
        }
    }
}
