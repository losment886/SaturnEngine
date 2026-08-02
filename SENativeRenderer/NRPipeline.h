#pragma once

// ============================================================
// NRPipeline.h
// 着色器模块、管线布局、图形/计算管线与管线缓存
// ============================================================

#include "NRDescriptor.h"

SE_EXTERN_C_BEGIN

// push constant：每次 draw 传递的小数据
typedef struct NRPushConstants
{
	NRMatrix4 model;
	u32 material_index;   // bindless 材质索引
	u32 object_flags;
	f32 time;
	f32 padding;
} NRPushConstants;

typedef struct NRShaderModule
{
	VkShaderModule module;
	u32 stage;            // NR_SHADER_STAGE_*
	char entry[64];
} NRShaderModule;

// 图形管线配置
typedef struct NRPipelineConfig
{
	VkShaderModule vertex;
	VkShaderModule fragment;
	const char* vs_entry;
	const char* fs_entry;
	VkRenderPass render_pass;
	u32 subpass;
	VkPipelineLayout layout;
	VkSampleCountFlagBits samples;

	b32 depth_test;
	b32 depth_write;
	VkCompareOp depth_compare;
	VkCullModeFlags cull_mode;
	VkFrontFace front_face;
	VkPolygonMode polygon_mode;
	f32 line_width;

	u32 blend_mode;          // NR_BLEND_*
	u32 color_attachment_count;

	b32 use_vertex_input;    // false 用于全屏三角形等无顶点缓冲的场合
	b32 depth_bias;          // 阴影渲染
} NRPipelineConfig;

typedef struct NRPipelineSystem
{
	VkPipelineCache cache;
	VkPipelineLayout main_layout;      // set0..set3 + push constants
	VkPipelineLayout postprocess_layout;
	VkPipelineLayout compute_layout;
	char cache_path[512];
	b32 initialized;
} NRPipelineSystem;

extern NRPipelineSystem nr_pipelines;

NRResult nrPipelineInit(const char* cache_directory);
void     nrPipelineShutdown(void);

// 着色器
NRResult nrShaderCreateFromSPIRV(const u32* spirv, u64 size_bytes, VkShaderModule* out);
void     nrShaderDestroy(VkShaderModule module);

// 默认配置填充
void     nrPipelineConfigDefaults(NRPipelineConfig* cfg);
// 顶点输入描述（NRVertex）
void     nrPipelineVertexInput(VkVertexInputBindingDescription* binding,
							   VkVertexInputAttributeDescription* attrs, u32* attr_count);

NRResult nrPipelineCreateGraphics(const NRPipelineConfig* cfg, VkPipeline* out);
NRResult nrPipelineCreateCompute(VkShaderModule shader, const char* entry,
								 VkPipelineLayout layout, VkPipeline* out);
void     nrPipelineDestroy(VkPipeline pipeline);

// 将管线缓存写入磁盘
NRResult nrPipelineSaveCache(void);

SE_EXTERN_C_END
