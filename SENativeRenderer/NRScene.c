#include "NRScene.h"
#include "NRVkLoader.h"
#include "NRDevice.h"

// ============================================================
// NRScene.c
// ============================================================

#define NR_HSHIFT 32
#define NR_HMASK  ((u64)0xFFFFFFFFull)

static NRScene* s_scenes = NULL;
static b32 s_sceneInited = FALSE;

static u64 nrMakeH(u32 slot, u32 gen)
{
	return ((u64)gen << NR_HSHIFT) | (((u64)slot + 1ull) & NR_HMASK);
}

NRScene* nrSceneResolve(NRSceneHandle h)
{
	if (h == 0 || s_scenes == NULL) return NULL;
	u64 slot = h & NR_HMASK;
	if (slot == 0 || slot > NR_MAX_SCENES) return NULL;
	NRScene* s = &s_scenes[slot - 1ull];
	if (!s->alive || (u32)(h >> NR_HSHIFT) != s->generation) return NULL;
	return s;
}

// ------------------------------------------------------------
// 系统生命周期
// ------------------------------------------------------------
NRResult nrSceneSystemInit(void)
{
	if (s_sceneInited) return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);

	s_scenes = (NRScene*)calloc(NR_MAX_SCENES, sizeof(NRScene));
	if (s_scenes == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_OUT_OF_MEMORY, 0);

	s_sceneInited = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);
}

void nrSceneSystemShutdown(void)
{
	if (s_scenes == NULL) return;
	for (u32 i = 0; i < NR_MAX_SCENES; i++)
		if (s_scenes[i].alive) nrSceneDestroy(nrMakeH(i, s_scenes[i].generation));
	free(s_scenes);
	s_scenes = NULL;
	s_sceneInited = FALSE;
}

// ------------------------------------------------------------
// 场景创建
// ------------------------------------------------------------
NRResult nrSceneCreate(NRSceneHandle* out)
{
	if (out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_PARAMETER, 0);
	if (!s_sceneInited)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_NOT_INITIALIZED, 0);

	*out = 0;

	u32 slot = UINT32_MAX;
	for (u32 i = 0; i < NR_MAX_SCENES; i++)
		if (!s_scenes[i].alive) { slot = i; break; }
	if (slot == UINT32_MAX)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_CAPACITY_EXCEEDED, 0);

	NRScene* sc = &s_scenes[slot];
	u32 gen = sc->generation;
	memset(sc, 0, sizeof(NRScene));
	sc->generation = gen;

	sc->objects     = (NRSceneObject*)calloc(NR_MAX_OBJECTS, sizeof(NRSceneObject));
	sc->lights      = (NRSceneLight*)calloc(NR_MAX_LIGHTS, sizeof(NRSceneLight));
	sc->opaque      = (NRDrawItem*)calloc(NR_MAX_OBJECTS, sizeof(NRDrawItem));
	sc->transparent = (NRDrawItem*)calloc(NR_MAX_OBJECTS, sizeof(NRDrawItem));
	sc->cpu_lights  = (NRLightGPU*)calloc(NR_MAX_LIGHTS, sizeof(NRLightGPU));

	if (sc->objects == NULL || sc->lights == NULL || sc->opaque == NULL ||
		sc->transparent == NULL || sc->cpu_lights == NULL)
	{
		free(sc->objects); free(sc->lights); free(sc->opaque);
		free(sc->transparent); free(sc->cpu_lights);
		memset(sc, 0, sizeof(NRScene));
		sc->generation = gen;
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_OUT_OF_MEMORY, 1);
	}

	// 相机 UBO 与光源 SSBO 每帧更新，常驻 HOST_VISIBLE
	VkMemoryPropertyFlags hostProps = VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT |
									  VK_MEMORY_PROPERTY_HOST_COHERENT_BIT;

	NRResult r = nrBufferCreate(sizeof(NRCameraUBO),
								VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT,
								hostProps, &sc->camera_ubo);
	if (NRR_FAILED(r)) goto fail;

	r = nrBufferCreate((u64)NR_MAX_LIGHTS * sizeof(NRLightGPU),
					   VK_BUFFER_USAGE_STORAGE_BUFFER_BIT,
					   hostProps, &sc->light_ssbo);
	if (NRR_FAILED(r)) { nrBufferDestroy(&sc->camera_ubo); goto fail; }

	r = nrDescriptorAllocate(nr_descriptors.global_layout, &sc->global_set);
	if (NRR_FAILED(r))
	{
		nrBufferDestroy(&sc->light_ssbo);
		nrBufferDestroy(&sc->camera_ubo);
		goto fail;
	}

	nrDescriptorWriteBuffer(sc->global_set, 0, VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
							sc->camera_ubo.buffer, 0, sizeof(NRCameraUBO));
	nrDescriptorWriteBuffer(sc->global_set, 1, VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
							sc->light_ssbo.buffer, 0,
							(u64)NR_MAX_LIGHTS * sizeof(NRLightGPU));

	// 默认相机，避免调用方未设置时构建出退化矩阵
	sc->camera.near_plane = 0.1f;
	sc->camera.far_plane = 1000.0f;
	sc->camera.fov_y_radians = 1.0472f;   // 60 度
	sc->camera.aspect = 16.0f / 9.0f;
	sc->camera.view = nrMat4Identity();
	sc->camera.projection = nrMat4Perspective(sc->camera.fov_y_radians,
											  sc->camera.aspect,
											  sc->camera.near_plane,
											  sc->camera.far_plane);

	sc->env.ambient_intensity = 0.03f;
	sc->alive = TRUE;
	*out = nrMakeH(slot, sc->generation);
	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);

fail:
	free(sc->objects); free(sc->lights); free(sc->opaque);
	free(sc->transparent); free(sc->cpu_lights);
	memset(sc, 0, sizeof(NRScene));
	sc->generation = gen;
	return r;
}

void nrSceneDestroy(NRSceneHandle handle)
{
	NRScene* sc = nrSceneResolve(handle);
	if (sc == NULL) return;

	nrBufferDestroy(&sc->camera_ubo);
	nrBufferDestroy(&sc->light_ssbo);

	free(sc->objects); free(sc->lights); free(sc->opaque);
	free(sc->transparent); free(sc->cpu_lights);

	u32 gen = sc->generation + 1u;
	memset(sc, 0, sizeof(NRScene));
	sc->generation = gen;
	sc->alive = FALSE;
}

// ------------------------------------------------------------
// 对象管理
// ------------------------------------------------------------
NRResult nrSceneAddObject(NRSceneHandle scene, const NRObjectDesc* desc, NRObjectHandle* out)
{
	NRScene* sc = nrSceneResolve(scene);
	if (sc == NULL || desc == NULL || out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_HANDLE, 0);

	*out = 0;

	u32 slot = UINT32_MAX;
	for (u32 i = 0; i < NR_MAX_OBJECTS; i++)
		if (!sc->objects[i].alive) { slot = i; break; }
	if (slot == UINT32_MAX)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_CAPACITY_EXCEEDED, 0);

	NRSceneObject* obj = &sc->objects[slot];
	u32 gen = obj->generation;
	memset(obj, 0, sizeof(NRSceneObject));
	obj->generation = gen;

	obj->world = desc->world;
	obj->mesh = desc->mesh;
	obj->material = desc->material;
	obj->visible = desc->visible;
	obj->cast_shadow = desc->cast_shadow;
	obj->layer_mask = (desc->layer_mask != 0) ? desc->layer_mask : 0xFFFFFFFFu;

	// 从网格取本地包围盒，剔除时再用世界矩阵变换
	NRMesh* mesh = nrMeshResolve(desc->mesh);
	if (mesh != NULL)
	{
		obj->local_bounds.min = mesh->bounds_min;
		obj->local_bounds.max = mesh->bounds_max;
	}

	// 蒙皮骨骼矩阵直接转交网格的关节缓冲
	if (desc->bone_matrices != NULL && desc->bone_count > 0 && mesh != NULL)
	{
		if (!mesh->skinned) nrMeshEnableSkinning(desc->mesh, desc->bone_count);
		nrMeshUpdateJoints(desc->mesh, desc->bone_matrices, desc->bone_count);
	}

	obj->alive = TRUE;
	sc->object_count++;
	*out = nrMakeH(slot, obj->generation);
	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);
}

static NRSceneObject* nrObjResolve(NRScene* sc, NRObjectHandle h)
{
	if (sc == NULL || h == 0) return NULL;
	u64 slot = h & NR_HMASK;
	if (slot == 0 || slot > NR_MAX_OBJECTS) return NULL;
	NRSceneObject* o = &sc->objects[slot - 1ull];
	if (!o->alive || (u32)(h >> NR_HSHIFT) != o->generation) return NULL;
	return o;
}

NRResult nrSceneRemoveObject(NRSceneHandle scene, NRObjectHandle obj)
{
	NRScene* sc = nrSceneResolve(scene);
	NRSceneObject* o = nrObjResolve(sc, obj);
	if (o == NULL) return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_HANDLE, 0);

	u32 gen = o->generation + 1u;
	memset(o, 0, sizeof(NRSceneObject));
	o->generation = gen;
	o->alive = FALSE;
	if (sc->object_count > 0) sc->object_count--;
	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);
}

NRResult nrSceneSetObjectTransform(NRSceneHandle scene, NRObjectHandle obj, const NRMatrix4* world)
{
	NRSceneObject* o = nrObjResolve(nrSceneResolve(scene), obj);
	if (o == NULL || world == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_HANDLE, 0);
	o->world = *world;
	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);
}

NRResult nrSceneSetObjectVisible(NRSceneHandle scene, NRObjectHandle obj, b32 visible)
{
	NRSceneObject* o = nrObjResolve(nrSceneResolve(scene), obj);
	if (o == NULL) return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_HANDLE, 0);
	o->visible = visible;
	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 光源管理
// ------------------------------------------------------------
NRResult nrSceneAddLight(NRSceneHandle scene, const NRLightDesc* desc, NRLightHandle* out)
{
	NRScene* sc = nrSceneResolve(scene);
	if (sc == NULL || desc == NULL || out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_HANDLE, 0);

	*out = 0;

	u32 slot = UINT32_MAX;
	for (u32 i = 0; i < NR_MAX_LIGHTS; i++)
		if (!sc->lights[i].alive) { slot = i; break; }
	if (slot == UINT32_MAX)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_CAPACITY_EXCEEDED, 0);

	NRSceneLight* lt = &sc->lights[slot];
	u32 gen = lt->generation;
	memset(lt, 0, sizeof(NRSceneLight));
	lt->generation = gen;
	lt->desc = *desc;
	lt->alive = TRUE;
	sc->light_count++;

	*out = nrMakeH(slot, lt->generation);
	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);
}

static NRSceneLight* nrLightResolve(NRScene* sc, NRLightHandle h)
{
	if (sc == NULL || h == 0) return NULL;
	u64 slot = h & NR_HMASK;
	if (slot == 0 || slot > NR_MAX_LIGHTS) return NULL;
	NRSceneLight* l = &sc->lights[slot - 1ull];
	if (!l->alive || (u32)(h >> NR_HSHIFT) != l->generation) return NULL;
	return l;
}

NRResult nrSceneRemoveLight(NRSceneHandle scene, NRLightHandle light)
{
	NRScene* sc = nrSceneResolve(scene);
	NRSceneLight* l = nrLightResolve(sc, light);
	if (l == NULL) return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_HANDLE, 0);

	u32 gen = l->generation + 1u;
	memset(l, 0, sizeof(NRSceneLight));
	l->generation = gen;
	l->alive = FALSE;
	if (sc->light_count > 0) sc->light_count--;
	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);
}

NRResult nrSceneUpdateLight(NRSceneHandle scene, NRLightHandle light, const NRLightDesc* desc)
{
	NRSceneLight* l = nrLightResolve(nrSceneResolve(scene), light);
	if (l == NULL || desc == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_HANDLE, 0);
	l->desc = *desc;
	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 相机 / 环境
// ------------------------------------------------------------
NRResult nrSceneSetCamera(NRSceneHandle scene, const NRCameraDesc* cam)
{
	NRScene* sc = nrSceneResolve(scene);
	if (sc == NULL || cam == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_HANDLE, 0);
	sc->camera = *cam;
	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);
}

NRResult nrSceneSetEnvironment(NRSceneHandle scene, const NRSceneEnvDesc* env)
{
	NRScene* sc = nrSceneResolve(scene);
	if (sc == NULL || env == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_HANDLE, 0);
	sc->env = *env;

	// 环境贴图写入全局集：irradiance / prefiltered / brdfLUT
	NRTexture* irr = nrTextureResolve(env->irradiance);
	if (irr != NULL)
		nrDescriptorWriteImage(sc->global_set, 2, irr->image.view, irr->image.sampler,
							   VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);

	NRTexture* pre = nrTextureResolve(env->prefiltered);
	if (pre != NULL)
		nrDescriptorWriteImage(sc->global_set, 3, pre->image.view, pre->image.sampler,
							   VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);

	NRTexture* lut = nrTextureResolve(env->brdf_lut);
	if (lut != NULL)
		nrDescriptorWriteImage(sc->global_set, 4, lut->image.view, lut->image.sampler,
							   VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);

	return NRR_MakeSuccess(NRR_STEP_NR_Scene, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 阴影矩阵：用相机视锥中心构造覆盖场景的正交投影
// ------------------------------------------------------------
NRMatrix4 nrSceneComputeShadowMatrix(const NRScene* scene)
{
	if (scene == NULL) return nrMat4Identity();

	// 找第一个投射阴影的方向光
	NRFloat3 dir = { 0.0f, -1.0f, 0.0f };
	b32 found = FALSE;
	for (u32 i = 0; i < NR_MAX_LIGHTS && !found; i++)
	{
		const NRSceneLight* l = &scene->lights[i];
		if (l->alive && l->desc.type == NR_LIGHT_DIRECTIONAL && l->desc.cast_shadow)
		{
			dir = nrV3Normalize(l->desc.direction);
			found = TRUE;
		}
	}
	if (!found) return nrMat4Identity();

	// 用场景所有可见对象的世界包围盒确定正交范围，
	// 这样阴影图分辨率不会被空旷区域浪费
	NRAABB scene_box;
	b32 has_box = FALSE;
	for (u32 i = 0; i < NR_MAX_OBJECTS; i++)
	{
		const NRSceneObject* o = &scene->objects[i];
		if (!o->alive || !o->visible || !o->cast_shadow) continue;

		NRAABB wb = nrAABBTransform(&o->local_bounds, &o->world);
		if (!has_box) { scene_box = wb; has_box = TRUE; continue; }
		if (wb.min.x < scene_box.min.x) scene_box.min.x = wb.min.x;
		if (wb.min.y < scene_box.min.y) scene_box.min.y = wb.min.y;
		if (wb.min.z < scene_box.min.z) scene_box.min.z = wb.min.z;
		if (wb.max.x > scene_box.max.x) scene_box.max.x = wb.max.x;
		if (wb.max.y > scene_box.max.y) scene_box.max.y = wb.max.y;
		if (wb.max.z > scene_box.max.z) scene_box.max.z = wb.max.z;
	}
	if (!has_box) return nrMat4Identity();

	NRFloat3 center = nrAABBCenter(&scene_box);
	NRFloat3 ext = nrV3Sub(scene_box.max, scene_box.min);
	f32 radius = nrV3Length(ext) * 0.5f;
	if (radius < 1e-4f) radius = 1.0f;

	NRFloat3 eye = nrV3Sub(center, nrV3Scale(dir, radius * 2.0f));
	NRFloat3 up = (fabsf(dir.y) > 0.99f)
				? (NRFloat3){ 0.0f, 0.0f, 1.0f }
				: (NRFloat3){ 0.0f, 1.0f, 0.0f };

	NRMatrix4 view = nrMat4LookAt(eye, center, up);
	NRMatrix4 proj = nrMat4Ortho(-radius, radius, -radius, radius,
								 0.01f, radius * 4.0f);
	return nrMat4Mul(proj, view);
}

// ------------------------------------------------------------
// 排序比较器
// ------------------------------------------------------------
static int nrCmpOpaque(const void* a, const void* b)
{
	// 升序：同材质同网格聚在一起，最大化减少绑定切换
	f32 ka = ((const NRDrawItem*)a)->sort_key;
	f32 kb = ((const NRDrawItem*)b)->sort_key;
	return (ka < kb) ? -1 : ((ka > kb) ? 1 : 0);
}

static int nrCmpTransparent(const void* a, const void* b)
{
	// 降序：由远及近绘制，保证 alpha 混合结果正确
	f32 ka = ((const NRDrawItem*)a)->sort_key;
	f32 kb = ((const NRDrawItem*)b)->sort_key;
	return (ka > kb) ? -1 : ((ka < kb) ? 1 : 0);
}

// ------------------------------------------------------------
// 构建帧队列
// ------------------------------------------------------------
NRResult nrSceneBuildQueue(NRSceneHandle scene, f32 time)
{
	NRScene* sc = nrSceneResolve(scene);
	if (sc == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_Scene, NRR_CODE_INVALID_HANDLE, 0);

	sc->opaque_count = 0;
	sc->transparent_count = 0;

	NRMatrix4 viewProj = nrMat4Mul(sc->camera.projection, sc->camera.view);
	sc->frustum = nrFrustumFromViewProj(&viewProj);

	NRFloat3 camPos = sc->camera.position;

	for (u32 i = 0; i < NR_MAX_OBJECTS; i++)
	{
		NRSceneObject* o = &sc->objects[i];
		if (!o->alive || !o->visible) continue;

		NRMesh* mesh = nrMeshResolve(o->mesh);
		if (mesh == NULL) continue;
		NRMaterial* mat = nrMaterialResolve(o->material);
		if (mat == NULL) continue;

		// 视锥剔除
		NRAABB wb = nrAABBTransform(&o->local_bounds, &o->world);
		if (!nrFrustumTestAABB(&sc->frustum, &wb)) continue;

		b32 isTransparent = (mat->blend_mode == NR_BLEND_ALPHA) ||
							(mat->blend_mode == NR_BLEND_ADD) ||
							(mat->blend_mode == NR_BLEND_MULTIPLY);

		f32 key;
		if (isTransparent)
		{
			NRFloat3 c = nrAABBCenter(&wb);
			NRFloat3 d = nrV3Sub(c, camPos);
			key = nrV3Dot(d, d);   // 平方距离即可，省一次 sqrt
		}
		else
		{
			// 材质在高位、网格在低位，排序后同材质相邻
			key = (f32)((o->material & 0xFFFFu) * 65536u + (o->mesh & 0xFFFFu));
		}

		// 每个子网格生成一个 draw item
		for (u32 s = 0; s < mesh->submesh_count; s++)
		{
			NRDrawItem* item;
			if (isTransparent)
			{
				if (sc->transparent_count >= NR_MAX_OBJECTS) break;
				item = &sc->transparent[sc->transparent_count++];
			}
			else
			{
				if (sc->opaque_count >= NR_MAX_OBJECTS) break;
				item = &sc->opaque[sc->opaque_count++];
			}
			item->mesh = mesh;
			item->material = mat;
			item->object = o;
			item->index_offset = mesh->submeshes[s].index_offset;
			item->index_count = mesh->submeshes[s].index_count;
			item->sort_key = key;
		}
	}

	if (sc->opaque_count > 1)
		qsort(sc->opaque, sc->opaque_count, sizeof(NRDrawItem), nrCmpOpaque);
	if (sc->transparent_count > 1)
		qsort(sc->transparent, sc->transparent_count, sizeof(NRDrawItem), nrCmpTransparent);

	// ---- 收集光源到 GPU 缓冲 ----
	u32 lightCount = 0;
	for (u32 i = 0; i < NR_MAX_LIGHTS && lightCount < NR_MAX_LIGHTS; i++)
	{
		const NRSceneLight* l = &sc->lights[i];
		if (!l->alive) continue;

		NRLightGPU* g = &sc->cpu_lights[lightCount++];
		g->position_range.x = l->desc.position.x;
		g->position_range.y = l->desc.position.y;
		g->position_range.z = l->desc.position.z;
		g->position_range.w = l->desc.range;

		NRFloat3 d = nrV3Normalize(l->desc.direction);
		g->direction_type.x = d.x;
		g->direction_type.y = d.y;
		g->direction_type.z = d.z;
		g->direction_type.w = (f32)l->desc.type;

		g->color_intensity.x = l->desc.color.x;
		g->color_intensity.y = l->desc.color.y;
		g->color_intensity.z = l->desc.color.z;
		g->color_intensity.w = l->desc.intensity;

		g->cone_bias.x = l->desc.inner_cone_cos;
		g->cone_bias.y = l->desc.outer_cone_cos;
		g->cone_bias.z = l->desc.shadow_bias;
		g->cone_bias.w = l->desc.cast_shadow ? 1.0f : 0.0f;
	}

	if (lightCount > 0)
	{
		NRResult r = nrBufferUpload(&sc->light_ssbo, sc->cpu_lights,
									(u64)lightCount * sizeof(NRLightGPU), 0);
		if (NRR_FAILED(r)) return r;
	}

	// ---- 相机 UBO ----
	sc->cpu_camera.view = sc->camera.view;
	sc->cpu_camera.proj = sc->camera.projection;
	sc->cpu_camera.view_proj = viewProj;
	sc->cpu_camera.light_view_proj = nrSceneComputeShadowMatrix(sc);
	sc->cpu_camera.camera_pos.x = camPos.x;
	sc->cpu_camera.camera_pos.y = camPos.y;
	sc->cpu_camera.camera_pos.z = camPos.z;
	sc->cpu_camera.camera_pos.w = 1.0f;
	sc->cpu_camera.params.x = sc->camera.near_plane;
	sc->cpu_camera.params.y = sc->camera.far_plane;
	sc->cpu_camera.params.z = time;
	sc->cpu_camera.params.w = 1.0f;
	sc->cpu_camera.counts[0] = lightCount;
	sc->cpu_camera.counts[1] = sc->opaque_count;
	sc->cpu_camera.counts[2] = sc->transparent_count;
	sc->cpu_camera.counts[3] = 0;

	return nrBufferUpload(&sc->camera_ubo, &sc->cpu_camera, sizeof(NRCameraUBO), 0);
}
