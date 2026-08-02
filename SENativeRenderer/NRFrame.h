#pragma once

// ============================================================
// NRFrame.h
// 帧图：把场景队列、粒子、后处理串成一次完整的帧提交
//
// 一帧的顺序：
//   1. Acquire 交换链图像
//   2. Begin 命令缓冲
//   3. [render pass 外] 粒子计算模拟
//   4. HDR 场景 pass：不透明 -> 天空盒 -> 透明 -> 粒子
//   5. 后处理链：Bloom -> Tonemap/合成 -> 交换链
//   6. End 命令缓冲 + Submit + Present
//
// 粒子模拟必须在 render pass 之外记录：Vulkan 禁止在 render pass
// 实例内部执行 vkCmdDispatch。
// ============================================================

#include "NRScene.h"
#include "NRPostProcess.h"
#include "NRParticle.h"

SE_EXTERN_C_BEGIN

typedef struct NRFrameSystem
{
	NRSceneHandle active_scene;
	// 叠加场景（UI）：在主场景之后于同一个 HDR pass 中绘制，0 表示无
	NRSceneHandle overlay_scene;

	// 场景 HDR pass 使用的管线
	VkPipeline pipe_opaque;
	VkPipeline pipe_masked;
	VkPipeline pipe_transparent;
	VkPipeline pipe_double_sided;

	NREmitterHandle emitters[NR_MAX_EMITTERS];
	u32 emitter_count;

	NRFrameStats stats;
	f64 time_accum;
	u64 frame_index;

	b32 in_frame;
	b32 initialized;
} NRFrameSystem;

extern NRFrameSystem nr_frame;

NRResult nrFrameInit(void);
void     nrFrameShutdown(void);
NRResult nrFrameResize(u32 width, u32 height);

NRResult nrFrameSetActiveScene(NRSceneHandle scene);
NRResult nrFrameSetOverlayScene(NRSceneHandle scene);
NRResult nrFrameRegisterEmitter(NREmitterHandle emitter);
void     nrFrameUnregisterEmitter(NREmitterHandle emitter);

// 完整一帧；内部处理交换链过期重建
NRResult nrFrameRender(f64 delta_time);

SE_EXTERN_C_END
