#include "NRPostProcess.h"
#include "NRShaderc.h"
#include "NRShaderLib.h"
#include "NRVkLoader.h"
#include "NRDevice.h"

// ============================================================
// NRPostProcess.c
// ============================================================

NRPostProcess nr_post;

#define NR_HDR_FORMAT VK_FORMAT_R16G16B16A16_SFLOAT

// ------------------------------------------------------------
// 单色附件 render pass（后处理各级共用）
// ------------------------------------------------------------
static NRResult nrCreateColorPass(VkFormat format, VkImageLayout final_layout,
								  VkRenderPass* out)
{
	VkAttachmentDescription color;
	memset(&color, 0, sizeof(color));
	color.format = format;
	color.samples = VK_SAMPLE_COUNT_1_BIT;
	color.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
	color.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
	color.stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE;
	color.stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
	color.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
	color.finalLayout = final_layout;

	VkAttachmentReference ref;
	ref.attachment = 0;
	ref.layout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;

	VkSubpassDescription sub;
	memset(&sub, 0, sizeof(sub));
	sub.pipelineBindPoint = VK_PIPELINE_BIND_POINT_GRAPHICS;
	sub.colorAttachmentCount = 1;
	sub.pColorAttachments = &ref;

	// 等待上一 pass 的采样完成再写入，避免读写冲突
	VkSubpassDependency deps[2];
	memset(deps, 0, sizeof(deps));
	deps[0].srcSubpass = VK_SUBPASS_EXTERNAL;
	deps[0].dstSubpass = 0;
	deps[0].srcStageMask = VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT;
	deps[0].dstStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
	deps[0].srcAccessMask = VK_ACCESS_SHADER_READ_BIT;
	deps[0].dstAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
	deps[0].dependencyFlags = VK_DEPENDENCY_BY_REGION_BIT;

	deps[1].srcSubpass = 0;
	deps[1].dstSubpass = VK_SUBPASS_EXTERNAL;
	deps[1].srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
	deps[1].dstStageMask = VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT;
	deps[1].srcAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
	deps[1].dstAccessMask = VK_ACCESS_SHADER_READ_BIT;
	deps[1].dependencyFlags = VK_DEPENDENCY_BY_REGION_BIT;

	VkRenderPassCreateInfo info;
	memset(&info, 0, sizeof(info));
	info.sType = VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO;
	info.attachmentCount = 1;
	info.pAttachments = &color;
	info.subpassCount = 1;
	info.pSubpasses = &sub;
	info.dependencyCount = 2;
	info.pDependencies = deps;

	VkResult vr = nrvk.CreateRenderPass(nr_device.device, &info, NULL, out);
	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_PostProcess, NRR_CODE_RENDERPASS_CREATION_FAILED, (u32)vr);
	return NRR_MakeSuccess(NRR_STEP_NR_PostProcess, NRR_CODE_SUCCESS);
}

// 场景 HDR pass：颜色 + 深度
static NRResult nrCreateHdrPass(void)
{
	VkAttachmentDescription atts[2];
	memset(atts, 0, sizeof(atts));

	atts[0].format = NR_HDR_FORMAT;
	atts[0].samples = VK_SAMPLE_COUNT_1_BIT;
	atts[0].loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
	atts[0].storeOp = VK_ATTACHMENT_STORE_OP_STORE;
	atts[0].stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE;
	atts[0].stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
	atts[0].initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
	atts[0].finalLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;

	atts[1].format = nr_swapchain.depth.format;
	atts[1].samples = VK_SAMPLE_COUNT_1_BIT;
	atts[1].loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
	atts[1].storeOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
	atts[1].stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE;
	atts[1].stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
	atts[1].initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
	atts[1].finalLayout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;

	VkAttachmentReference colorRef = { 0, VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL };
	VkAttachmentReference depthRef = { 1, VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL };

	VkSubpassDescription sub;
	memset(&sub, 0, sizeof(sub));
	sub.pipelineBindPoint = VK_PIPELINE_BIND_POINT_GRAPHICS;
	sub.colorAttachmentCount = 1;
	sub.pColorAttachments = &colorRef;
	sub.pDepthStencilAttachment = &depthRef;

	VkSubpassDependency dep;
	memset(&dep, 0, sizeof(dep));
	dep.srcSubpass = VK_SUBPASS_EXTERNAL;
	dep.dstSubpass = 0;
	dep.srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT |
					   VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT;
	dep.dstStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT |
					   VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT;
	dep.dstAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT |
						VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT;

	VkRenderPassCreateInfo info;
	memset(&info, 0, sizeof(info));
	info.sType = VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO;
	info.attachmentCount = 2;
	info.pAttachments = atts;
	info.subpassCount = 1;
	info.pSubpasses = &sub;
	info.dependencyCount = 1;
	info.pDependencies = &dep;

	VkResult vr = nrvk.CreateRenderPass(nr_device.device, &info, NULL, &nr_post.hdr_pass);
	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_PostProcess, NRR_CODE_RENDERPASS_CREATION_FAILED, (u32)vr);
	return NRR_MakeSuccess(NRR_STEP_NR_PostProcess, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 渲染目标
// ------------------------------------------------------------
static NRResult nrTargetCreate(NRRenderTarget* rt, u32 w, u32 h, VkRenderPass pass)
{
	if (w == 0) w = 1;
	if (h == 0) h = 1;

	rt->width = w;
	rt->height = h;
	rt->render_pass = pass;

	NRResult r = nrImageCreate(w, h, 1, 1, 1, NR_HDR_FORMAT, VK_IMAGE_TILING_OPTIMAL,
							   VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_SAMPLED_BIT,
							   VK_IMAGE_ASPECT_COLOR_BIT, VK_SAMPLE_COUNT_1_BIT,
							   FALSE, &rt->color);
	if (NRR_FAILED(r)) return r;

	rt->color.sampler = nr_post.linear_clamp;

	VkFramebufferCreateInfo fb;
	memset(&fb, 0, sizeof(fb));
	fb.sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO;
	fb.renderPass = pass;
	fb.attachmentCount = 1;
	fb.pAttachments = &rt->color.view;
	fb.width = w;
	fb.height = h;
	fb.layers = 1;

	VkResult vr = nrvk.CreateFramebuffer(nr_device.device, &fb, NULL, &rt->framebuffer);
	if (vr != VK_SUCCESS)
	{
		nrImageDestroy(&rt->color);
		return NRR_MakeFailure(NRR_STEP_NR_PostProcess, NRR_CODE_FRAMEBUFFER_CREATION_FAILED, (u32)vr);
	}
	return NRR_MakeSuccess(NRR_STEP_NR_PostProcess, NRR_CODE_SUCCESS);
}

static void nrTargetDestroy(NRRenderTarget* rt)
{
	if (rt->framebuffer != VK_NULL_HANDLE)
		nrvk.DestroyFramebuffer(nr_device.device, rt->framebuffer, NULL);
	// sampler 由 nr_post 统一持有，这里置空避免 nrImageDestroy 重复销毁
	rt->color.sampler = VK_NULL_HANDLE;
	nrImageDestroy(&rt->color);
	memset(rt, 0, sizeof(*rt));
}

// ------------------------------------------------------------
// 描述符：后处理各 pass 只需 2 个组合图像采样器
// ------------------------------------------------------------
static NRResult nrCreatePostLayout(void)
{
	// 布局已在描述符层统一创建，与 nr_pipelines.postprocess_layout 保持一致，
	// 这里只引用，不重复创建也不负责销毁。
	if (nr_descriptors.postprocess_layout == VK_NULL_HANDLE)
		return NRR_MakeFailure(NRR_STEP_NR_PostProcess, NRR_CODE_NOT_INITIALIZED, 0);

	nr_post.post_layout = nr_descriptors.postprocess_layout;
	return NRR_MakeSuccess(NRR_STEP_NR_PostProcess, NRR_CODE_SUCCESS);
}

static void nrWritePostSet(VkDescriptorSet set, u32 binding,
						   const NRImage* img, VkSampler sampler)
{
	if (set == VK_NULL_HANDLE || img == NULL || img->view == VK_NULL_HANDLE) return;
	nrDescriptorWriteImage(set, binding, img->view, sampler,
						   VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);
}

// ------------------------------------------------------------
// 管线
// ------------------------------------------------------------
static NRResult nrCompileAndCreate(const char* frag_src, const char* name,
								   VkRenderPass pass, VkPipeline* out)
{
	u32* vs = NULL; u64 vsSize = 0;
	u32* fs = NULL; u64 fsSize = 0;

	NRResult r = nrShadercCompile(g_NRFullscreenVertGLSL, "fullscreen.vert",
								  NR_SHADER_STAGE_VERTEX, "main", NULL, 0, &vs, &vsSize);
	if (NRR_FAILED(r)) return r;

	r = nrShadercCompile(frag_src, name, NR_SHADER_STAGE_FRAGMENT, "main",
						 NULL, 0, &fs, &fsSize);
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
	cfg.render_pass = pass;
	cfg.layout = nr_pipelines.postprocess_layout;
	cfg.samples = VK_SAMPLE_COUNT_1_BIT;
	cfg.use_vertex_input = FALSE;      // 全屏三角形由 gl_VertexIndex 生成
	cfg.depth_test = FALSE;
	cfg.depth_write = FALSE;
	cfg.cull_mode = VK_CULL_MODE_NONE;
	cfg.blend_mode = NR_BLEND_OPAQUE;

	r = nrPipelineCreateGraphics(&cfg, out);

	nrShaderDestroy(vsm);
	nrShaderDestroy(fsm);
	return r;
}

// ------------------------------------------------------------
// 初始化
// ------------------------------------------------------------
static void nrPostDestroySizeDependent(void);

static NRResult nrPostCreateSizeDependent(u32 width, u32 height)
{
	nr_post.width = width;
	nr_post.height = height;

	// 场景 HDR 颜色 + 深度
	NRResult r = nrImageCreate(width, height, 1, 1, 1, NR_HDR_FORMAT,
							   VK_IMAGE_TILING_OPTIMAL,
							   VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_SAMPLED_BIT,
							   VK_IMAGE_ASPECT_COLOR_BIT, VK_SAMPLE_COUNT_1_BIT,
							   FALSE, &nr_post.hdr_color);
	if (NRR_FAILED(r)) return r;
	nr_post.hdr_color.sampler = nr_post.linear_clamp;

	r = nrImageCreate(width, height, 1, 1, 1, nr_swapchain.depth.format,
					  VK_IMAGE_TILING_OPTIMAL,
					  VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT,
					  VK_IMAGE_ASPECT_DEPTH_BIT, VK_SAMPLE_COUNT_1_BIT,
					  FALSE, &nr_post.hdr_depth);
	if (NRR_FAILED(r)) return r;

	VkImageView views[2] = { nr_post.hdr_color.view, nr_post.hdr_depth.view };
	VkFramebufferCreateInfo fb;
	memset(&fb, 0, sizeof(fb));
	fb.sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO;
	fb.renderPass = nr_post.hdr_pass;
	fb.attachmentCount = 2;
	fb.pAttachments = views;
	fb.width = width;
	fb.height = height;
	fb.layers = 1;

	VkResult vr = nrvk.CreateFramebuffer(nr_device.device, &fb, NULL, &nr_post.hdr_fb);
	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_PostProcess, NRR_CODE_FRAMEBUFFER_CREATION_FAILED, (u32)vr);

	// Bloom 提取为半分辨率，模糊链逐级减半
	u32 bw = width / 2, bh = height / 2;
	r = nrTargetCreate(&nr_post.bloom_extract, bw, bh, nr_post.bloom_extract.render_pass);
	if (NRR_FAILED(r)) return r;

	for (u32 i = 0; i < NR_BLOOM_MIP_COUNT; i++)
	{
		u32 mw = bw >> i, mh = bh >> i;
		r = nrTargetCreate(&nr_post.bloom_ping[i], mw, mh, nr_post.bloom_extract.render_pass);
		if (NRR_FAILED(r)) return r;
		r = nrTargetCreate(&nr_post.bloom_pong[i], mw, mh, nr_post.bloom_extract.render_pass);
		if (NRR_FAILED(r)) return r;
	}

	// 描述符集
	r = nrDescriptorAllocate(nr_post.post_layout, &nr_post.set_extract);
	if (NRR_FAILED(r)) return r;
	nrWritePostSet(nr_post.set_extract, 0, &nr_post.hdr_color, nr_post.linear_clamp);
	nrWritePostSet(nr_post.set_extract, 1, &nr_post.hdr_color, nr_post.linear_clamp);

	for (u32 i = 0; i < NR_BLOOM_MIP_COUNT; i++)
	{
		r = nrDescriptorAllocate(nr_post.post_layout, &nr_post.set_blur_h[i]);
		if (NRR_FAILED(r)) return r;
		r = nrDescriptorAllocate(nr_post.post_layout, &nr_post.set_blur_v[i]);
		if (NRR_FAILED(r)) return r;

		// 水平模糊读 ping，垂直模糊读 pong
		const NRImage* src = (i == 0) ? &nr_post.bloom_extract.color
									  : &nr_post.bloom_ping[i - 1].color;
		nrWritePostSet(nr_post.set_blur_h[i], 0, src, nr_post.linear_clamp);
		nrWritePostSet(nr_post.set_blur_h[i], 1, src, nr_post.linear_clamp);

		nrWritePostSet(nr_post.set_blur_v[i], 0, &nr_post.bloom_pong[i].color,
					   nr_post.linear_clamp);
		nrWritePostSet(nr_post.set_blur_v[i], 1, &nr_post.bloom_pong[i].color,
					   nr_post.linear_clamp);
	}

	r = nrDescriptorAllocate(nr_post.post_layout, &nr_post.set_composite);
	if (NRR_FAILED(r)) return r;
	nrWritePostSet(nr_post.set_composite, 0, &nr_post.hdr_color, nr_post.linear_clamp);
	nrWritePostSet(nr_post.set_composite, 1, &nr_post.bloom_ping[0].color,
				   nr_post.linear_clamp);

	return NRR_MakeSuccess(NRR_STEP_NR_PostProcess, NRR_CODE_SUCCESS);
}

NRResult nrPostInit(u32 width, u32 height)
{
	if (nr_post.initialized) return nrPostResize(width, height);

	memset(&nr_post, 0, sizeof(nr_post));

	// 后处理采样一律 linear + clamp，避免边缘环绕导致泛光渗色
	NRResult r = nrSamplerCreate(TRUE, NR_WRAP_CLAMP_EDGE, NR_WRAP_CLAMP_EDGE,
								 NR_WRAP_CLAMP_EDGE, 1.0f, 1, &nr_post.linear_clamp);
	if (NRR_FAILED(r)) return r;

	r = nrCreateHdrPass();
	if (NRR_FAILED(r)) return r;

	r = nrCreateColorPass(NR_HDR_FORMAT, VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
						  &nr_post.bloom_extract.render_pass);
	if (NRR_FAILED(r)) return r;

	r = nrCreatePostLayout();
	if (NRR_FAILED(r)) return r;

	r = nrPostCreateSizeDependent(width, height);
	if (NRR_FAILED(r)) return r;

	// 管线
	r = nrCompileAndCreate(g_NRBloomExtractFragGLSL, "bloom_extract.frag",
						   nr_post.bloom_extract.render_pass, &nr_post.pipe_extract);
	if (NRR_FAILED(r)) return r;

	r = nrCompileAndCreate(g_NRBlurFragGLSL, "blur.frag",
						   nr_post.bloom_extract.render_pass, &nr_post.pipe_blur);
	if (NRR_FAILED(r)) return r;

	r = nrCompileAndCreate(g_NRPostFragGLSL, "post.frag",
						   nr_swapchain.render_pass, &nr_post.pipe_composite);
	if (NRR_FAILED(r)) return r;

	// 默认配置
	nr_post.desc.enable_bloom = TRUE;
	nr_post.desc.bloom_threshold = 1.0f;
	nr_post.desc.bloom_intensity = 0.5f;
	nr_post.desc.enable_tonemap = TRUE;
	nr_post.desc.tonemap_operator = 1;   // ACES
	nr_post.desc.exposure = 1.0f;
	nr_post.desc.vignette = 0.2f;

	nr_post.initialized = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_PostProcess, NRR_CODE_SUCCESS);
}

static void nrPostDestroySizeDependent(void)
{
	if (nr_post.hdr_fb != VK_NULL_HANDLE)
	{
		nrvk.DestroyFramebuffer(nr_device.device, nr_post.hdr_fb, NULL);
		nr_post.hdr_fb = VK_NULL_HANDLE;
	}
	nr_post.hdr_color.sampler = VK_NULL_HANDLE;
	nrImageDestroy(&nr_post.hdr_color);
	nrImageDestroy(&nr_post.hdr_depth);

	VkRenderPass keep = nr_post.bloom_extract.render_pass;
	nrTargetDestroy(&nr_post.bloom_extract);
	nr_post.bloom_extract.render_pass = keep;

	for (u32 i = 0; i < NR_BLOOM_MIP_COUNT; i++)
	{
		nrTargetDestroy(&nr_post.bloom_ping[i]);
		nrTargetDestroy(&nr_post.bloom_pong[i]);
	}
}

NRResult nrPostResize(u32 width, u32 height)
{
	if (!nr_post.initialized) return nrPostInit(width, height);
	if (width == nr_post.width && height == nr_post.height)
		return NRR_MakeSuccess(NRR_STEP_NR_PostProcess, NRR_CODE_SUCCESS);

	nrvk.DeviceWaitIdle(nr_device.device);
	nrPostDestroySizeDependent();
	return nrPostCreateSizeDependent(width, height);
}

void nrPostShutdown(void)
{
	if (!nr_post.initialized) return;

	nrvk.DeviceWaitIdle(nr_device.device);

	nrPipelineDestroy(nr_post.pipe_extract);
	nrPipelineDestroy(nr_post.pipe_blur);
	nrPipelineDestroy(nr_post.pipe_composite);
	nrPipelineDestroy(nr_post.pipe_fxaa);

	nrPostDestroySizeDependent();

	if (nr_post.bloom_extract.render_pass != VK_NULL_HANDLE)
		nrvk.DestroyRenderPass(nr_device.device, nr_post.bloom_extract.render_pass, NULL);
	if (nr_post.hdr_pass != VK_NULL_HANDLE)
		nrvk.DestroyRenderPass(nr_device.device, nr_post.hdr_pass, NULL);
	// post_layout 由描述符层持有，这里只是引用，不能销毁
	nr_post.post_layout = VK_NULL_HANDLE;
	if (nr_post.linear_clamp != VK_NULL_HANDLE)
		nrvk.DestroySampler(nr_device.device, nr_post.linear_clamp, NULL);

	memset(&nr_post, 0, sizeof(nr_post));
}

NRResult nrPostConfigure(const NRPostProcessDesc* desc)
{
	if (desc == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_PostProcess, NRR_CODE_INVALID_PARAMETER, 0);
	nr_post.desc = *desc;
	return NRR_MakeSuccess(NRR_STEP_NR_PostProcess, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 场景 pass
// ------------------------------------------------------------
void nrPostBeginScene(VkCommandBuffer cmd, const NRFloat4* clear_color)
{
	if (!nr_post.initialized || cmd == VK_NULL_HANDLE) return;

	VkClearValue clears[2];
	memset(clears, 0, sizeof(clears));
	if (clear_color != NULL)
	{
		clears[0].color.float32[0] = clear_color->x;
		clears[0].color.float32[1] = clear_color->y;
		clears[0].color.float32[2] = clear_color->z;
		clears[0].color.float32[3] = clear_color->w;
	}
	clears[1].depthStencil.depth = 1.0f;

	VkRenderPassBeginInfo bi;
	memset(&bi, 0, sizeof(bi));
	bi.sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO;
	bi.renderPass = nr_post.hdr_pass;
	bi.framebuffer = nr_post.hdr_fb;
	bi.renderArea.extent.width = nr_post.width;
	bi.renderArea.extent.height = nr_post.height;
	bi.clearValueCount = 2;
	bi.pClearValues = clears;

	nrvk.CmdBeginRenderPass(cmd, &bi, VK_SUBPASS_CONTENTS_INLINE);

	VkViewport vp = { 0.0f, 0.0f, (f32)nr_post.width, (f32)nr_post.height, 0.0f, 1.0f };
	VkRect2D sc = { { 0, 0 }, { nr_post.width, nr_post.height } };
	nrvk.CmdSetViewport(cmd, 0, 1, &vp);
	nrvk.CmdSetScissor(cmd, 0, 1, &sc);
}

void nrPostEndScene(VkCommandBuffer cmd)
{
	if (!nr_post.initialized || cmd == VK_NULL_HANDLE) return;
	nrvk.CmdEndRenderPass(cmd);
}

// ------------------------------------------------------------
// 后处理链执行
// ------------------------------------------------------------
static void nrDrawFullscreen(VkCommandBuffer cmd, VkPipeline pipe, VkDescriptorSet set,
							 const void* push, u32 push_size,
							 VkFramebuffer fb, VkRenderPass pass,
							 u32 w, u32 h)
{
	VkClearValue clear[2];
	memset(clear, 0, sizeof(clear));
	// 目标 pass 可能带深度附件（交换链 pass 就是），必须提供第二个 clear 值
	clear[1].depthStencil.depth = 1.0f;

	VkRenderPassBeginInfo bi;
	memset(&bi, 0, sizeof(bi));
	bi.sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO;
	bi.renderPass = pass;
	bi.framebuffer = fb;
	bi.renderArea.extent.width = w;
	bi.renderArea.extent.height = h;
	bi.clearValueCount = 2;
	bi.pClearValues = clear;

	nrvk.CmdBeginRenderPass(cmd, &bi, VK_SUBPASS_CONTENTS_INLINE);

	VkViewport vp = { 0.0f, 0.0f, (f32)w, (f32)h, 0.0f, 1.0f };
	VkRect2D sc = { { 0, 0 }, { w, h } };
	nrvk.CmdSetViewport(cmd, 0, 1, &vp);
	nrvk.CmdSetScissor(cmd, 0, 1, &sc);

	nrvk.CmdBindPipeline(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS, pipe);
	nrvk.CmdBindDescriptorSets(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS,
							   nr_pipelines.postprocess_layout, 0, 1, &set, 0, NULL);
	if (push != NULL && push_size > 0)
		nrvk.CmdPushConstants(cmd, nr_pipelines.postprocess_layout,
							  VK_SHADER_STAGE_VERTEX_BIT | VK_SHADER_STAGE_FRAGMENT_BIT,
							  0, push_size, push);

	// 全屏三角形：3 个顶点，无顶点缓冲
	nrvk.CmdDraw(cmd, 3, 1, 0, 0);
	nrvk.CmdEndRenderPass(cmd);
}

void nrPostExecute(VkCommandBuffer cmd, VkFramebuffer target_fb, VkExtent2D target_extent)
{
	if (!nr_post.initialized || cmd == VK_NULL_HANDLE) return;

	// ---- Bloom ----
	if (nr_post.desc.enable_bloom)
	{
		NRBloomPush bp;
		bp.threshold = nr_post.desc.bloom_threshold;
		bp.knee = 0.5f;
		nrDrawFullscreen(cmd, nr_post.pipe_extract, nr_post.set_extract,
						 &bp, sizeof(bp),
						 nr_post.bloom_extract.framebuffer,
						 nr_post.bloom_extract.render_pass,
						 nr_post.bloom_extract.width, nr_post.bloom_extract.height);

		for (u32 i = 0; i < NR_BLOOM_MIP_COUNT; i++)
		{
			NRBlurPush h = { { 1.0f, 0.0f } };
			nrDrawFullscreen(cmd, nr_post.pipe_blur, nr_post.set_blur_h[i],
							 &h, sizeof(h),
							 nr_post.bloom_pong[i].framebuffer,
							 nr_post.bloom_extract.render_pass,
							 nr_post.bloom_pong[i].width, nr_post.bloom_pong[i].height);

			NRBlurPush v = { { 0.0f, 1.0f } };
			nrDrawFullscreen(cmd, nr_post.pipe_blur, nr_post.set_blur_v[i],
							 &v, sizeof(v),
							 nr_post.bloom_ping[i].framebuffer,
							 nr_post.bloom_extract.render_pass,
							 nr_post.bloom_ping[i].width, nr_post.bloom_ping[i].height);
		}
	}

	// ---- 合成到交换链 ----
	NRPostPush pp;
	pp.exposure = (nr_post.desc.exposure > 0.0f) ? nr_post.desc.exposure : 1.0f;
	pp.gamma = 2.2f;
	pp.bloom_intensity = nr_post.desc.enable_bloom ? nr_post.desc.bloom_intensity : 0.0f;
	pp.vignette = nr_post.desc.vignette;
	pp.contrast = 1.0f;
	pp.saturation = 1.0f;
	// desc 的 0=Reinhard 1=ACES 2=Filmic 需映射到着色器的 1/2/3
	pp.tonemap_mode = nr_post.desc.enable_tonemap
					? (nr_post.desc.tonemap_operator + 1u) : 0u;
	pp.flags = 0;

	nrDrawFullscreen(cmd, nr_post.pipe_composite, nr_post.set_composite,
					 &pp, sizeof(pp), target_fb, nr_swapchain.render_pass,
					 target_extent.width, target_extent.height);
}
