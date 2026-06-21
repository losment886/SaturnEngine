#include "NRVec.h"

const Vector3D VECTOR3D_EMPTY  = {0.0, 0.0, 0.0};
const Vector3D VECTOR3D_ONE    = {1.0, 1.0, 1.0};
const Vector3D VECTOR3D_UNIT_X = {1.0, 0.0, 0.0};
const Vector3D VECTOR3D_UNIT_Y = {0.0, 1.0, 0.0};
const Vector3D VECTOR3D_UNIT_Z = {0.0, 0.0, 1.0};

// ============================================================
// 构造函数
// ============================================================
Vector3D Vector3D_Create(double x, double y, double z) {
    Vector3D v = {x, y, z};
    return v;
}

void Vector3D_Set(Vector3D* v, double x, double y, double z) {
    if (v) { v->x = x; v->y = y; v->z = z; }
}

// ============================================================
// 基本运算
// ============================================================
Vector3D Vector3D_Add(Vector3D a, Vector3D b) {
    return Vector3D_Create(a.x + b.x, a.y + b.y, a.z + b.z);
}
Vector3D Vector3D_AddScalar(Vector3D v, double s) {
    return Vector3D_Create(v.x + s, v.y + s, v.z + s);
}
Vector3D Vector3D_ScalarAdd(double s, Vector3D v) {
    return Vector3D_AddScalar(v, s);
}
Vector3D Vector3D_Sub(Vector3D a, Vector3D b) {
    return Vector3D_Create(a.x - b.x, a.y - b.y, a.z - b.z);
}
Vector3D Vector3D_SubScalar(Vector3D v, double s) {
    return Vector3D_Create(v.x - s, v.y - s, v.z - s);
}
Vector3D Vector3D_ScalarSub(double s, Vector3D v) {
    return Vector3D_Create(s - v.x, s - v.y, s - v.z);
}
Vector3D Vector3D_Mul(Vector3D a, Vector3D b) {
    return Vector3D_Create(a.x * b.x, a.y * b.y, a.z * b.z);
}
Vector3D Vector3D_MulScalar(Vector3D v, double s) {
    return Vector3D_Create(v.x * s, v.y * s, v.z * s);
}
Vector3D Vector3D_ScalarMul(double s, Vector3D v) {
    return Vector3D_MulScalar(v, s);
}
Vector3D Vector3D_Div(Vector3D a, Vector3D b) {
    return Vector3D_Create(a.x / b.x, a.y / b.y, a.z / b.z);
}
Vector3D Vector3D_DivScalar(Vector3D v, double s) {
    return Vector3D_Create(v.x / s, v.y / s, v.z / s);
}
Vector3D Vector3D_ScalarDiv(double s, Vector3D v) {
    return Vector3D_Create(s / v.x, s / v.y, s / v.z);
}
Vector3D Vector3D_Negate(Vector3D v) {
    return Vector3D_Create(-v.x, -v.y, -v.z);
}

// ============================================================
// 比较
// ============================================================
bool Vector3D_Equals(Vector3D a, Vector3D b) {
    return (a.x == b.x) && (a.y == b.y) && (a.z == b.z);
}

bool Vector3D_EqualsEpsilon(Vector3D a, Vector3D b, double epsilon) {
    double dx = a.x - b.x;
    double dy = a.y - b.y;
    double dz = a.z - b.z;
    return (dx * dx + dy * dy + dz * dz) <= (epsilon * epsilon);
}

// ============================================================
// 几何属性
// ============================================================
double Vector3D_GetLength(Vector3D v) {
    return sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
}
double Vector3D_GetLengthSq(Vector3D v) {
    return v.x * v.x + v.y * v.y + v.z * v.z;
}

Vector3D Vector3D_GetNormalized(Vector3D v) {
    double len = Vector3D_GetLength(v);
    if (len == 0.0) return VECTOR3D_EMPTY;
    return Vector3D_DivScalar(v, len);
}
void Vector3D_NormalizeInPlace(Vector3D* v) {
    if (!v) return;
    double len = Vector3D_GetLength(*v);
    if (len != 0.0) {
        v->x /= len;
        v->y /= len;
        v->z /= len;
    }
}

double Vector3D_GetDistance(Vector3D a, Vector3D b) {
    Vector3D diff = Vector3D_Sub(a, b);
    return Vector3D_GetLength(diff);
}

double Vector3D_GetDistanceSq(Vector3D a, Vector3D b) {
    Vector3D diff = Vector3D_Sub(a, b);
    return Vector3D_GetLengthSq(diff);
}

double Vector3D_Dot(Vector3D a, Vector3D b) {
    return a.x * b.x + a.y * b.y + a.z * b.z;
}

Vector3D Vector3D_Cross(Vector3D a, Vector3D b) {
    return Vector3D_Create(
        a.y * b.z - a.z * b.y,
        a.z * b.x - a.x * b.z,
        a.x * b.y - a.y * b.x
    );
}

double Vector3D_ProjectLength(Vector3D a, Vector3D b) {
    Vector3D normB = Vector3D_GetNormalized(b);
    return Vector3D_Dot(normB, a);
}

double Vector3D_GetAngle(Vector3D a, Vector3D b) {
    double dot = Vector3D_Dot(a, b);
    double lenA = Vector3D_GetLength(a);
    double lenB = Vector3D_GetLength(b);
    if (lenA == 0.0 || lenB == 0.0) return 0.0;
    double cosVal = dot / (lenA * lenB);
    if (cosVal > 1.0) cosVal = 1.0;
    if (cosVal < -1.0) cosVal = -1.0;
    return RadiansToDegrees(acos(cosVal));
}

// ============================================================
// 旋转
// ============================================================
Vector3D Vector3D_RotateByX(Vector3D v, double radians) {
    double c = cos(radians);
    double s = sin(radians);
    return Vector3D_Create(
        v.x,
        v.y * c - v.z * s,
        v.y * s + v.z * c
    );
}
void Vector3D_RotateByXInPlace(Vector3D* v, double radians) {
    if (!v) return;
    double c = cos(radians);
    double s = sin(radians);
    double ny = v->y * c - v->z * s;
    double nz = v->y * s + v->z * c;
    v->y = ny;
    v->z = nz;
}

Vector3D Vector3D_RotateByY(Vector3D v, double radians) {
    double c = cos(radians);
    double s = sin(radians);
    return Vector3D_Create(
        v.x * c + v.z * s,
        v.y,
        v.z * c - v.x * s
    );
}
void Vector3D_RotateByYInPlace(Vector3D* v, double radians) {
    if (!v) return;
    double c = cos(radians);
    double s = sin(radians);
    double nx = v->x * c + v->z * s;
    double nz = v->z * c - v->x * s;
    v->x = nx;
    v->z = nz;
}

Vector3D Vector3D_RotateByZ(Vector3D v, double radians) {
    double c = cos(radians);
    double s = sin(radians);
    return Vector3D_Create(
        v.x * c - v.y * s,
        v.y * c + v.x * s,
        v.z
    );
}
void Vector3D_RotateByZInPlace(Vector3D* v, double radians) {
    if (!v) return;
    double c = cos(radians);
    double s = sin(radians);
    double nx = v->x * c - v->y * s;
    double ny = v->y * c + v->x * s;
    v->x = nx;
    v->y = ny;
}

Vector3D Vector3D_Rotate(Vector3D v, double radians, Vector3D axis) {
    double c = cos(radians);
    double s = sin(radians);
    double t = 1.0 - c;
    double nx = v.x * (axis.x * axis.x * t + c)
              + v.y * (axis.x * axis.y * t - axis.z * s)
              + v.z * (axis.x * axis.z * t + axis.y * s);
    double ny = v.x * (axis.y * axis.x * t + axis.z * s)
              + v.y * (axis.y * axis.y * t + c)
              + v.z * (axis.y * axis.z * t - axis.x * s);
    double nz = v.x * (axis.z * axis.x * t - axis.y * s)
              + v.y * (axis.z * axis.y * t + axis.x * s)
              + v.z * (axis.z * axis.z * t + c);
    return Vector3D_Create(nx, ny, nz);
}
void Vector3D_RotateInPlace(Vector3D* v, double radians, Vector3D axis) {
    if (!v) return;
    Vector3D res = Vector3D_Rotate(*v, radians, axis);
    *v = res;
}

Vector3D Vector3D_RotateAroundTwoPoints(Vector3D point1, Vector3D point2, Vector3D v, double radians) {
    Vector3D axis = Vector3D_GetNormalized(Vector3D_Sub(point2, point1));
    Vector3D tv = Vector3D_Sub(v, point1);
    Vector3D rv = Vector3D_Rotate(tv, radians, axis);
    return Vector3D_Add(rv, point1);
}
void Vector3D_RotateAroundTwoPointsInPlace(Vector3D* v, Vector3D point1, Vector3D point2, double radians) {
    if (!v) return;
    Vector3D res = Vector3D_RotateAroundTwoPoints(point1, point2, *v, radians);
    *v = res;
}

// ============================================================
// 缩放
// ============================================================
Vector3D Vector3D_Zoom(Vector3D v, double k) {
    return Vector3D_MulScalar(v, k);
}
void Vector3D_ZoomInPlace(Vector3D* v, double k) {
    if (!v) return;
    v->x *= k;
    v->y *= k;
    v->z *= k;
}

Vector3D Vector3D_ZoomAlongAxis(Vector3D v, double k, Vector3D axis) {
    double t = k - 1.0;
    double nx = v.x * (axis.x * axis.x * t + 1.0)
              + v.y * (axis.y * axis.x * t)
              + v.z * (axis.z * axis.x * t);
    double ny = v.x * (axis.x * axis.y * t)
              + v.y * (axis.y * axis.y * t + 1.0)
              + v.z * (axis.z * axis.y * t);
    double nz = v.x * (axis.x * axis.z * t)
              + v.y * (axis.y * axis.z * t)
              + v.z * (axis.z * axis.z * t + 1.0);
    return Vector3D_Create(nx, ny, nz);
}
void Vector3D_ZoomAlongAxisInPlace(Vector3D* v, double k, Vector3D axis) {
    if (!v) return;
    Vector3D res = Vector3D_ZoomAlongAxis(*v, k, axis);
    *v = res;
}

// ============================================================
// 正交投影 (投影到垂直于 n 的平面)
// ============================================================
Vector3D Vector3D_Projection(Vector3D v, Vector3D n) {
    double nx = v.x * (1.0 - n.x * n.x) + v.y * (-n.x * n.y) + v.z * (-n.x * n.z);
    double ny = v.x * (-n.x * n.y) + v.y * (1.0 - n.y * n.y) + v.z * (-n.z * n.y);
    double nz = v.x * (-n.x * n.z) + v.y * (-n.z * n.y) + v.z * (1.0 - n.z * n.z);
    return Vector3D_Create(nx, ny, nz);
}
void Vector3D_ProjectionInPlace(Vector3D* v, Vector3D n) {
    if (!v) return;
    Vector3D res = Vector3D_Projection(*v, n);
    *v = res;
}

// ============================================================
// 反射与折射
// ============================================================
Vector3D Vector3D_Reflect(Vector3D v, Vector3D n) {
    // R = v - 2 * dot(v, n) * n
    double dot = Vector3D_Dot(v, n);
    return Vector3D_Create(
        v.x - 2.0 * dot * n.x,
        v.y - 2.0 * dot * n.y,
        v.z - 2.0 * dot * n.z
    );
}

Vector3D Vector3D_Refract(Vector3D v, Vector3D n, double eta) {
    // Snell 定律折射: eta = n1/n2
    double dot = Vector3D_Dot(v, n);
    double k = 1.0 - eta * eta * (1.0 - dot * dot);
    if (k < 0.0) {
        // 全内反射
        return VECTOR3D_EMPTY;
    }
    double sqrtK = sqrt(k);
    return Vector3D_Create(
        eta * v.x - (eta * dot + sqrtK) * n.x,
        eta * v.y - (eta * dot + sqrtK) * n.y,
        eta * v.z - (eta * dot + sqrtK) * n.z
    );
}

// ============================================================
// 插值
// ============================================================
Vector3D Vector3D_Lerp(Vector3D a, Vector3D b, double t) {
    // result = a + t * (b - a)
    return Vector3D_Create(
        a.x + t * (b.x - a.x),
        a.y + t * (b.y - a.y),
        a.z + t * (b.z - a.z)
    );
}

Vector3D Vector3D_NLerp(Vector3D a, Vector3D b, double t) {
    // 线性插值后归一化
    Vector3D lerped = Vector3D_Lerp(a, b, t);
    return Vector3D_GetNormalized(lerped);
}

Vector3D Vector3D_Slerp(Vector3D a, Vector3D b, double t) {
    // 球面插值
    double dot = Vector3D_Dot(a, b);

    // 处理边界情况
    if (dot > 0.9995) {
        // 夹角太小，退化为线性插值
        return Vector3D_NLerp(a, b, t);
    }

    // 处理方向相反的情况
    if (dot < -0.9995) {
        // 取垂直轴做 180 度旋转
        Vector3D axis = Vector3D_Cross(a, VECTOR3D_UNIT_X);
        if (Vector3D_GetLengthSq(axis) < 0.001) {
            axis = Vector3D_Cross(a, VECTOR3D_UNIT_Y);
        }
        axis = Vector3D_GetNormalized(axis);
        return Vector3D_Rotate(a, 3.14159265358979323846 * t, axis);
    }

    // 限制 dot 范围
    if (dot < -1.0) dot = -1.0;
    if (dot > 1.0) dot = 1.0;

    double theta = acos(dot);
    double sinTheta = sin(theta);
    double invSinTheta = 1.0 / sinTheta;

    double scaleA = sin((1.0 - t) * theta) * invSinTheta;
    double scaleB = sin(t * theta) * invSinTheta;

    return Vector3D_Create(
        a.x * scaleA + b.x * scaleB,
        a.y * scaleA + b.y * scaleB,
        a.z * scaleA + b.z * scaleB
    );
}

// ============================================================
// 极值/约束
// ============================================================
Vector3D Vector3D_Min(Vector3D a, Vector3D b) {
    return Vector3D_Create(
        a.x < b.x ? a.x : b.x,
        a.y < b.y ? a.y : b.y,
        a.z < b.z ? a.z : b.z
    );
}

Vector3D Vector3D_Max(Vector3D a, Vector3D b) {
    return Vector3D_Create(
        a.x > b.x ? a.x : b.x,
        a.y > b.y ? a.y : b.y,
        a.z > b.z ? a.z : b.z
    );
}

Vector3D Vector3D_Clamp(Vector3D v, Vector3D min, Vector3D max) {
    return Vector3D_Create(
        v.x < min.x ? min.x : (v.x > max.x ? max.x : v.x),
        v.y < min.y ? min.y : (v.y > max.y ? max.y : v.y),
        v.z < min.z ? min.z : (v.z > max.z ? max.z : v.z)
    );
}

Vector3D Vector3D_ClampLength(Vector3D v, double minLen, double maxLen) {
    double len = Vector3D_GetLength(v);
    if (len == 0.0) return VECTOR3D_EMPTY;

    double clampedLen = len < minLen ? minLen : (len > maxLen ? maxLen : len);
    double scale = clampedLen / len;
    return Vector3D_MulScalar(v, scale);
}

// ============================================================
// 调试辅助
// ============================================================
void Vector3D_Print(Vector3D v, const char* label) {
    if (label) {
        printf("%s: ", label);
    }
    printf("(%lf, %lf, %lf)\n", v.x, v.y, v.z);
}

int Vector3D_ToString(Vector3D v, char* buffer, size_t bufSize) {
    if (!buffer || bufSize == 0) return 0;
    return snprintf(buffer, bufSize, "(%lf, %lf, %lf)", v.x, v.y, v.z);
}
