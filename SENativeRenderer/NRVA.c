#include "NRFrame.h"
#include "NRShaderc.h"
#include "NRDevice.h"
#include "NRVkLoader.h"

// ============================================================
// NRVA.c
// 对外 C ABI 实现：把 NRApi.h 的导出函数接到各底层系统
//
// 初始化顺序（销毁时逆序）：
//   Loader -> Instance -> Surface -> Device -> Memory -> Descriptor
//   -> Pipeline -> Shaderc -> Mesh/Material -> Swapchain -> Scene
//   -> PostProcess -> Frame(含 Particle)
// ============================================================

static b32 nr_renderer_created = FALSE;
static struct NRRendererCreateInfo nr_create_info;

// ------------------------------------------------------------
// 着色器句柄表
// ------------------------------------------------------------
#define NR_MAX_SHADERS 512

typedef struct NRShaderSlot
{
	VkShaderModule module;
	u32 stage;
	b32 alive;
	u32 generation;
} NRShaderSlot;

static NRShaderSlot nr_shaders[NR_MAX_SHADERS];

static NRShaderSlot* nrShaderSlotResolve(NRShaderHandle h)
{
	if (h == 0) return NULL;
	u64 slot = h & 0xFFFFFFFFull;
	if (slot == 0 || slot > NR_MAX_SHADERS) return NULL;
	NRShaderSlot* s = &nr_shaders[slot - 1ull];
	if (!s->alive || (u32)(h >> 32) != s->generation) return NULL;
	return s;
}

static NRResult nrShaderSlotStore(VkShaderModule mod, u32 stage, NRShaderHandle* out)
{
	for (u32 i = 0; i < NR_MAX_SHADERS; i++)
	{
		if (nr_shaders[i].alive) continue;
		nr_shaders[i].module = mod;
		nr_shaders[i].stage = stage;
		nr_shaders[i].alive = TRUE;
		*out = ((u64)nr_shaders[i].generation << 32) | ((u64)i + 1ull);
		return NRR_MakeSuccess(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SUCCESS);
	}
	return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_CAPACITY_EXCEEDED, 0);
}

// ------------------------------------------------------------
// 渲染器生命周期
// ------------------------------------------------------------
static NRResult nrRendererCreateInternal(struct NRRendererCreateInfo info, u32 device_index)
{
	if (nr_renderer_created)
		return NRR_MakeWarning(NRR_STEP_NR_CreateRenderer, NRR_CODE_RENDERER_ALREADY_CREATED, 0);
	if (nr_window == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateRenderer, NRR_CODE_WINDOW_NOT_CREATED, 0);

	nr_create_info = info;
	nr_renderer_create_info = &nr_create_info;

	b32 validation = FALSE;
#ifdef _DEBUG
	validation = TRUE;
#endif

	NRResult r = nrVkLoadGlobal();
	if (NRR_FAILED(r)) return r;

	r = nrDeviceCreateInstance(&nr_create_info, validation);
	if (NRR_FAILED(r)) return r;

	r = nrDeviceCreateSurface();
	if (NRR_FAILED(r)) return r;

	r = nrDeviceCreateLogical(&nr_create_info, device_index);
	if (NRR_FAILED(r)) return r;

	r = nrMemoryInit();          if (NRR_FAILED(r)) return r;
	r = nrDescriptorInit();      if (NRR_FAILED(r)) return r;
	r = nrPipelineInit(NULL);    if (NRR_FAILED(r)) return r;
	r = nrShadercInit(NULL);     if (NRR_FAILED(r)) return r;
	r = nrMeshSystemInit();      if (NRR_FAILED(r)) return r;
	r = nrMaterialSystemInit();  if (NRR_FAILED(r)) return r;

	s32 w = 0, h = 0;
	SDL_GetWindowSizeInPixels(nr_window, &w, &h);

	r = nrSwapchainCreate((u32)w, (u32)h, TRUE, FALSE, 1);
	if (NRR_FAILED(r)) return r;

	r = nrSceneSystemInit();     if (NRR_FAILED(r)) return r;

	r = nrPostInit(nr_swapchain.extent.width, nr_swapchain.extent.height);
	if (NRR_FAILED(r)) return r;

	r = nrFrameInit();           if (NRR_FAILED(r)) return r;

	nr_renderer_created = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateRenderer, NRR_CODE_SUCCESS);
}

SE_OUT(NRResult) NR_CreateRenderer(struct NRRendererCreateInfo info)
{
	return nrRendererCreateInternal(info, NR_AUTO_DEVICE);
}

SE_OUT(NRResult) NR_CreateRendererOnDevice(struct NRRendererCreateInfo info, u32 device_index)
{
	return nrRendererCreateInternal(info, device_index);
}

SE_OUT(NRResult) NR_DestroyRenderer(void)
{
	if (!nr_renderer_created)
		return NRR_MakeWarning(NRR_STEP_NR_DestroyRenderer, NRR_CODE_RENDERER_NOT_CREATED, 0);

	if (nr_device.device != VK_NULL_HANDLE)
		nrvk.DeviceWaitIdle(nr_device.device);

	// 着色器模块由本层持有，须在管线系统关闭前释放
	for (u32 i = 0; i < NR_MAX_SHADERS; i++)
	{
		if (!nr_shaders[i].alive) continue;
		nrShaderDestroy(nr_shaders[i].module);
		nr_shaders[i].alive = FALSE;
		nr_shaders[i].generation++;
	}

	nrFrameShutdown();
	nrPostShutdown();
	nrSceneSystemShutdown();
	nrSwapchainDestroy();
	nrMaterialSystemShutdown();
	nrMeshSystemShutdown();
	nrShadercShutdown();
	nrPipelineSaveCache();
	nrPipelineShutdown();
	nrDescriptorShutdown();
	nrMemoryShutdown();
	nrDeviceDestroy();

	nr_renderer_created = FALSE;
	nr_renderer_create_info = NULL;
	return NRR_MakeSuccess(NRR_STEP_NR_DestroyRenderer, NRR_CODE_SUCCESS);
}

SE_OUT(NRResult) NR_Shutdown(void)
{
	if (nr_renderer_created) NR_DestroyRenderer();
	if (nr_window != NULL) NR_DestroyWindow();
	if (nr_sdl_init)
	{
		SDL_Quit();
		nr_sdl_init = FALSE;
	}
	return NRR_MakeSuccess(NRR_STEP_NR_Init, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 设备
// ------------------------------------------------------------
SE_OUT(NRResult) NR_EnumerateDevices(NRDeviceInfo* out_devices, u32* inout_count)
{
	if (inout_count == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_PARAMETER, 0);
	return nrDeviceEnumerate(out_devices, inout_count);
}

SE_OUT(NRResult) NR_GetDeviceInfo(NRDeviceInfo* out_info)
{
	if (out_info == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_PARAMETER, 0);
	return nrDeviceGetInfo(out_info);
}

SE_OUT(NRResult) NR_WaitDeviceIdle(void)
{
	if (!nr_renderer_created)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_RENDERER_NOT_CREATED, 0);
	nrvk.DeviceWaitIdle(nr_device.device);
	return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 交换链
// ------------------------------------------------------------
SE_OUT(NRResult) NR_ResizeSwapchain(u32 width, u32 height)
{
	if (!nr_renderer_created)
		return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_RENDERER_NOT_CREATED, 0);
	return nrFrameResize(width, height);
}

SE_OUT(NRResult) NR_SetVSync(b32 enable)
{
	if (!nr_renderer_created)
		return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_RENDERER_NOT_CREATED, 0);
	if (nr_swapchain.vsync == enable)
		return NRR_MakeSuccess(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SUCCESS);

	nrvk.DeviceWaitIdle(nr_device.device);
	nr_swapchain.vsync = enable;
	return nrSwapchainRecreate(nr_swapchain.extent.width, nr_swapchain.extent.height);
}

SE_OUT(NRResult) NR_SetHDR(b32 enable)
{
	if (!nr_renderer_created)
		return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_RENDERER_NOT_CREATED, 0);
	if (nr_swapchain.hdr == enable)
		return NRR_MakeSuccess(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SUCCESS);

	nrvk.DeviceWaitIdle(nr_device.device);
	nr_swapchain.hdr = enable;
	return nrSwapchainRecreate(nr_swapchain.extent.width, nr_swapchain.extent.height);
}

SE_OUT(NRResult) NR_SetMSAA(u32 samples)
{
	// 场景渲染走 HDR 离屏目标，MSAA 需重建整条 pass 链与全部管线，
	// 当前实现以 FXAA 作为抗锯齿路径。
	(void)samples;
	return NRR_MakeWarning(NRR_STEP_NR_CreateSwapchain, NRR_CODE_NOT_IMPLEMENTED, 0);
}

// ------------------------------------------------------------
// 着色器
// ------------------------------------------------------------
SE_OUT(NRResult) NR_CreateShaderFromSource(const char* source, u32 stage,
										   const char* entry_point,
										   NRShaderHandle* out_handle)
{
	if (source == NULL || out_handle == NULL)
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_INVALID_PARAMETER, 0);
	*out_handle = 0;

	u32* spirv = NULL; u64 size = 0;
	NRResult r = nrShadercCompile(source, "user_shader", stage,
								  (entry_point != NULL) ? entry_point : "main",
								  NULL, 0, &spirv, &size);
	if (NRR_FAILED(r)) return r;

	VkShaderModule mod = VK_NULL_HANDLE;
	r = nrShaderCreateFromSPIRV(spirv, size, &mod);
	nrShadercFree(spirv);
	if (NRR_FAILED(r)) return r;

	r = nrShaderSlotStore(mod, stage, out_handle);
	if (NRR_FAILED(r)) nrShaderDestroy(mod);
	return r;
}

SE_OUT(NRResult) NR_CreateShaderFromSPIRV(const u32* spirv, u64 size_bytes, u32 stage,
										  NRShaderHandle* out_handle)
{
	if (spirv == NULL || out_handle == NULL)
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_INVALID_PARAMETER, 0);
	*out_handle = 0;

	VkShaderModule mod = VK_NULL_HANDLE;
	NRResult r = nrShaderCreateFromSPIRV(spirv, size_bytes, &mod);
	if (NRR_FAILED(r)) return r;

	r = nrShaderSlotStore(mod, stage, out_handle);
	if (NRR_FAILED(r)) nrShaderDestroy(mod);
	return r;
}

SE_OUT(NRResult) NR_DestroyShader(NRShaderHandle handle)
{
	NRShaderSlot* s = nrShaderSlotResolve(handle);
	if (s == NULL)
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_INVALID_HANDLE, 0);

	nrShaderDestroy(s->module);
	s->module = VK_NULL_HANDLE;
	s->alive = FALSE;
	s->generation++;
	return NRR_MakeSuccess(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 资源
// ------------------------------------------------------------
SE_OUT(NRResult) NR_CreateMesh(const NRMeshCreateInfo* info, NRMeshHandle* out_handle)
{
	if (info == NULL || out_handle == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_PARAMETER, 0);
	return nrMeshCreate(info, out_handle);
}

SE_OUT(NRResult) NR_UpdateMesh(NRMeshHandle handle, const NRVertex* vertices, u32 vertex_count,
							   const u32* indices, u32 index_count)
{
	NRResult r = NRR_MakeSuccess(NRR_STEP_NR_CreateMesh, NRR_CODE_SUCCESS);
	if (vertices != NULL && vertex_count > 0)
	{
		r = nrMeshUpdateVertices(handle, vertices, vertex_count, 0);
		if (NRR_FAILED(r)) return r;
	}
	if (indices != NULL && index_count > 0)
		r = nrMeshUpdateIndices(handle, indices, index_count, 0);
	return r;
}

SE_OUT(NRResult) NR_DestroyMesh(NRMeshHandle handle)
{
	if (nrMeshResolve(handle) == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_HANDLE, 0);
	nrMeshDestroy(handle);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateMesh, NRR_CODE_SUCCESS);
}

SE_OUT(NRResult) NR_CreateTexture(const NRTextureCreateInfo* info, NRTextureHandle* out_handle)
{
	if (info == NULL || out_handle == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateTexture, NRR_CODE_INVALID_PARAMETER, 0);
	return nrTextureCreate(info, out_handle);
}

SE_OUT(NRResult) NR_UpdateTexture(NRTextureHandle handle, const void* pixels, u64 size_bytes,
								  u32 mip_level, u32 layer)
{
	NRTexture* tex = nrTextureResolve(handle);
	if (tex == NULL || pixels == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateTexture, NRR_CODE_INVALID_HANDLE, 0);
	return nrImageUpload(&tex->image, pixels, size_bytes, mip_level, layer);
}

SE_OUT(NRResult) NR_DestroyTexture(NRTextureHandle handle)
{
	if (nrTextureResolve(handle) == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateTexture, NRR_CODE_INVALID_HANDLE, 0);
	nrTextureDestroy(handle);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateTexture, NRR_CODE_SUCCESS);
}

SE_OUT(NRResult) NR_CreateMaterial(const NRMaterialCreateInfo* info, NRMaterialHandle* out_handle)
{
	if (info == NULL || out_handle == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMaterial, NRR_CODE_INVALID_PARAMETER, 0);
	return nrMaterialCreate(info, out_handle);
}

SE_OUT(NRResult) NR_UpdateMaterial(NRMaterialHandle handle, const NRMaterialCreateInfo* info)
{
	if (info == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMaterial, NRR_CODE_INVALID_PARAMETER, 0);
	return nrMaterialUpdate(handle, info);
}

SE_OUT(NRResult) NR_DestroyMaterial(NRMaterialHandle handle)
{
	if (nrMaterialResolve(handle) == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMaterial, NRR_CODE_INVALID_HANDLE, 0);
	nrMaterialDestroy(handle);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateMaterial, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 场景
// ------------------------------------------------------------
SE_OUT(NRResult) NR_CreateScene(NRSceneHandle* out_handle)
{
	if (out_handle == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_PARAMETER, 0);
	return nrSceneCreate(out_handle);
}

SE_OUT(NRResult) NR_DestroyScene(NRSceneHandle handle)
{
	if (nrSceneResolve(handle) == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_HANDLE, 0);
	if (nr_frame.active_scene == handle) nr_frame.active_scene = 0;
	if (nr_frame.overlay_scene == handle) nr_frame.overlay_scene = 0;
	nrSceneDestroy(handle);
	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);
}

SE_OUT(NRResult) NR_SetActiveScene(NRSceneHandle handle)
{
	return nrFrameSetActiveScene(handle);
}

SE_OUT(NRResult) NR_SetOverlayScene(NRSceneHandle handle)
{
	return nrFrameSetOverlayScene(handle);
}

SE_OUT(NRResult) NR_SetSceneEnvironment(NRSceneHandle scene, const NRSceneEnvDesc* desc)
{
	if (desc == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_PARAMETER, 0);
	return nrSceneSetEnvironment(scene, desc);
}

SE_OUT(NRResult) NR_AddObject(NRSceneHandle scene, const NRObjectDesc* desc,
							  NRObjectHandle* out_handle)
{
	if (desc == NULL || out_handle == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_PARAMETER, 0);
	return nrSceneAddObject(scene, desc, out_handle);
}

SE_OUT(NRResult) NR_UpdateObject(NRObjectHandle handle, const NRObjectDesc* desc)
{
	if (desc == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_PARAMETER, 0);
	NRResult r = nrSceneSetObjectTransform(nr_frame.active_scene, handle, &desc->world);
	if (NRR_FAILED(r)) return r;
	return nrSceneSetObjectVisible(nr_frame.active_scene, handle, desc->visible);
}

SE_OUT(NRResult) NR_SetObjectTransform(NRObjectHandle handle, const NRMatrix4* world)
{
	if (world == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_PARAMETER, 0);
	return nrSceneSetObjectTransform(nr_frame.active_scene, handle, world);
}

SE_OUT(NRResult) NR_SetObjectVisible(NRObjectHandle handle, b32 visible)
{
	return nrSceneSetObjectVisible(nr_frame.active_scene, handle, visible);
}

SE_OUT(NRResult) NR_RemoveObject(NRObjectHandle handle)
{
	return nrSceneRemoveObject(nr_frame.active_scene, handle);
}

SE_OUT(NRResult) NR_AddLight(NRSceneHandle scene, const NRLightDesc* desc,
							 NRLightHandle* out_handle)
{
	if (desc == NULL || out_handle == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_PARAMETER, 0);
	return nrSceneAddLight(scene, desc, out_handle);
}

SE_OUT(NRResult) NR_UpdateLight(NRLightHandle handle, const NRLightDesc* desc)
{
	if (desc == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_PARAMETER, 0);
	return nrSceneUpdateLight(nr_frame.active_scene, handle, desc);
}

SE_OUT(NRResult) NR_RemoveLight(NRLightHandle handle)
{
	return nrSceneRemoveLight(nr_frame.active_scene, handle);
}

SE_OUT(NRResult) NR_SetCamera(NRSceneHandle scene, const NRCameraDesc* desc)
{
	if (desc == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_PARAMETER, 0);
	return nrSceneSetCamera(scene, desc);
}

// ------------------------------------------------------------
// 特效
// ------------------------------------------------------------
SE_OUT(NRResult) NR_SetPostProcess(const NRPostProcessDesc* desc)
{
	if (desc == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_PostProcess, NRR_CODE_INVALID_PARAMETER, 0);
	return nrPostConfigure(desc);
}

SE_OUT(NRResult) NR_CreateParticleEmitter(NRSceneHandle scene,
										  const NRParticleEmitterDesc* desc,
										  NREmitterHandle* out_handle)
{
	if (desc == NULL || out_handle == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_INVALID_PARAMETER, 0);
	if (nrSceneResolve(scene) == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_INVALID_HANDLE, 0);

	NRResult r = nrEmitterCreate(desc, out_handle);
	if (NRR_FAILED(r)) return r;

	r = nrFrameRegisterEmitter(*out_handle);
	if (NRR_FAILED(r))
	{
		nrEmitterDestroy(*out_handle);
		*out_handle = 0;
	}
	return r;
}

SE_OUT(NRResult) NR_UpdateParticleEmitter(NREmitterHandle handle,
										  const NRParticleEmitterDesc* desc)
{
	if (desc == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_INVALID_PARAMETER, 0);
	return nrEmitterUpdate(handle, desc);
}

SE_OUT(NRResult) NR_SetParticleEmitterEnabled(NREmitterHandle handle, b32 enabled)
{
	if (nrEmitterResolve(handle) == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_INVALID_HANDLE, 0);

	// 停用只是从帧图移除，已有粒子随生命周期自然消亡，避免突兀消失
	if (enabled) return nrFrameRegisterEmitter(handle);

	nrFrameUnregisterEmitter(handle);
	return NRR_MakeSuccess(NRR_STEP_NR_Particle, NRR_CODE_SUCCESS);
}

SE_OUT(NRResult) NR_DestroyParticleEmitter(NREmitterHandle handle)
{
	if (nrEmitterResolve(handle) == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_INVALID_HANDLE, 0);

	nrvk.DeviceWaitIdle(nr_device.device);
	nrFrameUnregisterEmitter(handle);
	nrEmitterDestroy(handle);
	return NRR_MakeSuccess(NRR_STEP_NR_Particle, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 渲染
// ------------------------------------------------------------
SE_OUT(NRResult) NR_MainUpdate(f64 deltatime)
{
	(void)deltatime;
	if (!nr_sdl_init)
		return NRR_MakeFailure(NRR_STEP_NR_Init, NRR_CODE_NOT_INITIALIZED, 0);
	return NRR_MakeSuccess(NRR_STEP_NR_Init, NRR_CODE_SUCCESS);
}

SE_OUT(NRResult) NR_PrepareRender(f64 deltatime)
{
	(void)deltatime;
	if (!nr_renderer_created)
		return NRR_MakeFailure(NRR_STEP_NR_PrepareRender, NRR_CODE_RENDERER_NOT_CREATED, 0);

	// 交换链在上一帧被标记过期时，在这里统一重建，避免 Render 阶段中途失败
	if (nr_swapchain.needs_rebuild)
	{
		s32 w = 0, h = 0;
		SDL_GetWindowSizeInPixels(nr_window, &w, &h);
		if (w > 0 && h > 0) return nrFrameResize((u32)w, (u32)h);
	}
	return NRR_MakeSuccess(NRR_STEP_NR_PrepareRender, NRR_CODE_SUCCESS);
}

SE_OUT(NRResult) NR_Render(f64 deltatime)
{
	if (!nr_renderer_created)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_RENDERER_NOT_CREATED, 0);
	return nrFrameRender(deltatime);
}

SE_OUT(NRResult) NR_BeginFrame(f64 delta_time)
{
	return NR_PrepareRender(delta_time);
}

SE_OUT(NRResult) NR_EndFrame(void)
{
	if (!nr_renderer_created)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_RENDERER_NOT_CREATED, 0);
	return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);
}

SE_OUT(NRResult) NR_GetFrameStats(NRFrameStats* out_stats)
{
	if (out_stats == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_INVALID_PARAMETER, 0);

	*out_stats = nr_frame.stats;
	out_stats->gpu_memory_used = nrMemoryGetUsed();
	return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);
}
