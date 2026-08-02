#pragma once

// ============================================================
// NRMesh.h
// 网格资源：顶点/索引缓冲、子网格、实例化缓冲、骨骼蒙皮 SSBO
//
// 句柄策略：对外暴露 u32 句柄（NRMeshHandle），内部用带世代号的槽位表，
// 避免 C# 侧持有已释放资源的悬空句柄时误命中新资源。
//   handle = (generation << 20) | slot_index，0 表示无效句柄。
// ============================================================

#include "NRMemory.h"
#include "NRApi.h"

SE_EXTERN_C_BEGIN

#define NR_MAX_SUBMESHES 64
#define NR_MAX_JOINTS    256

// 子网格：同一顶点缓冲内的一段索引，对应一个材质
typedef struct NRSubmesh
{
	u32 index_offset;
	u32 index_count;
	u32 material_slot;    // 对象材质数组下标
	NRFloat3 bounds_min;
	NRFloat3 bounds_max;
} NRSubmesh;

typedef struct NRMesh
{
	NRBuffer vertex_buffer;
	NRBuffer index_buffer;

	u32 vertex_count;
	u32 index_count;

	NRSubmesh submeshes[NR_MAX_SUBMESHES];
	u32 submesh_count;

	NRFloat3 bounds_min;
	NRFloat3 bounds_max;

	// 蒙皮：每帧上传的关节矩阵，供 set 2 binding 1 的 SSBO 使用
	NRBuffer skin_buffer;
	u32  joint_count;
	b32  skinned;

	b32  dynamic;         // HOST_VISIBLE，可频繁更新
	b32  alive;
	u32  generation;
} NRMesh;

NRResult nrMeshSystemInit(void);
void     nrMeshSystemShutdown(void);

NRResult nrMeshCreate(const NRMeshCreateInfo* info, NRMeshHandle* out);
void     nrMeshDestroy(NRMeshHandle handle);
NRMesh*  nrMeshResolve(NRMeshHandle handle);   // 无效句柄返回 NULL

// 动态网格更新（要求创建时 dynamic = TRUE）
NRResult nrMeshUpdateVertices(NRMeshHandle handle, const NRVertex* vertices,
							  u32 count, u32 first_vertex);
NRResult nrMeshUpdateIndices(NRMeshHandle handle, const u32* indices,
							 u32 count, u32 first_index);

// 子网格
NRResult nrMeshAddSubmesh(NRMeshHandle handle, u32 index_offset, u32 index_count,
						  u32 material_slot);
void     nrMeshClearSubmeshes(NRMeshHandle handle);

// 蒙皮
NRResult nrMeshEnableSkinning(NRMeshHandle handle, u32 joint_count);
NRResult nrMeshUpdateJoints(NRMeshHandle handle, const NRMatrix4* matrices, u32 count);

// 绑定到命令缓冲（绑定顶点与索引缓冲）
void     nrMeshBind(VkCommandBuffer cmd, const NRMesh* mesh);

SE_EXTERN_C_END
