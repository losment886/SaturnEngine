using System.Runtime.InteropServices;

namespace SaturnEngine.SEGraphics.Native
{
    // ============================================================
    // 与 SENativeRenderer/NRApi.h 严格一一对应的托管镜像。
    //
    // 原生侧对所有跨界结构体使用 #pragma pack(push, 8)，因此这里统一
    // 使用 LayoutKind.Sequential + Pack = 8。字段顺序、类型宽度、
    // 数组长度都必须与 C 侧完全一致，任何改动都要同步修改两侧。
    //
    // C 侧的 b32 是 32 位布尔，托管侧用 int 表示（不能用 bool，
    // 因为默认封送为 4 字节 Win32 BOOL 在非 Windows 上不可靠）。
    // ============================================================

    #region 常量

    /// <summary>渲染器能力位掩码，对应 NR_FEATURE_*。</summary>
    [Flags]
    public enum NRFeature : uint
    {
        None = 0,
        Anisotropy = 1u << 0,
        SampleRateShading = 1u << 1,
        DescriptorIndexing = 1u << 2,
        GeometryShader = 1u << 3,
        Tessellation = 1u << 4,
        Compute = 1u << 5,
        RayTracing = 1u << 6,
        HdrSwapchain = 1u << 7,
        MultiDrawIndirect = 1u << 8,
    }

    /// <summary>纹理格式，对应 NR_TEXFMT_*。</summary>
    public enum NRTextureFormat : uint
    {
        R8G8B8A8Unorm = 0,
        R8G8B8A8Srgb = 1,
        R8Unorm = 2,
        R16G16B16A16Float = 3,
        R32G32B32A32Float = 4,
        Bc7Srgb = 5,
        Astc4x4Srgb = 6,
        Etc2Srgb = 7,
        D32Float = 8,
    }

    /// <summary>纹理类型，对应 NR_TEXTYPE_*。</summary>
    public enum NRTextureType : uint
    {
        Texture2D = 0,
        Cube = 1,
        Array = 2,
        Texture3D = 3,
    }

    /// <summary>采样器寻址模式，对应 NR_WRAP_*。</summary>
    public enum NRWrapMode : uint
    {
        Repeat = 0,
        MirroredRepeat = 1,
        ClampEdge = 2,
        ClampBorder = 3,
    }

    /// <summary>材质混合模式，对应 NR_BLEND_*。</summary>
    public enum NRBlendMode : uint
    {
        Opaque = 0,
        Mask = 1,
        Alpha = 2,
        Add = 3,
        Multiply = 4,
    }

    /// <summary>光源类型，对应 NR_LIGHT_*。</summary>
    public enum NRLightType : uint
    {
        Directional = 0,
        Point = 1,
        Spot = 2,
        Area = 3,
    }

    /// <summary>着色器阶段，对应 NR_SHADER_STAGE_*。</summary>
    public enum NRShaderStage : uint
    {
        Vertex = 0,
        Fragment = 1,
        Compute = 2,
        Geometry = 3,
    }

    /// <summary>事件类型，对应 NR_EVENT_*。</summary>
    public enum NREventType : uint
    {
        None = 0,
        Quit = 1,
        WindowResize = 2,
        WindowMove = 3,
        WindowFocus = 4,
        WindowMinimize = 5,
        KeyDown = 10,
        KeyUp = 11,
        TextInput = 12,
        MouseMove = 20,
        MouseDown = 21,
        MouseUp = 22,
        MouseWheel = 23,
        TouchDown = 30,
        TouchUp = 31,
        TouchMove = 32,
        GamepadAdded = 40,
        GamepadRemoved = 41,
        GamepadButton = 42,
        GamepadAxis = 43,
        Sensor = 50,
    }

    /// <summary>NRResult 中的严重级别，对应 NRR_SEVERITY_*。</summary>
    public enum NRSeverity : byte
    {
        Log = 0,
        Warning = 1,
        Error = 2,
    }

    #endregion

    #region 基础数学结构

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NRFloat2
    {
        public float X;
        public float Y;

        public NRFloat2(float x, float y) { X = x; Y = y; }

        public static implicit operator NRFloat2(System.Numerics.Vector2 v) => new(v.X, v.Y);
        public static implicit operator System.Numerics.Vector2(NRFloat2 v) => new(v.X, v.Y);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NRFloat3
    {
        public float X;
        public float Y;
        public float Z;

        public NRFloat3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static implicit operator NRFloat3(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
        public static implicit operator System.Numerics.Vector3(NRFloat3 v) => new(v.X, v.Y, v.Z);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NRFloat4
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public NRFloat4(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }

        public static implicit operator NRFloat4(System.Numerics.Vector4 v) => new(v.X, v.Y, v.Z, v.W);
        public static implicit operator System.Numerics.Vector4(NRFloat4 v) => new(v.X, v.Y, v.Z, v.W);
        public static implicit operator NRFloat4(System.Numerics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
        public static implicit operator System.Numerics.Quaternion(NRFloat4 v) => new(v.X, v.Y, v.Z, v.W);
    }

    /// <summary>
    /// 行主序 4x4 矩阵，与 C 侧 NRMatrix4 的 f32 m[16] 对应。
    /// System.Numerics.Matrix4x4 同样是行主序且内存布局为连续 16 个 float，
    /// 因此可以直接按位重解释，无需转置。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct NRMatrix4
    {
        public fixed float M[16];

        public static NRMatrix4 FromMatrix(in System.Numerics.Matrix4x4 m)
        {
            NRMatrix4 r = default;
            r.M[0] = m.M11; r.M[1] = m.M12; r.M[2] = m.M13; r.M[3] = m.M14;
            r.M[4] = m.M21; r.M[5] = m.M22; r.M[6] = m.M23; r.M[7] = m.M24;
            r.M[8] = m.M31; r.M[9] = m.M32; r.M[10] = m.M33; r.M[11] = m.M34;
            r.M[12] = m.M41; r.M[13] = m.M42; r.M[14] = m.M43; r.M[15] = m.M44;
            return r;
        }

        public System.Numerics.Matrix4x4 ToMatrix()
        {
            fixed (float* p = M)
            {
                return new System.Numerics.Matrix4x4(
                    p[0], p[1], p[2], p[3],
                    p[4], p[5], p[6], p[7],
                    p[8], p[9], p[10], p[11],
                    p[12], p[13], p[14], p[15]);
            }
        }

        public static NRMatrix4 Identity => FromMatrix(System.Numerics.Matrix4x4.Identity);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NRTransform3
    {
        public NRFloat3 Position;
        public NRFloat4 Rotation; // 四元数 (x, y, z, w)
        public NRFloat3 Scale;
    }

    #endregion

    #region 顶点

    /// <summary>
    /// 顶点格式，与 C 侧 NRVertex 严格对应，共 104 字节。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct NRVertex
    {
        public NRFloat3 Position;   //  0
        public NRFloat3 Normal;     // 12
        public NRFloat4 Tangent;    // 24  w 分量存副切线手性 (+1/-1)
        public NRFloat2 UV0;        // 40
        public NRFloat2 UV1;        // 48
        public NRFloat4 Color;      // 56
        public fixed uint Joints[4];// 72  骨骼索引
        public NRFloat4 Weights;    // 88  骨骼权重
    }

    #endregion

    #region 创建信息

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct NRTextureCreateInfo
    {
        public uint Width;
        public uint Height;
        /// <summary>3D 纹理深度 / 数组层数，2D 填 1。</summary>
        public uint Depth;
        /// <summary>0 表示自动生成完整 mip 链。</summary>
        public uint MipLevels;
        public NRTextureFormat Format;
        public NRTextureType Type;
        public NRWrapMode WrapU;
        public NRWrapMode WrapV;
        public NRWrapMode WrapW;
        public int FilterLinear;
        /// <summary>&lt;=1 表示关闭各向异性过滤。</summary>
        public float MaxAnisotropy;
        public void* Pixels;
        public ulong PixelsSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NRMaterialCreateInfo
    {
        public NRFloat4 BaseColorFactor;
        public NRFloat3 EmissiveFactor;
        public float MetallicFactor;
        public float RoughnessFactor;
        public float NormalScale;
        public float OcclusionStrength;
        public float AlphaCutoff;
        public NRBlendMode BlendMode;
        public int DoubleSided;
        public int CastShadow;
        public int ReceiveShadow;
        public ulong BaseColorTex;
        public ulong MetallicRoughnessTex;
        public ulong NormalTex;
        public ulong OcclusionTex;
        public ulong EmissiveTex;
        /// <summary>0 表示使用内置 PBR 着色器。</summary>
        public ulong CustomShader;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct NRMeshCreateInfo
    {
        public NRVertex* Vertices;
        public uint VertexCount;
        public uint* Indices;
        public uint IndexCount;
        /// <summary>是否需要频繁更新。</summary>
        public int Dynamic;
        public NRFloat3 BoundsMin;
        public NRFloat3 BoundsMax;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NRLightDesc
    {
        public NRLightType Type;
        public NRFloat3 Position;
        public NRFloat3 Direction;
        public NRFloat3 Color;
        public float Intensity;
        /// <summary>点光/聚光有效半径。</summary>
        public float Range;
        public float InnerConeCos;
        public float OuterConeCos;
        public int CastShadow;
        public float ShadowBias;
        /// <summary>0 表示使用默认阴影贴图尺寸。</summary>
        public uint ShadowMapSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NRCameraDesc
    {
        public NRMatrix4 View;
        public NRMatrix4 Projection;
        public NRFloat3 Position;
        public float NearPlane;
        public float FarPlane;
        public float FovYRadians;
        public float Aspect;
        public int Orthographic;
        public float OrthoSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct NRObjectDesc
    {
        public NRMatrix4 World;
        public ulong Mesh;
        public ulong Material;
        public int Visible;
        public int CastShadow;
        public uint LayerMask;
        /// <summary>蒙皮骨骼矩阵数组，非蒙皮填 null。</summary>
        public NRMatrix4* BoneMatrices;
        public uint BoneCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NRPostProcessDesc
    {
        public int EnableBloom;
        public float BloomThreshold;
        public float BloomIntensity;
        public int EnableTonemap;
        /// <summary>0=Reinhard 1=ACES 2=Filmic。</summary>
        public uint TonemapOperator;
        public float Exposure;
        public int EnableFxaa;
        public int EnableTaa;
        public int EnableSsao;
        public float SsaoRadius;
        public float SsaoIntensity;
        public int EnableMotionBlur;
        public float MotionBlurStrength;
        public int EnableColorGrading;
        public NRFloat3 ColorLift;
        public NRFloat3 ColorGamma;
        public NRFloat3 ColorGain;
        public float Vignette;
        public float ChromaticAberration;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NRParticleEmitterDesc
    {
        public uint MaxParticles;
        /// <summary>每秒发射数。</summary>
        public float EmissionRate;
        public NRFloat3 Position;
        public NRFloat3 Direction;
        public float SpreadRadians;
        public float SpeedMin;
        public float SpeedMax;
        public float LifeMin;
        public float LifeMax;
        public float SizeBegin;
        public float SizeEnd;
        public NRFloat4 ColorBegin;
        public NRFloat4 ColorEnd;
        public NRFloat3 Gravity;
        public float Drag;
        public ulong Texture;
        public NRBlendMode BlendMode;
        public int SoftParticles;
        public int WorldSpace;
        /// <summary>非 0 时使用网格粒子，否则为 billboard。</summary>
        public ulong Mesh;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NRSceneEnvDesc
    {
        public NRFloat3 AmbientColor;
        public float AmbientIntensity;
        /// <summary>cubemap，0 表示无。</summary>
        public ulong Skybox;
        public ulong Irradiance;
        public ulong Prefiltered;
        public ulong BrdfLut;
        public int EnableFog;
        public NRFloat3 FogColor;
        public float FogDensity;
        public float FogStart;
        public float FogEnd;
        public NRFloat4 ClearColor;
    }

    #endregion

    #region 查询结构

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct NRDeviceInfo
    {
        public fixed byte NameRaw[256];
        public uint VendorId;
        public uint DeviceId;
        /// <summary>0=other 1=integrated 2=discrete 3=virtual 4=cpu。</summary>
        public uint DeviceType;
        public ulong VramBytes;
        public uint ApiVersion;
        public uint DriverVersion;
        public NRFeature Features;
        public uint MaxMsaaSamples;

        /// <summary>把 C 侧固定长度的 UTF-8 设备名解码为托管字符串。</summary>
        public string Name
        {
            get
            {
                fixed (byte* p = NameRaw)
                {
                    int len = 0;
                    while (len < 256 && p[len] != 0) len++;
                    return System.Text.Encoding.UTF8.GetString(p, len);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NRFrameStats
    {
        public double CpuFrameMs;
        public double GpuFrameMs;
        public uint DrawCalls;
        public uint Triangles;
        public uint VisibleObjects;
        public uint CulledObjects;
        public uint ActiveParticles;
        public ulong GpuMemoryUsed;
    }

    #endregion

    #region 事件

    /// <summary>
    /// 统一事件结构（定长），与 C 侧 NREvent 对应。
    /// 使用定长而非变长联合体，避免跨界布局歧义。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct NREvent
    {
        public NREventType Type;
        /// <summary>手柄/触摸设备 id。</summary>
        public uint DeviceId;
        public int I0, I1, I2, I3;
        public float F0, F1, F2, F3;
        public ulong Timestamp;
        /// <summary>文本输入（UTF-8，含结尾 0）。</summary>
        public fixed byte TextRaw[16];

        /// <summary>把文本输入事件的 UTF-8 字节解码为托管字符串。</summary>
        public string Text
        {
            get
            {
                fixed (byte* p = TextRaw)
                {
                    int len = 0;
                    while (len < 16 && p[len] != 0) len++;
                    return System.Text.Encoding.UTF8.GetString(p, len);
                }
            }
        }
    }

    #endregion

    #region 渲染器创建信息

    /// <summary>
    /// 对应 NRDefine.h 的 struct NRRendererCreateInfo。
    /// 注意：该结构体位于 NRDefine.h 而非 NRApi.h，不受 pack(8) 约束，
    /// 但其字段全部是指针与 4/8 字节整型，自然对齐结果与 Pack = 8 一致。
    /// 该结构体按值传递给 NR_CreateRenderer。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct NRRendererCreateInfo
    {
        public byte* RendererName;
        public byte* AppName;
        public ulong AppVersion;
        /// <summary>渲染 API 类型（NRGraphicsAPI，s32）。</summary>
        public int Api;
        /// <summary>渲染类型（NRGraphicsType，u32）。</summary>
        public uint ApiType;
        public ulong ApiBaseVersion;
        public ulong ApiTargetVersion;
        public byte** RequiredInstanceExtensions;
        public byte** OptionalInstanceExtensions;
        public int RequiredInstanceExtensionsCount;
        public int OptionalInstanceExtensionsCount;
        public byte** RequiredDeviceExtensions;
        public byte** OptionalDeviceExtensions;
        public int RequiredDeviceExtensionsCount;
        public int OptionalDeviceExtensionsCount;
        public ulong** RequiredRendererFeatures;
        public ulong** OptionalRendererFeatures;
        public int RequiredRendererFeaturesCount;
        public int OptionalRendererFeaturesCount;
    }

    #endregion
}
