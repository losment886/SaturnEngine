// Vector3D.h
#ifndef VECTOR3D_H
#define VECTOR3D_H

#include <stdbool.h>
#include <math.h>

// 三维向量结构 (float)
typedef struct {
    float x, y, z;
} Vector3D;

// 静态常量零向量
extern const Vector3D VECTOR3D_EMPTY;

// 构造函数
Vector3D Vector3D_Create(float x, float y, float z);
void Vector3D_Set(Vector3D* v, float x, float y, float z);

// 基本运算 (返回新向量)
Vector3D Vector3D_Add(Vector3D a, Vector3D b);
Vector3D Vector3D_AddScalar(Vector3D v, float s);
Vector3D Vector3D_ScalarAdd(float s, Vector3D v);
Vector3D Vector3D_Sub(Vector3D a, Vector3D b);
Vector3D Vector3D_SubScalar(Vector3D v, float s);
Vector3D Vector3D_ScalarSub(float s, Vector3D v);
Vector3D Vector3D_Mul(Vector3D a, Vector3D b);      // 分量乘
Vector3D Vector3D_MulScalar(Vector3D v, float s);
Vector3D Vector3D_ScalarMul(float s, Vector3D v);
Vector3D Vector3D_Div(Vector3D a, Vector3D b);      // 分量除
Vector3D Vector3D_DivScalar(Vector3D v, float s);
Vector3D Vector3D_ScalarDiv(float s, Vector3D v);

// 比较
bool Vector3D_Equals(Vector3D a, Vector3D b);

// 几何属性
float Vector3D_GetLength(Vector3D v);
float Vector3D_GetLengthSq(Vector3D v);
Vector3D Vector3D_GetNormalized(Vector3D v);
void Vector3D_NormalizeInPlace(Vector3D* v);
float Vector3D_GetDistance(Vector3D a, Vector3D b);
float Vector3D_Dot(Vector3D a, Vector3D b);
Vector3D Vector3D_Cross(Vector3D a, Vector3D b);
float Vector3D_Projecture(Vector3D a, Vector3D b);   // a 投影到 b 上的长度 (b 会被归一化)
float Vector3D_GetAngle(Vector3D a, Vector3D b);      // 返回角度制 (0~180)

// 旋转 (返回新向量)
Vector3D Vector3D_RotateByX(Vector3D v, float radians);
Vector3D Vector3D_RotateByY(Vector3D v, float radians);
Vector3D Vector3D_RotateByZ(Vector3D v, float radians);
Vector3D Vector3D_Rotate(Vector3D v, float radians, Vector3D axis);   // 绕任意轴旋转 (axis 需单位化)
Vector3D Vector3D_RotateAroundTwoPoints(Vector3D point1, Vector3D point2, Vector3D v, float radians);

// 原地旋转
void Vector3D_RotateByXInPlace(Vector3D* v, float radians);
void Vector3D_RotateByYInPlace(Vector3D* v, float radians);
void Vector3D_RotateByZInPlace(Vector3D* v, float radians);
void Vector3D_RotateInPlace(Vector3D* v, float radians, Vector3D axis);
void Vector3D_RotateAroundTwoPointsInPlace(Vector3D* v, Vector3D point1, Vector3D point2, float radians);

// 缩放 (沿主轴)
Vector3D Vector3D_Zoom(Vector3D v, float k);
void Vector3D_ZoomInPlace(Vector3D* v, float k);
// 沿给定轴缩放 (axis 需单位化)
Vector3D Vector3D_ZoomAlongAxis(Vector3D v, float k, Vector3D axis);
void Vector3D_ZoomAlongAxisInPlace(Vector3D* v, float k, Vector3D axis);

// 正交投影 (投影到垂直于 n 的平面，n 需单位化)
Vector3D Vector3D_Projection(Vector3D v, Vector3D n);
void Vector3D_ProjectionInPlace(Vector3D* v, Vector3D n);

// 辅助函数 (角度弧度转换)
static inline float RadiansToDegrees(float rad) { return rad * 180.0f / 3.14159265358979323846f; }
static inline float DegreesToRadians(float deg) { return deg * 3.14159265358979323846f / 180.0f; }

#endif // VECTOR3D_H