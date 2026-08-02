#include "NRMesh.h"
#include "NRVkLoader.h"

// ============================================================
// NRMesh.c
// 槽位表 + 世代号的网格资源管理
// ============================================================

#define NR_MESH_CAPACITY    4096
#define NR_HANDLE_SLOT_BITS 32
#define NR_HANDLE_SLOT_MASK ((u64)0xFFFFFFFFull)

static NRMesh* s_meshes = NULL;
static u32     s_meshCount = 0;
static b32     s_meshInited = FALSE;

static NRMeshHandle nrMakeHandle(u32 slot, u32 generation)
{
	// slot 从 0 开始，但句柄 0 必须保留为无效值，故槽位 +1 编码
	return ((u64)generation << NR_HANDLE_SLOT_BITS) | (((u64)slot + 1ull) & NR_HANDLE_SLOT_MASK);
}

NRMesh* nrMeshResolve(NRMeshHandle handle)
{
	if (handle == 0 || s_meshes == NULL) return NULL;
	u64 slot = (handle & NR_HANDLE_SLOT_MASK);
	if (slot == 0 || slot > NR_MESH_CAPACITY) return NULL;
	slot -= 1ull;

	NRMesh* m = &s_meshes[slot];
	if (!m->alive) return NULL;
	if ((u32)(handle >> NR_HANDLE_SLOT_BITS) != m->generation) return NULL;
	return m;
}

static NRMesh* nrMeshAcquire(u32* out_slot)
{
	for (u32 i = 0; i < NR_MESH_CAPACITY; i++)
	{
		if (!s_meshes[i].alive)
		{
			*out_slot = i;
			return &s_meshes[i];
		}
	}
	return NULL;
}

NRResult nrMeshSystemInit(void)
{
	if (s_meshInited) return NRR_MakeSuccess(NRR_STEP_NR_CreateMesh, NRR_CODE_SUCCESS);

	s_meshes = (NRMesh*)calloc(NR_MESH_CAPACITY, sizeof(NRMesh));
	if (s_meshes == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_OUT_OF_MEMORY, 0);

	s_meshCount = 0;
	s_meshInited = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateMesh, NRR_CODE_SUCCESS);
}

void nrMeshSystemShutdown(void)
{
	if (s_meshes == NULL) return;
	for (u32 i = 0; i < NR_MESH_CAPACITY; i++)
	{
		if (s_meshes[i].alive)
			nrMeshDestroy(nrMakeHandle(i, s_meshes[i].generation));
	}
	free(s_meshes);
	s_meshes = NULL;
	s_meshCount = 0;
	s_meshInited = FALSE;
}

// ------------------------------------------------------------
// 创建
// ------------------------------------------------------------
NRResult nrMeshCreate(const NRMeshCreateInfo* info, NRMeshHandle* out)
{
	if (info == NULL || out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_PARAMETER, 0);
	if (!s_meshInited)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_NOT_INITIALIZED, 0);
	if (info->vertices == NULL || info->vertex_count == 0)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_PARAMETER, 1);

	*out = 0;

	u32 slot = 0;
	NRMesh* mesh = nrMeshAcquire(&slot);
	if (mesh == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_CAPACITY_EXCEEDED, 0);

	u32 generation = mesh->generation;   // 复用槽位时保留世代号
	memset(mesh, 0, sizeof(NRMesh));
	mesh->generation = generation;

	// 动态网格放 HOST_VISIBLE 以便直接 memcpy；静态网格放 DEVICE_LOCAL 走 staging
	VkMemoryPropertyFlags props = info->dynamic
		? (VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT)
		: VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT;

	u64 vbSize = (u64)info->vertex_count * sizeof(NRVertex);
	VkBufferUsageFlags vbUsage = VK_BUFFER_USAGE_VERTEX_BUFFER_BIT |
								 VK_BUFFER_USAGE_TRANSFER_DST_BIT |
								 VK_BUFFER_USAGE_STORAGE_BUFFER_BIT;

	NRResult r = nrBufferCreate(vbSize, vbUsage, props, &mesh->vertex_buffer);
	if (NRR_FAILED(r)) return r;

	r = nrBufferUpload(&mesh->vertex_buffer, info->vertices, vbSize, 0);
	if (NRR_FAILED(r)) { nrBufferDestroy(&mesh->vertex_buffer); return r; }

	mesh->vertex_count = info->vertex_count;

	if (info->indices != NULL && info->index_count > 0)
	{
		u64 ibSize = (u64)info->index_count * sizeof(u32);
		VkBufferUsageFlags ibUsage = VK_BUFFER_USAGE_INDEX_BUFFER_BIT |
									 VK_BUFFER_USAGE_TRANSFER_DST_BIT |
									 VK_BUFFER_USAGE_STORAGE_BUFFER_BIT;

		r = nrBufferCreate(ibSize, ibUsage, props, &mesh->index_buffer);
		if (NRR_FAILED(r)) { nrBufferDestroy(&mesh->vertex_buffer); return r; }

		r = nrBufferUpload(&mesh->index_buffer, info->indices, ibSize, 0);
		if (NRR_FAILED(r))
		{
			nrBufferDestroy(&mesh->index_buffer);
			nrBufferDestroy(&mesh->vertex_buffer);
			return r;
		}
		mesh->index_count = info->index_count;
	}

	mesh->bounds_min = info->bounds_min;
	mesh->bounds_max = info->bounds_max;
	mesh->dynamic = info->dynamic;

	// 默认整个网格作为一个子网格，避免调用方忘记添加导致无法绘制
	mesh->submeshes[0].index_offset = 0;
	mesh->submeshes[0].index_count = (mesh->index_count > 0) ? mesh->index_count : mesh->vertex_count;
	mesh->submeshes[0].material_slot = 0;
	mesh->submeshes[0].bounds_min = info->bounds_min;
	mesh->submeshes[0].bounds_max = info->bounds_max;
	mesh->submesh_count = 1;

	mesh->alive = TRUE;
	s_meshCount++;

	*out = nrMakeHandle(slot, mesh->generation);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateMesh, NRR_CODE_SUCCESS);
}

void nrMeshDestroy(NRMeshHandle handle)
{
	NRMesh* mesh = nrMeshResolve(handle);
	if (mesh == NULL) return;

	nrBufferDestroy(&mesh->vertex_buffer);
	if (mesh->index_count > 0) nrBufferDestroy(&mesh->index_buffer);
	if (mesh->skinned) nrBufferDestroy(&mesh->skin_buffer);

	u32 generation = mesh->generation + 1u;   // 世代号递增使旧句柄立即失效
	memset(mesh, 0, sizeof(NRMesh));
	mesh->generation = generation;
	mesh->alive = FALSE;

	if (s_meshCount > 0) s_meshCount--;
}

// ------------------------------------------------------------
// 动态更新
// ------------------------------------------------------------
NRResult nrMeshUpdateVertices(NRMeshHandle handle, const NRVertex* vertices,
							  u32 count, u32 first_vertex)
{
	NRMesh* mesh = nrMeshResolve(handle);
	if (mesh == NULL || vertices == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_HANDLE, 0);
	if (first_vertex + count > mesh->vertex_count)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_PARAMETER, 0);

	return nrBufferUpload(&mesh->vertex_buffer, vertices,
						  (u64)count * sizeof(NRVertex),
						  (u64)first_vertex * sizeof(NRVertex));
}

NRResult nrMeshUpdateIndices(NRMeshHandle handle, const u32* indices,
							 u32 count, u32 first_index)
{
	NRMesh* mesh = nrMeshResolve(handle);
	if (mesh == NULL || indices == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_HANDLE, 0);
	if (mesh->index_count == 0 || first_index + count > mesh->index_count)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_PARAMETER, 0);

	return nrBufferUpload(&mesh->index_buffer, indices,
						  (u64)count * sizeof(u32),
						  (u64)first_index * sizeof(u32));
}

// ------------------------------------------------------------
// 子网格
// ------------------------------------------------------------
NRResult nrMeshAddSubmesh(NRMeshHandle handle, u32 index_offset, u32 index_count,
						  u32 material_slot)
{
	NRMesh* mesh = nrMeshResolve(handle);
	if (mesh == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_HANDLE, 0);
	if (mesh->submesh_count >= NR_MAX_SUBMESHES)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_CAPACITY_EXCEEDED, 0);

	u32 total = (mesh->index_count > 0) ? mesh->index_count : mesh->vertex_count;
	if (index_offset + index_count > total)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_PARAMETER, 0);

	NRSubmesh* sm = &mesh->submeshes[mesh->submesh_count++];
	sm->index_offset = index_offset;
	sm->index_count = index_count;
	sm->material_slot = material_slot;
	sm->bounds_min = mesh->bounds_min;
	sm->bounds_max = mesh->bounds_max;

	return NRR_MakeSuccess(NRR_STEP_NR_CreateMesh, NRR_CODE_SUCCESS);
}

void nrMeshClearSubmeshes(NRMeshHandle handle)
{
	NRMesh* mesh = nrMeshResolve(handle);
	if (mesh != NULL) mesh->submesh_count = 0;
}

// ------------------------------------------------------------
// 蒙皮
// ------------------------------------------------------------
NRResult nrMeshEnableSkinning(NRMeshHandle handle, u32 joint_count)
{
	NRMesh* mesh = nrMeshResolve(handle);
	if (mesh == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_HANDLE, 0);
	if (joint_count == 0 || joint_count > NR_MAX_JOINTS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_PARAMETER, 0);

	if (mesh->skinned) nrBufferDestroy(&mesh->skin_buffer);

	// 关节矩阵每帧变化，直接放 HOST_VISIBLE 避免每帧 staging 拷贝
	NRResult r = nrBufferCreate((u64)joint_count * sizeof(NRMatrix4),
								VK_BUFFER_USAGE_STORAGE_BUFFER_BIT,
								VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT |
								VK_MEMORY_PROPERTY_HOST_COHERENT_BIT,
								&mesh->skin_buffer);
	if (NRR_FAILED(r)) { mesh->skinned = FALSE; return r; }

	mesh->joint_count = joint_count;
	mesh->skinned = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateMesh, NRR_CODE_SUCCESS);
}

NRResult nrMeshUpdateJoints(NRMeshHandle handle, const NRMatrix4* matrices, u32 count)
{
	NRMesh* mesh = nrMeshResolve(handle);
	if (mesh == NULL || matrices == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_HANDLE, 0);
	if (!mesh->skinned || count > mesh->joint_count)
		return NRR_MakeFailure(NRR_STEP_NR_CreateMesh, NRR_CODE_INVALID_PARAMETER, 0);

	return nrBufferUpload(&mesh->skin_buffer, matrices,
						  (u64)count * sizeof(NRMatrix4), 0);
}

// ------------------------------------------------------------
// 绑定
// ------------------------------------------------------------
void nrMeshBind(VkCommandBuffer cmd, const NRMesh* mesh)
{
	if (cmd == VK_NULL_HANDLE || mesh == NULL) return;

	VkDeviceSize offset = 0;
	nrvk.CmdBindVertexBuffers(cmd, 0, 1, &mesh->vertex_buffer.buffer, &offset);
	if (mesh->index_count > 0)
		nrvk.CmdBindIndexBuffer(cmd, mesh->index_buffer.buffer, 0, VK_INDEX_TYPE_UINT32);
}
