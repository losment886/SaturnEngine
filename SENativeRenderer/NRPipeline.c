#include "NRPipeline.h"

// ============================================================
// NRPipeline.c
// ============================================================

NRPipelineSystem nr_pipelines;

// ------------------------------------------------------------
// 管线缓存持久化
// ------------------------------------------------------------
static void nrLoadCacheData(const char* path, void** out_data, size_t* out_size)
{
	*out_data = NULL;
	*out_size = 0;

	SDL_IOStream* io = SDL_IOFromFile(path, "rb");
	if (io == NULL) return;

	Sint64 size = SDL_GetIOSize(io);
	if (size <= 0) { SDL_CloseIO(io); return; }

	void* buf = malloc((size_t)size);
	if (buf == NULL) { SDL_CloseIO(io); return; }

	size_t read = SDL_ReadIO(io, buf, (size_t)size);
	SDL_CloseIO(io);

	if (read != (size_t)size) { free(buf); return; }
	*out_data = buf;
	*out_size = (size_t)size;
}

NRResult nrPipelineInit(const char* cache_directory)
{
	if (nr_pipelines.initialized)
		return NRR_MakeSuccess(NRR_STEP_NR_CreatePipeline, NRR_CODE_SUCCESS);
	if (!nr_device.initialized)
		return NRR_MakeFailure(NRR_STEP_NR_CreatePipeline, NRR_CODE_NOT_INITIALIZED, 0);
	if (!nr_descriptors.initialized)
		return NRR_MakeFailure(NRR_STEP_NR_CreatePipeline, NRR_CODE_NOT_INITIALIZED, 1);

	memset(&nr_pipelines, 0, sizeof(nr_pipelines));

	// 缓存路径：优先调用方目录，否则用平台的应用可写目录
	if (cache_directory != NULL && cache_directory[0] != '\0')
	{
		SDL_snprintf(nr_pipelines.cache_path, sizeof(nr_pipelines.cache_path),
					 "%s%spipeline.cache", cache_directory,
					 (cache_directory[strlen(cache_directory) - 1] == '/' ||
					  cache_directory[strlen(cache_directory) - 1] == '\\') ? "" : "/");
	}
	else
	{
		const char* pref = SDL_GetPrefPath("SaturnEngine", "SENativeRenderer");
		if (pref != NULL)
		{
			SDL_snprintf(nr_pipelines.cache_path, sizeof(nr_pipelines.cache_path),
						 "%spipeline.cache", pref);
		}
	}

	void* cacheData = NULL;
	size_t cacheSize = 0;
	if (nr_pipelines.cache_path[0] != '\0')
		nrLoadCacheData(nr_pipelines.cache_path, &cacheData, &cacheSize);

	VkPipelineCacheCreateInfo cci;
	memset(&cci, 0, sizeof(cci));
	cci.sType = VK_STRUCTURE_TYPE_PIPELINE_CACHE_CREATE_INFO;
	cci.initialDataSize = cacheSize;
	cci.pInitialData = cacheData;

	VkResult vr = nrvk.CreatePipelineCache(nr_device.device, &cci, NULL, &nr_pipelines.cache);
	if (vr != VK_SUCCESS && cacheData != NULL)
	{
		// 缓存数据可能来自其他驱动版本，丢弃后重试
		cci.initialDataSize = 0;
		cci.pInitialData = NULL;
		vr = nrvk.CreatePipelineCache(nr_device.device, &cci, NULL, &nr_pipelines.cache);
	}
	free(cacheData);
	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreatePipeline, NRR_CODE_PIPELINE_CREATION_FAILED, (u32)vr);

	// ---- 主管线布局 ----
	VkDescriptorSetLayout sets[4];
	u32 setCount = 0;
	sets[setCount++] = nr_descriptors.global_layout;
	sets[setCount++] = nr_descriptors.material_layout;
	sets[setCount++] = nr_descriptors.object_layout;
	if (nr_descriptors.bindless_enabled)
		sets[setCount++] = nr_descriptors.bindless_layout;

	VkPushConstantRange pcr;
	pcr.stageFlags = VK_SHADER_STAGE_VERTEX_BIT | VK_SHADER_STAGE_FRAGMENT_BIT;
	pcr.offset = 0;
	pcr.size = sizeof(NRPushConstants);
	if (pcr.size > nr_device.props.limits.maxPushConstantsSize)
		pcr.size = nr_device.props.limits.maxPushConstantsSize;

	VkPipelineLayoutCreateInfo lci;
	memset(&lci, 0, sizeof(lci));
	lci.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
	lci.setLayoutCount = setCount;
	lci.pSetLayouts = sets;
	lci.pushConstantRangeCount = 1;
	lci.pPushConstantRanges = &pcr;

	if (nrvk.CreatePipelineLayout(nr_device.device, &lci, NULL, &nr_pipelines.main_layout) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreatePipeline, NRR_CODE_PIPELINE_CREATION_FAILED, 1);

	// ---- 后处理布局：仅后处理采样图 set + push constants ----
	VkPipelineLayoutCreateInfo pci;
	memset(&pci, 0, sizeof(pci));
	pci.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
	pci.setLayoutCount = 1;
	pci.pSetLayouts = &nr_descriptors.postprocess_layout;
	pci.pushConstantRangeCount = 1;
	pci.pPushConstantRanges = &pcr;
	if (nrvk.CreatePipelineLayout(nr_device.device, &pci, NULL,
								  &nr_pipelines.postprocess_layout) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreatePipeline, NRR_CODE_PIPELINE_CREATION_FAILED, 2);

	// ---- 计算布局 ----
	VkPipelineLayoutCreateInfo cmpci;
	memset(&cmpci, 0, sizeof(cmpci));
	cmpci.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
	cmpci.setLayoutCount = 1;
	cmpci.pSetLayouts = &nr_descriptors.global_layout;
	VkPushConstantRange cpcr = pcr;
	cpcr.stageFlags = VK_SHADER_STAGE_COMPUTE_BIT;
	cmpci.pushConstantRangeCount = 1;
	cmpci.pPushConstantRanges = &cpcr;
	if (nrvk.CreatePipelineLayout(nr_device.device, &cmpci, NULL,
								  &nr_pipelines.compute_layout) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreatePipeline, NRR_CODE_PIPELINE_CREATION_FAILED, 3);

	nr_pipelines.initialized = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_CreatePipeline, NRR_CODE_SUCCESS);
}

NRResult nrPipelineSaveCache(void)
{
	if (nr_pipelines.cache == VK_NULL_HANDLE || nr_pipelines.cache_path[0] == '\0')
		return NRR_MakeSuccess(NRR_STEP_NR_CreatePipeline, NRR_CODE_SUCCESS);

	size_t size = 0;
	if (nrvk.GetPipelineCacheData(nr_device.device, nr_pipelines.cache, &size, NULL) != VK_SUCCESS ||
		size == 0)
		return NRR_MakeSuccess(NRR_STEP_NR_CreatePipeline, NRR_CODE_SUCCESS);

	void* data = malloc(size);
	if (data == NULL)
		return NRR_MakeWarning(NRR_STEP_NR_CreatePipeline, NRR_CODE_OUT_OF_MEMORY, 0);

	if (nrvk.GetPipelineCacheData(nr_device.device, nr_pipelines.cache, &size, data) == VK_SUCCESS)
	{
		SDL_IOStream* io = SDL_IOFromFile(nr_pipelines.cache_path, "wb");
		if (io != NULL)
		{
			SDL_WriteIO(io, data, size);
			SDL_CloseIO(io);
		}
	}
	free(data);
	return NRR_MakeSuccess(NRR_STEP_NR_CreatePipeline, NRR_CODE_SUCCESS);
}

void nrPipelineShutdown(void)
{
	if (nr_device.device == VK_NULL_HANDLE)
	{
		memset(&nr_pipelines, 0, sizeof(nr_pipelines));
		return;
	}

	nrPipelineSaveCache();

	if (nr_pipelines.compute_layout != VK_NULL_HANDLE)
		nrvk.DestroyPipelineLayout(nr_device.device, nr_pipelines.compute_layout, NULL);
	if (nr_pipelines.postprocess_layout != VK_NULL_HANDLE)
		nrvk.DestroyPipelineLayout(nr_device.device, nr_pipelines.postprocess_layout, NULL);
	if (nr_pipelines.main_layout != VK_NULL_HANDLE)
		nrvk.DestroyPipelineLayout(nr_device.device, nr_pipelines.main_layout, NULL);
	if (nr_pipelines.cache != VK_NULL_HANDLE)
		nrvk.DestroyPipelineCache(nr_device.device, nr_pipelines.cache, NULL);

	memset(&nr_pipelines, 0, sizeof(nr_pipelines));
}

// ------------------------------------------------------------
// 着色器
// ------------------------------------------------------------
NRResult nrShaderCreateFromSPIRV(const u32* spirv, u64 size_bytes, VkShaderModule* out)
{
	if (spirv == NULL || size_bytes == 0 || out == NULL || (size_bytes % 4) != 0)
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_INVALID_PARAMETER, 0);

	VkShaderModuleCreateInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO;
	ci.codeSize = (size_t)size_bytes;
	ci.pCode = spirv;

	VkResult vr = nrvk.CreateShaderModule(nr_device.device, &ci, NULL, out);
	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule,
							   NRR_CODE_SHADER_COMPILATION_FAILED, (u32)vr);
	return NRR_MakeSuccess(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SUCCESS);
}

void nrShaderDestroy(VkShaderModule module)
{
	if (module != VK_NULL_HANDLE && nr_device.device != VK_NULL_HANDLE)
		nrvk.DestroyShaderModule(nr_device.device, module, NULL);
}

// ------------------------------------------------------------
// 顶点输入
// ------------------------------------------------------------
void nrPipelineVertexInput(VkVertexInputBindingDescription* binding,
						   VkVertexInputAttributeDescription* attrs, u32* attr_count)
{
	binding->binding = 0;
	binding->stride = sizeof(NRVertex);
	binding->inputRate = VK_VERTEX_INPUT_RATE_VERTEX;

	u32 i = 0;
	attrs[i].location = 0; attrs[i].binding = 0;
	attrs[i].format = VK_FORMAT_R32G32B32_SFLOAT;
	attrs[i].offset = (u32)offsetof(NRVertex, position); i++;

	attrs[i].location = 1; attrs[i].binding = 0;
	attrs[i].format = VK_FORMAT_R32G32B32_SFLOAT;
	attrs[i].offset = (u32)offsetof(NRVertex, normal); i++;

	attrs[i].location = 2; attrs[i].binding = 0;
	attrs[i].format = VK_FORMAT_R32G32B32A32_SFLOAT;
	attrs[i].offset = (u32)offsetof(NRVertex, tangent); i++;

	attrs[i].location = 3; attrs[i].binding = 0;
	attrs[i].format = VK_FORMAT_R32G32_SFLOAT;
	attrs[i].offset = (u32)offsetof(NRVertex, uv0); i++;

	attrs[i].location = 4; attrs[i].binding = 0;
	attrs[i].format = VK_FORMAT_R32G32_SFLOAT;
	attrs[i].offset = (u32)offsetof(NRVertex, uv1); i++;

	attrs[i].location = 5; attrs[i].binding = 0;
	attrs[i].format = VK_FORMAT_R32G32B32A32_SFLOAT;
	attrs[i].offset = (u32)offsetof(NRVertex, color); i++;

	attrs[i].location = 6; attrs[i].binding = 0;
	attrs[i].format = VK_FORMAT_R32G32B32A32_UINT;
	attrs[i].offset = (u32)offsetof(NRVertex, joints); i++;

	attrs[i].location = 7; attrs[i].binding = 0;
	attrs[i].format = VK_FORMAT_R32G32B32A32_SFLOAT;
	attrs[i].offset = (u32)offsetof(NRVertex, weights); i++;

	*attr_count = i;
}

void nrPipelineConfigDefaults(NRPipelineConfig* cfg)
{
	if (cfg == NULL) return;
	memset(cfg, 0, sizeof(NRPipelineConfig));
	cfg->vs_entry = "main";
	cfg->fs_entry = "main";
	cfg->subpass = 0;
	cfg->samples = VK_SAMPLE_COUNT_1_BIT;
	cfg->depth_test = TRUE;
	cfg->depth_write = TRUE;
	cfg->depth_compare = VK_COMPARE_OP_LESS_OR_EQUAL;
	cfg->cull_mode = VK_CULL_MODE_BACK_BIT;
	cfg->front_face = VK_FRONT_FACE_COUNTER_CLOCKWISE;
	cfg->polygon_mode = VK_POLYGON_MODE_FILL;
	cfg->line_width = 1.0f;
	cfg->blend_mode = NR_BLEND_OPAQUE;
	cfg->color_attachment_count = 1;
	cfg->use_vertex_input = TRUE;
	cfg->layout = nr_pipelines.main_layout;
}

// ------------------------------------------------------------
// 混合状态
// ------------------------------------------------------------
static void nrFillBlendState(u32 mode, VkPipelineColorBlendAttachmentState* out)
{
	memset(out, 0, sizeof(*out));
	out->colorWriteMask = VK_COLOR_COMPONENT_R_BIT | VK_COLOR_COMPONENT_G_BIT |
						  VK_COLOR_COMPONENT_B_BIT | VK_COLOR_COMPONENT_A_BIT;

	switch (mode)
	{
		case NR_BLEND_ALPHA:
			out->blendEnable = VK_TRUE;
			out->srcColorBlendFactor = VK_BLEND_FACTOR_SRC_ALPHA;
			out->dstColorBlendFactor = VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA;
			out->colorBlendOp = VK_BLEND_OP_ADD;
			out->srcAlphaBlendFactor = VK_BLEND_FACTOR_ONE;
			out->dstAlphaBlendFactor = VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA;
			out->alphaBlendOp = VK_BLEND_OP_ADD;
			break;
		case NR_BLEND_ADD:
			out->blendEnable = VK_TRUE;
			out->srcColorBlendFactor = VK_BLEND_FACTOR_SRC_ALPHA;
			out->dstColorBlendFactor = VK_BLEND_FACTOR_ONE;
			out->colorBlendOp = VK_BLEND_OP_ADD;
			out->srcAlphaBlendFactor = VK_BLEND_FACTOR_ZERO;
			out->dstAlphaBlendFactor = VK_BLEND_FACTOR_ONE;
			out->alphaBlendOp = VK_BLEND_OP_ADD;
			break;
		case NR_BLEND_MULTIPLY:
			out->blendEnable = VK_TRUE;
			out->srcColorBlendFactor = VK_BLEND_FACTOR_DST_COLOR;
			out->dstColorBlendFactor = VK_BLEND_FACTOR_ZERO;
			out->colorBlendOp = VK_BLEND_OP_ADD;
			out->srcAlphaBlendFactor = VK_BLEND_FACTOR_DST_ALPHA;
			out->dstAlphaBlendFactor = VK_BLEND_FACTOR_ZERO;
			out->alphaBlendOp = VK_BLEND_OP_ADD;
			break;
		default: // OPAQUE / MASK
			out->blendEnable = VK_FALSE;
			break;
	}
}

NRResult nrPipelineCreateGraphics(const NRPipelineConfig* cfg, VkPipeline* out)
{
	if (cfg == NULL || out == NULL || cfg->vertex == VK_NULL_HANDLE)
		return NRR_MakeFailure(NRR_STEP_NR_CreatePipeline, NRR_CODE_INVALID_PARAMETER, 0);

	VkPipelineShaderStageCreateInfo stages[2];
	memset(stages, 0, sizeof(stages));
	u32 stageCount = 0;

	stages[stageCount].sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
	stages[stageCount].stage = VK_SHADER_STAGE_VERTEX_BIT;
	stages[stageCount].module = cfg->vertex;
	stages[stageCount].pName = (cfg->vs_entry != NULL) ? cfg->vs_entry : "main";
	stageCount++;

	if (cfg->fragment != VK_NULL_HANDLE)
	{
		stages[stageCount].sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
		stages[stageCount].stage = VK_SHADER_STAGE_FRAGMENT_BIT;
		stages[stageCount].module = cfg->fragment;
		stages[stageCount].pName = (cfg->fs_entry != NULL) ? cfg->fs_entry : "main";
		stageCount++;
	}

	VkVertexInputBindingDescription binding;
	VkVertexInputAttributeDescription attrs[8];
	u32 attrCount = 0;
	nrPipelineVertexInput(&binding, attrs, &attrCount);

	VkPipelineVertexInputStateCreateInfo vi;
	memset(&vi, 0, sizeof(vi));
	vi.sType = VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO;
	if (cfg->use_vertex_input)
	{
		vi.vertexBindingDescriptionCount = 1;
		vi.pVertexBindingDescriptions = &binding;
		vi.vertexAttributeDescriptionCount = attrCount;
		vi.pVertexAttributeDescriptions = attrs;
	}

	VkPipelineInputAssemblyStateCreateInfo ia;
	memset(&ia, 0, sizeof(ia));
	ia.sType = VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO;
	ia.topology = VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;

	// viewport/scissor 全部走动态状态，避免窗口尺寸变化时重建管线
	VkPipelineViewportStateCreateInfo vp;
	memset(&vp, 0, sizeof(vp));
	vp.sType = VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO;
	vp.viewportCount = 1;
	vp.scissorCount = 1;

	VkPipelineRasterizationStateCreateInfo rs;
	memset(&rs, 0, sizeof(rs));
	rs.sType = VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO;
	rs.polygonMode = cfg->polygon_mode;
	rs.cullMode = cfg->cull_mode;
	rs.frontFace = cfg->front_face;
	rs.lineWidth = (cfg->line_width > 0.0f) ? cfg->line_width : 1.0f;
	rs.depthClampEnable = VK_FALSE;
	rs.rasterizerDiscardEnable = VK_FALSE;
	if (cfg->depth_bias)
	{
		rs.depthBiasEnable = VK_TRUE;
		rs.depthBiasConstantFactor = 1.25f;
		rs.depthBiasSlopeFactor = 1.75f;
	}

	VkPipelineMultisampleStateCreateInfo ms;
	memset(&ms, 0, sizeof(ms));
	ms.sType = VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO;
	ms.rasterizationSamples = (cfg->samples != 0) ? cfg->samples : VK_SAMPLE_COUNT_1_BIT;
	if (nr_device.features.sampleRateShading && ms.rasterizationSamples != VK_SAMPLE_COUNT_1_BIT)
	{
		ms.sampleShadingEnable = VK_TRUE;
		ms.minSampleShading = 0.25f;
	}

	VkPipelineDepthStencilStateCreateInfo ds;
	memset(&ds, 0, sizeof(ds));
	ds.sType = VK_STRUCTURE_TYPE_PIPELINE_DEPTH_STENCIL_STATE_CREATE_INFO;
	ds.depthTestEnable = cfg->depth_test ? VK_TRUE : VK_FALSE;
	ds.depthWriteEnable = cfg->depth_write ? VK_TRUE : VK_FALSE;
	ds.depthCompareOp = cfg->depth_compare;
	ds.maxDepthBounds = 1.0f;

	VkPipelineColorBlendAttachmentState blends[8];
	u32 blendCount = (cfg->color_attachment_count > 0 && cfg->color_attachment_count <= 8)
				   ? cfg->color_attachment_count : 1;
	for (u32 i = 0; i < blendCount; i++)
		nrFillBlendState(cfg->blend_mode, &blends[i]);

	VkPipelineColorBlendStateCreateInfo cb;
	memset(&cb, 0, sizeof(cb));
	cb.sType = VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO;
	cb.attachmentCount = blendCount;
	cb.pAttachments = blends;

	VkDynamicState dyn[3];
	u32 dynCount = 0;
	dyn[dynCount++] = VK_DYNAMIC_STATE_VIEWPORT;
	dyn[dynCount++] = VK_DYNAMIC_STATE_SCISSOR;
	if (cfg->depth_bias) dyn[dynCount++] = VK_DYNAMIC_STATE_DEPTH_BIAS;

	VkPipelineDynamicStateCreateInfo dsi;
	memset(&dsi, 0, sizeof(dsi));
	dsi.sType = VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO;
	dsi.dynamicStateCount = dynCount;
	dsi.pDynamicStates = dyn;

	VkGraphicsPipelineCreateInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO;
	ci.stageCount = stageCount;
	ci.pStages = stages;
	ci.pVertexInputState = &vi;
	ci.pInputAssemblyState = &ia;
	ci.pViewportState = &vp;
	ci.pRasterizationState = &rs;
	ci.pMultisampleState = &ms;
	ci.pDepthStencilState = &ds;
	ci.pColorBlendState = &cb;
	ci.pDynamicState = &dsi;
	ci.layout = (cfg->layout != VK_NULL_HANDLE) ? cfg->layout : nr_pipelines.main_layout;
	ci.renderPass = cfg->render_pass;
	ci.subpass = cfg->subpass;

	VkResult vr = nrvk.CreateGraphicsPipelines(nr_device.device, nr_pipelines.cache, 1, &ci, NULL, out);
	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreatePipeline, NRR_CODE_PIPELINE_CREATION_FAILED, (u32)vr);
	return NRR_MakeSuccess(NRR_STEP_NR_CreatePipeline, NRR_CODE_SUCCESS);
}

NRResult nrPipelineCreateCompute(VkShaderModule shader, const char* entry,
								 VkPipelineLayout layout, VkPipeline* out)
{
	if (shader == VK_NULL_HANDLE || out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreatePipeline, NRR_CODE_INVALID_PARAMETER, 0);

	VkComputePipelineCreateInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_COMPUTE_PIPELINE_CREATE_INFO;
	ci.stage.sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
	ci.stage.stage = VK_SHADER_STAGE_COMPUTE_BIT;
	ci.stage.module = shader;
	ci.stage.pName = (entry != NULL) ? entry : "main";
	ci.layout = (layout != VK_NULL_HANDLE) ? layout : nr_pipelines.compute_layout;

	VkResult vr = nrvk.CreateComputePipelines(nr_device.device, nr_pipelines.cache, 1, &ci, NULL, out);
	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreatePipeline, NRR_CODE_PIPELINE_CREATION_FAILED, (u32)vr);
	return NRR_MakeSuccess(NRR_STEP_NR_CreatePipeline, NRR_CODE_SUCCESS);
}

void nrPipelineDestroy(VkPipeline pipeline)
{
	if (pipeline != VK_NULL_HANDLE && nr_device.device != VK_NULL_HANDLE)
		nrvk.DestroyPipeline(nr_device.device, pipeline, NULL);
}
