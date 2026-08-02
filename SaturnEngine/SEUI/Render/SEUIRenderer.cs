using SaturnEngine.Asset;
using SaturnEngine.SEGraphics.Native;
using SaturnEngine.SEMath;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SaturnEngine.SEUI.Render
{
    /// <summary>
    /// 由控件实现，用于在渲染器构建阶段追加自定义文本绘制。
    /// </summary>
    public interface ISEUITextDrawable
    {
        void Draw(SEUIRenderer renderer);
    }

    /// <summary>
    /// UI 渲染器：持有独立的 UI 场景/网格/材质/图集，把控件树转换为一批
    /// 顶点后通过原生接口提交。使用正交相机将窗口像素坐标直接映射到裁剪空间，
    /// 材质固定为 Alpha 混合、双面、不投影不接收阴影，从而保证 UI 始终以
    /// 半透明、最顶层的方式绘制。
    /// </summary>
    public unsafe class SEUIRenderer : IDisposable
    {
        private readonly SEUIAtlas _atlas;
        private readonly SEUIDrawList _drawList;

        private ulong _scene;
        private ulong _mesh;
        private ulong _material;
        private ulong _object;

        private bool _sceneCreated;
        private bool _meshCreated;
        private bool _materialCreated;
        private bool _objectCreated;
        private bool _disposed;

        private int _pixelWidth;
        private int _pixelHeight;

        /// <summary>控件图片在图集中的注册缓存，key 为精灵实例。</summary>
        private readonly Dictionary<SESpirit, UVRect> _spiritUV = new();

        public SEUIAtlas Atlas => _atlas;
        public SEUIDrawList DrawList => _drawList;
        public SEUIFont? DefaultFont { get; set; }
        public ulong SceneHandle => _scene;

        public SEUIRenderer(int atlasSize = 1024)
        {
            _atlas = new SEUIAtlas(atlasSize);
            _drawList = new SEUIDrawList(_atlas);
        }

        /// <summary>
        /// 创建 UI 专用场景。必须在渲染设备就绪后调用。
        /// </summary>
        public void Initialize()
        {
            if (_sceneCreated)
                return;

            ulong scene;
            NRNative.CreateScene(&scene);
            _scene = scene;
            _sceneCreated = true;

            // UI 作为叠加场景，在主场景之后绘制，不会顶替掉当前活动场景
            NRNative.SetOverlayScene(_scene);
        }

        /// <summary>
        /// 遍历控件树生成绘制数据。像素尺寸用于正交投影与裁剪。
        /// </summary>
        public void Build(SEControls controls, int pixelWidth, int pixelHeight)
        {
            ArgumentNullException.ThrowIfNull(controls);

            _pixelWidth = Math.Max(1, pixelWidth);
            _pixelHeight = Math.Max(1, pixelHeight);

            _drawList.Clear();

            var roots = new List<SEControl>(controls.Controls);
            roots.Sort(static (a, b) => a.ZOrder.CompareTo(b.ZOrder));

            foreach (var control in roots)
                BuildControl(control);
        }

        private void BuildControl(SEControl? control)
        {
            if (control is null || !control.Visible || control.Position is null)
                return;

            double opacity = control.GetEffectiveOpacity();
            if (opacity <= 0d)
                return;

            var pos = control.Position.Value;
            float x = (float)pos[0][0];
            float y = (float)pos[0][1];
            float w = (float)(pos[1][0] - pos[0][0]);
            float h = (float)(pos[1][1] - pos[0][1]);
            var rect = new SEUIRect(x, y, w, h);

            if (w > 0f && h > 0f)
            {
                var uv = TryRegisterSpirit(control.Spirit);
                if (uv.HasValue)
                    _drawList.AddImage(rect, uv.Value, control.Tint, opacity, control.Angle);
                else
                    _drawList.AddRectFilled(rect, control.Tint, opacity, control.Angle);
            }

            // 使用图集字体的控件自行追加字形，位图字体控件已由上面的 Spirit 分支绘制
            (control as ISEUITextDrawable)?.Draw(this);

            if (control.Child is null)
                return;

            // 子控件裁剪到父控件范围内，并按 ZOrder 绘制
            _drawList.PushClipRect(rect);

            var children = new List<SEControl>(control.Child);
            children.Sort(static (a, b) => a.ZOrder.CompareTo(b.ZOrder));
            foreach (var child in children)
                BuildControl(child);

            _drawList.PopClipRect();
        }

        private UVRect? TryRegisterSpirit(SESpirit? spirit)
        {
            if (spirit is null || !spirit.IsLoaded || spirit.BaseImage is null)
                return null;

            if (_spiritUV.TryGetValue(spirit, out var cached))
                return cached;

            var uv = _atlas.Register(spirit.BaseImage, $"spirit_{spirit.GetHashCode()}");
            _spiritUV[spirit] = uv;
            return uv;
        }

        /// <summary>
        /// 在绘制列表上追加一段文本（供 Label 等控件在 Build 之后调用）。
        /// </summary>
        public void AddText(string text, float x, float y, SEColor color, double opacity, SEUIFont? font = null)
        {
            var target = font ?? DefaultFont;
            if (target is null)
                return;
            _drawList.AddText(text, x, y, color, opacity, target);
        }

        /// <summary>
        /// 上传图集与网格数据并配置正交相机。必须在 PrepareFrame 之前调用。
        /// </summary>
        public void Flush()
        {
            if (!_sceneCreated)
                return;

            _atlas.Flush();

            var (vertices, indices) = _drawList.GetData();
            if (vertices.Length == 0 || indices.Length == 0)
            {
                if (_objectCreated)
                    NRNative.SetObjectVisible(_object, 0);
                return;
            }

            EnsureMaterial();

            fixed (NRVertex* vptr = vertices)
            fixed (uint* iptr = indices)
            {
                if (!_meshCreated)
                {
                    var meshInfo = new NRMeshCreateInfo
                    {
                        Vertices = vptr,
                        VertexCount = (uint)vertices.Length,
                        Indices = iptr,
                        IndexCount = (uint)indices.Length,
                        Dynamic = 1,
                        BoundsMin = new NRFloat3(0f, 0f, -1f),
                        BoundsMax = new NRFloat3(_pixelWidth, _pixelHeight, 1f)
                    };

                    ulong mesh;
                    NRNative.CreateMesh(&meshInfo, &mesh);
                    _mesh = mesh;
                    _meshCreated = true;
                }
                else
                {
                    NRNative.UpdateMesh(_mesh, vptr, (uint)vertices.Length, iptr, (uint)indices.Length);
                }
            }

            EnsureObject();
            NRNative.SetObjectVisible(_object, 1);
            UpdateCamera();
        }

        private void EnsureMaterial()
        {
            var info = new NRMaterialCreateInfo
            {
                BaseColorFactor = new NRFloat4(1f, 1f, 1f, 1f),
                EmissiveFactor = new NRFloat3(0f, 0f, 0f),
                MetallicFactor = 0f,
                RoughnessFactor = 1f,
                NormalScale = 1f,
                OcclusionStrength = 0f,
                AlphaCutoff = 0f,
                BlendMode = NRBlendMode.Alpha,
                DoubleSided = 1,
                CastShadow = 0,
                ReceiveShadow = 0,
                BaseColorTex = _atlas.TextureHandle
            };

            if (!_materialCreated)
            {
                ulong material;
                NRNative.CreateMaterial(&info, &material);
                _material = material;
                _materialCreated = true;
            }
            else
            {
                // 图集扩容会重建纹理，需要同步材质引用
                NRNative.UpdateMaterial(_material, &info);
            }
        }

        private void EnsureObject()
        {
            var desc = new NRObjectDesc
            {
                World = NRMatrix4.FromMatrix(Matrix4x4.Identity),
                Mesh = _mesh,
                Material = _material,
                Visible = 1,
                CastShadow = 0,
                LayerMask = 0xFFFFFFFF,
                BoneMatrices = null,
                BoneCount = 0
            };

            if (!_objectCreated)
            {
                ulong obj;
                NRNative.AddObject(_scene, &desc, &obj);
                _object = obj;
                _objectCreated = true;
            }
            else
            {
                NRNative.UpdateObject(_object, &desc);
            }
        }

        /// <summary>
        /// 正交相机：把左上原点的窗口像素坐标映射到标准裁剪空间。
        /// </summary>
        private void UpdateCamera()
        {
            var projection = Matrix4x4.CreateOrthographicOffCenter(
                left: 0f,
                right: _pixelWidth,
                bottom: _pixelHeight,   // 下边界大于上边界即可翻转 Y，使原点位于左上
                top: 0f,
                zNearPlane: -1f,
                zFarPlane: 1f);

            var desc = new NRCameraDesc
            {
                View = NRMatrix4.FromMatrix(Matrix4x4.Identity),
                Projection = NRMatrix4.FromMatrix(projection),
                Position = new NRFloat3(0f, 0f, 0f),
                NearPlane = -1f,
                FarPlane = 1f,
                FovYRadians = 0f,
                Aspect = (float)_pixelWidth / _pixelHeight,
                Orthographic = 1,
                OrthoSize = _pixelHeight
            };

            NRNative.SetCamera(_scene, &desc);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            // 严格逆序：对象 → 材质 → 纹理(图集) → 网格 → 场景
            if (_objectCreated)
            {
                NRNative.RemoveObject(_object);
                _objectCreated = false;
            }

            if (_materialCreated)
            {
                NRNative.DestroyMaterial(_material);
                _materialCreated = false;
            }

            // 图集纹理仍被材质引用，必须在材质销毁之后释放
            _atlas.Dispose();

            if (_meshCreated)
            {
                NRNative.DestroyMesh(_mesh);
                _meshCreated = false;
            }

            if (_sceneCreated)
            {
                NRNative.SetOverlayScene(0);
                NRNative.DestroyScene(_scene);
                _sceneCreated = false;
            }

            DefaultFont?.Dispose();
            _spiritUV.Clear();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
