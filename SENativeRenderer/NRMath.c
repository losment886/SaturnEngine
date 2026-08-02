#include "NRMath.h"
#include <math.h>

// ============================================================
// NRMath.c
// 列主序矩阵：m[col * 4 + row]
// ============================================================

#define M(mat, r, c) ((mat).m[(c) * 4 + (r)])

// ---------------- 向量 ----------------
NRFloat3 nrV3Add(NRFloat3 a, NRFloat3 b)
{ NRFloat3 r = { a.x + b.x, a.y + b.y, a.z + b.z }; return r; }

NRFloat3 nrV3Sub(NRFloat3 a, NRFloat3 b)
{ NRFloat3 r = { a.x - b.x, a.y - b.y, a.z - b.z }; return r; }

NRFloat3 nrV3Scale(NRFloat3 a, f32 s)
{ NRFloat3 r = { a.x * s, a.y * s, a.z * s }; return r; }

NRFloat3 nrV3Cross(NRFloat3 a, NRFloat3 b)
{
	NRFloat3 r = { a.y * b.z - a.z * b.y,
				   a.z * b.x - a.x * b.z,
				   a.x * b.y - a.y * b.x };
	return r;
}

f32 nrV3Dot(NRFloat3 a, NRFloat3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }

f32 nrV3Length(NRFloat3 a) { return sqrtf(nrV3Dot(a, a)); }

NRFloat3 nrV3Normalize(NRFloat3 a)
{
	f32 len = nrV3Length(a);
	if (len < 1e-6f) { NRFloat3 z = { 0.0f, 0.0f, 0.0f }; return z; }
	return nrV3Scale(a, 1.0f / len);
}

// ---------------- 矩阵 ----------------
NRMatrix4 nrMat4Identity(void)
{
	NRMatrix4 r;
	memset(&r, 0, sizeof(r));
	r.m[0] = r.m[5] = r.m[10] = r.m[15] = 1.0f;
	return r;
}

NRMatrix4 nrMat4Mul(NRMatrix4 a, NRMatrix4 b)
{
	NRMatrix4 r;
	for (int c = 0; c < 4; c++)
		for (int rw = 0; rw < 4; rw++)
		{
			f32 sum = 0.0f;
			for (int k = 0; k < 4; k++)
				sum += M(a, rw, k) * M(b, k, c);
			M(r, rw, c) = sum;
		}
	return r;
}

NRFloat3 nrMat4MulPoint(const NRMatrix4* m, NRFloat3 p)
{
	NRFloat3 r;
	r.x = m->m[0] * p.x + m->m[4] * p.y + m->m[8]  * p.z + m->m[12];
	r.y = m->m[1] * p.x + m->m[5] * p.y + m->m[9]  * p.z + m->m[13];
	r.z = m->m[2] * p.x + m->m[6] * p.y + m->m[10] * p.z + m->m[14];
	return r;
}

NRMatrix4 nrMat4Transpose(NRMatrix4 m)
{
	NRMatrix4 r;
	for (int c = 0; c < 4; c++)
		for (int rw = 0; rw < 4; rw++)
			M(r, rw, c) = M(m, c, rw);
	return r;
}

NRMatrix4 nrMat4Inverse(NRMatrix4 mat)
{
	const f32* m = mat.m;
	f32 inv[16];

	inv[0]  =  m[5]*m[10]*m[15] - m[5]*m[11]*m[14] - m[9]*m[6]*m[15]
			 + m[9]*m[7]*m[14] + m[13]*m[6]*m[11] - m[13]*m[7]*m[10];
	inv[4]  = -m[4]*m[10]*m[15] + m[4]*m[11]*m[14] + m[8]*m[6]*m[15]
			 - m[8]*m[7]*m[14] - m[12]*m[6]*m[11] + m[12]*m[7]*m[10];
	inv[8]  =  m[4]*m[9]*m[15] - m[4]*m[11]*m[13] - m[8]*m[5]*m[15]
			 + m[8]*m[7]*m[13] + m[12]*m[5]*m[11] - m[12]*m[7]*m[9];
	inv[12] = -m[4]*m[9]*m[14] + m[4]*m[10]*m[13] + m[8]*m[5]*m[14]
			 - m[8]*m[6]*m[13] - m[12]*m[5]*m[10] + m[12]*m[6]*m[9];
	inv[1]  = -m[1]*m[10]*m[15] + m[1]*m[11]*m[14] + m[9]*m[2]*m[15]
			 - m[9]*m[3]*m[14] - m[13]*m[2]*m[11] + m[13]*m[3]*m[10];
	inv[5]  =  m[0]*m[10]*m[15] - m[0]*m[11]*m[14] - m[8]*m[2]*m[15]
			 + m[8]*m[3]*m[14] + m[12]*m[2]*m[11] - m[12]*m[3]*m[10];
	inv[9]  = -m[0]*m[9]*m[15] + m[0]*m[11]*m[13] + m[8]*m[1]*m[15]
			 - m[8]*m[3]*m[13] - m[12]*m[1]*m[11] + m[12]*m[3]*m[9];
	inv[13] =  m[0]*m[9]*m[14] - m[0]*m[10]*m[13] - m[8]*m[1]*m[14]
			 + m[8]*m[2]*m[13] + m[12]*m[1]*m[10] - m[12]*m[2]*m[9];
	inv[2]  =  m[1]*m[6]*m[15] - m[1]*m[7]*m[14] - m[5]*m[2]*m[15]
			 + m[5]*m[3]*m[14] + m[13]*m[2]*m[7] - m[13]*m[3]*m[6];
	inv[6]  = -m[0]*m[6]*m[15] + m[0]*m[7]*m[14] + m[4]*m[2]*m[15]
			 - m[4]*m[3]*m[14] - m[12]*m[2]*m[7] + m[12]*m[3]*m[6];
	inv[10] =  m[0]*m[5]*m[15] - m[0]*m[7]*m[13] - m[4]*m[1]*m[15]
			 + m[4]*m[3]*m[13] + m[12]*m[1]*m[7] - m[12]*m[3]*m[5];
	inv[14] = -m[0]*m[5]*m[14] + m[0]*m[6]*m[13] + m[4]*m[1]*m[14]
			 - m[4]*m[2]*m[13] - m[12]*m[1]*m[6] + m[12]*m[2]*m[5];
	inv[3]  = -m[1]*m[6]*m[11] + m[1]*m[7]*m[10] + m[5]*m[2]*m[11]
			 - m[5]*m[3]*m[10] - m[9]*m[2]*m[7] + m[9]*m[3]*m[6];
	inv[7]  =  m[0]*m[6]*m[11] - m[0]*m[7]*m[10] - m[4]*m[2]*m[11]
			 + m[4]*m[3]*m[10] + m[8]*m[2]*m[7] - m[8]*m[3]*m[6];
	inv[11] = -m[0]*m[5]*m[11] + m[0]*m[7]*m[9] + m[4]*m[1]*m[11]
			 - m[4]*m[3]*m[9] - m[8]*m[1]*m[7] + m[8]*m[3]*m[5];
	inv[15] =  m[0]*m[5]*m[10] - m[0]*m[6]*m[9] - m[4]*m[1]*m[10]
			 + m[4]*m[2]*m[9] + m[8]*m[1]*m[6] - m[8]*m[2]*m[5];

	f32 det = m[0]*inv[0] + m[1]*inv[4] + m[2]*inv[8] + m[3]*inv[12];
	if (fabsf(det) < 1e-9f) return nrMat4Identity();   // 奇异矩阵回落为单位阵

	det = 1.0f / det;
	NRMatrix4 out;
	for (int i = 0; i < 16; i++) out.m[i] = inv[i] * det;
	return out;
}

NRMatrix4 nrMat4LookAt(NRFloat3 eye, NRFloat3 center, NRFloat3 up)
{
	NRFloat3 f = nrV3Normalize(nrV3Sub(center, eye));
	NRFloat3 s = nrV3Normalize(nrV3Cross(f, up));
	NRFloat3 u = nrV3Cross(s, f);

	NRMatrix4 r = nrMat4Identity();
	M(r, 0, 0) = s.x; M(r, 0, 1) = s.y; M(r, 0, 2) = s.z;
	M(r, 1, 0) = u.x; M(r, 1, 1) = u.y; M(r, 1, 2) = u.z;
	M(r, 2, 0) = -f.x; M(r, 2, 1) = -f.y; M(r, 2, 2) = -f.z;
	M(r, 0, 3) = -nrV3Dot(s, eye);
	M(r, 1, 3) = -nrV3Dot(u, eye);
	M(r, 2, 3) =  nrV3Dot(f, eye);
	return r;
}

NRMatrix4 nrMat4Perspective(f32 fov_y_radians, f32 aspect, f32 near_p, f32 far_p)
{
	NRMatrix4 r;
	memset(&r, 0, sizeof(r));

	f32 t = tanf(fov_y_radians * 0.5f);
	if (t < 1e-6f || aspect < 1e-6f) return nrMat4Identity();

	M(r, 0, 0) = 1.0f / (aspect * t);
	// Vulkan NDC 的 Y 轴向下，故这里取负
	M(r, 1, 1) = -1.0f / t;
	M(r, 2, 2) = far_p / (near_p - far_p);
	M(r, 2, 3) = (far_p * near_p) / (near_p - far_p);
	M(r, 3, 2) = -1.0f;
	return r;
}

NRMatrix4 nrMat4Ortho(f32 left, f32 right, f32 bottom, f32 top, f32 near_p, f32 far_p)
{
	NRMatrix4 r = nrMat4Identity();
	M(r, 0, 0) = 2.0f / (right - left);
	M(r, 1, 1) = -2.0f / (top - bottom);          // Y 向下
	M(r, 2, 2) = 1.0f / (near_p - far_p);         // 深度 [0,1]
	M(r, 0, 3) = -(right + left) / (right - left);
	M(r, 1, 3) =  (top + bottom) / (top - bottom);
	M(r, 2, 3) = near_p / (near_p - far_p);
	return r;
}

NRMatrix4 nrMat4FromTransform(const NRTransform3* t)
{
	if (t == NULL) return nrMat4Identity();

	f32 x = t->rotation.x, y = t->rotation.y, z = t->rotation.z, w = t->rotation.w;
	f32 n = x*x + y*y + z*z + w*w;
	if (n < 1e-9f) return nrMat4Identity();
	f32 s = 2.0f / n;

	f32 xs = x*s, ys = y*s, zs = z*s;
	f32 wx = w*xs, wy = w*ys, wz = w*zs;
	f32 xx = x*xs, xy = x*ys, xz = x*zs;
	f32 yy = y*ys, yz = y*zs, zz = z*zs;

	NRMatrix4 r = nrMat4Identity();
	M(r, 0, 0) = (1.0f - (yy + zz)) * t->scale.x;
	M(r, 1, 0) = (xy + wz) * t->scale.x;
	M(r, 2, 0) = (xz - wy) * t->scale.x;

	M(r, 0, 1) = (xy - wz) * t->scale.y;
	M(r, 1, 1) = (1.0f - (xx + zz)) * t->scale.y;
	M(r, 2, 1) = (yz + wx) * t->scale.y;

	M(r, 0, 2) = (xz + wy) * t->scale.z;
	M(r, 1, 2) = (yz - wx) * t->scale.z;
	M(r, 2, 2) = (1.0f - (xx + yy)) * t->scale.z;

	M(r, 0, 3) = t->position.x;
	M(r, 1, 3) = t->position.y;
	M(r, 2, 3) = t->position.z;
	return r;
}

// ---------------- 视锥 ----------------
static NRFloat4 nrPlaneNormalize(NRFloat4 p)
{
	f32 len = sqrtf(p.x*p.x + p.y*p.y + p.z*p.z);
	if (len < 1e-6f) return p;
	NRFloat4 r = { p.x/len, p.y/len, p.z/len, p.w/len };
	return r;
}

NRFrustum nrFrustumFromViewProj(const NRMatrix4* vp)
{
	NRFrustum f;
	if (vp == NULL) { memset(&f, 0, sizeof(f)); return f; }

	// Gribb-Hartmann：行 i 记作 R_i（列主序下 R_i[c] = m[c*4 + i]）
	#define ROW(i, c) (vp->m[(c) * 4 + (i)])

	// left  = R3 + R0
	f.planes[0].x = ROW(3,0) + ROW(0,0); f.planes[0].y = ROW(3,1) + ROW(0,1);
	f.planes[0].z = ROW(3,2) + ROW(0,2); f.planes[0].w = ROW(3,3) + ROW(0,3);
	// right = R3 - R0
	f.planes[1].x = ROW(3,0) - ROW(0,0); f.planes[1].y = ROW(3,1) - ROW(0,1);
	f.planes[1].z = ROW(3,2) - ROW(0,2); f.planes[1].w = ROW(3,3) - ROW(0,3);
	// bottom = R3 + R1
	f.planes[2].x = ROW(3,0) + ROW(1,0); f.planes[2].y = ROW(3,1) + ROW(1,1);
	f.planes[2].z = ROW(3,2) + ROW(1,2); f.planes[2].w = ROW(3,3) + ROW(1,3);
	// top = R3 - R1
	f.planes[3].x = ROW(3,0) - ROW(1,0); f.planes[3].y = ROW(3,1) - ROW(1,1);
	f.planes[3].z = ROW(3,2) - ROW(1,2); f.planes[3].w = ROW(3,3) - ROW(1,3);
	// near = R2（Vulkan 深度范围 [0,1]，近平面不是 R3+R2）
	f.planes[4].x = ROW(2,0); f.planes[4].y = ROW(2,1);
	f.planes[4].z = ROW(2,2); f.planes[4].w = ROW(2,3);
	// far = R3 - R2
	f.planes[5].x = ROW(3,0) - ROW(2,0); f.planes[5].y = ROW(3,1) - ROW(2,1);
	f.planes[5].z = ROW(3,2) - ROW(2,2); f.planes[5].w = ROW(3,3) - ROW(2,3);

	#undef ROW

	for (int i = 0; i < 6; i++) f.planes[i] = nrPlaneNormalize(f.planes[i]);
	return f;
}

b32 nrFrustumTestAABB(const NRFrustum* f, const NRAABB* box)
{
	if (f == NULL || box == NULL) return TRUE;

	for (int i = 0; i < 6; i++)
	{
		NRFloat4 p = f->planes[i];
		// 取沿平面法线方向最远的顶点；若它都在平面负侧，则整个盒子在外
		NRFloat3 pv;
		pv.x = (p.x >= 0.0f) ? box->max.x : box->min.x;
		pv.y = (p.y >= 0.0f) ? box->max.y : box->min.y;
		pv.z = (p.z >= 0.0f) ? box->max.z : box->min.z;

		if (p.x * pv.x + p.y * pv.y + p.z * pv.z + p.w < 0.0f)
			return FALSE;
	}
	return TRUE;
}

NRAABB nrAABBTransform(const NRAABB* box, const NRMatrix4* world)
{
	NRAABB out;
	if (box == NULL || world == NULL)
	{
		memset(&out, 0, sizeof(out));
		return out;
	}

	// 变换 8 个角点后重新求轴对齐范围
	NRFloat3 corners[8] = {
		{ box->min.x, box->min.y, box->min.z },
		{ box->max.x, box->min.y, box->min.z },
		{ box->min.x, box->max.y, box->min.z },
		{ box->max.x, box->max.y, box->min.z },
		{ box->min.x, box->min.y, box->max.z },
		{ box->max.x, box->min.y, box->max.z },
		{ box->min.x, box->max.y, box->max.z },
		{ box->max.x, box->max.y, box->max.z },
	};

	NRFloat3 first = nrMat4MulPoint(world, corners[0]);
	out.min = first;
	out.max = first;

	for (int i = 1; i < 8; i++)
	{
		NRFloat3 c = nrMat4MulPoint(world, corners[i]);
		if (c.x < out.min.x) out.min.x = c.x;
		if (c.y < out.min.y) out.min.y = c.y;
		if (c.z < out.min.z) out.min.z = c.z;
		if (c.x > out.max.x) out.max.x = c.x;
		if (c.y > out.max.y) out.max.y = c.y;
		if (c.z > out.max.z) out.max.z = c.z;
	}
	return out;
}

NRFloat3 nrAABBCenter(const NRAABB* box)
{
	NRFloat3 c = { (box->min.x + box->max.x) * 0.5f,
				   (box->min.y + box->max.y) * 0.5f,
				   (box->min.z + box->max.z) * 0.5f };
	return c;
}
