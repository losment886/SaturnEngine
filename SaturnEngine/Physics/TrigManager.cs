using SaturnEngine.Asset;
using SaturnEngine.Base;
using SaturnEngine.Management;
using SaturnEngine.SEComponents;
using SaturnEngine.SEMath;

namespace SaturnEngine.Physics
{
    /// <summary>
    /// 场景级碰撞与物理管理器：维护注册对象、驱动 <see cref="IPhysicsWorld"/> 模拟，
    /// 并把结果同步回各游戏对象的 Transform。
    /// </summary>
    public class TrigManager : SEBase, IDisposable
    {
        /// <summary>已注册到本管理器的游戏对象。</summary>
        public List<GameObject> gos;

        /// <summary>底层物理世界，未调用 <see cref="InitializePhysics"/> 时为 null。</summary>
        public IPhysicsWorld? World { get; private set; }

        /// <summary>本帧产生的碰撞与触发事件。</summary>
        public IReadOnlyList<SECollisionEvent> CollisionEvents => _events;

        public delegate void CollisionHandler(in SECollisionEvent e);
        public event CollisionHandler? OnCollision;
        public event CollisionHandler? OnTrigger;

        /// <summary>固定物理步长，秒。</summary>
        public double FixedTimeStep { get; set; } = 1.0 / 60.0;

        private readonly List<SECollisionEvent> _events = new();
        private readonly Dictionary<GameObject, CollisionBox3D> _bodies = new();
        private double _accumulator;

        public TrigManager()
        {
            gos = new List<GameObject>();
        }

        /// <summary>创建并启用物理世界，未调用时本管理器仅作为对象容器。</summary>
        public void InitializePhysics(Vector3D? gravity = null)
        {
            if (World is not null)
                return;

            var world = new BepuPhysicsWorld();
            if (gravity.HasValue)
                world.Gravity = gravity.Value;

            try
            {
                world.Initialize();
                World = world;
            }
            catch (Exception ex)
            {
                SELogger.Error($"物理世界初始化失败: {ex.Message}", "TrigManager");
                world.Dispose();
            }
        }

        public void Add(GameObject go)
        {
            if (gos.Contains(go))
                return;

            gos.Add(go);
            RegisterBody(go);
        }

        public void Remove(GameObject go)
        {
            if (!gos.Remove(go))
                return;

            if (_bodies.TryGetValue(go, out var collider))
            {
                World?.RemoveBody(collider.Body);
                collider.Body = SEBodyHandle.Invalid;
                _bodies.Remove(go);
            }
        }

        /// <summary>把对象上的 3D 碰撞体注册到物理世界。</summary>
        private void RegisterBody(GameObject go)
        {
            if (World is null)
                return;

            if (((IComponent)go).Components?.Search(SEComponentType.CollisionBox3D) is not CollisionBox3D collider)
                return;

            var transform = ((ITransform)go).Transform;
            var description = new SEBodyDescription
            {
                BodyType = collider.BodyType,
                Shape = collider.Shape,
                Position = transform?.BaseVector ?? new Vector3D(),
                LinearVelocity = new Vector3D(),
                Mass = collider.Mass,
                IsTrigger = collider.IsTrigger,
                Owner = go,
            };

            collider.Body = World.AddBody(in description);
            _bodies[go] = collider;
        }

        /// <summary>
        /// 以固定步长推进物理，并把新位姿写回 Transform。可用可变帧间隔安全调用。
        /// </summary>
        public void Update(double deltaTime)
        {
            if (World is null || deltaTime <= 0)
                return;

            _accumulator += deltaTime;
            // 限制单帧最多补偿的步数，避免卡顿后出现死亡螺旋。
            int maxSteps = 8;

            _events.Clear();
            while (_accumulator >= FixedTimeStep && maxSteps-- > 0)
            {
                World.Step(FixedTimeStep, _events);
                _accumulator -= FixedTimeStep;
            }
            if (maxSteps <= 0)
                _accumulator = 0;

            SyncTransforms();
            DispatchEvents();
        }

        private void SyncTransforms()
        {
            foreach (var (go, collider) in _bodies)
            {
                if (!collider.Body.IsValid)
                    continue;

                var transform = ((ITransform)go).Transform;
                if (transform is not null)
                    transform.BaseVector = World!.GetPosition(collider.Body);
            }
        }

        private void DispatchEvents()
        {
            foreach (var collider in _bodies.Values)
                collider.OnTrigger = false;

            foreach (var e in _events)
            {
                MarkTriggered(e.ObjectA);
                MarkTriggered(e.ObjectB);

                if (e.IsTrigger)
                    OnTrigger?.Invoke(in e);
                else
                    OnCollision?.Invoke(in e);
            }
        }

        private void MarkTriggered(GameObject? go)
        {
            if (go is not null && _bodies.TryGetValue(go, out var collider))
                collider.OnTrigger = true;
        }

        /// <summary>对物理世界执行射线检测。</summary>
        public SERaycastHit Raycast(Vector3D origin, Vector3D direction, double maxDistance)
            => World?.Raycast(in origin, in direction, maxDistance) ?? default;

        public void Dispose()
        {
            World?.Dispose();
            World = null;
            _bodies.Clear();
            _events.Clear();
            gos.Clear();
        }
    }
}
