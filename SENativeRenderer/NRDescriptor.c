#include "NRDescriptor.h"

// ============================================================
// NRDescriptor.c
// ============================================================

NRDescriptorSystem nr_descriptors;

#define NR_SETS_PER_POOL 512

// ------------------------------------------------------------
// 池链
// ------------------------------------------------------------
static NRResult nrChainAddPool(NRDescriptorPoolChain* chain)
{
	VkDescriptorPoolSize sizes[5];
	memset(sizes, 0, sizeof(sizes));
	sizes[0].type = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
	sizes[0].descriptorCount = chain->sets_per_pool * 4;
	sizes[1].type = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC;
	sizes[1].descriptorCount = chain->sets_per_pool * 2;
	sizes[2].type = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;
	sizes[2].descriptorCount = chain->sets_per_pool * 2;
	sizes[3].type = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
	sizes[3].descriptorCount = chain->sets_per_pool * 8;
	sizes[4].type = VK_DESCRIPTOR_TYPE_STORAGE_IMAGE;
	sizes[4].descriptorCount = chain->sets_per_pool;

	VkDescriptorPoolCreateInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
	ci.flags = chain->flags;
	ci.maxSets = chain->sets_per_pool;
	ci.poolSizeCount = 5;
	ci.pPoolSizes = sizes;

	VkDescriptorPool pool = VK_NULL_HANDLE;
	if (nrvk.CreateDescriptorPool(nr_device.device, &ci, NULL, &pool) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDescriptor, NRR_CODE_DESCRIPTOR_SET_FAILED, 0);

	if (chain->pool_count == chain->pool_capacity)
	{
		u32 cap = (chain->pool_capacity == 0) ? 4 : chain->pool_capacity * 2;
		VkDescriptorPool* np = (VkDescriptorPool*)realloc(chain->pools, sizeof(VkDescriptorPool) * cap);
		if (np == NULL)
		{
			nrvk.DestroyDescriptorPool(nr_device.device, pool, NULL);
			return NRR_MakeFailure(NRR_STEP_NR_CreateDescriptor, NRR_CODE_OUT_OF_MEMORY, 0);
		}
		chain->pools = np;
		chain->pool_capacity = cap;
	}
	chain->pools[chain->pool_count++] = pool;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDescriptor, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 布局
// ------------------------------------------------------------
static NRResult nrCreateLayout(const VkDescriptorSetLayoutBinding* bindings, u32 count,
							   VkDescriptorSetLayout* out)
{
	VkDescriptorSetLayoutCreateInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
	ci.bindingCount = count;
	ci.pBindings = bindings;

	if (nrvk.CreateDescriptorSetLayout(nr_device.device, &ci, NULL, out) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDescriptor, NRR_CODE_DESCRIPTOR_SET_FAILED, 1);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDescriptor, NRR_CODE_SUCCESS);
}

static NRResult nrCreateGlobalLayout(void)
{
	VkDescriptorSetLayoutBinding b[6];
	memset(b, 0, sizeof(b));
	// 0: camera UBO
	b[0].binding = 0;
	b[0].descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
	b[0].descriptorCount = 1;
	b[0].stageFlags = VK_SHADER_STAGE_VERTEX_BIT | VK_SHADER_STAGE_FRAGMENT_BIT |
					  VK_SHADER_STAGE_COMPUTE_BIT;
	// 1: 光源 SSBO
	b[1].binding = 1;
	b[1].descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;
	b[1].descriptorCount = 1;
	b[1].stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT | VK_SHADER_STAGE_COMPUTE_BIT;
	// 2: IBL irradiance cubemap
	b[2].binding = 2;
	b[2].descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
	b[2].descriptorCount = 1;
	b[2].stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT;
	// 3: IBL prefiltered cubemap
	b[3].binding = 3;
	b[3].descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
	b[3].descriptorCount = 1;
	b[3].stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT;
	// 4: BRDF LUT
	b[4].binding = 4;
	b[4].descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
	b[4].descriptorCount = 1;
	b[4].stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT;
	// 5: 阴影图
	b[5].binding = 5;
	b[5].descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
	b[5].descriptorCount = 1;
	b[5].stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT;

	return nrCreateLayout(b, 6, &nr_descriptors.global_layout);
}

// 后处理（bloom 提取/模糊/合成）统一使用：set0 binding0/1 为采样图
static NRResult nrCreatePostProcessLayout(void)
{
	VkDescriptorSetLayoutBinding b[2];
	memset(b, 0, sizeof(b));
	for (u32 i = 0; i < 2; i++)
	{
		b[i].binding = i;
		b[i].descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
		b[i].descriptorCount = 1;
		b[i].stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT;
	}
	return nrCreateLayout(b, 2, &nr_descriptors.postprocess_layout);
}

static NRResult nrCreateMaterialLayout(void)
{
	VkDescriptorSetLayoutBinding b[6];
	memset(b, 0, sizeof(b));
	// 0: 材质参数 UBO
	b[0].binding = 0;
	b[0].descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
	b[0].descriptorCount = 1;
	b[0].stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT;
	// 1..5: baseColor / metallicRoughness / normal / occlusion / emissive
	for (u32 i = 1; i < 6; i++)
	{
		b[i].binding = i;
		b[i].descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
		b[i].descriptorCount = 1;
		b[i].stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT;
	}
	return nrCreateLayout(b, 6, &nr_descriptors.material_layout);
}

static NRResult nrCreateObjectLayout(void)
{
	VkDescriptorSetLayoutBinding b[2];
	memset(b, 0, sizeof(b));
	// 0: 对象变换 UBO（动态偏移，一个大缓冲服务全部对象）
	b[0].binding = 0;
	b[0].descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC;
	b[0].descriptorCount = 1;
	b[0].stageFlags = VK_SHADER_STAGE_VERTEX_BIT;
	// 1: 骨骼矩阵 SSBO
	b[1].binding = 1;
	b[1].descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;
	b[1].descriptorCount = 1;
	b[1].stageFlags = VK_SHADER_STAGE_VERTEX_BIT;

	return nrCreateLayout(b, 2, &nr_descriptors.object_layout);
}

static NRResult nrCreateBindlessLayout(void)
{
	VkDescriptorSetLayoutBinding b;
	memset(&b, 0, sizeof(b));
	b.binding = 0;
	b.descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
	b.descriptorCount = NR_BINDLESS_CAPACITY;
	b.stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT;

	VkDescriptorBindingFlagsEXT flags =
		VK_DESCRIPTOR_BINDING_PARTIALLY_BOUND_BIT_EXT |
		VK_DESCRIPTOR_BINDING_UPDATE_AFTER_BIND_BIT_EXT |
		VK_DESCRIPTOR_BINDING_UPDATE_UNUSED_WHILE_PENDING_BIT_EXT;

	VkDescriptorSetLayoutBindingFlagsCreateInfoEXT bf;
	memset(&bf, 0, sizeof(bf));
	bf.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_BINDING_FLAGS_CREATE_INFO_EXT;
	bf.bindingCount = 1;
	bf.pBindingFlags = &flags;

	VkDescriptorSetLayoutCreateInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
	ci.flags = VK_DESCRIPTOR_SET_LAYOUT_CREATE_UPDATE_AFTER_BIND_POOL_BIT_EXT;
	ci.bindingCount = 1;
	ci.pBindings = &b;
	ci.pNext = &bf;

	if (nrvk.CreateDescriptorSetLayout(nr_device.device, &ci, NULL,
									   &nr_descriptors.bindless_layout) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDescriptor, NRR_CODE_DESCRIPTOR_SET_FAILED, 2);

	// 专用池
	VkDescriptorPoolSize ps;
	ps.type = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
	ps.descriptorCount = NR_BINDLESS_CAPACITY;

	VkDescriptorPoolCreateInfo pci;
	memset(&pci, 0, sizeof(pci));
	pci.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
	pci.flags = VK_DESCRIPTOR_POOL_CREATE_UPDATE_AFTER_BIND_BIT_EXT;
	pci.maxSets = 1;
	pci.poolSizeCount = 1;
	pci.pPoolSizes = &ps;

	if (nrvk.CreateDescriptorPool(nr_device.device, &pci, NULL,
								  &nr_descriptors.bindless_pool) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDescriptor, NRR_CODE_DESCRIPTOR_SET_FAILED, 3);

	VkDescriptorSetAllocateInfo ai;
	memset(&ai, 0, sizeof(ai));
	ai.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO;
	ai.descriptorPool = nr_descriptors.bindless_pool;
	ai.descriptorSetCount = 1;
	ai.pSetLayouts = &nr_descriptors.bindless_layout;

	if (nrvk.AllocateDescriptorSets(nr_device.device, &ai, &nr_descriptors.bindless_set) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDescriptor, NRR_CODE_DESCRIPTOR_SET_FAILED, 4);

	nr_descriptors.bindless_enabled = TRUE;
	nr_descriptors.bindless_next = 0;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDescriptor, NRR_CODE_SUCCESS);
}

NRResult nrDescriptorInit(void)
{
	if (nr_descriptors.initialized)
		return NRR_MakeSuccess(NRR_STEP_NR_CreateDescriptor, NRR_CODE_SUCCESS);
	if (!nr_device.initialized)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDescriptor, NRR_CODE_NOT_INITIALIZED, 0);

	memset(&nr_descriptors, 0, sizeof(nr_descriptors));
	nr_descriptors.chain.sets_per_pool = NR_SETS_PER_POOL;
	nr_descriptors.chain.flags = VK_DESCRIPTOR_POOL_CREATE_FREE_DESCRIPTOR_SET_BIT;

	NRResult r = nrChainAddPool(&nr_descriptors.chain);
	if (NRR_FAILED(r)) return r;

	r = nrCreateGlobalLayout();   if (NRR_FAILED(r)) return r;
	r = nrCreateMaterialLayout(); if (NRR_FAILED(r)) return r;
	r = nrCreateObjectLayout();   if (NRR_FAILED(r)) return r;
	r = nrCreatePostProcessLayout(); if (NRR_FAILED(r)) return r;

	// bindless 是可选优化，失败时静默回退到每材质一组描述符
	if ((nr_device.enabled_features & NR_FEATURE_DESCRIPTOR_INDEXING) &&
		nr_device.update_after_bind)
	{
		NRResult br = nrCreateBindlessLayout();
		if (NRR_FAILED(br))
		{
			nr_descriptors.bindless_enabled = FALSE;
			nr_descriptors.bindless_layout = VK_NULL_HANDLE;
		}
	}

	nr_descriptors.initialized = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDescriptor, NRR_CODE_SUCCESS);
}

void nrDescriptorShutdown(void)
{
	if (nr_device.device == VK_NULL_HANDLE)
	{
		memset(&nr_descriptors, 0, sizeof(nr_descriptors));
		return;
	}

	for (u32 i = 0; i < nr_descriptors.chain.pool_count; i++)
		nrvk.DestroyDescriptorPool(nr_device.device, nr_descriptors.chain.pools[i], NULL);
	free(nr_descriptors.chain.pools);

	if (nr_descriptors.bindless_pool != VK_NULL_HANDLE)
		nrvk.DestroyDescriptorPool(nr_device.device, nr_descriptors.bindless_pool, NULL);
	if (nr_descriptors.bindless_layout != VK_NULL_HANDLE)
		nrvk.DestroyDescriptorSetLayout(nr_device.device, nr_descriptors.bindless_layout, NULL);
	if (nr_descriptors.object_layout != VK_NULL_HANDLE)
		nrvk.DestroyDescriptorSetLayout(nr_device.device, nr_descriptors.object_layout, NULL);
	if (nr_descriptors.postprocess_layout != VK_NULL_HANDLE)
		nrvk.DestroyDescriptorSetLayout(nr_device.device, nr_descriptors.postprocess_layout, NULL);
	if (nr_descriptors.material_layout != VK_NULL_HANDLE)
		nrvk.DestroyDescriptorSetLayout(nr_device.device, nr_descriptors.material_layout, NULL);
	if (nr_descriptors.global_layout != VK_NULL_HANDLE)
		nrvk.DestroyDescriptorSetLayout(nr_device.device, nr_descriptors.global_layout, NULL);

	memset(&nr_descriptors, 0, sizeof(nr_descriptors));
}

NRResult nrDescriptorAllocate(VkDescriptorSetLayout layout, VkDescriptorSet* out)
{
	if (layout == VK_NULL_HANDLE || out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDescriptor, NRR_CODE_INVALID_PARAMETER, 0);

	NRDescriptorPoolChain* chain = &nr_descriptors.chain;

	for (u32 attempt = 0; attempt < 2; attempt++)
	{
		VkDescriptorPool pool = chain->pools[chain->pool_count - 1];

		VkDescriptorSetAllocateInfo ai;
		memset(&ai, 0, sizeof(ai));
		ai.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO;
		ai.descriptorPool = pool;
		ai.descriptorSetCount = 1;
		ai.pSetLayouts = &layout;

		VkResult vr = nrvk.AllocateDescriptorSets(nr_device.device, &ai, out);
		if (vr == VK_SUCCESS)
			return NRR_MakeSuccess(NRR_STEP_NR_CreateDescriptor, NRR_CODE_SUCCESS);

		if (vr != VK_ERROR_OUT_OF_POOL_MEMORY && vr != VK_ERROR_FRAGMENTED_POOL)
			return NRR_MakeFailure(NRR_STEP_NR_CreateDescriptor, NRR_CODE_DESCRIPTOR_SET_FAILED, (u32)vr);

		// 池满：新建一个再试
		NRResult r = nrChainAddPool(chain);
		if (NRR_FAILED(r)) return r;
	}

	return NRR_MakeFailure(NRR_STEP_NR_CreateDescriptor, NRR_CODE_DESCRIPTOR_SET_FAILED, 99);
}

NRResult nrDescriptorResetAll(void)
{
	for (u32 i = 0; i < nr_descriptors.chain.pool_count; i++)
		nrvk.ResetDescriptorPool(nr_device.device, nr_descriptors.chain.pools[i], 0);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDescriptor, NRR_CODE_SUCCESS);
}

u32 nrDescriptorRegisterTexture(VkImageView view, VkSampler sampler)
{
	if (!nr_descriptors.bindless_enabled) return 0xFFFFFFFFu;
	if (nr_descriptors.bindless_next >= NR_BINDLESS_CAPACITY) return 0xFFFFFFFFu;
	if (view == VK_NULL_HANDLE || sampler == VK_NULL_HANDLE) return 0xFFFFFFFFu;

	u32 index = nr_descriptors.bindless_next++;

	VkDescriptorImageInfo ii;
	ii.imageView = view;
	ii.sampler = sampler;
	ii.imageLayout = VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;

	VkWriteDescriptorSet w;
	memset(&w, 0, sizeof(w));
	w.sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
	w.dstSet = nr_descriptors.bindless_set;
	w.dstBinding = 0;
	w.dstArrayElement = index;
	w.descriptorCount = 1;
	w.descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
	w.pImageInfo = &ii;

	nrvk.UpdateDescriptorSets(nr_device.device, 1, &w, 0, NULL);
	return index;
}

void nrDescriptorUnregisterTexture(u32 index)
{
	// 简化策略：槽位不回收，纹理销毁后该槽位保持 partially-bound 未使用状态。
	// 着色器永远不会引用已释放材质的索引，因此安全。
	(void)index;
}

void nrDescriptorWriteBuffer(VkDescriptorSet set, u32 binding, VkDescriptorType type,
							 VkBuffer buffer, u64 offset, u64 range)
{
	if (set == VK_NULL_HANDLE || buffer == VK_NULL_HANDLE) return;

	VkDescriptorBufferInfo bi;
	bi.buffer = buffer;
	bi.offset = offset;
	bi.range = range;

	VkWriteDescriptorSet w;
	memset(&w, 0, sizeof(w));
	w.sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
	w.dstSet = set;
	w.dstBinding = binding;
	w.descriptorCount = 1;
	w.descriptorType = type;
	w.pBufferInfo = &bi;

	nrvk.UpdateDescriptorSets(nr_device.device, 1, &w, 0, NULL);
}

void nrDescriptorWriteImage(VkDescriptorSet set, u32 binding,
							VkImageView view, VkSampler sampler, VkImageLayout layout)
{
	if (set == VK_NULL_HANDLE || view == VK_NULL_HANDLE) return;

	VkDescriptorImageInfo ii;
	ii.imageView = view;
	ii.sampler = sampler;
	ii.imageLayout = layout;

	VkWriteDescriptorSet w;
	memset(&w, 0, sizeof(w));
	w.sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
	w.dstSet = set;
	w.dstBinding = binding;
	w.descriptorCount = 1;
	w.descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
	w.pImageInfo = &ii;

	nrvk.UpdateDescriptorSets(nr_device.device, 1, &w, 0, NULL);
}
