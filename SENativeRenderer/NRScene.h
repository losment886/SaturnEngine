#pragma once

// ============================================================
// NRScene.h
// 场景：相机、光源、可渲染对象、环境设置，以及帧级渲染队列
//
// 渲染队列构建流程（每帧）：
//   nrSceneBuildQueue()
//     -> 视锥剔除（AABB vs 6 平面）
//     -> 按 不透明/透明 分桶
//     -> 不透明按 (材质, 网格) 排序以减少状态切换
//     -> 透明按到相机距离由远及近排序以保证混合正确
// ============================================================

#include "NRMesh.h"
#include "NRMaterial.h"
#include "NRMath.h"

SE_EXTERN_C_BEGIN

#define NR_MAX_SCENES        16
#define NR_MAX_OBJECTS       16384
#define NR_MAX_LIGHTS        1024
#define NR_MAX_SHADOW_LIGHTS 8
#define NR_SHADOW_MAP_SIZE   2048

// ---------------- 光源 GPU 布局（须与 GLSL NRLight 一致）----------------
typedef struct NRLightGPU
{
	NRFloat4 position_range;
	NRFloat4 direction_type;
	NRFloat4 color_intensity;
	NRFloat4 cone_bias;        // x=innerCos y=outerCos z=shadowBias w=shadowIndex
} NRLightGPU;

// ---------------- 相机 UBO（须与 GLSL CameraUBO 一致）----------------
typedef struct NRCameraUBO
{
	NRMatrix4 view;
	NRMatrix4 proj;
	NRMatrix4 view_proj;
	NRMatrix4 light_view_proj;
	NRFloat4  camera_pos;
	NRFloat4  params;    // x=near y=far z=time w=exposure
	u32       counts[4]; // x=lightCount
} NRCameraUBO;

// ---------------- 场景元素 ----------------
typedef struct NRSceneObject
{
	NRMatrix4 world;
	NRMeshHandle mesh;
	NRMaterialHandle material;
	b32 visible;
	b32 cast_shadow;
	u32 layer_mask;
	NRAABB local_bounds;
	b32 alive;
	u32 generation;
} NRSceneObject;

typedef struct NRSceneLight
{
	NRLightDesc desc;
	b32 alive;
	u32 generation;
} NRSceneLight;

// 渲染队列条目：一次 draw call 的全部信息
typedef struct NRDrawItem
{
	const NRMesh* mesh;
	const NRMaterial* material;
	const NRSceneObject* object;
	u32 index_offset;
	u32 index_count;
	f32 sort_key;      // 不透明=材质/网格哈希，透明=到相机距离
} NRDrawItem;

typedef struct NRScene
{
	NRSceneObject* objects;
	NRSceneLight*  lights;
	u32 object_count;
	u32 light_count;

	NRCameraDesc camera;
	NRSceneEnvDesc env;

	// 每帧重建的队列
	NRDrawItem* opaque;
	u32 opaque_count;
	NRDrawItem* transparent;
	u32 transparent_count;

	NRFrustum frustum;

	// GPU 资源
	NRBuffer camera_ubo;
	NRBuffer light_ssbo;
	VkDescriptorSet global_set;

	NRCameraUBO cpu_camera;
	NRLightGPU* cpu_lights;

	b32 alive;
	u32 generation;
} NRScene;

NRResult nrSceneSystemInit(void);
void     nrSceneSystemShutdown(void);

NRResult nrSceneCreate(NRSceneHandle* out);
void     nrSceneDestroy(NRSceneHandle handle);
NRScene* nrSceneResolve(NRSceneHandle handle);

// 对象
NRResult nrSceneAddObject(NRSceneHandle scene, const NRObjectDesc* desc, NRObjectHandle* out);
NRResult nrSceneRemoveObject(NRSceneHandle scene, NRObjectHandle obj);
NRResult nrSceneSetObjectTransform(NRSceneHandle scene, NRObjectHandle obj, const NRMatrix4* world);
NRResult nrSceneSetObjectVisible(NRSceneHandle scene, NRObjectHandle obj, b32 visible);

// 光源
NRResult nrSceneAddLight(NRSceneHandle scene, const NRLightDesc* desc, NRLightHandle* out);
NRResult nrSceneRemoveLight(NRSceneHandle scene, NRLightHandle light);
NRResult nrSceneUpdateLight(NRSceneHandle scene, NRLightHandle light, const NRLightDesc* desc);

// 相机与环境
NRResult nrSceneSetCamera(NRSceneHandle scene, const NRCameraDesc* cam);
NRResult nrSceneSetEnvironment(NRSceneHandle scene, const NRSceneEnvDesc* env);

// 每帧：剔除 + 排序 + 上传 UBO/SSBO
NRResult nrSceneBuildQueue(NRSceneHandle scene, f32 time);
// 计算方向光的阴影 viewProj（覆盖相机视锥的正交投影）
NRMatrix4 nrSceneComputeShadowMatrix(const NRScene* scene);

SE_EXTERN_C_END
