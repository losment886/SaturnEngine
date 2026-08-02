#pragma once

// ============================================================
// NRPostProcess.h
// 离屏 HDR 渲染目标 + 可配置后处理 pass 链
//
// 帧内流程：
//   场景渲染 -> HDR color (R16G16B16A16_SFLOAT)
//     -> [Bloom] 亮度提取 -> 降采样链 -> 水平/垂直高斯模糊
//     -> [Tonemap + 色彩分级 + 暗角 + Gamma] -> LDR
//     -> [FXAA] -> 交换链
//
// 为什么用 R16F 而不是交换链的 8bit 格式：
//   泛光与色调映射必须在线性 HDR 空间进行，8bit 会在提取亮度前就已截断。
// ============================================================

#include "NRSwapchain.h"
#include "NRPipeline.h"
#include "NRMaterial.h"

SE_EXTERN_C_BEGIN

#define NR_BLOOM_MIP_COUNT 5

// 后处理 push constant（与 NRShaderLib.h 的 PostBlock 一致）
typedef struct NRPostPush
{
	f32 exposure;
	f32 gamma;
	f32 bloom_intensity;
	f32 vignette;
	f32 contrast;
	f32 saturation;
	u32 tonemap_mode;
	u32 flags;
} NRPostPush;

typedef struct NRBloomPush
{
	f32 threshold;
	f32 knee;
} NRBloomPush;

typedef struct NRBlurPush
{
	NRFloat2 direction;
} NRBlurPush;

// 单个离屏目标
typedef struct NRRenderTarget
{
	NRImage color;
	VkFramebuffer framebuffer;
	VkRenderPass render_pass;
	u32 width, height;
} NRRenderTarget;

typedef struct NRPostProcess
{
	// 场景 HDR 目标（含深度，供场景 pass 使用）
	NRImage hdr_color;
	NRImage hdr_depth;
	VkFramebuffer hdr_fb;
	VkRenderPass hdr_pass;
	u32 width, height;

	// Bloom 链：提取 + 逐级模糊的乒乓目标
	NRRenderTarget bloom_extract;
	NRRenderTarget bloom_ping[NR_BLOOM_MIP_COUNT];
	NRRenderTarget bloom_pong[NR_BLOOM_MIP_COUNT];

	// 输出到交换链的 LDR pass 复用 nr_swapchain.render_pass

	// 管线
	VkPipeline pipe_extract;
	VkPipeline pipe_blur;
	VkPipeline pipe_composite;
	VkPipeline pipe_fxaa;

	// 每个 pass 一个描述符集（绑定源图）
	VkDescriptorSetLayout post_layout;
	VkDescriptorSet set_extract;
	VkDescriptorSet set_blur_h[NR_BLOOM_MIP_COUNT];
	VkDescriptorSet set_blur_v[NR_BLOOM_MIP_COUNT];
	VkDescriptorSet set_composite;

	VkSampler linear_clamp;

	NRPostProcessDesc desc;
	b32 initialized;
} NRPostProcess;

extern NRPostProcess nr_post;

// 创建/销毁；尺寸变化时调用 Resize
NRResult nrPostInit(u32 width, u32 height);
NRResult nrPostResize(u32 width, u32 height);
void     nrPostShutdown(void);

NRResult nrPostConfigure(const NRPostProcessDesc* desc);

// 开始/结束场景 HDR pass
void nrPostBeginScene(VkCommandBuffer cmd, const NRFloat4* clear_color);
void nrPostEndScene(VkCommandBuffer cmd);

// 执行完整后处理链，最终写入交换链帧缓冲
void nrPostExecute(VkCommandBuffer cmd, VkFramebuffer target_fb, VkExtent2D target_extent);

SE_EXTERN_C_END
