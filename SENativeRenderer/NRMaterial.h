#pragma once

// ============================================================
// NRMaterial.h
// 纹理资源与 PBR 材质：参数 UBO + 五张贴图绑定
//
// 两种绑定路径：
//   1) 支持 descriptor indexing 时，贴图注册进 set 3 的 bindless 数组，
//      材质 UBO 里只存索引，切材质无需重新绑定描述符集。
//   2) 否则退化为每材质一个 set 1 描述符集，绑定 5 张贴图。
// 无论哪条路径，材质 UBO 布局与 NRShaderLib.h 中 MaterialUBO 严格一致。
// ============================================================

#include "NRMemory.h"
#include "NRDescriptor.h"
#include "NRApi.h"

SE_EXTERN_C_BEGIN

#define NR_MAX_TEXTURES  4096
#define NR_MAX_MATERIALS 4096

// ---------------- 纹理 ----------------
typedef struct NRTexture
{
	NRImage image;
	u32  bindless_index;   // UINT32_MAX 表示未注册
	b32  alive;
	u32  generation;
} NRTexture;

// ---------------- 材质 UBO（须与 GLSL MaterialUBO 逐字节一致）----------------
typedef struct NRMaterialUBO
{
	NRFloat4 base_color_factor;
	NRFloat4 emissive_factor;   // w = alpha_cutoff
	NRFloat4 pbr_factors;       // x=metallic y=roughness z=normalScale w=occlusion
	u32 tex_indices[4];         // base, metallicRoughness, normal, occlusion
	u32 tex_indices2[4];        // emissive, 保留
} NRMaterialUBO;

typedef struct NRMaterial
{
	NRMaterialUBO ubo;
	NRBuffer ubo_buffer;
	VkDescriptorSet set;        // 非 bindless 路径使用

	NRTextureHandle base_color_tex;
	NRTextureHandle metallic_roughness_tex;
	NRTextureHandle normal_tex;
	NRTextureHandle occlusion_tex;
	NRTextureHandle emissive_tex;
	NRShaderHandle  custom_shader;

	u32 blend_mode;
	b32 double_sided;
	b32 cast_shadow;
	b32 receive_shadow;

	b32 dirty;                  // 参数已改，需重新上传 UBO
	b32 alive;
	u32 generation;
} NRMaterial;

NRResult nrMaterialSystemInit(void);
void     nrMaterialSystemShutdown(void);

// ---------------- 纹理 API ----------------
NRResult   nrTextureCreate(const NRTextureCreateInfo* info, NRTextureHandle* out);
void       nrTextureDestroy(NRTextureHandle handle);
NRTexture* nrTextureResolve(NRTextureHandle handle);
// 返回一张 1x1 白色纹理，用于材质缺省贴图槽，避免着色器采样未绑定描述符
NRTextureHandle nrTextureWhite(void);
NRTextureHandle nrTextureNormalFlat(void);

// ---------------- 材质 API ----------------
NRResult    nrMaterialCreate(const NRMaterialCreateInfo* info, NRMaterialHandle* out);
void        nrMaterialDestroy(NRMaterialHandle handle);
NRMaterial* nrMaterialResolve(NRMaterialHandle handle);
// 参数变更后调用；内部按 dirty 标志合并上传
NRResult    nrMaterialUpdate(NRMaterialHandle handle, const NRMaterialCreateInfo* info);
NRResult    nrMaterialFlush(NRMaterialHandle handle);
// 绑定材质描述符集到 set 1（bindless 路径下为空操作）
void        nrMaterialBind(VkCommandBuffer cmd, VkPipelineLayout layout,
						   const NRMaterial* mat);

SE_EXTERN_C_END
