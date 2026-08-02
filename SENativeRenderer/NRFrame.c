#include "NRFrame.h"
#include "NRShaderc.h"
#include "NRShaderLib.h"
#include "NRVkLoader.h"
#include "NRDevice.h"

// ============================================================
// NRFrame.c
// ============================================================

NRFrameSystem nr_frame;

// ------------------------------------------------------------
// 场景管线
// ------------------------------------------------------------
static NRResult nrCreateScenePipeline(u32 blend_mode, b32 double_sided,
									  b32 depth_write, VkPipeline* out)
{
	u32* vs = NULL; u64 vsSize = 0;
	u32* fs = NULL; u64 fsSize = 0;

	NRResult r = nrShadercCompile(g_NRPbrVertGLSL, "pbr.vert",
								  NR_SHADER_STAGE_VERTEX, "main", NULL, 0, &vs, &vsSize);
	if (NRR_FAILED(r)) return r;

	r = nrShadercCompile(g_NRPbrFragGLSL, "pbr.frag",
						 NR_SHADER_STAGE_FRAGMENT, "main", NULL, 0, &fs, &fsSize);
	if (NRR_FAILED(r)) { nrShadercFree(vs); return r; }

	VkShaderModule vsm = VK_NULL_HANDLE, fsm = VK_NULL_HANDLE;
	r = nrShaderCreateFromSPIRV(vs, vsSize, &vsm);
	if (NRR_FAILED(r)) { nrShadercFree(vs); nrShadercFree(fs); return r; }
	r = nrShaderCreateFromSPIRV(fs, fsSize, &fsm);
	nrShadercFree(vs); nrShadercFree(fs);
	if (NRR_FAILED(r)) { nrShaderDestroy(vsm); return r; }

	NRPipelineConfig cfg;
	nrPipelineConfigDefaults(&cfg);
	cfg.vertex = vsm;
	cfg.fragment = fsm;
	cfg.render_pass = nr_post.hdr_pass;
	cfg.layout = nr_pipelines.main_layout;
	cfg.samples = VK_SAMPLE_COUNT_1_BIT;   // HDR 离屏目标不做 MSAA，抗锯齿交给 FXAA
	cfg.use_vertex_input = TRUE;
	cfg.depth_test = TRUE;
	cfg.depth_write = depth_write;
	cfg.cull_mode = double_sided ? VK_CULL_MODE_NONE : VK_CULL_MODE_BACK_BIT;
	cfg.blend_mode = blend_mode;

	r = nrPipelineCreateGraphics(&cfg, out);
	nrShaderDestroy(vsm);
	nrShaderDestroy(fsm);
	return r;
}

// ------------------------------------------------------------
// 初始化
// ------------------------------------------------------------
NRResult nrFrameInit(void)
{
	if (nr_frame.initialized)
		return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);
	if (!nr_swapchain.created || !nr_post.initialized)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_NOT_INITIALIZED, 0);

	memset(&nr_frame, 0, sizeof(nr_frame));

	NRResult r = nrCreateScenePipeline(NR_BLEND_OPAQUE, FALSE, TRUE, &nr_frame.pipe_opaque);
	if (NRR_FAILED(r)) return r;

	r = nrCreateScenePipeline(NR_BLEND_MASK, FALSE, TRUE, &nr_frame.pipe_masked);
	if (NRR_FAILED(r)) return r;

	r = nrCreateScenePipeline(NR_BLEND_OPAQUE, TRUE, TRUE, &nr_frame.pipe_double_sided);
	if (NRR_FAILED(r)) return r;

	// 透明物体不写深度，否则后绘制的透明面会被自身遮挡
	r = nrCreateScenePipeline(NR_BLEND_ALPHA, TRUE, FALSE, &nr_frame.pipe_transparent);
	if (NRR_FAILED(r)) return r;

	r = nrParticleInit(nr_post.hdr_pass);
	if (NRR_FAILED(r)) return r;

	// 宿主可能只想要清屏 + UI 叠加而不建场景，这里准备一个默认空场景，
	// 保证 nrFrameRender 始终有可用的 active_scene（可被 NR_SetActiveScene 覆盖）
	NRSceneHandle defaultScene = 0;
	r = nrSceneCreate(&defaultScene);
	if (NRR_FAILED(r)) return r;
	nr_frame.active_scene = defaultScene;

	nr_frame.initialized = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);
}

void nrFrameShutdown(void)
{
	if (!nr_frame.initialized) return;

	nrvk.DeviceWaitIdle(nr_device.device);

	nrParticleShutdown();

	nrPipelineDestroy(nr_frame.pipe_opaque);
	nrPipelineDestroy(nr_frame.pipe_masked);
	nrPipelineDestroy(nr_frame.pipe_transparent);
	nrPipelineDestroy(nr_frame.pipe_double_sided);

	memset(&nr_frame, 0, sizeof(nr_frame));
}

NRResult nrFrameResize(u32 width, u32 height)
{
	if (width == 0 || height == 0)
		return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);

	nrvk.DeviceWaitIdle(nr_device.device);

	NRResult r = nrSwapchainRecreate(width, height);
	if (NRR_FAILED(r)) return r;

	// HDR 目标尺寸随交换链走；render pass 不变，故场景管线无需重建
	return nrPostResize(nr_swapchain.extent.width, nr_swapchain.extent.height);
}

NRResult nrFrameSetActiveScene(NRSceneHandle scene)
{
	if (nrSceneResolve(scene) == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_INVALID_HANDLE, 0);
	nr_frame.active_scene = scene;
	return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);
}

NRResult nrFrameSetOverlayScene(NRSceneHandle scene)
{
	// 0 表示关闭叠加层
	if (scene != 0 && nrSceneResolve(scene) == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_INVALID_HANDLE, 0);
	nr_frame.overlay_scene = scene;
	return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);
}

NRResult nrFrameRegisterEmitter(NREmitterHandle emitter)
{
	if (nrEmitterResolve(emitter) == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_INVALID_HANDLE, 0);
	if (nr_frame.emitter_count >= NR_MAX_EMITTERS)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_CAPACITY_EXCEEDED, 0);

	for (u32 i = 0; i < nr_frame.emitter_count; i++)
		if (nr_frame.emitters[i] == emitter)
			return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);

	nr_frame.emitters[nr_frame.emitter_count++] = emitter;
	return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);
}

void nrFrameUnregisterEmitter(NREmitterHandle emitter)
{
	for (u32 i = 0; i < nr_frame.emitter_count; i++)
	{
		if (nr_frame.emitters[i] != emitter) continue;
		nr_frame.emitters[i] = nr_frame.emitters[nr_frame.emitter_count - 1u];
		nr_frame.emitter_count--;
		return;
	}
}

// ------------------------------------------------------------
// 场景绘制
// ------------------------------------------------------------
static VkPipeline nrPickPipeline(const NRMaterial* mat)
{
	if (mat == NULL) return nr_frame.pipe_opaque;

	switch (mat->blend_mode)
	{
	case NR_BLEND_MASK:
		return nr_frame.pipe_masked;
	case NR_BLEND_ALPHA:
	case NR_BLEND_ADD:
	case NR_BLEND_MULTIPLY:
		return nr_frame.pipe_transparent;
	default:
		return mat->double_sided ? nr_frame.pipe_double_sided : nr_frame.pipe_opaque;
	}
}

static void nrDrawQueue(VkCommandBuffer cmd, const NRScene* scene,
						const NRDrawItem* items, u32 count, f32 time)
{
	VkPipeline bound = VK_NULL_HANDLE;
	const NRMesh* bound_mesh = NULL;
	const NRMaterial* bound_mat = NULL;

	for (u32 i = 0; i < count; i++)
	{
		const NRDrawItem* item = &items[i];
		if (item->mesh == NULL || item->index_count == 0) continue;

		VkPipeline pipe = nrPickPipeline(item->material);
		if (pipe != bound)
		{
			nrvk.CmdBindPipeline(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS, pipe);
			bound = pipe;
			// 管线切换会失效已绑定的描述符集布局兼容性，全局集需重绑
			nrvk.CmdBindDescriptorSets(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS,
									   nr_pipelines.main_layout, NR_SET_GLOBAL,
									   1, &scene->global_set, 0, NULL);
			if (nr_descriptors.bindless_enabled)
				nrvk.CmdBindDescriptorSets(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS,
										   nr_pipelines.main_layout, NR_SET_BINDLESS,
										   1, &nr_descriptors.bindless_set, 0, NULL);
			bound_mat = NULL;
		}

		if (item->material != bound_mat)
		{
			nrMaterialBind(cmd, nr_pipelines.main_layout, item->material);
			bound_mat = item->material;
		}

		if (item->mesh != bound_mesh)
		{
			nrMeshBind(cmd, item->mesh);
			bound_mesh = item->mesh;
		}

		NRPushConstants push;
		memset(&push, 0, sizeof(push));
		push.model = item->object->world;
		push.material_index = 0;
		push.object_flags = item->object->cast_shadow ? 1u : 0u;
		push.time = time;

		nrvk.CmdPushConstants(cmd, nr_pipelines.main_layout,
							  VK_SHADER_STAGE_VERTEX_BIT | VK_SHADER_STAGE_FRAGMENT_BIT,
							  0, sizeof(push), &push);

		nrvk.CmdDrawIndexed(cmd, item->index_count, 1, item->index_offset, 0, 0);

		nr_frame.stats.draw_calls++;
		nr_frame.stats.triangles += item->index_count / 3u;
	}
}

static void nrSetFullViewport(VkCommandBuffer cmd, u32 width, u32 height)
{
	VkViewport vp;
	vp.x = 0.0f;
	vp.y = 0.0f;
	vp.width = (f32)width;
	vp.height = (f32)height;
	vp.minDepth = 0.0f;
	vp.maxDepth = 1.0f;
	nrvk.CmdSetViewport(cmd, 0, 1, &vp);

	VkRect2D sc;
	sc.offset.x = 0;
	sc.offset.y = 0;
	sc.extent.width = width;
	sc.extent.height = height;
	nrvk.CmdSetScissor(cmd, 0, 1, &sc);
}

// ------------------------------------------------------------
// 一帧
// ------------------------------------------------------------
NRResult nrFrameRender(f64 delta_time)
{
	if (!nr_frame.initialized)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_NOT_INITIALIZED, 0);

	NRScene* scene = nrSceneResolve(nr_frame.active_scene);
	if (scene == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_INVALID_HANDLE, 0);

	nr_frame.time_accum += delta_time;
	const f32 time = (f32)nr_frame.time_accum;
	const f32 dt = (f32)delta_time;

	memset(&nr_frame.stats, 0, sizeof(nr_frame.stats));

	// ---- 1. 获取交换链图像 ----
	NRResult r = nrSwapchainAcquire();
	if (NRR_GetCode(r) == NRR_CODE_SWAPCHAIN_OUT_OF_DATE)
	{
		// 交换链过期时重建并跳过本帧，下一帧自然恢复
		return nrFrameResize(nr_swapchain.extent.width, nr_swapchain.extent.height);
	}
	if (NRR_FAILED(r)) return r;

	// ---- 2. 构建渲染队列（剔除 + 排序 + 上传 UBO）----
	r = nrSceneBuildQueue(nr_frame.active_scene, time);
	if (NRR_FAILED(r)) return r;

	// UI 叠加层单独构建队列；构建失败不应阻断整帧
	NRScene* overlay = NULL;
	if (nr_frame.overlay_scene != 0 && nr_frame.overlay_scene != nr_frame.active_scene)
	{
		overlay = nrSceneResolve(nr_frame.overlay_scene);
		if (overlay != NULL && NRR_FAILED(nrSceneBuildQueue(nr_frame.overlay_scene, time)))
			overlay = NULL;
	}

	nr_frame.stats.visible_objects = scene->opaque_count + scene->transparent_count;
	nr_frame.stats.culled_objects =
		(scene->object_count > nr_frame.stats.visible_objects)
		? (scene->object_count - nr_frame.stats.visible_objects) : 0u;

	// CPU 侧发射粒子（会写入 HOST_VISIBLE 缓冲）
	for (u32 i = 0; i < nr_frame.emitter_count; i++)
		nrEmitterEmit(nr_frame.emitters[i], dt);

	VkCommandBuffer cmd = nrSwapchainCurrentCmd();
	if (cmd == VK_NULL_HANDLE)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_COMMAND_BUFFER_FAILED, 0);

	VkCommandBufferBeginInfo bi;
	memset(&bi, 0, sizeof(bi));
	bi.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
	bi.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
	if (nrvk.BeginCommandBuffer(cmd, &bi) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_COMMAND_BUFFER_FAILED, 1);

	// ---- 3. 粒子模拟（必须在 render pass 之外）----
	for (u32 i = 0; i < nr_frame.emitter_count; i++)
	{
		nrEmitterSimulate(cmd, nr_frame.emitters[i], dt);
		const NREmitter* em = nrEmitterResolve(nr_frame.emitters[i]);
		if (em != NULL) nr_frame.stats.active_particles += em->alive_count;
	}

	// ---- 4. HDR 场景 pass ----
	nrPostBeginScene(cmd, &scene->env.clear_color);
	nrSetFullViewport(cmd, nr_post.width, nr_post.height);

	nrDrawQueue(cmd, scene, scene->opaque, scene->opaque_count, time);
	nrDrawQueue(cmd, scene, scene->transparent, scene->transparent_count, time);

	// 粒子最后绘制：不写深度但需要与已有几何做深度测试
	for (u32 i = 0; i < nr_frame.emitter_count; i++)
		nrEmitterRender(cmd, nr_frame.emitters[i], scene->global_set);

	// ---- 4.1 UI 叠加层：在主场景之后绘制，保证始终位于最上层 ----
	if (overlay != NULL)
	{
		nrDrawQueue(cmd, overlay, overlay->opaque, overlay->opaque_count, time);
		nrDrawQueue(cmd, overlay, overlay->transparent, overlay->transparent_count, time);
	}

	nrPostEndScene(cmd);
	// ---- 5. 后处理链 -> 交换链 ----
	nrPostExecute(cmd,
				  nr_swapchain.framebuffers[nr_swapchain.current_image],
				  nr_swapchain.extent);

	if (nrvk.EndCommandBuffer(cmd) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_COMMAND_BUFFER_FAILED, 2);

	// ---- 6. 提交 + 呈现 ----
	r = nrSwapchainPresent();
	nr_frame.frame_index++;
	nr_frame.stats.cpu_frame_ms = delta_time * 1000.0;

	if (NRR_GetCode(r) == NRR_CODE_SWAPCHAIN_OUT_OF_DATE)
		return nrFrameResize(nr_swapchain.extent.width, nr_swapchain.extent.height);

	return r;
}
