#include "NRMaterial.h"
#include "NRVkLoader.h"
#include "NRDevice.h"

// ============================================================
// NRMaterial.c
// ============================================================

#define NR_HSHIFT 32
#define NR_HMASK  ((u64)0xFFFFFFFFull)

static NRTexture*  s_textures = NULL;
static NRMaterial* s_materials = NULL;
static b32 s_matInited = FALSE;

static NRTextureHandle s_whiteTex = 0;
static NRTextureHandle s_normalTex = 0;

static u64 nrMakeH(u32 slot, u32 gen)
{
	return ((u64)gen << NR_HSHIFT) | (((u64)slot + 1ull) & NR_HMASK);
}

// ------------------------------------------------------------
// 解析
// ------------------------------------------------------------
NRTexture* nrTextureResolve(NRTextureHandle h)
{
	if (h == 0 || s_textures == NULL) return NULL;
	u64 slot = h & NR_HMASK;
	if (slot == 0 || slot > NR_MAX_TEXTURES) return NULL;
	NRTexture* t = &s_textures[slot - 1ull];
	if (!t->alive || (u32)(h >> NR_HSHIFT) != t->generation) return NULL;
	return t;
}

NRMaterial* nrMaterialResolve(NRMaterialHandle h)
{
	if (h == 0 || s_materials == NULL) return NULL;
	u64 slot = h & NR_HMASK;
	if (slot == 0 || slot > NR_MAX_MATERIALS) return NULL;
	NRMaterial* m = &s_materials[slot - 1ull];
	if (!m->alive || (u32)(h >> NR_HSHIFT) != m->generation) return NULL;
	return m;
}

// ------------------------------------------------------------
// 内建缺省纹理
// ------------------------------------------------------------
static NRResult nrCreateBuiltinTextures(void)
{
	const u8 white[4] = { 255, 255, 255, 255 };
	// 切线空间“无扰动”法线 = (0,0,1) 编码为 (128,128,255)
	const u8 flat[4] = { 128, 128, 255, 255 };

	NRTextureCreateInfo info;
	memset(&info, 0, sizeof(info));
	info.width = 1; info.height = 1; info.depth = 1;
	info.mip_levels = 1;
	info.format = NR_TEXFMT_R8G8B8A8_UNORM;
	info.type = NR_TEXTYPE_2D;
	info.wrap_u = info.wrap_v = info.wrap_w = NR_WRAP_REPEAT;
	info.filter_linear = TRUE;
	info.max_anisotropy = 1.0f;
	info.pixels_size = 4;

	info.pixels = white;
	NRResult r = nrTextureCreate(&info, &s_whiteTex);
	if (NRR_FAILED(r)) return r;

	info.pixels = flat;
	return nrTextureCreate(&info, &s_normalTex);
}

NRTextureHandle nrTextureWhite(void)      { return s_whiteTex; }
NRTextureHandle nrTextureNormalFlat(void) { return s_normalTex; }

// ------------------------------------------------------------
// 系统生命周期
// ------------------------------------------------------------
NRResult nrMaterialSystemInit(void)
{
	if (s_matInited) return NRR_MakeSuccess(NRR_STEP_NR_CreateMaterial, NRR_CODE_SUCCESS);

	s_textures = (NRTexture*)calloc(NR_MAX_TEXTURES, sizeof(NRTexture));
	s_materials = (NRMaterial*)calloc(NR_MAX_MATERIALS, sizeof(NRMaterial));
	if (s_textures == NULL || s_materials == NULL)
	{
		free(s_textures); free(s_materials);
		s_textures = NULL; s_materials = NULL;
		return NRR_MakeFailure(NRR_STEP_NR_CreateMaterial, NRR_CODE_OUT_OF_MEMORY, 0);
	}

	s_matInited = TRUE;

	NRResult r = nrCreateBuiltinTextures();
	if (NRR_FAILED(r)) { nrMaterialSystemShutdown(); return r; }

	return NRR_MakeSuccess(NRR_STEP_NR_CreateMaterial, NRR_CODE_SUCCESS);
}

void nrMaterialSystemShutdown(void)
{
	if (s_materials != NULL)
	{
		for (u32 i = 0; i < NR_MAX_MATERIALS; i++)
			if (s_materials[i].alive) nrMaterialDestroy(nrMakeH(i, s_materials[i].generation));
		free(s_materials);
		s_materials = NULL;
	}
	if (s_textures != NULL)
	{
		for (u32 i = 0; i < NR_MAX_TEXTURES; i++)
			if (s_textures[i].alive) nrTextureDestroy(nrMakeH(i, s_textures[i].generation));
		free(s_textures);
		s_textures = NULL;
	}
	s_whiteTex = 0;
	s_normalTex = 0;
	s_matInited = FALSE;
}

// ------------------------------------------------------------
// 纹理创建
// ------------------------------------------------------------
NRResult nrTextureCreate(const NRTextureCreateInfo* info, NRTextureHandle* out)
{
	if (info == NULL || out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateTexture, NRR_CODE_INVALID_PARAMETER, 0);
	if (!s_matInited)
		return NRR_MakeFailure(NRR_STEP_NR_CreateTexture, NRR_CODE_NOT_INITIALIZED, 0);
	if (info->width == 0 || info->height == 0)
		return NRR_MakeFailure(NRR_STEP_NR_CreateTexture, NRR_CODE_INVALID_PARAMETER, 1);

	*out = 0;

	VkFormat fmt = nrFormatFromNR(info->format);
	if (fmt == VK_FORMAT_UNDEFINED)
		return NRR_MakeFailure(NRR_STEP_NR_CreateTexture, NRR_CODE_INVALID_PARAMETER, 2);

	u32 slot = UINT32_MAX;
	for (u32 i = 0; i < NR_MAX_TEXTURES; i++)
		if (!s_textures[i].alive) { slot = i; break; }
	if (slot == UINT32_MAX)
		return NRR_MakeFailure(NRR_STEP_NR_CreateTexture, NRR_CODE_CAPACITY_EXCEEDED, 0);

	NRTexture* tex = &s_textures[slot];
	u32 gen = tex->generation;
	memset(tex, 0, sizeof(NRTexture));
	tex->generation = gen;
	tex->bindless_index = UINT32_MAX;

	// mip_levels 为 0 时按最长边推导完整 mip 链
	u32 mips = info->mip_levels;
	if (mips == 0)
	{
		u32 maxDim = (info->width > info->height) ? info->width : info->height;
		mips = 1;
		while (maxDim > 1) { maxDim >>= 1; mips++; }
	}

	b32 cube = (info->type == NR_TEXTYPE_CUBE);
	u32 layers = cube ? 6u : ((info->depth > 0) ? info->depth : 1u);
	u32 depth = (info->type == NR_TEXTYPE_3D) ? ((info->depth > 0) ? info->depth : 1u) : 1u;
	if (info->type == NR_TEXTYPE_3D) layers = 1u;

	VkImageUsageFlags usage = VK_IMAGE_USAGE_SAMPLED_BIT |
							  VK_IMAGE_USAGE_TRANSFER_DST_BIT |
							  VK_IMAGE_USAGE_TRANSFER_SRC_BIT;  // mipmap blit 需要 SRC

	NRResult r = nrImageCreate(info->width, info->height, depth, mips, layers,
							   fmt, VK_IMAGE_TILING_OPTIMAL, usage,
							   VK_IMAGE_ASPECT_COLOR_BIT, VK_SAMPLE_COUNT_1_BIT,
							   cube, &tex->image);
	if (NRR_FAILED(r)) return r;

	if (info->pixels != NULL && info->pixels_size > 0)
	{
		r = nrImageUpload(&tex->image, info->pixels, info->pixels_size, 0, 0);
		if (NRR_FAILED(r)) { nrImageDestroy(&tex->image); return r; }

		if (mips > 1)
			nrImageGenerateMipmaps(&tex->image);
		else
			nrImageTransition(VK_NULL_HANDLE, &tex->image,
							  VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL, 0, mips);
	}

	r = nrSamplerCreate(info->filter_linear, info->wrap_u, info->wrap_v, info->wrap_w,
						info->max_anisotropy, mips, &tex->image.sampler);
	if (NRR_FAILED(r)) { nrImageDestroy(&tex->image); return r; }

	// 支持 bindless 时立即注册，材质只需记录索引
	if (nr_descriptors.bindless_enabled)
		tex->bindless_index = nrDescriptorRegisterTexture(tex->image.view, tex->image.sampler);

	tex->alive = TRUE;
	*out = nrMakeH(slot, tex->generation);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateTexture, NRR_CODE_SUCCESS);
}

void nrTextureDestroy(NRTextureHandle handle)
{
	NRTexture* tex = nrTextureResolve(handle);
	if (tex == NULL) return;

	if (tex->bindless_index != UINT32_MAX)
		nrDescriptorUnregisterTexture(tex->bindless_index);

	nrImageDestroy(&tex->image);

	u32 gen = tex->generation + 1u;
	memset(tex, 0, sizeof(NRTexture));
	tex->generation = gen;
	tex->alive = FALSE;
}

// ------------------------------------------------------------
// 材质
// ------------------------------------------------------------
static NRTextureHandle nrOrDefault(NRTextureHandle h, NRTextureHandle fallback)
{
	return (nrTextureResolve(h) != NULL) ? h : fallback;
}

static u32 nrTexBindlessIndex(NRTextureHandle h)
{
	NRTexture* t = nrTextureResolve(h);
	return (t != NULL) ? t->bindless_index : UINT32_MAX;
}

static void nrFillUBO(NRMaterial* mat, const NRMaterialCreateInfo* info)
{
	mat->ubo.base_color_factor = info->base_color_factor;
	mat->ubo.emissive_factor.x = info->emissive_factor.x;
	mat->ubo.emissive_factor.y = info->emissive_factor.y;
	mat->ubo.emissive_factor.z = info->emissive_factor.z;
	mat->ubo.emissive_factor.w = info->alpha_cutoff;
	mat->ubo.pbr_factors.x = info->metallic_factor;
	mat->ubo.pbr_factors.y = info->roughness_factor;
	mat->ubo.pbr_factors.z = info->normal_scale;
	mat->ubo.pbr_factors.w = info->occlusion_strength;

	mat->ubo.tex_indices[0] = nrTexBindlessIndex(mat->base_color_tex);
	mat->ubo.tex_indices[1] = nrTexBindlessIndex(mat->metallic_roughness_tex);
	mat->ubo.tex_indices[2] = nrTexBindlessIndex(mat->normal_tex);
	mat->ubo.tex_indices[3] = nrTexBindlessIndex(mat->occlusion_tex);
	mat->ubo.tex_indices2[0] = nrTexBindlessIndex(mat->emissive_tex);
	mat->ubo.tex_indices2[1] = 0;
	mat->ubo.tex_indices2[2] = 0;
	mat->ubo.tex_indices2[3] = 0;
}

// 非 bindless 路径：把 5 张贴图写进材质描述符集
static void nrWriteMaterialSet(NRMaterial* mat)
{
	if (mat->set == VK_NULL_HANDLE) return;

	nrDescriptorWriteBuffer(mat->set, 0, VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
							mat->ubo_buffer.buffer, 0, sizeof(NRMaterialUBO));

	const NRTextureHandle handles[5] = {
		mat->base_color_tex, mat->metallic_roughness_tex, mat->normal_tex,
		mat->occlusion_tex, mat->emissive_tex
	};
	for (u32 i = 0; i < 5; i++)
	{
		NRTexture* t = nrTextureResolve(handles[i]);
		if (t == NULL) continue;
		nrDescriptorWriteImage(mat->set, i + 1u, t->image.view, t->image.sampler,
							   VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);
	}
}

NRResult nrMaterialCreate(const NRMaterialCreateInfo* info, NRMaterialHandle* out)
{
	if (info == NULL || out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMaterial, NRR_CODE_INVALID_PARAMETER, 0);
	if (!s_matInited)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMaterial, NRR_CODE_NOT_INITIALIZED, 0);

	*out = 0;

	u32 slot = UINT32_MAX;
	for (u32 i = 0; i < NR_MAX_MATERIALS; i++)
		if (!s_materials[i].alive) { slot = i; break; }
	if (slot == UINT32_MAX)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMaterial, NRR_CODE_CAPACITY_EXCEEDED, 0);

	NRMaterial* mat = &s_materials[slot];
	u32 gen = mat->generation;
	memset(mat, 0, sizeof(NRMaterial));
	mat->generation = gen;

	// 缺失贴图回落到内建缺省，保证描述符始终被写满
	mat->base_color_tex          = nrOrDefault(info->base_color_tex, s_whiteTex);
	mat->metallic_roughness_tex  = nrOrDefault(info->metallic_roughness_tex, s_whiteTex);
	mat->normal_tex              = nrOrDefault(info->normal_tex, s_normalTex);
	mat->occlusion_tex           = nrOrDefault(info->occlusion_tex, s_whiteTex);
	mat->emissive_tex            = nrOrDefault(info->emissive_tex, s_whiteTex);
	mat->custom_shader           = info->custom_shader;

	mat->blend_mode     = info->blend_mode;
	mat->double_sided   = info->double_sided;
	mat->cast_shadow    = info->cast_shadow;
	mat->receive_shadow = info->receive_shadow;

	nrFillUBO(mat, info);

	NRResult r = nrBufferCreate(sizeof(NRMaterialUBO),
								VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT |
								VK_BUFFER_USAGE_TRANSFER_DST_BIT,
								VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT |
								VK_MEMORY_PROPERTY_HOST_COHERENT_BIT,
								&mat->ubo_buffer);
	if (NRR_FAILED(r)) return r;

	r = nrBufferUpload(&mat->ubo_buffer, &mat->ubo, sizeof(NRMaterialUBO), 0);
	if (NRR_FAILED(r)) { nrBufferDestroy(&mat->ubo_buffer); return r; }

	r = nrDescriptorAllocate(nr_descriptors.material_layout, &mat->set);
	if (NRR_FAILED(r)) { nrBufferDestroy(&mat->ubo_buffer); return r; }

	nrWriteMaterialSet(mat);

	mat->alive = TRUE;
	mat->dirty = FALSE;
	*out = nrMakeH(slot, mat->generation);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateMaterial, NRR_CODE_SUCCESS);
}

void nrMaterialDestroy(NRMaterialHandle handle)
{
	NRMaterial* mat = nrMaterialResolve(handle);
	if (mat == NULL) return;

	nrBufferDestroy(&mat->ubo_buffer);
	// 描述符集随池链整体回收，此处不单独 free

	u32 gen = mat->generation + 1u;
	memset(mat, 0, sizeof(NRMaterial));
	mat->generation = gen;
	mat->alive = FALSE;
}

NRResult nrMaterialUpdate(NRMaterialHandle handle, const NRMaterialCreateInfo* info)
{
	NRMaterial* mat = nrMaterialResolve(handle);
	if (mat == NULL || info == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMaterial, NRR_CODE_INVALID_HANDLE, 0);

	NRTextureHandle oldBase = mat->base_color_tex;
	NRTextureHandle oldMR   = mat->metallic_roughness_tex;
	NRTextureHandle oldNrm  = mat->normal_tex;
	NRTextureHandle oldOcc  = mat->occlusion_tex;
	NRTextureHandle oldEmi  = mat->emissive_tex;

	mat->base_color_tex         = nrOrDefault(info->base_color_tex, s_whiteTex);
	mat->metallic_roughness_tex = nrOrDefault(info->metallic_roughness_tex, s_whiteTex);
	mat->normal_tex             = nrOrDefault(info->normal_tex, s_normalTex);
	mat->occlusion_tex          = nrOrDefault(info->occlusion_tex, s_whiteTex);
	mat->emissive_tex           = nrOrDefault(info->emissive_tex, s_whiteTex);

	mat->blend_mode     = info->blend_mode;
	mat->double_sided   = info->double_sided;
	mat->cast_shadow    = info->cast_shadow;
	mat->receive_shadow = info->receive_shadow;

	nrFillUBO(mat, info);
	mat->dirty = TRUE;

	// 仅在贴图实际变化时重写描述符集，避免每帧无谓的 vkUpdateDescriptorSets
	b32 texChanged = (oldBase != mat->base_color_tex) || (oldMR != mat->metallic_roughness_tex)
				  || (oldNrm != mat->normal_tex) || (oldOcc != mat->occlusion_tex)
				  || (oldEmi != mat->emissive_tex);
	if (texChanged) nrWriteMaterialSet(mat);

	return nrMaterialFlush(handle);
}

NRResult nrMaterialFlush(NRMaterialHandle handle)
{
	NRMaterial* mat = nrMaterialResolve(handle);
	if (mat == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMaterial, NRR_CODE_INVALID_HANDLE, 0);
	if (!mat->dirty)
		return NRR_MakeSuccess(NRR_STEP_NR_CreateMaterial, NRR_CODE_SUCCESS);

	NRResult r = nrBufferUpload(&mat->ubo_buffer, &mat->ubo, sizeof(NRMaterialUBO), 0);
	if (NRR_FAILED(r)) return r;

	mat->dirty = FALSE;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateMaterial, NRR_CODE_SUCCESS);
}

void nrMaterialBind(VkCommandBuffer cmd, VkPipelineLayout layout, const NRMaterial* mat)
{
	if (cmd == VK_NULL_HANDLE || mat == NULL || mat->set == VK_NULL_HANDLE) return;

	nrvk.CmdBindDescriptorSets(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS, layout,
							   NR_SET_MATERIAL, 1, &mat->set, 0, NULL);
}
