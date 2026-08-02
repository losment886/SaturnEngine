using SaturnEngine.Asset;
using SaturnEngine.SEComponents;
using SaturnEngine.SEMath;

namespace SaturnEngine.SEComponents
{
    /// <summary>
    /// 3D 碰撞体组件，把游戏对象接入场景的物理世界。
    /// </summary>
    public class CollisionBox3D : SEComponent
    {
        /// <summary>碰撞形状，默认为单位立方体。</summary>
        public SaturnEngine.Physics.SEColliderShape Shape { get; set; }
            = SaturnEngine.Physics.SEColliderShape.CreateBox(new Vector3D(1, 1, 1));

        public SaturnEngine.Physics.SEBodyType BodyType { get; set; }
            = SaturnEngine.Physics.SEBodyType.Dynamic;

        public double Mass { get; set; } = 1.0;

        /// <summary>为真时只上报事件而不产生碰撞响应。</summary>
        public bool IsTrigger { get; set; }

        public bool Enabled { get; set; }

        /// <summary>是否正在与其它碰撞体接触，由物理更新写入。</summary>
        public bool OnTrigger { get; internal set; }

        /// <summary>注册到物理世界后得到的刚体句柄。</summary>
        public SaturnEngine.Physics.SEBodyHandle Body { get; internal set; }
            = SaturnEngine.Physics.SEBodyHandle.Invalid;

        public CollisionBox3D()
        {
            CType = SEComponentType.CollisionBox3D;
        }

        /// <summary>
        /// 依据 <see cref="Enabled"/> 把本碰撞体注册到或移出场景的物理世界。
        /// </summary>
        public void RegisterBox()
        {
            var manager = Owner?.OwnerScene?.ThisTrigManager;
            if (manager is null)
                return;

            if (Enabled)
                manager.Add(Owner!);
            else
                manager.Remove(Owner!);
        }
    }

    /// <summary>
    /// 3D 角色控制器，提供移动、跳跃与重力参数。实际位移由物理世界执行。
    /// </summary>
    public class Controller3D : SEComponent
    {
        public double MoveSpeed { get; set; } = 1.0;
        public double JumpSpeed { get; set; } = 1.0;
        public Vector3D Gravity { get; set; } = new Vector3D(0, -9.81, 0);

        /// <summary>是否踩在地面上，由物理更新通过向下射线检测写入。</summary>
        public bool IsGrounded { get; internal set; }

        /// <summary>本帧期望的移动方向，由游戏逻辑写入。</summary>
        public Vector3D MoveInput { get; set; } = new Vector3D();

        public Controller3D()
        {
            CType = SEComponentType.Controller3D;
        }
    }
}
