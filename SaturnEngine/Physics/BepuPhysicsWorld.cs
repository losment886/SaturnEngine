using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;
using SaturnEngine.Asset;
using SaturnEngine.SEMath;

namespace SaturnEngine.Physics
{
    /// <summary>
    /// 基于 BepuPhysics 2 的物理世界实现，支持动态/运动学/静态刚体、触发器与射线检测。
    /// </summary>
    public sealed class BepuPhysicsWorld : IPhysicsWorld
    {
        /// <summary>每个刚体的引擎侧元数据，按 collidable 打包索引存取。</summary>
        internal sealed class BodyRecord
        {
            public GameObject? Owner;
            public bool IsTrigger;
            public SEBodyHandle Handle;
        }

        /// <summary>在窄相回调与主循环之间传递碰撞对，回调运行在物理线程上故需加锁。</summary>
        internal sealed class CollisionCollector
        {
            private readonly List<(CollidableReference A, CollidableReference B)> _pairs = new();
            private readonly object _lock = new();

            public void Report(CollidableReference a, CollidableReference b)
            {
                lock (_lock)
                    _pairs.Add((a, b));
            }

            public void Drain(List<(CollidableReference A, CollidableReference B)> target)
            {
                lock (_lock)
                {
                    target.AddRange(_pairs);
                    _pairs.Clear();
                }
            }
        }

        private struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
        {
            public CollisionCollector Collector;
            public Dictionary<uint, BodyRecord> Triggers;

            public void Initialize(Simulation simulation) { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
            {
                // 至少一方可移动才需要生成接触。
                return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold,
                out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
            {
                pairMaterial.FrictionCoefficient = 1f;
                pairMaterial.MaximumRecoveryVelocity = 2f;
                pairMaterial.SpringSettings = new SpringSettings(30, 1);

                if (manifold.Count > 0)
                    Collector.Report(pair.A, pair.B);

                // 触发器只上报事件，不产生接触约束。
                bool triggerInvolved = Triggers.ContainsKey(pair.A.Packed) || Triggers.ContainsKey(pair.B.Packed);
                return !triggerInvolved;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB,
                ref ConvexContactManifold manifold) => true;

            public void Dispose() { }
        }

        private struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
        {
            public Vector3 Gravity;
            private Vector3Wide _gravityWideDt;

            public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
            public bool AllowSubstepsForUnconstrainedBodies => false;
            public bool IntegrateVelocityForKinematics => false;

            public void Initialize(Simulation simulation) { }

            public void PrepareForIntegration(float dt)
                => _gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation,
                BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt,
                ref BodyVelocityWide velocity)
                => velocity.Linear += _gravityWideDt;
        }

        private struct RayHitHandler : IRayHitHandler
        {
            public float T;
            public Vector3 Normal;
            public CollidableReference Collidable;
            public bool Hit;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable) => true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowTest(CollidableReference collidable, int childIndex) => true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnRayHit(in RayData ray, ref float maximumT, float t, Vector3 normal,
                CollidableReference collidable, int childIndex)
            {
                if (Hit && t >= T)
                    return;

                Hit = true;
                T = t;
                Normal = normal;
                Collidable = collidable;
                // 收紧上限，让后续测试只关心更近的命中。
                maximumT = t;
            }
        }

        private Simulation? _simulation;
        private BufferPool? _pool;
        private ThreadDispatcher? _dispatcher;
        private readonly CollisionCollector _collector = new();
        private readonly Dictionary<uint, BodyRecord> _triggers = new();
        private readonly Dictionary<uint, BodyRecord> _records = new();
        private readonly List<(CollidableReference A, CollidableReference B)> _pairScratch = new();

        private Vector3D _gravity = new(0, -9.81, 0);

        public string BackendName => "BepuPhysics";
        public bool IsInitialized { get; private set; }

        public Vector3D Gravity
        {
            get => _gravity;
            set
            {
                _gravity = value;
                // Bepu 的重力烘焙在位姿积分回调里，改变后需要重建模拟才能生效，
                // 因此这里仅记录，Initialize 之前设置才有效。
            }
        }

        public void Initialize()
        {
            if (IsInitialized)
                return;

            _pool = new BufferPool();
            _dispatcher = new ThreadDispatcher(Math.Max(1, Environment.ProcessorCount - 1));

            var narrow = new NarrowPhaseCallbacks { Collector = _collector, Triggers = _triggers };
            var pose = new PoseIntegratorCallbacks
            {
                Gravity = new Vector3((float)_gravity.X, (float)_gravity.Y, (float)_gravity.Z),
            };

            _simulation = Simulation.Create(_pool, narrow, pose, new SolveDescription(8, 1));
            IsInitialized = true;
        }

        public void Shutdown()
        {
            if (!IsInitialized)
                return;

            _simulation?.Dispose();
            _simulation = null;
            _dispatcher?.Dispose();
            _dispatcher = null;
            _pool?.Clear();
            _pool = null;

            _records.Clear();
            _triggers.Clear();
            IsInitialized = false;
        }

        #region 刚体管理

        public SEBodyHandle AddBody(in SEBodyDescription description)
        {
            EnsureInitialized();
            var sim = _simulation!;

            var (shapeIndex, inertia) = CreateShape(sim, description.Shape, (float)description.Mass);
            var position = ToVector3(description.Position);

            SEBodyHandle handle;
            CollidableReference reference;

            if (description.BodyType == SEBodyType.Static)
            {
                var staticHandle = sim.Statics.Add(new StaticDescription(position, shapeIndex));
                handle = new SEBodyHandle(staticHandle.Value, true);
                reference = new CollidableReference(staticHandle);
            }
            else
            {
                var collidable = new CollidableDescription(shapeIndex, 0.1f);
                var activity = new BodyActivityDescription(0.01f);
                var pose = new RigidPose(position);
                var velocity = new BodyVelocity(ToVector3(description.LinearVelocity));

                BodyHandle bodyHandle = description.BodyType == SEBodyType.Kinematic
                    ? sim.Bodies.Add(BodyDescription.CreateKinematic(pose, velocity, collidable, activity))
                    : sim.Bodies.Add(BodyDescription.CreateDynamic(pose, velocity, inertia, collidable, activity));

                handle = new SEBodyHandle(bodyHandle.Value, false);
                var mobility = description.BodyType == SEBodyType.Kinematic
                    ? CollidableMobility.Kinematic
                    : CollidableMobility.Dynamic;
                reference = new CollidableReference(mobility, bodyHandle);
            }

            var record = new BodyRecord
            {
                Owner = description.Owner,
                IsTrigger = description.IsTrigger,
                Handle = handle,
            };
            _records[reference.Packed] = record;
            if (description.IsTrigger)
                _triggers[reference.Packed] = record;

            return handle;
        }

        private static (TypedIndex Shape, BodyInertia Inertia) CreateShape(Simulation sim, in SEColliderShape shape, float mass)
        {
            switch (shape.Type)
            {
                case SEShapeType.Sphere:
                {
                    var s = new Sphere((float)shape.Radius);
                    return (sim.Shapes.Add(s), s.ComputeInertia(mass));
                }
                case SEShapeType.Capsule:
                {
                    var s = new Capsule((float)shape.Radius, (float)shape.Length);
                    return (sim.Shapes.Add(s), s.ComputeInertia(mass));
                }
                case SEShapeType.Cylinder:
                {
                    var s = new Cylinder((float)shape.Radius, (float)shape.Length);
                    return (sim.Shapes.Add(s), s.ComputeInertia(mass));
                }
                default:
                {
                    var s = new Box((float)shape.Size.X, (float)shape.Size.Y, (float)shape.Size.Z);
                    return (sim.Shapes.Add(s), s.ComputeInertia(mass));
                }
            }
        }

        public void RemoveBody(SEBodyHandle handle)
        {
            if (!IsInitialized || !handle.IsValid)
                return;

            var sim = _simulation!;
            CollidableReference reference;
            if (handle.IsStatic)
            {
                var staticHandle = new StaticHandle(handle.Id);
                reference = new CollidableReference(staticHandle);
                sim.Statics.Remove(staticHandle);
            }
            else
            {
                var bodyHandle = new BodyHandle(handle.Id);
                reference = FindReference(handle);
                sim.Bodies.Remove(bodyHandle);
            }

            _records.Remove(reference.Packed);
            _triggers.Remove(reference.Packed);
        }

        private CollidableReference FindReference(SEBodyHandle handle)
        {
            foreach (var (packed, record) in _records)
            {
                if (record.Handle.Equals(handle))
                    return new CollidableReference { Packed = packed };
            }
            return default;
        }

        #endregion

        #region 位姿与速度

        public Vector3D GetPosition(SEBodyHandle handle)
        {
            EnsureInitialized();
            var sim = _simulation!;
            if (handle.IsStatic)
                return ToVector3D(sim.Statics[new StaticHandle(handle.Id)].Pose.Position);

            return ToVector3D(sim.Bodies[new BodyHandle(handle.Id)].Pose.Position);
        }

        public void SetPosition(SEBodyHandle handle, in Vector3D position)
        {
            EnsureInitialized();
            var sim = _simulation!;
            var v = ToVector3(position);

            if (handle.IsStatic)
            {
                var s = sim.Statics[new StaticHandle(handle.Id)];
                s.Pose.Position = v;
                return;
            }

            var body = sim.Bodies[new BodyHandle(handle.Id)];
            body.Pose.Position = v;
            body.Awake = true;
        }

        public Vector3D GetLinearVelocity(SEBodyHandle handle)
        {
            EnsureInitialized();
            if (handle.IsStatic)
                return new Vector3D();

            return ToVector3D(_simulation!.Bodies[new BodyHandle(handle.Id)].Velocity.Linear);
        }

        public void SetLinearVelocity(SEBodyHandle handle, in Vector3D velocity)
        {
            EnsureInitialized();
            if (handle.IsStatic)
                return;

            var body = _simulation!.Bodies[new BodyHandle(handle.Id)];
            body.Velocity.Linear = ToVector3(velocity);
            body.Awake = true;
        }

        public void ApplyImpulse(SEBodyHandle handle, in Vector3D impulse)
        {
            EnsureInitialized();
            if (handle.IsStatic)
                return;

            var body = _simulation!.Bodies[new BodyHandle(handle.Id)];
            body.ApplyLinearImpulse(ToVector3(impulse));
            body.Awake = true;
        }

        #endregion

        public void Step(double deltaTime, List<SECollisionEvent> events)
        {
            EnsureInitialized();
            if (deltaTime <= 0)
                return;

            _simulation!.Timestep((float)deltaTime, _dispatcher);

            _pairScratch.Clear();
            _collector.Drain(_pairScratch);

            foreach (var (a, b) in _pairScratch)
            {
                _records.TryGetValue(a.Packed, out var ra);
                _records.TryGetValue(b.Packed, out var rb);

                events.Add(new SECollisionEvent
                {
                    A = ra?.Handle ?? SEBodyHandle.Invalid,
                    B = rb?.Handle ?? SEBodyHandle.Invalid,
                    ObjectA = ra?.Owner,
                    ObjectB = rb?.Owner,
                    IsTrigger = (ra?.IsTrigger ?? false) || (rb?.IsTrigger ?? false),
                });
            }
        }

        public SERaycastHit Raycast(in Vector3D origin, in Vector3D direction, double maxDistance)
        {
            EnsureInitialized();

            var dir = ToVector3(direction);
            float length = dir.Length();
            if (length < 1e-6f)
                return default;
            dir /= length;

            var start = ToVector3(origin);
            var handler = new RayHitHandler();
            _simulation!.RayCast(start, dir, (float)maxDistance, _pool!, ref handler);

            if (!handler.Hit)
                return default;

            _records.TryGetValue(handler.Collidable.Packed, out var record);
            return new SERaycastHit
            {
                Hit = true,
                Body = record?.Handle ?? SEBodyHandle.Invalid,
                Object = record?.Owner,
                Point = ToVector3D(start + dir * handler.T),
                Normal = ToVector3D(handler.Normal),
                Distance = handler.T,
            };
        }

        private static Vector3 ToVector3(in Vector3D v) => new((float)v.X, (float)v.Y, (float)v.Z);
        private static Vector3D ToVector3D(in Vector3 v) => new(v.X, v.Y, v.Z);

        private void EnsureInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("物理世界尚未初始化。");
        }

        public void Dispose() => Shutdown();
    }
}
