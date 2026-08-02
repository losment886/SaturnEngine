#pragma once

// ============================================================
// NRMemory.h
// 显存分配、缓冲与图像资源封装
//
// 分配策略：为每个 (memoryTypeIndex) 维护一组 256MB 的大块，
// 在块内做线性/空闲链表子分配，避免 vkAllocateMemory 次数
// 触及 maxMemoryAllocationCount（部分驱动仅 4096）。
// 超过块大小的请求走独占分配。
// ============================================================

#include "NRDevice.h"

SE_EXTERN_C_BEGIN

#define NR_MEM_BLOCK_SIZE (256ull * 1024ull * 1024ull)

typedef struct NRAllocation
{
	VkDeviceMemory memory;
	u64 offset;
	u64 size;
	void* mapped;        // 仅 HOST_VISIBLE 时非 NULL
	u32 memory_type;
	b32 dedicated;       // 独占分配，释放时直接 vkFreeMemory
	void* block;         // 所属块（内部使用）
} NRAllocation;

typedef struct NRBuffer
{
	VkBuffer buffer;
	NRAllocation alloc;
	u64 size;
	VkBufferUsageFlags usage;
} NRBuffer;

typedef struct NRImage
{
	VkImage image;
	VkImageView view;
	VkSampler sampler;
	NRAllocation alloc;
	VkFormat format;
	VkImageLayout layout;
	u32 width, height, depth;
	u32 mip_levels;
	u32 array_layers;
	VkImageAspectFlags aspect;
} NRImage;

// 初始化 / 释放分配器
NRResult nrMemoryInit(void);
void     nrMemoryShutdown(void);
u64      nrMemoryGetUsed(void);

// 底层分配
NRResult nrMemoryAlloc(const VkMemoryRequirements* req, VkMemoryPropertyFlags props,
					   b32 force_dedicated, NRAllocation* out);
void     nrMemoryFree(NRAllocation* alloc);

// ---------- 缓冲 ----------
NRResult nrBufferCreate(u64 size, VkBufferUsageFlags usage, VkMemoryPropertyFlags props,
						NRBuffer* out);
void     nrBufferDestroy(NRBuffer* buf);
// 将 CPU 数据写入缓冲（HOST_VISIBLE 直写；DEVICE_LOCAL 走 staging）
NRResult nrBufferUpload(NRBuffer* buf, const void* data, u64 size, u64 offset);

// ---------- 图像 ----------
NRResult nrImageCreate(u32 width, u32 height, u32 depth, u32 mip_levels, u32 array_layers,
					   VkFormat format, VkImageTiling tiling, VkImageUsageFlags usage,
					   VkImageAspectFlags aspect, VkSampleCountFlagBits samples,
					   b32 cube, NRImage* out);
void     nrImageDestroy(NRImage* img);
// 布局转换（内部自动推导 stage/access mask）
NRResult nrImageTransition(VkCommandBuffer cmd, NRImage* img,
						   VkImageLayout new_layout, u32 base_mip, u32 mip_count);
// 从 CPU 像素上传（自动 staging + 布局转换）
NRResult nrImageUpload(NRImage* img, const void* pixels, u64 size, u32 mip_level, u32 layer);
// 使用 vkCmdBlitImage 生成完整 mip 链
NRResult nrImageGenerateMipmaps(NRImage* img);
// 创建采样器
NRResult nrSamplerCreate(b32 linear, u32 wrap_u, u32 wrap_v, u32 wrap_w,
						 f32 max_anisotropy, u32 mip_levels, VkSampler* out);

// 将 NRApi 的 NR_TEXFMT_* 映射到 VkFormat；不支持返回 VK_FORMAT_UNDEFINED
VkFormat nrFormatFromNR(u32 nr_format);
u32      nrFormatBytesPerPixel(VkFormat fmt);

SE_EXTERN_C_END
