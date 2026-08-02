#pragma once

// ============================================================
// NRMath.h
// 面向 NRApi 的矩阵/视锥数学工具
//
// 为什么不放进 NRVec.h：那份文件是旧的 2D/3D 向量结构，
// 不含 NRMatrix4，且被既有代码依赖，避免污染。
//
// 约定：NRMatrix4.m 为 16 个 f32，列主序（column-major），
// 与 GLSL mat4 内存布局一致，可直接 memcpy 进 UBO。
// 投影矩阵采用 Vulkan 深度范围 [0,1] 且 Y 轴向下。
// ============================================================

#include "NRApi.h"

SE_EXTERN_C_BEGIN

// ---------------- 向量 ----------------
NRFloat3 nrV3Add(NRFloat3 a, NRFloat3 b);
NRFloat3 nrV3Sub(NRFloat3 a, NRFloat3 b);
NRFloat3 nrV3Scale(NRFloat3 a, f32 s);
NRFloat3 nrV3Cross(NRFloat3 a, NRFloat3 b);
f32      nrV3Dot(NRFloat3 a, NRFloat3 b);
f32      nrV3Length(NRFloat3 a);
NRFloat3 nrV3Normalize(NRFloat3 a);

// ---------------- 矩阵 ----------------
NRMatrix4 nrMat4Identity(void);
NRMatrix4 nrMat4Mul(NRMatrix4 a, NRMatrix4 b);         // 返回 a * b
NRFloat3  nrMat4MulPoint(const NRMatrix4* m, NRFloat3 p);
NRMatrix4 nrMat4Inverse(NRMatrix4 m);
NRMatrix4 nrMat4Transpose(NRMatrix4 m);

NRMatrix4 nrMat4LookAt(NRFloat3 eye, NRFloat3 center, NRFloat3 up);
// Vulkan 风格透视：深度 [0,1]，已翻转 Y
NRMatrix4 nrMat4Perspective(f32 fov_y_radians, f32 aspect, f32 near_p, f32 far_p);
NRMatrix4 nrMat4Ortho(f32 left, f32 right, f32 bottom, f32 top, f32 near_p, f32 far_p);
NRMatrix4 nrMat4FromTransform(const NRTransform3* t);

// ---------------- 包围盒 / 视锥 ----------------
typedef struct NRAABB
{
	NRFloat3 min;
	NRFloat3 max;
} NRAABB;

// 视锥 6 平面，格式 (a,b,c,d) 满足 ax+by+cz+d=0，法线指向视锥内部
typedef struct NRFrustum
{
	NRFloat4 planes[6];
} NRFrustum;

// 从 viewProj 提取视锥平面（Gribb-Hartmann）
NRFrustum nrFrustumFromViewProj(const NRMatrix4* view_proj);
// AABB 与视锥求交；完全在外返回 FALSE
b32       nrFrustumTestAABB(const NRFrustum* f, const NRAABB* box);
// 用世界矩阵变换 AABB，返回变换后的轴对齐包围盒
NRAABB    nrAABBTransform(const NRAABB* box, const NRMatrix4* world);
NRFloat3  nrAABBCenter(const NRAABB* box);

SE_EXTERN_C_END
