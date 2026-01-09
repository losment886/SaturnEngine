using SaturnEngine.Asset;
using SaturnEngine.SEMath;

namespace SaturnEngine.SEComponents
{
    /// <summary>
    /// 2D碰撞盒组件，用于2D物理碰撞检测
    /// </summary>
    public class CollisionBox2D : SEComponent
    {
        public SERect Box { get; set; }
        public bool IsTrigger { get; set; }//是否为触发器
        public bool Enabled { get; set; }
        public bool AllowCollision { get; set; }//是否允许碰撞，为否则不进行碰撞检测，但仍然触发触发器，此时允许穿透
        public bool OnTrigger { get; set; }//是否正在触发触发器

        public CollisionBox2D()
        {
            CType = SEComponentType.CollisionBox2D;
            Box = new SERect([[0, 0], [0, 0]]);//默认为空盒，且是矩形
            IsTrigger = false;
            Enabled = false;
            AllowCollision = false;
        }
        /// <summary>
        /// 使能碰撞盒，并注册到物理引擎，如果enabled为false则删除已注册的碰撞盒
        /// </summary>
        public void RegisterBox()
        {
            if (Enabled)
            {
                Owner.OwnerScene.ThisTrigManager.Add(Owner);
            }
            else
            {
                Owner.OwnerScene.ThisTrigManager.Remove(Owner);
            }
        }
    }
}
