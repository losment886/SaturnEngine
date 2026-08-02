#include "NRMemory.h"

// ============================================================
// NRMemory.c
// 显存分配器 + 缓冲/图像资源
// ============================================================

// ------------------------------------------------------------
// 块与空闲区间
// ------------------------------------------------------------
typedef struct NRMemRange
{
	u64 offset;
	u64 size;
	struct NRMemRange* next;
} NRMemRange;

typedef struct NRMemBlock
{
	VkDeviceMemory memory;
	u64 size;
	u32 memory_type;
	void* mapped;
	NRMemRange* free_list;   // 按 offset 升序
	struct NRMemBlock* next;
} NRMemBlock;

static NRMemBlock* s_blocks = NULL;
static u64 s_used = 0;
static u64 s_reserved = 0;
static b32 s_inited = FALSE;

static u64 nrAlignUp(u64 v, u64 a)
{
	if (a == 0) return v;
	return (v + a - 1) & ~(a - 1);
}

NRResult nrMemoryInit(void)
{
	if (s_inited) return NRR_MakeSuccess(NRR_STEP_NR_CreateBuffer, NRR_CODE_SUCCESS);
	s_blocks = NULL;
	s_used = 0;
	s_reserved = 0;
	s_inited = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateBuffer, NRR_CODE_SUCCESS);
}

static void nrFreeListDestroy(NRMemRange* r)
{
	while (r != NULL)
	{
		NRMemRange* n = r->next;
		free(r);
		r = n;
	}
}

void nrMemoryShutdown(void)
{
	NRMemBlock* b = s_blocks;
	while (b != NULL)
	{
		NRMemBlock* n = b->next;
		if (b->mapped != NULL && nrvk.UnmapMemory != NULL)
			nrvk.UnmapMemory(nr_device.device, b->memory);
		if (b->memory != VK_NULL_HANDLE && nrvk.FreeMemory != NULL)
			nrvk.FreeMemory(nr_device.device, b->memory, NULL);
		nrFreeListDestroy(b->free_list);
		free(b);
		b = n;
	}
	s_blocks = NULL;
	s_used = 0;
	s_reserved = 0;
	s_inited = FALSE;
}

u64 nrMemoryGetUsed(void) { return s_used; }

// 在块的空闲链表中寻找满足 size/alignment 的区间
static bool nrBlockAllocate(NRMemBlock* block, u64 size, u64 alignment, u64* out_offset)
{
	NRMemRange* prev = NULL;
	NRMemRange* cur = block->free_list;
	while (cur != NULL)
	{
		u64 aligned = nrAlignUp(cur->offset, alignment);
		u64 padding = aligned - cur->offset;
		if (cur->size >= padding + size)
		{
			*out_offset = aligned;

			u64 tailOffset = aligned + size;
			u64 tailSize = cur->offset + cur->size - tailOffset;

			if (padding > 0)
			{
				// 保留前置碎片
				cur->size = padding;
				if (tailSize > 0)
				{
					NRMemRange* tail = (NRMemRange*)malloc(sizeof(NRMemRange));
					if (tail == NULL) return false;
					tail->offset = tailOffset;
					tail->size = tailSize;
					tail->next = cur->next;
					cur->next = tail;
				}
			}
			else
			{
				if (tailSize > 0)
				{
					cur->offset = tailOffset;
					cur->size = tailSize;
				}
				else
				{
					if (prev == NULL) block->free_list = cur->next;
					else prev->next = cur->next;
					free(cur);
				}
			}
			return true;
		}
		prev = cur;
		cur = cur->next;
	}
	return false;
}

// 归还并合并相邻空闲区间
static void nrBlockRelease(NRMemBlock* block, u64 offset, u64 size)
{
	NRMemRange* prev = NULL;
	NRMemRange* cur = block->free_list;
	while (cur != NULL && cur->offset < offset)
	{
		prev = cur;
		cur = cur->next;
	}

	NRMemRange* node = (NRMemRange*)malloc(sizeof(NRMemRange));
	if (node == NULL) return;
	node->offset = offset;
	node->size = size;
	node->next = cur;
	if (prev == NULL) block->free_list = node;
	else prev->next = node;

	// 与后继合并
	if (node->next != NULL && node->offset + node->size == node->next->offset)
	{
		NRMemRange* n = node->next;
		node->size += n->size;
		node->next = n->next;
		free(n);
	}
	// 与前驱合并
	if (prev != NULL && prev->offset + prev->size == node->offset)
	{
		prev->size += node->size;
		prev->next = node->next;
		free(node);
	}
}

NRResult nrMemoryAlloc(const VkMemoryRequirements* req, VkMemoryPropertyFlags props,
					   b32 force_dedicated, NRAllocation* out)
{
	if (req == NULL || out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_INVALID_PARAMETER, 0);

	memset(out, 0, sizeof(NRAllocation));

	s32 typeIndex = nrDeviceFindMemoryType(req->memoryTypeBits, props);
	if (typeIndex < 0)
	{
		// 回退：去掉 HOST_COHERENT 再试
		typeIndex = nrDeviceFindMemoryType(req->memoryTypeBits,
										   props & ~VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
		if (typeIndex < 0)
			return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_OUT_OF_MEMORY, 1);
	}

	const bool hostVisible = (props & VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT) != 0;

	// ---- 独占分配 ----
	if (force_dedicated || req->size > NR_MEM_BLOCK_SIZE / 2)
	{
		VkMemoryAllocateInfo ai;
		memset(&ai, 0, sizeof(ai));
		ai.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
		ai.allocationSize = req->size;
		ai.memoryTypeIndex = (u32)typeIndex;

		VkDeviceMemory mem = VK_NULL_HANDLE;
		if (nrvk.AllocateMemory(nr_device.device, &ai, NULL, &mem) != VK_SUCCESS)
			return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_OUT_OF_MEMORY, 2);

		out->memory = mem;
		out->offset = 0;
		out->size = req->size;
		out->memory_type = (u32)typeIndex;
		out->dedicated = TRUE;
		out->block = NULL;
		if (hostVisible)
			nrvk.MapMemory(nr_device.device, mem, 0, req->size, 0, &out->mapped);

		s_used += req->size;
		return NRR_MakeSuccess(NRR_STEP_NR_CreateBuffer, NRR_CODE_SUCCESS);
	}

	// ---- 在已有块中子分配 ----
	for (NRMemBlock* b = s_blocks; b != NULL; b = b->next)
	{
		if (b->memory_type != (u32)typeIndex) continue;
		u64 offset = 0;
		if (nrBlockAllocate(b, req->size, req->alignment, &offset))
		{
			out->memory = b->memory;
			out->offset = offset;
			out->size = req->size;
			out->memory_type = (u32)typeIndex;
			out->dedicated = FALSE;
			out->block = b;
			out->mapped = (b->mapped != NULL) ? ((u8*)b->mapped + offset) : NULL;
			s_used += req->size;
			return NRR_MakeSuccess(NRR_STEP_NR_CreateBuffer, NRR_CODE_SUCCESS);
		}
	}

	// ---- 新建块 ----
	NRMemBlock* block = (NRMemBlock*)malloc(sizeof(NRMemBlock));
	if (block == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_OUT_OF_MEMORY, 3);
	memset(block, 0, sizeof(NRMemBlock));

	VkMemoryAllocateInfo ai;
	memset(&ai, 0, sizeof(ai));
	ai.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
	ai.allocationSize = NR_MEM_BLOCK_SIZE;
	ai.memoryTypeIndex = (u32)typeIndex;

	if (nrvk.AllocateMemory(nr_device.device, &ai, NULL, &block->memory) != VK_SUCCESS)
	{
		free(block);
		return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_OUT_OF_MEMORY, 4);
	}

	block->size = NR_MEM_BLOCK_SIZE;
	block->memory_type = (u32)typeIndex;
	if (hostVisible)
		nrvk.MapMemory(nr_device.device, block->memory, 0, NR_MEM_BLOCK_SIZE, 0, &block->mapped);

	block->free_list = (NRMemRange*)malloc(sizeof(NRMemRange));
	if (block->free_list == NULL)
	{
		nrvk.FreeMemory(nr_device.device, block->memory, NULL);
		free(block);
		return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_OUT_OF_MEMORY, 5);
	}
	block->free_list->offset = 0;
	block->free_list->size = NR_MEM_BLOCK_SIZE;
	block->free_list->next = NULL;

	block->next = s_blocks;
	s_blocks = block;
	s_reserved += NR_MEM_BLOCK_SIZE;

	u64 offset = 0;
	if (!nrBlockAllocate(block, req->size, req->alignment, &offset))
		return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_OUT_OF_MEMORY, 6);

	out->memory = block->memory;
	out->offset = offset;
	out->size = req->size;
	out->memory_type = (u32)typeIndex;
	out->dedicated = FALSE;
	out->block = block;
	out->mapped = (block->mapped != NULL) ? ((u8*)block->mapped + offset) : NULL;
	s_used += req->size;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateBuffer, NRR_CODE_SUCCESS);
}

void nrMemoryFree(NRAllocation* alloc)
{
	if (alloc == NULL || alloc->memory == VK_NULL_HANDLE) return;

	if (alloc->dedicated)
	{
		if (alloc->mapped != NULL) nrvk.UnmapMemory(nr_device.device, alloc->memory);
		nrvk.FreeMemory(nr_device.device, alloc->memory, NULL);
	}
	else if (alloc->block != NULL)
	{
		nrBlockRelease((NRMemBlock*)alloc->block, alloc->offset, alloc->size);
	}

	if (s_used >= alloc->size) s_used -= alloc->size;
	memset(alloc, 0, sizeof(NRAllocation));
}

// ------------------------------------------------------------
// 缓冲
// ------------------------------------------------------------
NRResult nrBufferCreate(u64 size, VkBufferUsageFlags usage, VkMemoryPropertyFlags props,
						NRBuffer* out)
{
	if (out == NULL || size == 0)
		return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_INVALID_PARAMETER, 0);

	memset(out, 0, sizeof(NRBuffer));

	VkBufferCreateInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
	ci.size = size;
	ci.usage = usage;
	ci.sharingMode = VK_SHARING_MODE_EXCLUSIVE;

	if (nrvk.CreateBuffer(nr_device.device, &ci, NULL, &out->buffer) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_BUFFER_CREATION_FAILED, 0);

	VkMemoryRequirements req;
	nrvk.GetBufferMemoryRequirements(nr_device.device, out->buffer, &req);

	NRResult r = nrMemoryAlloc(&req, props, FALSE, &out->alloc);
	if (NRR_FAILED(r))
	{
		nrvk.DestroyBuffer(nr_device.device, out->buffer, NULL);
		out->buffer = VK_NULL_HANDLE;
		return r;
	}

	if (nrvk.BindBufferMemory(nr_device.device, out->buffer,
							  out->alloc.memory, out->alloc.offset) != VK_SUCCESS)
	{
		nrMemoryFree(&out->alloc);
		nrvk.DestroyBuffer(nr_device.device, out->buffer, NULL);
		out->buffer = VK_NULL_HANDLE;
		return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_BUFFER_CREATION_FAILED, 1);
	}

	out->size = size;
	out->usage = usage;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateBuffer, NRR_CODE_SUCCESS);
}

void nrBufferDestroy(NRBuffer* buf)
{
	if (buf == NULL) return;
	if (buf->buffer != VK_NULL_HANDLE)
		nrvk.DestroyBuffer(nr_device.device, buf->buffer, NULL);
	nrMemoryFree(&buf->alloc);
	memset(buf, 0, sizeof(NRBuffer));
}

NRResult nrBufferUpload(NRBuffer* buf, const void* data, u64 size, u64 offset)
{
	if (buf == NULL || data == NULL || size == 0)
		return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_INVALID_PARAMETER, 0);
	if (offset + size > buf->size)
		return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_CAPACITY_EXCEEDED, 0);

	// 直写路径
	if (buf->alloc.mapped != NULL)
	{
		memcpy((u8*)buf->alloc.mapped + offset, data, (size_t)size);

		VkMappedMemoryRange range;
		memset(&range, 0, sizeof(range));
		range.sType = VK_STRUCTURE_TYPE_MAPPED_MEMORY_RANGE;
		range.memory = buf->alloc.memory;
		// offset 必须是 nonCoherentAtomSize 的整数倍，否则 flush 会被驱动丢弃，写入的数据永远不会对 GPU 可见
		u64 atom = nr_device.props.limits.nonCoherentAtomSize;
		u64 start = buf->alloc.offset + offset;
		if (atom > 1)
			start -= start % atom;
		range.offset = start;
		range.size = VK_WHOLE_SIZE;
		nrvk.FlushMappedMemoryRanges(nr_device.device, 1, &range);
		return NRR_MakeSuccess(NRR_STEP_NR_CreateBuffer, NRR_CODE_SUCCESS);
	}

	// staging 路径
	NRBuffer staging;
	NRResult r = nrBufferCreate(size, VK_BUFFER_USAGE_TRANSFER_SRC_BIT,
								VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT |
								VK_MEMORY_PROPERTY_HOST_COHERENT_BIT, &staging);
	if (NRR_FAILED(r)) return r;

	if (staging.alloc.mapped != NULL)
		memcpy(staging.alloc.mapped, data, (size_t)size);

	VkCommandBuffer cmd = nrDeviceBeginOneShot();
	if (cmd == VK_NULL_HANDLE)
	{
		nrBufferDestroy(&staging);
		return NRR_MakeFailure(NRR_STEP_NR_CreateBuffer, NRR_CODE_COMMAND_BUFFER_FAILED, 0);
	}

	VkBufferCopy copy;
	copy.srcOffset = 0;
	copy.dstOffset = offset;
	copy.size = size;
	nrvk.CmdCopyBuffer(cmd, staging.buffer, buf->buffer, 1, &copy);

	r = nrDeviceEndOneShot(cmd);
	nrBufferDestroy(&staging);
	return r;
}

// ------------------------------------------------------------
// 格式映射
// ------------------------------------------------------------
VkFormat nrFormatFromNR(u32 nr_format)
{
	switch (nr_format)
	{
		case NR_TEXFMT_R8G8B8A8_UNORM:  return VK_FORMAT_R8G8B8A8_UNORM;
		case NR_TEXFMT_R8G8B8A8_SRGB:   return VK_FORMAT_R8G8B8A8_SRGB;
		case NR_TEXFMT_R8_UNORM:        return VK_FORMAT_R8_UNORM;
		case NR_TEXFMT_R16G16B16A16_SF: return VK_FORMAT_R16G16B16A16_SFLOAT;
		case NR_TEXFMT_R32G32B32A32_SF: return VK_FORMAT_R32G32B32A32_SFLOAT;
		case NR_TEXFMT_BC7_SRGB:        return VK_FORMAT_BC7_SRGB_BLOCK;
		case NR_TEXFMT_ASTC_4x4_SRGB:   return VK_FORMAT_ASTC_4x4_SRGB_BLOCK;
		case NR_TEXFMT_ETC2_SRGB:       return VK_FORMAT_ETC2_R8G8B8A8_SRGB_BLOCK;
		case NR_TEXFMT_D32_SFLOAT:      return VK_FORMAT_D32_SFLOAT;
		default:                        return VK_FORMAT_UNDEFINED;
	}
}

u32 nrFormatBytesPerPixel(VkFormat fmt)
{
	switch (fmt)
	{
		case VK_FORMAT_R8_UNORM:              return 1;
		case VK_FORMAT_R8G8B8A8_UNORM:
		case VK_FORMAT_R8G8B8A8_SRGB:
		case VK_FORMAT_B8G8R8A8_UNORM:
		case VK_FORMAT_B8G8R8A8_SRGB:
		case VK_FORMAT_D32_SFLOAT:            return 4;
		case VK_FORMAT_R16G16B16A16_SFLOAT:   return 8;
		case VK_FORMAT_R32G32B32A32_SFLOAT:   return 16;
		default:                              return 4;
	}
}

static VkSamplerAddressMode nrWrapToVk(u32 wrap)
{
	switch (wrap)
	{
		case NR_WRAP_MIRRORED_REPEAT: return VK_SAMPLER_ADDRESS_MODE_MIRRORED_REPEAT;
		case NR_WRAP_CLAMP_EDGE:      return VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE;
		case NR_WRAP_CLAMP_BORDER:    return VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_BORDER;
		default:                      return VK_SAMPLER_ADDRESS_MODE_REPEAT;
	}
}

// ------------------------------------------------------------
// 图像
// ------------------------------------------------------------
NRResult nrImageCreate(u32 width, u32 height, u32 depth, u32 mip_levels, u32 array_layers,
					   VkFormat format, VkImageTiling tiling, VkImageUsageFlags usage,
					   VkImageAspectFlags aspect, VkSampleCountFlagBits samples,
					   b32 cube, NRImage* out)
{
	if (out == NULL || width == 0 || height == 0)
		return NRR_MakeFailure(NRR_STEP_NR_CreateImage, NRR_CODE_INVALID_PARAMETER, 0);

	memset(out, 0, sizeof(NRImage));
	if (depth == 0) depth = 1;
	if (array_layers == 0) array_layers = 1;
	if (mip_levels == 0)
	{
		u32 m = width > height ? width : height;
		mip_levels = 1;
		while (m > 1) { m >>= 1; mip_levels++; }
	}

	VkImageCreateInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO;
	ci.imageType = (depth > 1) ? VK_IMAGE_TYPE_3D : VK_IMAGE_TYPE_2D;
	ci.extent.width = width;
	ci.extent.height = height;
	ci.extent.depth = depth;
	ci.mipLevels = mip_levels;
	ci.arrayLayers = cube ? 6 : array_layers;
	ci.format = format;
	ci.tiling = tiling;
	ci.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
	ci.usage = usage;
	ci.samples = samples;
	ci.sharingMode = VK_SHARING_MODE_EXCLUSIVE;
	if (cube) ci.flags |= VK_IMAGE_CREATE_CUBE_COMPATIBLE_BIT;

	if (nrvk.CreateImage(nr_device.device, &ci, NULL, &out->image) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateImage, NRR_CODE_IMAGE_CREATION_FAILED, 0);

	VkMemoryRequirements req;
	nrvk.GetImageMemoryRequirements(nr_device.device, out->image, &req);

	NRResult r = nrMemoryAlloc(&req, VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT, FALSE, &out->alloc);
	if (NRR_FAILED(r))
	{
		nrvk.DestroyImage(nr_device.device, out->image, NULL);
		out->image = VK_NULL_HANDLE;
		return r;
	}

	if (nrvk.BindImageMemory(nr_device.device, out->image,
							 out->alloc.memory, out->alloc.offset) != VK_SUCCESS)
	{
		nrMemoryFree(&out->alloc);
		nrvk.DestroyImage(nr_device.device, out->image, NULL);
		out->image = VK_NULL_HANDLE;
		return NRR_MakeFailure(NRR_STEP_NR_CreateImage, NRR_CODE_IMAGE_CREATION_FAILED, 1);
	}

	VkImageViewCreateInfo vci;
	memset(&vci, 0, sizeof(vci));
	vci.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
	vci.image = out->image;
	if (cube) vci.viewType = VK_IMAGE_VIEW_TYPE_CUBE;
	else if (depth > 1) vci.viewType = VK_IMAGE_VIEW_TYPE_3D;
	else if (array_layers > 1) vci.viewType = VK_IMAGE_VIEW_TYPE_2D_ARRAY;
	else vci.viewType = VK_IMAGE_VIEW_TYPE_2D;
	vci.format = format;
	vci.subresourceRange.aspectMask = aspect;
	vci.subresourceRange.baseMipLevel = 0;
	vci.subresourceRange.levelCount = mip_levels;
	vci.subresourceRange.baseArrayLayer = 0;
	vci.subresourceRange.layerCount = ci.arrayLayers;

	if (nrvk.CreateImageView(nr_device.device, &vci, NULL, &out->view) != VK_SUCCESS)
	{
		nrMemoryFree(&out->alloc);
		nrvk.DestroyImage(nr_device.device, out->image, NULL);
		out->image = VK_NULL_HANDLE;
		return NRR_MakeFailure(NRR_STEP_NR_CreateImage, NRR_CODE_IMAGE_CREATION_FAILED, 2);
	}

	out->format = format;
	out->layout = VK_IMAGE_LAYOUT_UNDEFINED;
	out->width = width;
	out->height = height;
	out->depth = depth;
	out->mip_levels = mip_levels;
	out->array_layers = ci.arrayLayers;
	out->aspect = aspect;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateImage, NRR_CODE_SUCCESS);
}

void nrImageDestroy(NRImage* img)
{
	if (img == NULL) return;
	if (img->sampler != VK_NULL_HANDLE)
		nrvk.DestroySampler(nr_device.device, img->sampler, NULL);
	if (img->view != VK_NULL_HANDLE)
		nrvk.DestroyImageView(nr_device.device, img->view, NULL);
	if (img->image != VK_NULL_HANDLE)
		nrvk.DestroyImage(nr_device.device, img->image, NULL);
	nrMemoryFree(&img->alloc);
	memset(img, 0, sizeof(NRImage));
}

// 由布局推导访问掩码与管线阶段
static void nrLayoutMasks(VkImageLayout layout, VkAccessFlags* access, VkPipelineStageFlags* stage)
{
	switch (layout)
	{
		case VK_IMAGE_LAYOUT_UNDEFINED:
			*access = 0; *stage = VK_PIPELINE_STAGE_TOP_OF_PIPE_BIT; break;
		case VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL:
			*access = VK_ACCESS_TRANSFER_WRITE_BIT; *stage = VK_PIPELINE_STAGE_TRANSFER_BIT; break;
		case VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL:
			*access = VK_ACCESS_TRANSFER_READ_BIT; *stage = VK_PIPELINE_STAGE_TRANSFER_BIT; break;
		case VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL:
			*access = VK_ACCESS_SHADER_READ_BIT;
			*stage = VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT; break;
		case VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL:
			*access = VK_ACCESS_COLOR_ATTACHMENT_READ_BIT | VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
			*stage = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT; break;
		case VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL:
			*access = VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_READ_BIT |
					  VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT;
			*stage = VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT |
					 VK_PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT; break;
		case VK_IMAGE_LAYOUT_DEPTH_STENCIL_READ_ONLY_OPTIMAL:
			*access = VK_ACCESS_SHADER_READ_BIT;
			*stage = VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT; break;
		case VK_IMAGE_LAYOUT_GENERAL:
			*access = VK_ACCESS_SHADER_READ_BIT | VK_ACCESS_SHADER_WRITE_BIT;
			*stage = VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT; break;
		case VK_IMAGE_LAYOUT_PRESENT_SRC_KHR:
			*access = 0; *stage = VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT; break;
		default:
			*access = 0; *stage = VK_PIPELINE_STAGE_ALL_COMMANDS_BIT; break;
	}
}

NRResult nrImageTransition(VkCommandBuffer cmd, NRImage* img,
						   VkImageLayout new_layout, u32 base_mip, u32 mip_count)
{
	if (cmd == VK_NULL_HANDLE || img == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateImage, NRR_CODE_INVALID_PARAMETER, 0);
	if (mip_count == 0) mip_count = img->mip_levels - base_mip;

	VkAccessFlags srcAccess, dstAccess;
	VkPipelineStageFlags srcStage, dstStage;
	nrLayoutMasks(img->layout, &srcAccess, &srcStage);
	nrLayoutMasks(new_layout, &dstAccess, &dstStage);

	VkImageMemoryBarrier b;
	memset(&b, 0, sizeof(b));
	b.sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER;
	b.oldLayout = img->layout;
	b.newLayout = new_layout;
	b.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
	b.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
	b.image = img->image;
	b.subresourceRange.aspectMask = img->aspect;
	b.subresourceRange.baseMipLevel = base_mip;
	b.subresourceRange.levelCount = mip_count;
	b.subresourceRange.baseArrayLayer = 0;
	b.subresourceRange.layerCount = img->array_layers;
	b.srcAccessMask = srcAccess;
	b.dstAccessMask = dstAccess;

	nrvk.CmdPipelineBarrier(cmd, srcStage, dstStage, 0, 0, NULL, 0, NULL, 1, &b);
	img->layout = new_layout;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateImage, NRR_CODE_SUCCESS);
}

NRResult nrImageUpload(NRImage* img, const void* pixels, u64 size, u32 mip_level, u32 layer)
{
	if (img == NULL || pixels == NULL || size == 0)
		return NRR_MakeFailure(NRR_STEP_NR_CreateImage, NRR_CODE_INVALID_PARAMETER, 0);

	NRBuffer staging;
	NRResult r = nrBufferCreate(size, VK_BUFFER_USAGE_TRANSFER_SRC_BIT,
								VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT |
								VK_MEMORY_PROPERTY_HOST_COHERENT_BIT, &staging);
	if (NRR_FAILED(r)) return r;
	if (staging.alloc.mapped != NULL)
		memcpy(staging.alloc.mapped, pixels, (size_t)size);

	VkCommandBuffer cmd = nrDeviceBeginOneShot();
	if (cmd == VK_NULL_HANDLE)
	{
		nrBufferDestroy(&staging);
		return NRR_MakeFailure(NRR_STEP_NR_CreateImage, NRR_CODE_COMMAND_BUFFER_FAILED, 0);
	}

	VkImageLayout original = img->layout;
	nrImageTransition(cmd, img, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 0, img->mip_levels);

	u32 w = img->width >> mip_level;  if (w == 0) w = 1;
	u32 h = img->height >> mip_level; if (h == 0) h = 1;
	u32 d = img->depth >> mip_level;  if (d == 0) d = 1;

	VkBufferImageCopy copy;
	memset(&copy, 0, sizeof(copy));
	copy.bufferOffset = 0;
	copy.imageSubresource.aspectMask = img->aspect;
	copy.imageSubresource.mipLevel = mip_level;
	copy.imageSubresource.baseArrayLayer = layer;
	copy.imageSubresource.layerCount = 1;
	copy.imageExtent.width = w;
	copy.imageExtent.height = h;
	copy.imageExtent.depth = d;

	nrvk.CmdCopyBufferToImage(cmd, staging.buffer, img->image,
							  VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &copy);

	VkImageLayout target = (original == VK_IMAGE_LAYOUT_UNDEFINED)
						 ? VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL : original;
	nrImageTransition(cmd, img, target, 0, img->mip_levels);

	r = nrDeviceEndOneShot(cmd);
	nrBufferDestroy(&staging);
	return r;
}

NRResult nrImageGenerateMipmaps(NRImage* img)
{
	if (img == NULL || img->mip_levels <= 1)
		return NRR_MakeSuccess(NRR_STEP_NR_CreateImage, NRR_CODE_SUCCESS);

	VkFormatProperties fp;
	nrvk.GetPhysicalDeviceFormatProperties(nr_device.physical, img->format, &fp);
	if (!(fp.optimalTilingFeatures & VK_FORMAT_FEATURE_SAMPLED_IMAGE_FILTER_LINEAR_BIT))
	{
		// 压缩格式等无法线性过滤，由调用方提供完整 mip 数据
		return NRR_MakeWarning(NRR_STEP_NR_CreateImage, NRR_CODE_NOT_IMPLEMENTED, 0);
	}

	VkCommandBuffer cmd = nrDeviceBeginOneShot();
	if (cmd == VK_NULL_HANDLE)
		return NRR_MakeFailure(NRR_STEP_NR_CreateImage, NRR_CODE_COMMAND_BUFFER_FAILED, 0);

	s32 mipW = (s32)img->width;
	s32 mipH = (s32)img->height;

	VkImageMemoryBarrier b;
	memset(&b, 0, sizeof(b));
	b.sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER;
	b.image = img->image;
	b.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
	b.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
	b.subresourceRange.aspectMask = img->aspect;
	b.subresourceRange.baseArrayLayer = 0;
	b.subresourceRange.layerCount = img->array_layers;
	b.subresourceRange.levelCount = 1;

	for (u32 i = 1; i < img->mip_levels; i++)
	{
		// 上一级 -> TRANSFER_SRC
		b.subresourceRange.baseMipLevel = i - 1;
		b.oldLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
		b.newLayout = VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL;
		b.srcAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT;
		b.dstAccessMask = VK_ACCESS_TRANSFER_READ_BIT;
		nrvk.CmdPipelineBarrier(cmd, VK_PIPELINE_STAGE_TRANSFER_BIT,
								VK_PIPELINE_STAGE_TRANSFER_BIT, 0, 0, NULL, 0, NULL, 1, &b);

		VkImageBlit blit;
		memset(&blit, 0, sizeof(blit));
		blit.srcOffsets[1].x = mipW;
		blit.srcOffsets[1].y = mipH;
		blit.srcOffsets[1].z = 1;
		blit.srcSubresource.aspectMask = img->aspect;
		blit.srcSubresource.mipLevel = i - 1;
		blit.srcSubresource.baseArrayLayer = 0;
		blit.srcSubresource.layerCount = img->array_layers;
		blit.dstOffsets[1].x = (mipW > 1) ? mipW / 2 : 1;
		blit.dstOffsets[1].y = (mipH > 1) ? mipH / 2 : 1;
		blit.dstOffsets[1].z = 1;
		blit.dstSubresource.aspectMask = img->aspect;
		blit.dstSubresource.mipLevel = i;
		blit.dstSubresource.baseArrayLayer = 0;
		blit.dstSubresource.layerCount = img->array_layers;

		nrvk.CmdBlitImage(cmd,
						  img->image, VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
						  img->image, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
						  1, &blit, VK_FILTER_LINEAR);

		// 上一级 -> SHADER_READ
		b.oldLayout = VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL;
		b.newLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
		b.srcAccessMask = VK_ACCESS_TRANSFER_READ_BIT;
		b.dstAccessMask = VK_ACCESS_SHADER_READ_BIT;
		nrvk.CmdPipelineBarrier(cmd, VK_PIPELINE_STAGE_TRANSFER_BIT,
								VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, 0, NULL, 0, NULL, 1, &b);

		if (mipW > 1) mipW /= 2;
		if (mipH > 1) mipH /= 2;
	}

	// 最后一级 -> SHADER_READ
	b.subresourceRange.baseMipLevel = img->mip_levels - 1;
	b.oldLayout = VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
	b.newLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
	b.srcAccessMask = VK_ACCESS_TRANSFER_WRITE_BIT;
	b.dstAccessMask = VK_ACCESS_SHADER_READ_BIT;
	nrvk.CmdPipelineBarrier(cmd, VK_PIPELINE_STAGE_TRANSFER_BIT,
							VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, 0, NULL, 0, NULL, 1, &b);

	img->layout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
	return nrDeviceEndOneShot(cmd);
}

NRResult nrSamplerCreate(b32 linear, u32 wrap_u, u32 wrap_v, u32 wrap_w,
						 f32 max_anisotropy, u32 mip_levels, VkSampler* out)
{
	if (out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateImage, NRR_CODE_INVALID_PARAMETER, 0);

	VkSamplerCreateInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO;
	ci.magFilter = linear ? VK_FILTER_LINEAR : VK_FILTER_NEAREST;
	ci.minFilter = ci.magFilter;
	ci.mipmapMode = linear ? VK_SAMPLER_MIPMAP_MODE_LINEAR : VK_SAMPLER_MIPMAP_MODE_NEAREST;
	ci.addressModeU = nrWrapToVk(wrap_u);
	ci.addressModeV = nrWrapToVk(wrap_v);
	ci.addressModeW = nrWrapToVk(wrap_w);
	ci.borderColor = VK_BORDER_COLOR_FLOAT_OPAQUE_BLACK;
	ci.minLod = 0.0f;
	ci.maxLod = (mip_levels > 0) ? (f32)mip_levels : 0.0f;

	f32 limit = nr_device.props.limits.maxSamplerAnisotropy;
	if (nr_device.features.samplerAnisotropy && max_anisotropy > 1.0f)
	{
		ci.anisotropyEnable = VK_TRUE;
		ci.maxAnisotropy = (max_anisotropy > limit) ? limit : max_anisotropy;
	}

	if (nrvk.CreateSampler(nr_device.device, &ci, NULL, out) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateImage, NRR_CODE_SAMPLER_CREATION_FAILED, 0);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateImage, NRR_CODE_SUCCESS);
}
