#pragma once

// ============================================================
// NRDescriptor.h
// 描述符集布局缓存、描述符池自动扩容、bindless 纹理表
//
// 集合布局约定（所有内置管线共用）：
//   set 0 : 每帧全局数据（camera UBO、light SSBO、环境/IBL 贴图、阴影图）
//   set 1 : 每材质数据（材质 UBO + 5 张 PBR 贴图；bindless 时退化为一个索引 UBO）
//   set 2 : 每对象数据（object UBO 动态偏移、骨骼矩阵 SSBO）
//   set 3 : bindless 纹理数组（仅在支持 descriptor indexing 时存在）
// ============================================================

#include "NRMemory.h"

SE_EXTERN_C_BEGIN

#define NR_SET_GLOBAL   0
#define NR_SET_MATERIAL 1
#define NR_SET_OBJECT   2
#define NR_SET_BINDLESS 3

#define NR_BINDLESS_CAPACITY 4096

typedef struct NRDescriptorPoolChain
{
	VkDescriptorPool* pools;
	u32 pool_count;
	u32 pool_capacity;
	u32 sets_per_pool;
	VkDescriptorPoolCreateFlags flags;
} NRDescriptorPoolChain;

typedef struct NRDescriptorSystem
{
	VkDescriptorSetLayout global_layout;
	VkDescriptorSetLayout material_layout;
	VkDescriptorSetLayout object_layout;
	VkDescriptorSetLayout postprocess_layout;
	VkDescriptorSetLayout bindless_layout;

	NRDescriptorPoolChain chain;

	VkDescriptorPool bindless_pool;
	VkDescriptorSet  bindless_set;
	u32 bindless_next;              // 下一个可用纹理槽
	b32 bindless_enabled;

	b32 initialized;
} NRDescriptorSystem;

extern NRDescriptorSystem nr_descriptors;

NRResult nrDescriptorInit(void);
void     nrDescriptorShutdown(void);

// 从池链分配；池满时自动新建池
NRResult nrDescriptorAllocate(VkDescriptorSetLayout layout, VkDescriptorSet* out);
// 释放所有池中的集合（用于场景重载）
NRResult nrDescriptorResetAll(void);

// bindless：注册一张纹理，返回着色器可用的索引；不支持时返回 UINT32_MAX
u32      nrDescriptorRegisterTexture(VkImageView view, VkSampler sampler);
void     nrDescriptorUnregisterTexture(u32 index);

// 便捷写入
void nrDescriptorWriteBuffer(VkDescriptorSet set, u32 binding, VkDescriptorType type,
							 VkBuffer buffer, u64 offset, u64 range);
void nrDescriptorWriteImage(VkDescriptorSet set, u32 binding,
							VkImageView view, VkSampler sampler, VkImageLayout layout);

SE_EXTERN_C_END
