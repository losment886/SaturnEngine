#pragma once

// ============================================================
// NRApi.h
// SENativeRenderer 对外唯一 C ABI 头文件
//
// 约定：
//   1. 所有导出函数使用 SE_OUT(type) 宏，返回 NRResult（除非另有说明）。
//   2. 所有跨界结构体使用 #pragma pack(push, 8)，仅使用固定宽度整型
//      与不透明句柄（u64 id），禁止 C++ 类型、位域、指针成员对齐依赖。
//   3. 句柄 0 表示无效句柄。
//   4. 字符串一律为 UTF-8 的 const char*，由调用方负责生命周期。
//   5. C# 侧使用 [LibraryImport] + CallingConvention.Cdecl 对接。
// ============================================================

#include "NRDefine.h"

SE_EXTERN_C_BEGIN

#pragma pack(push, 8)

// ============================================================
// 不透明句柄类型
// ============================================================
typedef u64 NRMeshHandle;
typedef u64 NRTextureHandle;
typedef u64 NRMaterialHandle;
typedef u64 NRShaderHandle;
typedef u64 NRObjectHandle;   // 场景中的可渲染实例
typedef u64 NRLightHandle;
typedef u64 NRCameraHandle;
typedef u64 NREmitterHandle;  // 粒子发射器
typedef u64 NRSceneHandle;

#define NR_INVALID_HANDLE ((u64)0)

// ============================================================
// 基础数学结构（与 C# System.Numerics 布局一致）
// ============================================================
typedef struct NRFloat2 { f32 x, y; } NRFloat2;
typedef struct NRFloat3 { f32 x, y, z; } NRFloat3;
typedef struct NRFloat4 { f32 x, y, z, w; } NRFloat4;
// 行主序 4x4 矩阵
typedef struct NRMatrix4 { f32 m[16]; } NRMatrix4;

// 位置 + 四元数旋转 + 缩放
typedef struct NRTransform3
{
	NRFloat3 position;
	NRFloat4 rotation; // 四元数 (x, y, z, w)
	NRFloat3 scale;
} NRTransform3;

// ============================================================
// 顶点格式
// 与 C# 侧 SEVertex 严格一一对应，共 96 字节
// ============================================================
typedef struct NRVertex
{
	NRFloat3 position;   //  0
	NRFloat3 normal;     // 12
	NRFloat4 tangent;    // 24  w 分量存副切线手性 (+1/-1)
	NRFloat2 uv0;        // 40
	NRFloat2 uv1;        // 48
	NRFloat4 color;      // 56
	u32      joints[4];  // 72  骨骼索引
	NRFloat4 weights;    // 88  骨骼权重
} NRVertex;              // = 104 -> pack(8) 对齐为 104

// ============================================================
// 渲染器能力标志（位掩码）
// ============================================================
#define NR_FEATURE_ANISOTROPY          (1u << 0)
#define NR_FEATURE_SAMPLE_RATE_SHADING (1u << 1)
#define NR_FEATURE_DESCRIPTOR_INDEXING (1u << 2)
#define NR_FEATURE_GEOMETRY_SHADER     (1u << 3)
#define NR_FEATURE_TESSELLATION        (1u << 4)
#define NR_FEATURE_COMPUTE             (1u << 5)
#define NR_FEATURE_RAY_TRACING         (1u << 6)
#define NR_FEATURE_HDR_SWAPCHAIN       (1u << 7)
#define NR_FEATURE_MULTI_DRAW_INDIRECT (1u << 8)

// ============================================================
// 纹理格式
// ============================================================
#define NR_TEXFMT_R8G8B8A8_UNORM   0
#define NR_TEXFMT_R8G8B8A8_SRGB    1
#define NR_TEXFMT_R8_UNORM         2
#define NR_TEXFMT_R16G16B16A16_SF  3
#define NR_TEXFMT_R32G32B32A32_SF  4
#define NR_TEXFMT_BC7_SRGB         5
#define NR_TEXFMT_ASTC_4x4_SRGB    6
#define NR_TEXFMT_ETC2_SRGB        7
#define NR_TEXFMT_D32_SFLOAT       8

// 纹理类型
#define NR_TEXTYPE_2D    0
#define NR_TEXTYPE_CUBE  1
#define NR_TEXTYPE_ARRAY 2
#define NR_TEXTYPE_3D    3

// 采样器寻址模式
#define NR_WRAP_REPEAT          0
#define NR_WRAP_MIRRORED_REPEAT 1
#define NR_WRAP_CLAMP_EDGE      2
#define NR_WRAP_CLAMP_BORDER    3

// 材质混合模式
#define NR_BLEND_OPAQUE  0
#define NR_BLEND_MASK    1
#define NR_BLEND_ALPHA   2
#define NR_BLEND_ADD     3
#define NR_BLEND_MULTIPLY 4

// 光源类型
#define NR_LIGHT_DIRECTIONAL 0
#define NR_LIGHT_POINT       1
#define NR_LIGHT_SPOT        2
#define NR_LIGHT_AREA        3

// 着色器阶段
#define NR_SHADER_STAGE_VERTEX   0
#define NR_SHADER_STAGE_FRAGMENT 1
#define NR_SHADER_STAGE_COMPUTE  2
#define NR_SHADER_STAGE_GEOMETRY 3

// ============================================================
// 创建信息结构体
// ============================================================

// 纹理创建信息
typedef struct NRTextureCreateInfo
{
	u32 width;
	u32 height;
	u32 depth;        // 3D 纹理深度 / 数组层数，2D 填 1
	u32 mip_levels;   // 0 表示自动生成完整 mip 链
	u32 format;       // NR_TEXFMT_*
	u32 type;         // NR_TEXTYPE_*
	u32 wrap_u;       // NR_WRAP_*
	u32 wrap_v;
	u32 wrap_w;
	b32 filter_linear;
	f32 max_anisotropy; // <=1 表示关闭
	const void* pixels; // 初始像素数据，可为 NULL
	u64 pixels_size;
} NRTextureCreateInfo;

// PBR 材质创建信息
typedef struct NRMaterialCreateInfo
{
	NRFloat4 base_color_factor;
	NRFloat3 emissive_factor;
	f32 metallic_factor;
	f32 roughness_factor;
	f32 normal_scale;
	f32 occlusion_strength;
	f32 alpha_cutoff;
	u32 blend_mode;      // NR_BLEND_*
	b32 double_sided;
	b32 cast_shadow;
	b32 receive_shadow;
	NRTextureHandle base_color_tex;
	NRTextureHandle metallic_roughness_tex;
	NRTextureHandle normal_tex;
	NRTextureHandle occlusion_tex;
	NRTextureHandle emissive_tex;
	NRShaderHandle  custom_shader; // 0 表示使用内置 PBR
} NRMaterialCreateInfo;

// 网格创建信息
typedef struct NRMeshCreateInfo
{
	const NRVertex* vertices;
	u32 vertex_count;
	const u32* indices;
	u32 index_count;
	b32 dynamic;      // 是否需要频繁更新
	NRFloat3 bounds_min;
	NRFloat3 bounds_max;
} NRMeshCreateInfo;

// 光源描述
typedef struct NRLightDesc
{
	u32 type;             // NR_LIGHT_*
	NRFloat3 position;
	NRFloat3 direction;
	NRFloat3 color;
	f32 intensity;
	f32 range;            // 点光/聚光有效半径
	f32 inner_cone_cos;   // 聚光内锥余弦
	f32 outer_cone_cos;   // 聚光外锥余弦
	b32 cast_shadow;
	f32 shadow_bias;
	u32 shadow_map_size;  // 0 表示使用默认
} NRLightDesc;

// 相机描述
typedef struct NRCameraDesc
{
	NRMatrix4 view;
	NRMatrix4 projection;
	NRFloat3 position;
	f32 near_plane;
	f32 far_plane;
	f32 fov_y_radians;
	f32 aspect;
	b32 orthographic;
	f32 ortho_size;
} NRCameraDesc;

// 可渲染实例描述
typedef struct NRObjectDesc
{
	NRMatrix4 world;
	NRMeshHandle mesh;
	NRMaterialHandle material;
	b32 visible;
	b32 cast_shadow;
	u32 layer_mask;
	// 蒙皮：骨骼矩阵数组，非蒙皮填 NULL/0
	const NRMatrix4* bone_matrices;
	u32 bone_count;
} NRObjectDesc;

// 后处理配置
typedef struct NRPostProcessDesc
{
	b32 enable_bloom;
	f32 bloom_threshold;
	f32 bloom_intensity;
	b32 enable_tonemap;
	u32 tonemap_operator;  // 0=Reinhard 1=ACES 2=Filmic
	f32 exposure;
	b32 enable_fxaa;
	b32 enable_taa;
	b32 enable_ssao;
	f32 ssao_radius;
	f32 ssao_intensity;
	b32 enable_motion_blur;
	f32 motion_blur_strength;
	b32 enable_color_grading;
	NRFloat3 color_lift;
	NRFloat3 color_gamma;
	NRFloat3 color_gain;
	f32 vignette;
	f32 chromatic_aberration;
} NRPostProcessDesc;

// 粒子发射器描述
typedef struct NRParticleEmitterDesc
{
	u32 max_particles;
	f32 emission_rate;      // 每秒发射数
	NRFloat3 position;
	NRFloat3 direction;
	f32 spread_radians;
	f32 speed_min, speed_max;
	f32 life_min, life_max;
	f32 size_begin, size_end;
	NRFloat4 color_begin;
	NRFloat4 color_end;
	NRFloat3 gravity;
	f32 drag;
	NRTextureHandle texture;
	u32 blend_mode;         // NR_BLEND_*
	b32 soft_particles;
	b32 world_space;
	NRMeshHandle mesh;      // 非 0 时使用网格粒子，否则 billboard
} NRParticleEmitterDesc;

// 场景环境设置
typedef struct NRSceneEnvDesc
{
	NRFloat3 ambient_color;
	f32 ambient_intensity;
	NRTextureHandle skybox;        // cubemap，0 表示无
	NRTextureHandle irradiance;    // IBL 漫反射
	NRTextureHandle prefiltered;   // IBL 镜面反射
	NRTextureHandle brdf_lut;
	b32 enable_fog;
	NRFloat3 fog_color;
	f32 fog_density;
	f32 fog_start;
	f32 fog_end;
	NRFloat4 clear_color;
} NRSceneEnvDesc;

// 设备信息（查询用）
typedef struct NRDeviceInfo
{
	char name[256];
	u32 vendor_id;
	u32 device_id;
	u32 device_type;      // 0=other 1=integrated 2=discrete 3=virtual 4=cpu
	u64 vram_bytes;
	u32 api_version;
	u32 driver_version;
	u32 features;         // NR_FEATURE_* 位掩码
	u32 max_msaa_samples;
} NRDeviceInfo;

// 帧统计
typedef struct NRFrameStats
{
	f64 cpu_frame_ms;
	f64 gpu_frame_ms;
	u32 draw_calls;
	u32 triangles;
	u32 visible_objects;
	u32 culled_objects;
	u32 active_particles;
	u64 gpu_memory_used;
} NRFrameStats;

// ============================================================
// 事件回调（由 C 侧在 NR_PumpEvents 中调用）
// C# 侧使用 [UnmanagedCallersOnly] 静态方法注册
// ============================================================

// 事件类型
#define NR_EVENT_QUIT            1
#define NR_EVENT_WINDOW_RESIZE   2
#define NR_EVENT_WINDOW_MOVE     3
#define NR_EVENT_WINDOW_FOCUS    4
#define NR_EVENT_WINDOW_MINIMIZE 5
#define NR_EVENT_KEY_DOWN        10
#define NR_EVENT_KEY_UP          11
#define NR_EVENT_TEXT_INPUT      12
#define NR_EVENT_MOUSE_MOVE      20
#define NR_EVENT_MOUSE_DOWN      21
#define NR_EVENT_MOUSE_UP        22
#define NR_EVENT_MOUSE_WHEEL     23
#define NR_EVENT_TOUCH_DOWN      30
#define NR_EVENT_TOUCH_UP        31
#define NR_EVENT_TOUCH_MOVE      32
#define NR_EVENT_GAMEPAD_ADDED   40
#define NR_EVENT_GAMEPAD_REMOVED 41
#define NR_EVENT_GAMEPAD_BUTTON  42
#define NR_EVENT_GAMEPAD_AXIS    43
#define NR_EVENT_SENSOR          50

// 统一事件结构（64 字节定长，避免变长联合体跨界问题）
typedef struct NREvent
{
	u32 type;        // NR_EVENT_*
	u32 device_id;   // 手柄/触摸设备 id
	s32 i0, i1, i2, i3;  // 整型参数：键码、按钮号、坐标等
	f32 f0, f1, f2, f3;  // 浮点参数：轴值、压力、相对位移等
	u64 timestamp;
	char text[16];   // 文本输入（UTF-8，含结尾 0）
} NREvent;

typedef void (*NREventCallback)(const NREvent* evt, void* user_data);
typedef void (*NRLogCallback)(s32 severity, const char* message, void* user_data);

// ============================================================
// 导出函数
// ============================================================

// ---------- 生命周期 ----------
// 初始化 SDL 子系统（sdl_flags 为 SDL3 的 SDL_INIT_* 组合）
// 已在 NRWindow.c 中定义：SE_OUT(NRResult) NR_Init(u32 sdl_flags);
// 关闭并释放所有资源
SE_OUT(NRResult) NR_Shutdown(void);

// ---------- 窗口 ----------
// 已有：NR_CreateWindow / NR_DestroyWindow
SE_OUT(NRResult) NR_ShowWindow(void);
SE_OUT(NRResult) NR_HideWindow(void);
SE_OUT(NRResult) NR_SetWindowTitle(const char* title);
SE_OUT(NRResult) NR_SetWindowSize(u32 width, u32 height);
SE_OUT(NRResult) NR_GetWindowSize(u32* out_width, u32* out_height);
SE_OUT(NRResult) NR_GetWindowPixelSize(u32* out_width, u32* out_height);
SE_OUT(NRResult) NR_SetWindowPosition(s32 x, s32 y);
SE_OUT(NRResult) NR_GetWindowPosition(s32* out_x, s32* out_y);
SE_OUT(NRResult) NR_SetWindowFullscreen(b32 fullscreen);
SE_OUT(NRResult) NR_SetWindowResizable(b32 resizable);
SE_OUT(NRResult) NR_SetWindowIcon(const void* rgba_pixels, u32 width, u32 height);
SE_OUT(f32)      NR_GetWindowDisplayScale(void);
SE_OUT(void*)    NR_GetNativeWindowHandle(void);
SE_OUT(void*)    NR_GetSDLWindow(void);

// ---------- 事件 ----------
SE_OUT(NRResult) NR_SetEventCallback(NREventCallback cb, void* user_data);
SE_OUT(NRResult) NR_SetLogCallback(NRLogCallback cb, void* user_data);
// 抽干事件队列，逐个回调；返回处理的事件数量写入 out_count
SE_OUT(NRResult) NR_PumpEvents(u32* out_count);
SE_OUT(NRResult) NR_SetRelativeMouseMode(b32 enable);
SE_OUT(NRResult) NR_SetCursorVisible(b32 visible);
SE_OUT(NRResult) NR_StartTextInput(void);
SE_OUT(NRResult) NR_StopTextInput(void);
SE_OUT(NRResult) NR_RumbleGamepad(u32 device_id, f32 low_freq, f32 high_freq, u32 duration_ms);

// ---------- 设备 ----------
SE_OUT(NRResult) NR_EnumerateDevices(NRDeviceInfo* out_devices, u32* inout_count);
// 已有：NR_CreateRenderer(struct NRRendererCreateInfo info) —— 内部选择设备 0
SE_OUT(NRResult) NR_CreateRendererOnDevice(struct NRRendererCreateInfo info, u32 device_index);
SE_OUT(NRResult) NR_GetDeviceInfo(NRDeviceInfo* out_info);
SE_OUT(NRResult) NR_WaitDeviceIdle(void);

// ---------- 交换链 ----------
SE_OUT(NRResult) NR_ResizeSwapchain(u32 width, u32 height);
SE_OUT(NRResult) NR_SetVSync(b32 enable);
SE_OUT(NRResult) NR_SetHDR(b32 enable);
SE_OUT(NRResult) NR_SetMSAA(u32 samples);

// ---------- 着色器 ----------
SE_OUT(NRResult) NR_CreateShaderFromSource(const char* source, u32 stage,
										   const char* entry_point,
										   NRShaderHandle* out_handle);
SE_OUT(NRResult) NR_CreateShaderFromSPIRV(const u32* spirv, u64 size_bytes, u32 stage,
										  NRShaderHandle* out_handle);
SE_OUT(NRResult) NR_DestroyShader(NRShaderHandle handle);

// ---------- 资源 ----------
SE_OUT(NRResult) NR_CreateMesh(const NRMeshCreateInfo* info, NRMeshHandle* out_handle);
SE_OUT(NRResult) NR_UpdateMesh(NRMeshHandle handle, const NRVertex* vertices, u32 vertex_count,
							   const u32* indices, u32 index_count);
SE_OUT(NRResult) NR_DestroyMesh(NRMeshHandle handle);

SE_OUT(NRResult) NR_CreateTexture(const NRTextureCreateInfo* info, NRTextureHandle* out_handle);
SE_OUT(NRResult) NR_UpdateTexture(NRTextureHandle handle, const void* pixels, u64 size_bytes,
								  u32 mip_level, u32 layer);
SE_OUT(NRResult) NR_DestroyTexture(NRTextureHandle handle);

SE_OUT(NRResult) NR_CreateMaterial(const NRMaterialCreateInfo* info, NRMaterialHandle* out_handle);
SE_OUT(NRResult) NR_UpdateMaterial(NRMaterialHandle handle, const NRMaterialCreateInfo* info);
SE_OUT(NRResult) NR_DestroyMaterial(NRMaterialHandle handle);

// ---------- 场景 ----------
SE_OUT(NRResult) NR_CreateScene(NRSceneHandle* out_handle);
SE_OUT(NRResult) NR_DestroyScene(NRSceneHandle handle);
SE_OUT(NRResult) NR_SetActiveScene(NRSceneHandle handle);
// 叠加场景（UI）：在主场景之后绘制，传 0 关闭
SE_OUT(NRResult) NR_SetOverlayScene(NRSceneHandle handle);
SE_OUT(NRResult) NR_SetSceneEnvironment(NRSceneHandle scene, const NRSceneEnvDesc* desc);

SE_OUT(NRResult) NR_AddObject(NRSceneHandle scene, const NRObjectDesc* desc, NRObjectHandle* out_handle);
SE_OUT(NRResult) NR_UpdateObject(NRObjectHandle handle, const NRObjectDesc* desc);
SE_OUT(NRResult) NR_SetObjectTransform(NRObjectHandle handle, const NRMatrix4* world);
SE_OUT(NRResult) NR_SetObjectVisible(NRObjectHandle handle, b32 visible);
SE_OUT(NRResult) NR_RemoveObject(NRObjectHandle handle);

SE_OUT(NRResult) NR_AddLight(NRSceneHandle scene, const NRLightDesc* desc, NRLightHandle* out_handle);
SE_OUT(NRResult) NR_UpdateLight(NRLightHandle handle, const NRLightDesc* desc);
SE_OUT(NRResult) NR_RemoveLight(NRLightHandle handle);

SE_OUT(NRResult) NR_SetCamera(NRSceneHandle scene, const NRCameraDesc* desc);

// ---------- 特效 ----------
SE_OUT(NRResult) NR_SetPostProcess(const NRPostProcessDesc* desc);
SE_OUT(NRResult) NR_CreateParticleEmitter(NRSceneHandle scene, const NRParticleEmitterDesc* desc,
										  NREmitterHandle* out_handle);
SE_OUT(NRResult) NR_UpdateParticleEmitter(NREmitterHandle handle, const NRParticleEmitterDesc* desc);
SE_OUT(NRResult) NR_SetParticleEmitterEnabled(NREmitterHandle handle, b32 enabled);
SE_OUT(NRResult) NR_DestroyParticleEmitter(NREmitterHandle handle);

// ---------- 渲染 ----------
// 已有：NR_MainUpdate(f64) / NR_Render(f64) / NR_PollEvents(SDL_Event*)
SE_OUT(NRResult) NR_BeginFrame(f64 delta_time);
SE_OUT(NRResult) NR_EndFrame(void);
SE_OUT(NRResult) NR_GetFrameStats(NRFrameStats* out_stats);

#pragma pack(pop)

SE_EXTERN_C_END
