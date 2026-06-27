#pragma once

#include "NRDefine.h"

typedef struct {
    double x, y, z;
} Vector3D;

// 静态常量
extern const Vector3D VECTOR3D_EMPTY;       // 零向量
extern const Vector3D VECTOR3D_ONE;         // (1,1,1)
extern const Vector3D VECTOR3D_UNIT_X;      // (1,0,0)
extern const Vector3D VECTOR3D_UNIT_Y;      // (0,1,0)
extern const Vector3D VECTOR3D_UNIT_Z;      // (0,0,1)

// ============================================================
// 4x4 矩阵（列主序，与 HLSL 一致）
// ============================================================
typedef struct {
    f32 m[4][4]; // [列][行]
} Matrix4x4;

// ============================================================
// Uniform Buffer 数据（与 HLSL 的 cbuffer MVPBuffer 对应）
// ============================================================
typedef struct {
    Matrix4x4 mvp;
} MVPBuffer;

// ============================================================
// 辅助函数 (角度弧度转换)
// ============================================================
static inline double RadiansToDegrees(double rad) { return rad * 180.0 / 3.14159265358979323846; }
static inline double DegreesToRadians(double deg) { return deg * 3.14159265358979323846 / 180.0; }

// ============================================================
// 矩阵数学函数
// ============================================================

// 单位矩阵
static inline Matrix4x4 Matrix4x4_Identity(void) {
    Matrix4x4 m = {0};
    m.m[0][0] = 1.0f;
    m.m[1][1] = 1.0f;
    m.m[2][2] = 1.0f;
    m.m[3][3] = 1.0f;
    return m;
}

// 矩阵乘法 (a * b)
static inline Matrix4x4 Matrix4x4_Mul(Matrix4x4 a, Matrix4x4 b) {
    Matrix4x4 result = {0};
    for (int col = 0; col < 4; col++) {
        for (int row = 0; row < 4; row++) {
            result.m[col][row] =
                a.m[0][row] * b.m[col][0] +
                a.m[1][row] * b.m[col][1] +
                a.m[2][row] * b.m[col][2] +
                a.m[3][row] * b.m[col][3];
        }
    }
    return result;
}

// 透视投影矩阵 (列主序)
// fovRadians: 垂直视场角（弧度）
// aspect: 宽高比
// nearZ: 近裁剪面
// farZ: 远裁剪面
static inline Matrix4x4 Matrix4x4_Perspective(f32 fovRadians, f32 aspect, f32 nearZ, f32 farZ) {
    Matrix4x4 m = {0};
    f32 tanHalfFov = tanf(fovRadians * 0.5f);
    f32 range = nearZ - farZ;
    
    m.m[0][0] = 1.0f / (tanHalfFov * aspect);
    m.m[1][1] = 1.0f / tanHalfFov;
    m.m[2][2] = (-nearZ - farZ) / range;
    m.m[2][3] = 1.0f;
    m.m[3][2] = 2.0f * farZ * nearZ / range;
    
    return m;
}



// 平移矩阵 (列主序)
static inline Matrix4x4 Matrix4x4_Translate(f32 x, f32 y, f32 z) {
    Matrix4x4 m = Matrix4x4_Identity();
    m.m[3][0] = x;
    m.m[3][1] = y;
    m.m[3][2] = z;
    return m;
}

// 缩放矩阵 (列主序)
static inline Matrix4x4 Matrix4x4_Scale(f32 x, f32 y, f32 z) {
    Matrix4x4 m = {0};
    m.m[0][0] = x;
    m.m[1][1] = y;
    m.m[2][2] = z;
    m.m[3][3] = 1.0f;
    return m;
}

// 绕 Y 轴旋转矩阵 (列主序)
static inline Matrix4x4 Matrix4x4_RotateY(f32 radians) {
    f32 c = cosf(radians);
    f32 s = sinf(radians);
    Matrix4x4 m = Matrix4x4_Identity();
    m.m[0][0] = c;   m.m[0][2] = -s;
    m.m[2][0] = s;   m.m[2][2] = c;
    return m;
}

// ============================================================
// 函数声明
// ============================================================

// Vector3D 函数声明
Vector3D Vector3D_Create(double x, double y, double z);
void Vector3D_Set(Vector3D* v, double x, double y, double z);
Vector3D Vector3D_Add(Vector3D a, Vector3D b);
Vector3D Vector3D_AddScalar(Vector3D v, double s);
Vector3D Vector3D_ScalarAdd(double s, Vector3D v);
Vector3D Vector3D_Sub(Vector3D a, Vector3D b);
Vector3D Vector3D_SubScalar(Vector3D v, double s);
Vector3D Vector3D_ScalarSub(double s, Vector3D v);
Vector3D Vector3D_Mul(Vector3D a, Vector3D b);
Vector3D Vector3D_MulScalar(Vector3D v, double s);
Vector3D Vector3D_ScalarMul(double s, Vector3D v);
Vector3D Vector3D_Div(Vector3D a, Vector3D b);
Vector3D Vector3D_DivScalar(Vector3D v, double s);
Vector3D Vector3D_ScalarDiv(double s, Vector3D v);
Vector3D Vector3D_Negate(Vector3D v);
bool Vector3D_Equals(Vector3D a, Vector3D b);
bool Vector3D_EqualsEpsilon(Vector3D a, Vector3D b, double epsilon);
double Vector3D_GetLength(Vector3D v);
double Vector3D_GetLengthSq(Vector3D v);
Vector3D Vector3D_GetNormalized(Vector3D v);
void Vector3D_NormalizeInPlace(Vector3D* v);
double Vector3D_GetDistance(Vector3D a, Vector3D b);
double Vector3D_GetDistanceSq(Vector3D a, Vector3D b);
double Vector3D_Dot(Vector3D a, Vector3D b);
Vector3D Vector3D_Cross(Vector3D a, Vector3D b);
double Vector3D_ProjectLength(Vector3D a, Vector3D b);
double Vector3D_GetAngle(Vector3D a, Vector3D b);
Vector3D Vector3D_RotateByX(Vector3D v, double radians);
Vector3D Vector3D_RotateByY(Vector3D v, double radians);
Vector3D Vector3D_RotateByZ(Vector3D v, double radians);
Vector3D Vector3D_Rotate(Vector3D v, double radians, Vector3D axis);
Vector3D Vector3D_RotateAroundTwoPoints(Vector3D point1, Vector3D point2, Vector3D v, double radians);
void Vector3D_RotateByXInPlace(Vector3D* v, double radians);
void Vector3D_RotateByYInPlace(Vector3D* v, double radians);
void Vector3D_RotateByZInPlace(Vector3D* v, double radians);
void Vector3D_RotateInPlace(Vector3D* v, double radians, Vector3D axis);
void Vector3D_RotateAroundTwoPointsInPlace(Vector3D* v, Vector3D point1, Vector3D point2, double radians);
Vector3D Vector3D_Zoom(Vector3D v, double k);
void Vector3D_ZoomInPlace(Vector3D* v, double k);
Vector3D Vector3D_ZoomAlongAxis(Vector3D v, double k, Vector3D axis);
void Vector3D_ZoomAlongAxisInPlace(Vector3D* v, double k, Vector3D axis);
Vector3D Vector3D_Projection(Vector3D v, Vector3D n);
void Vector3D_ProjectionInPlace(Vector3D* v, Vector3D n);
Vector3D Vector3D_Reflect(Vector3D v, Vector3D n);
Vector3D Vector3D_Refract(Vector3D v, Vector3D n, double eta);
Vector3D Vector3D_Lerp(Vector3D a, Vector3D b, double t);
Vector3D Vector3D_NLerp(Vector3D a, Vector3D b, double t);
Vector3D Vector3D_Slerp(Vector3D a, Vector3D b, double t);
Vector3D Vector3D_Min(Vector3D a, Vector3D b);
Vector3D Vector3D_Max(Vector3D a, Vector3D b);
Vector3D Vector3D_Clamp(Vector3D v, Vector3D min, Vector3D max);
Vector3D Vector3D_ClampLength(Vector3D v, double minLen, double maxLen);
void Vector3D_Print(Vector3D v, const char* label);
int Vector3D_ToString(Vector3D v, char* buffer, size_t bufSize);

// 兼容旧名称 (已废弃，请使用 Vector3D_ProjectLength)
static inline double Vector3D_Projecture(Vector3D a, Vector3D b) {
    return Vector3D_ProjectLength(a, b);
}


// 观察矩阵 (LookAt, 列主序)
// eye: 相机位置
// center: 目标点
// up: 上方向
static inline Matrix4x4 Matrix4x4_LookAt(Vector3D eye, Vector3D center, Vector3D up) {
	Vector3D f = Vector3D_GetNormalized(Vector3D_Sub(center, eye)); // 前方向
	Vector3D s = Vector3D_GetNormalized(Vector3D_Cross(f, up));     // 右方向
	Vector3D u = Vector3D_Cross(s, f);                               // 上方向
    
	Matrix4x4 m = {0};
	m.m[0][0] = (f32)s.x;   m.m[0][1] = (f32)u.x;   m.m[0][2] = (f32)(-f.x);  m.m[0][3] = 0.0f;
	m.m[1][0] = (f32)s.y;   m.m[1][1] = (f32)u.y;   m.m[1][2] = (f32)(-f.y);  m.m[1][3] = 0.0f;
	m.m[2][0] = (f32)s.z;   m.m[2][1] = (f32)u.z;   m.m[2][2] = (f32)(-f.z);  m.m[2][3] = 0.0f;
	m.m[3][0] = (f32)(-Vector3D_Dot(s, eye));
	m.m[3][1] = (f32)(-Vector3D_Dot(u, eye));
	m.m[3][2] = (f32)(Vector3D_Dot(f, eye));
	m.m[3][3] = 1.0f;
    
	return m;
}
