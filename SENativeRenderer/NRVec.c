#include "vec.h"

const Vector3D VECTOR3D_EMPTY = {0.0f, 0.0f, 0.0f};

Vector3D Vector3D_Create(float x, float y, float z) {
    Vector3D v = {x, y, z};
    return v;
}

void Vector3D_Set(Vector3D* v, float x, float y, float z) {
    if (v) { v->x = x; v->y = y; v->z = z; }
}

// ----- 基本运算 -----
Vector3D Vector3D_Add(Vector3D a, Vector3D b) {
    return Vector3D_Create(a.x + b.x, a.y + b.y, a.z + b.z);
}
Vector3D Vector3D_AddScalar(Vector3D v, float s) {
    return Vector3D_Create(v.x + s, v.y + s, v.z + s);
}
Vector3D Vector3D_ScalarAdd(float s, Vector3D v) {
    return Vector3D_AddScalar(v, s);
}
Vector3D Vector3D_Sub(Vector3D a, Vector3D b) {
    return Vector3D_Create(a.x - b.x, a.y - b.y, a.z - b.z);
}
Vector3D Vector3D_SubScalar(Vector3D v, float s) {
    return Vector3D_Create(v.x - s, v.y - s, v.z - s);
}
Vector3D Vector3D_ScalarSub(float s, Vector3D v) {
    return Vector3D_Create(s - v.x, s - v.y, s - v.z);
}
Vector3D Vector3D_Mul(Vector3D a, Vector3D b) {
    return Vector3D_Create(a.x * b.x, a.y * b.y, a.z * b.z);
}
Vector3D Vector3D_MulScalar(Vector3D v, float s) {
    return Vector3D_Create(v.x * s, v.y * s, v.z * s);
}
Vector3D Vector3D_ScalarMul(float s, Vector3D v) {
    return Vector3D_MulScalar(v, s);
}
Vector3D Vector3D_Div(Vector3D a, Vector3D b) {
    return Vector3D_Create(a.x / b.x, a.y / b.y, a.z / b.z);
}
Vector3D Vector3D_DivScalar(Vector3D v, float s) {
    return Vector3D_Create(v.x / s, v.y / s, v.z / s);
}
Vector3D Vector3D_ScalarDiv(float s, Vector3D v) {
    return Vector3D_Create(s / v.x, s / v.y, s / v.z);
}

bool Vector3D_Equals(Vector3D a, Vector3D b) {
    return (a.x == b.x) && (a.y == b.y) && (a.z == b.z);
}

float Vector3D_GetLength(Vector3D v) {
    return sqrtf(v.x * v.x + v.y * v.y + v.z * v.z);
}
float Vector3D_GetLengthSq(Vector3D v) {
    return v.x * v.x + v.y * v.y + v.z * v.z;
}

Vector3D Vector3D_GetNormalized(Vector3D v) {
    float len = Vector3D_GetLength(v);
    if (len == 0.0f) return VECTOR3D_EMPTY;
    return Vector3D_DivScalar(v, len);
}
void Vector3D_NormalizeInPlace(Vector3D* v) {
    if (!v) return;
    float len = Vector3D_GetLength(*v);
    if (len != 0.0f) {
        v->x /= len;
        v->y /= len;
        v->z /= len;
    }
}

float Vector3D_GetDistance(Vector3D a, Vector3D b) {
    Vector3D diff = Vector3D_Sub(a, b);
    return Vector3D_GetLength(diff);
}

float Vector3D_Dot(Vector3D a, Vector3D b) {
    return a.x * b.x + a.y * b.y + a.z * b.z;
}

Vector3D Vector3D_Cross(Vector3D a, Vector3D b) {
    return Vector3D_Create(
        a.y * b.z - a.z * b.y,
        a.z * b.x - a.x * b.z,
        a.x * b.y - a.y * b.x
    );
}

float Vector3D_Projecture(Vector3D a, Vector3D b) {
    Vector3D normB = Vector3D_GetNormalized(b);
    return Vector3D_Dot(normB, a);
}

float Vector3D_GetAngle(Vector3D a, Vector3D b) {
    float dot = Vector3D_Dot(a, b);
    float lenA = Vector3D_GetLength(a);
    float lenB = Vector3D_GetLength(b);
    if (lenA == 0.0f || lenB == 0.0f) return 0.0f;
    float cosVal = dot / (lenA * lenB);
    if (cosVal > 1.0f) cosVal = 1.0f;
    if (cosVal < -1.0f) cosVal = -1.0f;
    return RadiansToDegrees(acosf(cosVal));
}

// ----- 旋转函数 (使用 float 三角函数) -----
Vector3D Vector3D_RotateByX(Vector3D v, float radians) {
    float c = cosf(radians);
    float s = sinf(radians);
    return Vector3D_Create(
        v.x,
        v.y * c - v.z * s,
        v.y * s + v.z * c
    );
}
void Vector3D_RotateByXInPlace(Vector3D* v, float radians) {
    if (!v) return;
    float c = cosf(radians);
    float s = sinf(radians);
    float ny = v->y * c - v->z * s;
    float nz = v->y * s + v->z * c;
    v->y = ny;
    v->z = nz;
}

Vector3D Vector3D_RotateByY(Vector3D v, float radians) {
    float c = cosf(radians);
    float s = sinf(radians);
    return Vector3D_Create(
        v.x * c + v.z * s,
        v.y,
        v.z * c - v.x * s
    );
}
void Vector3D_RotateByYInPlace(Vector3D* v, float radians) {
    if (!v) return;
    float c = cosf(radians);
    float s = sinf(radians);
    float nx = v->x * c + v->z * s;
    float nz = v->z * c - v->x * s;
    v->x = nx;
    v->z = nz;
}

Vector3D Vector3D_RotateByZ(Vector3D v, float radians) {
    float c = cosf(radians);
    float s = sinf(radians);
    return Vector3D_Create(
        v.x * c - v.y * s,
        v.y * c + v.x * s,
        v.z
    );
}
void Vector3D_RotateByZInPlace(Vector3D* v, float radians) {
    if (!v) return;
    float c = cosf(radians);
    float s = sinf(radians);
    float nx = v->x * c - v->y * s;
    float ny = v->y * c + v->x * s;
    v->x = nx;
    v->y = ny;
}

Vector3D Vector3D_Rotate(Vector3D v, float radians, Vector3D axis) {
    float c = cosf(radians);
    float s = sinf(radians);
    float t = 1.0f - c;
    float nx = v.x * (axis.x * axis.x * t + c)
              + v.y * (axis.x * axis.y * t - axis.z * s)
              + v.z * (axis.x * axis.z * t + axis.y * s);
    float ny = v.x * (axis.y * axis.x * t + axis.z * s)
              + v.y * (axis.y * axis.y * t + c)
              + v.z * (axis.y * axis.z * t - axis.x * s);
    float nz = v.x * (axis.z * axis.x * t - axis.y * s)
              + v.y * (axis.z * axis.y * t + axis.x * s)
              + v.z * (axis.z * axis.z * t + c);
    return Vector3D_Create(nx, ny, nz);
}
void Vector3D_RotateInPlace(Vector3D* v, float radians, Vector3D axis) {
    if (!v) return;
    Vector3D res = Vector3D_Rotate(*v, radians, axis);
    *v = res;
}

Vector3D Vector3D_RotateAroundTwoPoints(Vector3D point1, Vector3D point2, Vector3D v, float radians) {
    Vector3D axis = Vector3D_GetNormalized(Vector3D_Sub(point2, point1));
    Vector3D tv = Vector3D_Sub(v, point1);
    Vector3D rv = Vector3D_Rotate(tv, radians, axis);
    return Vector3D_Add(rv, point1);
}
void Vector3D_RotateAroundTwoPointsInPlace(Vector3D* v, Vector3D point1, Vector3D point2, float radians) {
    if (!v) return;
    Vector3D res = Vector3D_RotateAroundTwoPoints(point1, point2, *v, radians);
    *v = res;
}

// ----- 缩放 -----
Vector3D Vector3D_Zoom(Vector3D v, float k) {
    return Vector3D_MulScalar(v, k);
}
void Vector3D_ZoomInPlace(Vector3D* v, float k) {
    if (!v) return;
    v->x *= k;
    v->y *= k;
    v->z *= k;
}

Vector3D Vector3D_ZoomAlongAxis(Vector3D v, float k, Vector3D axis) {
    float t = k - 1.0f;
    float nx = v.x * (axis.x * axis.x * t + 1.0f)
              + v.y * (axis.y * axis.x * t)
              + v.z * (axis.z * axis.x * t);
    float ny = v.x * (axis.x * axis.y * t)
              + v.y * (axis.y * axis.y * t + 1.0f)
              + v.z * (axis.z * axis.y * t);
    float nz = v.x * (axis.x * axis.z * t)
              + v.y * (axis.y * axis.z * t)
              + v.z * (axis.z * axis.z * t + 1.0f);
    return Vector3D_Create(nx, ny, nz);
}
void Vector3D_ZoomAlongAxisInPlace(Vector3D* v, float k, Vector3D axis) {
    if (!v) return;
    Vector3D res = Vector3D_ZoomAlongAxis(*v, k, axis);
    *v = res;
}

// 正交投影 (投影到垂直于 n 的平面)
Vector3D Vector3D_Projection(Vector3D v, Vector3D n) {
    float nx = v.x * (1.0f - n.x * n.x) + v.y * (-n.x * n.y) + v.z * (-n.x * n.z);
    float ny = v.x * (-n.x * n.y) + v.y * (1.0f - n.y * n.y) + v.z * (-n.z * n.y);
    float nz = v.x * (-n.x * n.z) + v.y * (-n.z * n.y) + v.z * (1.0f - n.z * n.z);
    return Vector3D_Create(nx, ny, nz);
}
void Vector3D_ProjectionInPlace(Vector3D* v, Vector3D n) {
    if (!v) return;
    Vector3D res = Vector3D_Projection(*v, n);
    *v = res;
}