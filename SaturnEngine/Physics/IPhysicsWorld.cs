using SaturnEngine.Asset;
using SaturnEngine.SEMath;

namespace SaturnEngine.Physics
{
    /// <summary>刚体类型。</summary>
    public enum SEBodyType
    {
        /// <summary>参与模拟并受力影响。</summary>
        Dynamic,
        /// <summary>由代码驱动位姿，不受力影响，但会推动动态物体。</summary>
        Kinematic,
        /// <summary>完全不动的场景几何。</summary>
        Static,
    }

    /// <summary>碰撞形状类型。</summary>
    public enum SEShapeType
    {
        Box,
        Sphere,
        Capsule,
        Cylinder,
    }

    /// <summary>碰撞形状描述。未使用的字段按形状类型忽略。</summary>
    public struct SEColliderShape
    {
        public SEShapeType Type;
        /// <summary>盒体的全尺寸（宽/高/深）。</summary>
        public Vector3D Size;
        /// <summary>球/胶囊/圆柱的半径。</summary>
        public double Radius;
        /// <summary>胶囊/圆柱的圆柱段长度。</summary>
        public double Length;

        public static SEColliderShape CreateBox(Vector3D size)
            => new() { Type = SEShapeType.Box, Size = size };

        public static SEColliderShape CreateSphere(double radius)
            => new() { Type = SEShapeType.Sphere, Radius = radius };

        public static SEColliderShape CreateCapsule(double radius, double length)
            => new() { Type = SEShapeType.Capsule, Radius = radius, Length = length };

        public static SEColliderShape CreateCylinder(double radius, double length)
            => new() { Type = SEShapeType.Cylinder, Radius = radius, Length = length };
    }

    /// <summary>刚体创建参数。</summary>
    public struct SEBodyDescription
    {
        public SEBodyType BodyType;
        public SEColliderShape Shape;
        public Vector3D Position;
        public Vector3D LinearVelocity;
        public double Mass;
        /// <summary>为真时只上报碰撞事件而不产生碰撞响应。</summary>
        public bool IsTrigger;
        /// <summary>关联的游戏对象，用于把碰撞事件回传到逻辑层。</summary>
        public GameObject? Owner;

        public static SEBodyDescription Default => new()
        {
            BodyType = SEBodyType.Dynamic,
            Shape = SEColliderShape.CreateBox(new Vector3D(1, 1, 1)),
            Position = new Vector3D(),
            LinearVelocity = new Vector3D(),
            Mass = 1.0,
            IsTrigger = false,
            Owner = null,
        };
    }

    /// <summary>物理世界中一个刚体的不透明句柄。</summary>
    public readonly struct SEBodyHandle : IEquatable<SEBodyHandle>
    {
        public readonly int Id;
        public readonly bool IsStatic;

        public SEBodyHandle(int id, bool isStatic)
        {
            Id = id;
            IsStatic = isStatic;
        }

        public bool IsValid => Id >= 0;
        public static SEBodyHandle Invalid => new(-1, false);

        public bool Equals(SEBodyHandle other) => Id == other.Id && IsStatic == other.IsStatic;
        public override bool Equals(object? obj) => obj is SEBodyHandle h && Equals(h);
        public override int GetHashCode() => HashCode.Combine(Id, IsStatic);
    }

    /// <summary>一次碰撞/触发事件。</summary>
    public struct SECollisionEvent
    {
        public SEBodyHandle A;
        public SEBodyHandle B;
        public GameObject? ObjectA;
        public GameObject? ObjectB;
        /// <summary>该次事件是否来自触发器（无碰撞响应）。</summary>
        public bool IsTrigger;
    }

    /// <summary>射线检测结果。</summary>
    public struct SERaycastHit
    {
        public bool Hit;
        public SEBodyHandle Body;
        public GameObject? Object;
        public Vector3D Point;
        public Vector3D Normal;
        public double Distance;
    }

    /// <summary>
    /// 物理世界抽象。具体实现（如 BepuPhysics）负责模拟推进与碰撞事件生成。
    /// </summary>
    public interface IPhysicsWorld : IDisposable
    {
        string BackendName { get; }
        bool IsInitialized { get; }

        Vector3D Gravity { get; set; }

        void Initialize();
        void Shutdown();

        SEBodyHandle AddBody(in SEBodyDescription description);
        void RemoveBody(SEBodyHandle handle);

        Vector3D GetPosition(SEBodyHandle handle);
        void SetPosition(SEBodyHandle handle, in Vector3D position);

        Vector3D GetLinearVelocity(SEBodyHandle handle);
        void SetLinearVelocity(SEBodyHandle handle, in Vector3D velocity);

        /// <summary>施加一次性冲量。</summary>
        void ApplyImpulse(SEBodyHandle handle, in Vector3D impulse);

        /// <summary>推进模拟固定步长，并把本步产生的碰撞事件填入 <paramref name="events"/>。</summary>
        void Step(double deltaTime, List<SECollisionEvent> events);

        SERaycastHit Raycast(in Vector3D origin, in Vector3D direction, double maxDistance);
    }
}
