#pragma once

// ============================================================
// NRSwapchain.h
// 交换链、深度附件、帧同步对象与呈现
// ============================================================

#include "NRMemory.h"

SE_EXTERN_C_BEGIN

#define NR_MAX_FRAMES_IN_FLIGHT 2
#define NR_MAX_SWAPCHAIN_IMAGES 8

typedef struct NRSwapchain
{
	VkSwapchainKHR handle;
	VkFormat format;
	VkColorSpaceKHR color_space;
	VkPresentModeKHR present_mode;
	VkExtent2D extent;

	u32 image_count;
	VkImage images[NR_MAX_SWAPCHAIN_IMAGES];
	VkImageView views[NR_MAX_SWAPCHAIN_IMAGES];
	VkFramebuffer framebuffers[NR_MAX_SWAPCHAIN_IMAGES];

	// 深度 / MSAA 解析目标
	NRImage depth;
	NRImage color_msaa;
	VkSampleCountFlagBits samples;

	VkRenderPass render_pass;

	// 帧同步
	VkSemaphore image_available[NR_MAX_FRAMES_IN_FLIGHT];
	VkSemaphore render_finished[NR_MAX_FRAMES_IN_FLIGHT];
	VkFence in_flight[NR_MAX_FRAMES_IN_FLIGHT];
	VkFence images_in_flight[NR_MAX_SWAPCHAIN_IMAGES];

	// 命令
	VkCommandPool cmd_pool;
	VkCommandBuffer cmd_buffers[NR_MAX_FRAMES_IN_FLIGHT];

	u32 current_frame;
	u32 current_image;
	b32 vsync;
	b32 hdr;
	b32 needs_rebuild;
	b32 created;
} NRSwapchain;

extern NRSwapchain nr_swapchain;

// 创建/重建（width/height 为 0 时自动从 surface 能力取）
NRResult nrSwapchainCreate(u32 width, u32 height, b32 vsync, b32 hdr, u32 msaa_samples);
NRResult nrSwapchainRecreate(u32 width, u32 height);
void     nrSwapchainDestroy(void);

// 获取下一帧图像，写入 nr_swapchain.current_image
// 返回 warning + NRR_CODE_SWAPCHAIN_OUT_OF_DATE 时调用方应重建
NRResult nrSwapchainAcquire(void);
// 提交当前帧命令缓冲并呈现
NRResult nrSwapchainPresent(void);
// 当前帧的命令缓冲
VkCommandBuffer nrSwapchainCurrentCmd(void);

SE_EXTERN_C_END
