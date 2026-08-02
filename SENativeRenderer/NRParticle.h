#pragma once

// ============================================================
// NRParticle.h
// GPU 粒子系统：计算着色器模拟 + billboard/网格实例化渲染
//
// 每个发射器持有一个粒子 SSBO，CPU 只负责发射新粒子（写入死亡槽位），
// 模拟（重力/阻力/生命周期）全部在计算着色器内完成，
// 渲染时用 gl_InstanceIndex 直接索引同一 SSBO，避免回读。
// ============================================================

#include "NRScene.h"
#include "NRPipeline.h"

SE_EXTERN_C_BEGIN

#define NR_MAX_EMITTERS  256
#define NR_PARTICLE_GROUP_SIZE 256

// 粒子 GPU 布局（须与 GLSL NRParticle 一致，48 字节）
typedef struct NRParticleGPU
{
	NRFloat4 position_size;   // xyz=位置 w=尺寸
	NRFloat4 color;
	NRFloat4 velocity_life;   // xyz=速度 w=剩余生命
} NRParticleGPU;

// 模拟 push constant（与 GLSL SimBlock 一致）
typedef struct NRParticleSimPush
{
	NRFloat4 gravity_dt;   // xyz=重力 w=deltaTime
	u32 count;
	f32 drag;
} NRParticleSimPush;

typedef struct NREmitter
{
	NRParticleEmitterDesc desc;

	NRBuffer buffer;             // 粒子 SSBO
	NRParticleGPU* cpu;          // CPU 镜像，用于发射新粒子
	u32 capacity;

	VkDescriptorSet sim_set;     // 计算着色器用（binding 0 = SSBO）
	VkDescriptorSet render_set;  // 渲染用（set 2 binding 0 = SSBO）
	VkDescriptorSet tex_set;     // set 1 binding 1 = 粒子贴图

	f32 emit_accumulator;        // 不足 1 个粒子的发射量累计
	u32 alive_count;
	u32 rng_state;

	b32 alive;
	u32 generation;
} NREmitter;

typedef struct NRParticleSystem
{
	NREmitter* emitters;

	VkPipeline sim_pipeline;
	VkPipeline render_pipeline_alpha;
	VkPipeline render_pipeline_add;

	// 粒子专用布局：通用 object/compute 布局的绑定类型与粒子着色器不兼容
	VkDescriptorSetLayout sim_layout;      // set0 binding0 = 粒子 SSBO（计算）
	VkDescriptorSetLayout tex_layout;      // set1 binding1 = 粒子贴图
	VkDescriptorSetLayout ssbo_layout;     // set2 binding0 = 粒子 SSBO（顶点）

	VkPipelineLayout sim_pipeline_layout;
	VkPipelineLayout render_pipeline_layout;

	b32 initialized;
} NRParticleSystem;

extern NRParticleSystem nr_particles;

NRResult nrParticleInit(VkRenderPass scene_pass);
void     nrParticleShutdown(void);

NRResult nrEmitterCreate(const NRParticleEmitterDesc* desc, NREmitterHandle* out);
void     nrEmitterDestroy(NREmitterHandle handle);
NREmitter* nrEmitterResolve(NREmitterHandle handle);

NRResult nrEmitterUpdate(NREmitterHandle handle, const NRParticleEmitterDesc* desc);
NRResult nrEmitterSetPosition(NREmitterHandle handle, NRFloat3 position);

// CPU 侧发射新粒子并上传（每帧调用一次）
NRResult nrEmitterEmit(NREmitterHandle handle, f32 delta_time);
// 记录计算着色器模拟命令（须在 render pass 之外）
void     nrEmitterSimulate(VkCommandBuffer cmd, NREmitterHandle handle, f32 delta_time);
// 记录绘制命令（须在场景 render pass 之内）
void     nrEmitterRender(VkCommandBuffer cmd, NREmitterHandle handle,
						 VkDescriptorSet global_set);

SE_EXTERN_C_END
