#include "NRParticle.h"
#include "NRShaderc.h"
#include "NRShaderLib.h"
#include "NRVkLoader.h"
#include "NRDevice.h"
#include <math.h>

// ============================================================
// NRParticle.c
// ============================================================

NRParticleSystem nr_particles;

#define NR_HSHIFT 32
#define NR_HMASK  ((u64)0xFFFFFFFFull)

static u64 nrMakeH(u32 slot, u32 gen)
{
	return ((u64)gen << NR_HSHIFT) | (((u64)slot + 1ull) & NR_HMASK);
}

NREmitter* nrEmitterResolve(NREmitterHandle h)
{
	if (h == 0 || nr_particles.emitters == NULL) return NULL;
	u64 slot = h & NR_HMASK;
	if (slot == 0 || slot > NR_MAX_EMITTERS) return NULL;
	NREmitter* e = &nr_particles.emitters[slot - 1ull];
	if (!e->alive || (u32)(h >> NR_HSHIFT) != e->generation) return NULL;
	return e;
}

// 轻量 xorshift，避免依赖 rand() 的全局状态与线程安全问题
static f32 nrRand01(u32* state)
{
	u32 x = *state;
	x ^= x << 13; x ^= x >> 17; x ^= x << 5;
	*state = x;
	return (f32)(x & 0xFFFFFFu) / (f32)0x1000000u;
}

static f32 nrRandRange(u32* state, f32 lo, f32 hi)
{
	return lo + (hi - lo) * nrRand01(state);
}

// ------------------------------------------------------------
// 系统初始化
// ------------------------------------------------------------
static NRResult nrMakeSetLayout(const VkDescriptorSetLayoutBinding* b, u32 count,
								VkDescriptorSetLayout* out)
{
	VkDescriptorSetLayoutCreateInfo info;
	memset(&info, 0, sizeof(info));
	info.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
	info.bindingCount = count;
	info.pBindings = b;

	VkResult vr = nrvk.CreateDescriptorSetLayout(nr_device.device, &info, NULL, out);
	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_DESCRIPTOR_SET_FAILED, (u32)vr);
	return NRR_MakeSuccess(NRR_STEP_NR_Particle, NRR_CODE_SUCCESS);
}

// 通用 object_layout 的 binding 0 是 UNIFORM_BUFFER_DYNAMIC，compute_layout 的
// set0 又是 global_layout，二者都与粒子着色器声明不符，故粒子系统自建全套布局。
static NRResult nrCreateParticleLayouts(void)
{
	VkDescriptorSetLayoutBinding b;

	// sim: set0 binding0 = SSBO (compute)
	memset(&b, 0, sizeof(b));
	b.binding = 0;
	b.descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;
	b.descriptorCount = 1;
	b.stageFlags = VK_SHADER_STAGE_COMPUTE_BIT;
	NRResult r = nrMakeSetLayout(&b, 1, &nr_particles.sim_layout);
	if (NRR_FAILED(r)) return r;

	// tex: set1 binding1 = 粒子贴图（与 GLSL 声明的 binding 号对齐）
	memset(&b, 0, sizeof(b));
	b.binding = 1;
	b.descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER;
	b.descriptorCount = 1;
	b.stageFlags = VK_SHADER_STAGE_FRAGMENT_BIT;
	r = nrMakeSetLayout(&b, 1, &nr_particles.tex_layout);
	if (NRR_FAILED(r)) return r;

	// ssbo: set2 binding0 = SSBO (vertex)
	memset(&b, 0, sizeof(b));
	b.binding = 0;
	b.descriptorType = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER;
	b.descriptorCount = 1;
	b.stageFlags = VK_SHADER_STAGE_VERTEX_BIT;
	r = nrMakeSetLayout(&b, 1, &nr_particles.ssbo_layout);
	if (NRR_FAILED(r)) return r;

	// 计算管线布局
	VkPushConstantRange pcr;
	pcr.stageFlags = VK_SHADER_STAGE_COMPUTE_BIT;
	pcr.offset = 0;
	pcr.size = sizeof(NRParticleSimPush);

	VkPipelineLayoutCreateInfo lci;
	memset(&lci, 0, sizeof(lci));
	lci.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
	lci.setLayoutCount = 1;
	lci.pSetLayouts = &nr_particles.sim_layout;
	lci.pushConstantRangeCount = 1;
	lci.pPushConstantRanges = &pcr;
	if (nrvk.CreatePipelineLayout(nr_device.device, &lci, NULL,
								  &nr_particles.sim_pipeline_layout) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_PIPELINE_CREATION_FAILED, 1);

	// 渲染管线布局：set0=global（相机），set1=贴图，set2=粒子 SSBO
	VkDescriptorSetLayout sets[3];
	sets[0] = nr_descriptors.global_layout;
	sets[1] = nr_particles.tex_layout;
	sets[2] = nr_particles.ssbo_layout;

	memset(&lci, 0, sizeof(lci));
	lci.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
	lci.setLayoutCount = 3;
	lci.pSetLayouts = sets;
	if (nrvk.CreatePipelineLayout(nr_device.device, &lci, NULL,
								  &nr_particles.render_pipeline_layout) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_PIPELINE_CREATION_FAILED, 2);

	return NRR_MakeSuccess(NRR_STEP_NR_Particle, NRR_CODE_SUCCESS);
}

static NRResult nrCreateRenderPipeline(VkRenderPass pass, u32 blend, VkPipeline* out)
{
	u32* vs = NULL; u64 vsSize = 0;
	u32* fs = NULL; u64 fsSize = 0;

	NRResult r = nrShadercCompile(g_NRParticleVertGLSL, "particle.vert",
								  NR_SHADER_STAGE_VERTEX, "main", NULL, 0, &vs, &vsSize);
	if (NRR_FAILED(r)) return r;

	r = nrShadercCompile(g_NRParticleFragGLSL, "particle.frag",
						 NR_SHADER_STAGE_FRAGMENT, "main", NULL, 0, &fs, &fsSize);
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
	cfg.layout = nr_particles.render_pipeline_layout;
	cfg.samples = VK_SAMPLE_COUNT_1_BIT;
	cfg.use_vertex_input = FALSE;    // 顶点由 gl_VertexIndex 生成
	cfg.depth_test = TRUE;
	cfg.depth_write = FALSE;         // 粒子不写深度，避免互相遮挡出硬边
	cfg.cull_mode = VK_CULL_MODE_NONE;
	cfg.blend_mode = blend;

	r = nrPipelineCreateGraphics(&cfg, out);
	nrShaderDestroy(vsm);
	nrShaderDestroy(fsm);
	return r;
}

NRResult nrParticleInit(VkRenderPass scene_pass)
{
	if (nr_particles.initialized)
		return NRR_MakeSuccess(NRR_STEP_NR_Particle, NRR_CODE_SUCCESS);

	memset(&nr_particles, 0, sizeof(nr_particles));

	nr_particles.emitters = (NREmitter*)calloc(NR_MAX_EMITTERS, sizeof(NREmitter));
	if (nr_particles.emitters == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_OUT_OF_MEMORY, 0);

	NRResult r = nrCreateParticleLayouts();
	if (NRR_FAILED(r)) { free(nr_particles.emitters); nr_particles.emitters = NULL; return r; }

	// 计算管线
	u32* cs = NULL; u64 csSize = 0;
	r = nrShadercCompile(g_NRParticleCompGLSL, "particle.comp",
						 NR_SHADER_STAGE_COMPUTE, "main", NULL, 0, &cs, &csSize);
	if (NRR_FAILED(r)) goto fail;

	VkShaderModule csm = VK_NULL_HANDLE;
	r = nrShaderCreateFromSPIRV(cs, csSize, &csm);
	nrShadercFree(cs);
	if (NRR_FAILED(r)) goto fail;

	r = nrPipelineCreateCompute(csm, "main", nr_particles.sim_pipeline_layout,
								&nr_particles.sim_pipeline);
	nrShaderDestroy(csm);
	if (NRR_FAILED(r)) goto fail;

	r = nrCreateRenderPipeline(scene_pass, NR_BLEND_ALPHA,
							   &nr_particles.render_pipeline_alpha);
	if (NRR_FAILED(r)) goto fail;

	r = nrCreateRenderPipeline(scene_pass, NR_BLEND_ADD,
							   &nr_particles.render_pipeline_add);
	if (NRR_FAILED(r)) goto fail;

	nr_particles.initialized = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_Particle, NRR_CODE_SUCCESS);

fail:
	free(nr_particles.emitters);
	nr_particles.emitters = NULL;
	return r;
}

void nrParticleShutdown(void)
{
	if (!nr_particles.initialized) return;

	nrvk.DeviceWaitIdle(nr_device.device);

	for (u32 i = 0; i < NR_MAX_EMITTERS; i++)
		if (nr_particles.emitters[i].alive)
			nrEmitterDestroy(nrMakeH(i, nr_particles.emitters[i].generation));

	nrPipelineDestroy(nr_particles.sim_pipeline);
	nrPipelineDestroy(nr_particles.render_pipeline_alpha);
	nrPipelineDestroy(nr_particles.render_pipeline_add);

	if (nr_particles.sim_pipeline_layout != VK_NULL_HANDLE)
		nrvk.DestroyPipelineLayout(nr_device.device, nr_particles.sim_pipeline_layout, NULL);
	if (nr_particles.render_pipeline_layout != VK_NULL_HANDLE)
		nrvk.DestroyPipelineLayout(nr_device.device, nr_particles.render_pipeline_layout, NULL);

	if (nr_particles.sim_layout != VK_NULL_HANDLE)
		nrvk.DestroyDescriptorSetLayout(nr_device.device, nr_particles.sim_layout, NULL);
	if (nr_particles.tex_layout != VK_NULL_HANDLE)
		nrvk.DestroyDescriptorSetLayout(nr_device.device, nr_particles.tex_layout, NULL);
	if (nr_particles.ssbo_layout != VK_NULL_HANDLE)
		nrvk.DestroyDescriptorSetLayout(nr_device.device, nr_particles.ssbo_layout, NULL);

	free(nr_particles.emitters);
	memset(&nr_particles, 0, sizeof(nr_particles));
}

// ------------------------------------------------------------
// 发射器
// ------------------------------------------------------------
NRResult nrEmitterCreate(const NRParticleEmitterDesc* desc, NREmitterHandle* out)
{
	if (desc == NULL || out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_INVALID_PARAMETER, 0);
	if (!nr_particles.initialized)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_NOT_INITIALIZED, 0);

	*out = 0;

	u32 capacity = desc->max_particles;
	if (capacity == 0) capacity = 1024;

	u32 slot = UINT32_MAX;
	for (u32 i = 0; i < NR_MAX_EMITTERS; i++)
		if (!nr_particles.emitters[i].alive) { slot = i; break; }
	if (slot == UINT32_MAX)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_CAPACITY_EXCEEDED, 0);

	NREmitter* em = &nr_particles.emitters[slot];
	u32 gen = em->generation;
	memset(em, 0, sizeof(NREmitter));
	em->generation = gen;
	em->desc = *desc;
	em->capacity = capacity;
	em->rng_state = 0x9E3779B9u ^ (slot * 2654435761u);
	if (em->rng_state == 0) em->rng_state = 1;

	em->cpu = (NRParticleGPU*)calloc(capacity, sizeof(NRParticleGPU));
	if (em->cpu == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_OUT_OF_MEMORY, 0);

	// 粒子缓冲既被计算着色器读写，也被顶点着色器读取，故常驻 HOST_VISIBLE
	// 以便 CPU 直接写入新发射的粒子而无需 staging
	NRResult r = nrBufferCreate((u64)capacity * sizeof(NRParticleGPU),
								VK_BUFFER_USAGE_STORAGE_BUFFER_BIT |
								VK_BUFFER_USAGE_TRANSFER_DST_BIT,
								VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT |
								VK_MEMORY_PROPERTY_HOST_COHERENT_BIT,
								&em->buffer);
	if (NRR_FAILED(r)) { free(em->cpu); em->cpu = NULL; return r; }

	r = nrBufferUpload(&em->buffer, em->cpu, (u64)capacity * sizeof(NRParticleGPU), 0);
	if (NRR_FAILED(r)) goto fail;

	r = nrDescriptorAllocate(nr_particles.sim_layout, &em->sim_set);
	if (NRR_FAILED(r)) goto fail;
	nrDescriptorWriteBuffer(em->sim_set, 0, VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
							em->buffer.buffer, 0,
							(u64)capacity * sizeof(NRParticleGPU));

	// 渲染时 SSBO 绑到 set 2 binding 0
	r = nrDescriptorAllocate(nr_particles.ssbo_layout, &em->render_set);
	if (NRR_FAILED(r)) goto fail;
	nrDescriptorWriteBuffer(em->render_set, 0, VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
							em->buffer.buffer, 0,
							(u64)capacity * sizeof(NRParticleGPU));

	// 粒子贴图绑到 set 1 binding 1
	r = nrDescriptorAllocate(nr_particles.tex_layout, &em->tex_set);
	if (NRR_FAILED(r)) goto fail;

	NRTexture* tex = nrTextureResolve(desc->texture);
	if (tex == NULL) tex = nrTextureResolve(nrTextureWhite());
	if (tex != NULL)
		nrDescriptorWriteImage(em->tex_set, 1, tex->image.view, tex->image.sampler,
							   VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);

	em->alive = TRUE;
	*out = nrMakeH(slot, em->generation);
	return NRR_MakeSuccess(NRR_STEP_NR_Particle, NRR_CODE_SUCCESS);

fail:
	nrBufferDestroy(&em->buffer);
	free(em->cpu);
	em->cpu = NULL;
	return r;
}

void nrEmitterDestroy(NREmitterHandle handle)
{
	NREmitter* em = nrEmitterResolve(handle);
	if (em == NULL) return;

	nrBufferDestroy(&em->buffer);
	free(em->cpu);

	u32 gen = em->generation + 1u;
	memset(em, 0, sizeof(NREmitter));
	em->generation = gen;
	em->alive = FALSE;
}

NRResult nrEmitterUpdate(NREmitterHandle handle, const NRParticleEmitterDesc* desc)
{
	NREmitter* em = nrEmitterResolve(handle);
	if (em == NULL || desc == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_INVALID_HANDLE, 0);

	// max_particles 变更需重建缓冲，这里只接受不改容量的参数更新
	u32 cap = em->capacity;
	em->desc = *desc;
	em->desc.max_particles = cap;
	return NRR_MakeSuccess(NRR_STEP_NR_Particle, NRR_CODE_SUCCESS);
}

NRResult nrEmitterSetPosition(NREmitterHandle handle, NRFloat3 position)
{
	NREmitter* em = nrEmitterResolve(handle);
	if (em == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_INVALID_HANDLE, 0);
	em->desc.position = position;
	return NRR_MakeSuccess(NRR_STEP_NR_Particle, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 发射
// ------------------------------------------------------------
// 在以 dir 为轴、spread 为半角的圆锥内取随机方向
static NRFloat3 nrRandomCone(u32* rng, NRFloat3 dir, f32 spread)
{
	NRFloat3 d = nrV3Normalize(dir);
	if (spread <= 1e-4f) return d;

	// 构造与 d 正交的基
	NRFloat3 up = (fabsf(d.y) > 0.99f) ? (NRFloat3){ 1.0f, 0.0f, 0.0f }
									   : (NRFloat3){ 0.0f, 1.0f, 0.0f };
	NRFloat3 t = nrV3Normalize(nrV3Cross(up, d));
	NRFloat3 b = nrV3Cross(d, t);

	f32 theta = nrRandRange(rng, 0.0f, 6.2831853f);
	// sqrt 保证方向在圆锥内均匀分布，否则会向轴心聚集
	f32 r = sqrtf(nrRand01(rng)) * tanf(spread);

	NRFloat3 offset = nrV3Add(nrV3Scale(t, cosf(theta) * r),
							  nrV3Scale(b, sinf(theta) * r));
	return nrV3Normalize(nrV3Add(d, offset));
}

NRResult nrEmitterEmit(NREmitterHandle handle, f32 delta_time)
{
	NREmitter* em = nrEmitterResolve(handle);
	if (em == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Particle, NRR_CODE_INVALID_HANDLE, 0);

	// 累计不足 1 个的发射量，避免低发射率下永远发不出粒子
	em->emit_accumulator += em->desc.emission_rate * delta_time;
	u32 toEmit = (u32)em->emit_accumulator;
	if (toEmit == 0) return NRR_MakeSuccess(NRR_STEP_NR_Particle, NRR_CODE_SUCCESS);
	em->emit_accumulator -= (f32)toEmit;

	u32 emitted = 0;
	u32 alive = 0;

	for (u32 i = 0; i < em->capacity; i++)
	{
		NRParticleGPU* p = &em->cpu[i];

		if (p->velocity_life.w > 0.0f) { alive++; continue; }
		if (emitted >= toEmit) continue;

		NRFloat3 dir = nrRandomCone(&em->rng_state, em->desc.direction,
									em->desc.spread_radians);
		f32 speed = nrRandRange(&em->rng_state, em->desc.speed_min, em->desc.speed_max);
		f32 life = nrRandRange(&em->rng_state, em->desc.life_min, em->desc.life_max);

		p->position_size.x = em->desc.position.x;
		p->position_size.y = em->desc.position.y;
		p->position_size.z = em->desc.position.z;
		p->position_size.w = em->desc.size_begin;

		p->velocity_life.x = dir.x * speed;
		p->velocity_life.y = dir.y * speed;
		p->velocity_life.z = dir.z * speed;
		p->velocity_life.w = (life > 0.0f) ? life : 1.0f;

		p->color = em->desc.color_begin;

		emitted++;
		alive++;
	}

	em->alive_count = alive;

	if (emitted > 0)
		return nrBufferUpload(&em->buffer, em->cpu,
							  (u64)em->capacity * sizeof(NRParticleGPU), 0);

	return NRR_MakeSuccess(NRR_STEP_NR_Particle, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 模拟
// ------------------------------------------------------------
void nrEmitterSimulate(VkCommandBuffer cmd, NREmitterHandle handle, f32 delta_time)
{
	NREmitter* em = nrEmitterResolve(handle);
	if (em == NULL || cmd == VK_NULL_HANDLE) return;

	NRParticleSimPush push;
	push.gravity_dt.x = em->desc.gravity.x;
	push.gravity_dt.y = em->desc.gravity.y;
	push.gravity_dt.z = em->desc.gravity.z;
	push.gravity_dt.w = delta_time;
	push.count = em->capacity;
	push.drag = em->desc.drag;

	nrvk.CmdBindPipeline(cmd, VK_PIPELINE_BIND_POINT_COMPUTE, nr_particles.sim_pipeline);
	nrvk.CmdBindDescriptorSets(cmd, VK_PIPELINE_BIND_POINT_COMPUTE,
							   nr_particles.sim_pipeline_layout, 0, 1, &em->sim_set, 0, NULL);
	nrvk.CmdPushConstants(cmd, nr_particles.sim_pipeline_layout,
						  VK_SHADER_STAGE_COMPUTE_BIT, 0, sizeof(push), &push);

	u32 groups = (em->capacity + NR_PARTICLE_GROUP_SIZE - 1) / NR_PARTICLE_GROUP_SIZE;
	nrvk.CmdDispatch(cmd, groups, 1, 1);

	// 计算写入必须对后续顶点着色器读取可见
	VkMemoryBarrier barrier;
	memset(&barrier, 0, sizeof(barrier));
	barrier.sType = VK_STRUCTURE_TYPE_MEMORY_BARRIER;
	barrier.srcAccessMask = VK_ACCESS_SHADER_WRITE_BIT;
	barrier.dstAccessMask = VK_ACCESS_SHADER_READ_BIT;

	nrvk.CmdPipelineBarrier(cmd, VK_PIPELINE_STAGE_COMPUTE_SHADER_BIT,
							VK_PIPELINE_STAGE_VERTEX_SHADER_BIT, 0,
							1, &barrier, 0, NULL, 0, NULL);
}

// ------------------------------------------------------------
// 渲染
// ------------------------------------------------------------
void nrEmitterRender(VkCommandBuffer cmd, NREmitterHandle handle,
					 VkDescriptorSet global_set)
{
	NREmitter* em = nrEmitterResolve(handle);
	if (em == NULL || cmd == VK_NULL_HANDLE) return;

	VkPipeline pipe = (em->desc.blend_mode == NR_BLEND_ADD)
					? nr_particles.render_pipeline_add
					: nr_particles.render_pipeline_alpha;

	nrvk.CmdBindPipeline(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS, pipe);

	if (global_set != VK_NULL_HANDLE)
		nrvk.CmdBindDescriptorSets(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS,
								   nr_particles.render_pipeline_layout, 0,
								   1, &global_set, 0, NULL);

	nrvk.CmdBindDescriptorSets(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS,
							   nr_particles.render_pipeline_layout, 1,
							   1, &em->tex_set, 0, NULL);
	nrvk.CmdBindDescriptorSets(cmd, VK_PIPELINE_BIND_POINT_GRAPHICS,
							   nr_particles.render_pipeline_layout, 2,
							   1, &em->render_set, 0, NULL);

	// 每个粒子一个 billboard：3 顶点全屏三角裁出的四边形，实例数 = 容量
	nrvk.CmdDraw(cmd, 6, em->capacity, 0, 0);
}
