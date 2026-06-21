#pragma once
#ifdef __cplusplus
#undef __cplusplus
#endif // __cplusplus


#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>
#include <math.h>

#include <vulkan/vulkan.h>

#include <SDL3/SDL.h>
#include <SDL3/SDL_vulkan.h>


#ifdef _WIN32
    #include <windows.h>
#elif defined(__APPLE__)
    #include <TargetConditionals.h>
    #if TARGET_OS_OSX
        #include <Cocoa/Cocoa.h>
    #endif
#elif defined(__linux__)
    #include <X11/Xlib.h>
    #include <X11/Xutil.h>
#endif



#if defined(__APPLE__)
typedef int8_t			s8;
typedef int16_t			s16;
typedef int				s32;
typedef long long		s64;
typedef uint8_t			u8;
typedef uint16_t		u16;
typedef unsigned int	u32;
typedef uint64_t		u64;
typedef float			f32;
typedef double			f64;
typedef uint8_t			byte;
typedef uint32_t		b32;
#else
typedef int8_t      s8;
typedef int16_t     s16;
typedef int32_t     s32;
typedef int64_t     s64;
typedef uint8_t     u8;
typedef uint16_t    u16;
typedef uint32_t    u32;
typedef uint64_t    u64;
typedef float       f32;
typedef double      f64;
typedef uint8_t     byte;
typedef uint32_t    b32;
#endif


//64位宽的结果类型，包含步骤码、严重性、错误码和系统错误码，便于溯源与纠错
typedef u64 	    NRResult;
//渲染API类型，如Vulkan、OpenGL等
typedef s32		    NRGraphicsAPI;
typedef u64 	    NRVersion;
typedef u64			NRResourceID;
typedef u32			NRResourceType;
typedef u32			NRGameObjectType;

//未知API
#define NR_GRAPHICS_API_UNKNOWN 0
//通用Vulkan API
#define NR_GRAPHICS_API_VULKAN 1
//通用OpenGL API
#define NR_GRAPHICS_API_OPENGL 2
//微软DirectX API
#define NR_GRAPHICS_API_DIRECTX 3
//苹果Metal API
#define NR_GRAPHICS_API_METAL 4
//华为ArkGraphics API
#define NR_GRAPHICS_API_ARKGRAPHICS 5

//无或未知资源类型
#define NR_RESOURCE_TYPE_NONE 0
//模型
#define NR_RESOURCE_TYPE_MODEL 1
//贴图
#define NR_RESOURCE_TYPE_TEXTURE 2

//空对象
#define NR_GAMEOBJECFT_TYPE_NONE 0
//基础对象
#define NR_GAMEOBJECFT_TYPE_BASE 1
//光源对象
#define NR_GAMEOBJECFT_TYPE_LIGHT 2
//摄像机
#define NR_GAMEOBJECFT_TYPE_CAMERA 3

// ============================================================
// 跨平台动态库导出宏
// 在 Windows 上使用 __declspec(dllexport)
// 在 macOS/Linux 上使用 __attribute__((visibility("default")))
// ============================================================

#ifdef SE_BUILD_SHARED
    // 当前正在构建动态库
    #ifdef _MSC_VER
        #define SE_API __declspec(dllexport)
    #elif defined(__GNUC__) || defined(__clang__)
        #define SE_API __attribute__((visibility("default")))
    #else
        #define SE_API
    #endif
#else
    // 使用动态库（P/Invoke 或其他消费者）
    #ifdef _MSC_VER
        #define SE_API __declspec(dllimport)
    #elif defined(__GNUC__) || defined(__clang__)
        #define SE_API __attribute__((visibility("default")))
    #else
        #define SE_API
    #endif
#endif


#define SE_EXP __stdcall

#define SE_OUT(type) SE_API type SE_EXP

// ============================================================
// 三维向量 (Vector3D) - double 精度
// ============================================================

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

//变换
struct NRTransForm
{
	Vector3D forward, up, right;
	Vector3D position;
	Vector3D scale;
	Vector3D rotation;
};
//绑定的资源，可以是模型，材质，贴图
struct NRResource
{
	NRResourceID resource_id;
	char* resource_name;
	NRResourceType resource_type;
	void* resource_data;
	u64 resource_size;
};
//游戏对象，结构与c#内的一致，但是去除了碰撞以及物理相关的变量，这里只用渲染。
struct NRGameObject
{
	struct NRTransForm transform;
	u64* bind_resources;
	u32 bind_resources_count;
	NRGameObjectType type;
	struct NRGameObject* childs;
	u32 childs_count;
	struct NRGameObject* father;
	byte nod[64];
};
//场景，用于存放游戏对象
struct NRSpace
{
	struct NRSpace* father;
	struct NRSpace* childs;
	u32 childs_count;
	struct NRGameObject* objects;
	u32 objects_count;
};
//渲染器创建传入信息
struct NRRendererCreateInfo
{
	const char* renderer_name; //渲染器名称
	const char* app_name; //应用程序名称
	NRVersion app_version; //应用程序版本
	NRGraphicsAPI api; //渲染API类型
	NRVersion api_base_version; //渲染API基础版本
	NRVersion api_target_version; //渲染API目标版本
	const char** required_instance_extensions; //必需的实例扩展列表（额外）
	const char** optional_instance_extensions; //可选的实例扩展列表（额外）
	s32 required_instance_extensions_count; //必需的实例扩展列表数量（额外）
	s32 optional_instance_extensions_count; //可选的实例扩展列表数量（额外）
	const char** required_device_extensions; //必需的设备扩展列表（额外）
	const char** optional_device_extensions; //可选的设备扩展列表（额外）
	s32 required_device_extensions_count; //必需的设备扩展列表数量（额外）
	s32 optional_device_extensions_count; //可选的设备扩展列表数量（额外）
	u64** required_renderer_features; //必需的渲染器功能列表（额外）（STC）
	u64** optional_renderer_features; //可选的渲染器功能列表（额外）（STC）
	s32 required_renderer_features_count; //必需的渲染器功能列表数量（额外）（STC）
	s32 optional_renderer_features_count; //可选的渲染器功能列表数量（额外）（STC）
};

//与C#共用的渲染器结构体，不涉及具体实现细节，抽象于Vulkan或其他渲染API，用于交换信息，比如需要渲染的GameObject与Texture等
struct NRRenderer
{
	struct NRRendererCreateInfo createInfo;
	struct NRSpace* spaces;
	u32 spaces_count;
	u32 current_space;
};





//[内部]错误处理函数，记录最后一次错误的结果和各项错误信息
NRResult nr_OnError(NRResult result);

#define NRR_Make(stepcode, severity, code, systemcode) nr_OnError((((u64)(severity) << 56) | ((u64)(stepcode) << 48) | ((u64)(code) << 32) | (systemcode)))
#define NRR_GetStepCode(result) ((u8)((result >> 48) & 0xFF))
#define NRR_GetSeverity(result) ((u8)((result >> 56) & 0xFF))
#define NRR_GetCode(result) ((u16)((result >> 32) & 0xFFFF))
#define NRR_GetSystemCode(result) ((u32)(result & 0xFFFFFFFF))
          
#define NRR_MakeSuccess(stepcode, code) NRR_Make(stepcode, 0, code, 0)
#define NRR_MakeFailure(stepcode, code, systemcode) NRR_Make(stepcode, 2, code, systemcode)
#define NRR_MakeWarning(stepcode, code, systemcode) NRR_Make(stepcode, 1, code, systemcode)
#define NRR_MakeLog(stepcode, code, systemcode) NRR_Make(stepcode, 0, code, systemcode)
          
#define NRR_SUCCESS(result) (NRR_GetSeverity(result) == 0)
#define NRR_FAILED(result) (NRR_GetSeverity(result) >= 1)
          
#define NRR_SEVERITY_LOG 0
#define NRR_SEVERITY_WARNING 1
#define NRR_SEVERITY_ERROR 2


#define NRR_STEP_NR_Init 1
#define NRR_STEP_NR_CreateWindow 2
#define NRR_STEP_NR_DestroyWindow 3
#define NRR_STEP_NR_CreateRenderer 4
#define NRR_STEP_NR_DestroyRenderer 5
#define NRR_STEP_NR_PrepareRender 6
#define NRR_STEP_NR_Render 7

// Vulkan 内部步骤码（10~19）
#define NRR_STEP_VK_CreateInstance 10
#define NRR_STEP_VK_CreateSurface 11
#define NRR_STEP_VK_SelectPhysicalDevice 12
#define NRR_STEP_VK_CreateDevice 13
#define NRR_STEP_VK_CreateSwapchain 14
#define NRR_STEP_VK_CreateRenderPass 15
#define NRR_STEP_VK_CreatePipeline 16
#define NRR_STEP_VK_CreateCommandPool 17
#define NRR_STEP_VK_CreateSyncObjects 18
#define NRR_STEP_VK_CreateShaderModule 19
#define NRR_STEP_VK_CreateBuffer 20
#define NRR_STEP_VK_CreateImage 21
#define NRR_STEP_VK_CreateSampler 22

#define NRR_CODE_SUCCESS 0
#define NRR_CODE_ALREADY_INITIALIZED 1
#define NRR_CODE_NOT_INITIALIZED 2
#define NRR_CODE_CREATE_WINDOW_FAILED 3
#define NRR_CODE_WINDOW_NOT_CREATED 4
#define NRR_CODE_RENDERER_ALREADY_CREATED 5
#define NRR_CODE_RENDERER_NOT_CREATED 6
#define NRR_CODE_INVALID_API 7
#define NRR_CODE_API_VERSION_UNSUPPORTED 8
#define NRR_CODE_DEVICE_NOT_FOUND 9
#define NRR_CODE_DEVICE_EXTENSION_MISSING 10
#define NRR_CODE_SWAPCHAIN_CREATION_FAILED 11
#define NRR_CODE_PIPELINE_CREATION_FAILED 12
#define NRR_CODE_SHADER_COMPILATION_FAILED 13
#define NRR_CODE_OUT_OF_MEMORY 14
#define NRR_CODE_SURFACE_CREATION_FAILED 15
#define NRR_CODE_QUEUE_NOT_FOUND 16
#define NRR_CODE_COMMAND_BUFFER_FAILED 17
#define NRR_CODE_SYNC_CREATION_FAILED 18
#define NRR_CODE_FRAMEBUFFER_CREATION_FAILED 19
#define NRR_CODE_RENDERPASS_CREATION_FAILED 20
#define NRR_CODE_BUFFER_CREATION_FAILED 21
#define NRR_CODE_IMAGE_CREATION_FAILED 22
#define NRR_CODE_SAMPLER_CREATION_FAILED 23
#define NRR_CODE_DESCRIPTOR_SET_FAILED 24
#define NRR_CODE_SWAPCHAIN_OUT_OF_DATE 25
#define NRR_CODE_SUBMIT_FAILED 26
#define NRR_CODE_PRESENT_FAILED 27
#define NRR_CODE_INVALID_PARAMETER 28
#define NRR_CODE_NOT_IMPLEMENTED 29


#define NRV_Make(major, minor, patch, user) (((u64)(major) << 48) | ((u64)(minor) << 32) | ((u64)(patch) << 16) | (user))
#define NRV_GetMajor(version) ((u16)((version >> 48) & 0xFFFF))
#define NRV_GetMinor(version) ((u16)((version >> 32) & 0xFFFF))
#define NRV_GetPatch(version) ((u16)((version >> 16) & 0xFFFF))
#define NRV_GetUser(version) ((u16)(version & 0xFFFF))


extern bool nr_sdl_init;
extern SDL_Window* nr_window;


extern const char* nr_last_sdl_error_msg;
extern NRResult nr_last_result;

// ============================================================
// Vulkan 内部状态结构体
// ============================================================

// 队列族索引
struct NRVkQueueFamilies {
    u32 graphicsFamily;
    u32 presentFamily;
    u32 computeFamily;
    b32 graphicsFound;
    b32 presentFound;
    b32 computeFound;
};

// 交换链详细信息
struct NRVkSwapchainInfo {
    VkSwapchainKHR swapchain;
    VkFormat imageFormat;
    VkExtent2D extent;
    u32 imageCount;
    VkImage* images;
    VkImageView* imageViews;
};

// Vulkan 渲染器内部状态
struct NRVkState {
    // 核心对象
    VkInstance instance;
    VkPhysicalDevice physicalDevice;
    VkDevice device;
    VkSurfaceKHR surface;
    
    // 队列
    struct NRVkQueueFamilies queueFamilies;
    VkQueue graphicsQueue;
    VkQueue presentQueue;
    VkQueue computeQueue;
    
    // 交换链
    struct NRVkSwapchainInfo swapchainInfo;
    
    // 渲染管线
    VkRenderPass renderPass;
    VkPipelineLayout pipelineLayout;
    VkPipeline graphicsPipeline;
    VkFramebuffer* framebuffers;
    
    // 命令
    VkCommandPool commandPool;
    VkCommandBuffer* commandBuffers;
    
    // 同步
    VkSemaphore* imageAvailableSemaphores;
    VkSemaphore* renderFinishedSemaphores;
    VkFence* inFlightFences;
    VkFence* imagesInFlight;
    
    // 当前帧索引（用于帧同步循环）
    u32 currentFrame;
    // 当前交换链图像索引（由 PrepareRender 设置，Render 使用）
    u32 currentImageIndex;
    u32 maxFramesInFlight;
    
    // 版本信息
    NRVersion apiVersion;
    b32 useDynamicRendering;
    b32 useSynchronization2;
    
    // 窗口尺寸（用于重建交换链）
    u32 windowWidth;
    u32 windowHeight;
    
    // 深度缓冲
    VkImage depthImage;
    VkDeviceMemory depthMemory;
    VkImageView depthImageView;
    
    // 顶点 Buffer
    VkBuffer vertexBuffer;
    VkDeviceMemory vertexMemory;
    
    // 索引 Buffer
    VkBuffer indexBuffer;
    VkDeviceMemory indexMemory;
    
    // Uniform Buffer
    VkBuffer uniformBuffer;
    VkDeviceMemory uniformMemory;
    void* uniformMappedData;
    
    // Descriptor
    VkDescriptorSetLayout descriptorSetLayout;
    VkDescriptorPool descriptorPool;
    VkDescriptorSet descriptorSet;
    
    // Shader 模块
    VkShaderModule vertShaderModule;
    VkShaderModule fragShaderModule;
    
    // 是否已初始化
    b32 initialized;
};

// 全局 Vulkan 状态
extern struct NRVkState nr_vk_state;

// 最大并行帧数
#define NRVK_MAX_FRAMES_IN_FLIGHT 2

// ============================================================
// Vulkan 结果检查宏
// ============================================================
#define VK_CHECK(x, stepcode, errorcode)													\
	do {																					\
		VkResult __vk_result = (x);															\
		if (__vk_result != VK_SUCCESS) {													\
			return NRR_MakeFailure(stepcode, errorcode, (u32)__vk_result);					\
		}																					\
	} while (0)

#define VK_CHECK_VOID(x)																	\
	do {																					\
		VkResult __vk_result = (x);															\
		(void)__vk_result;																	\
	} while (0)

// ============================================================
// Vulkan 版本判断宏
// 根据 api_target_version 判断是否支持某特性
// ============================================================

// 判断 Vulkan 版本 >= major.minor
#define NRVK_VERSION_GE(version, major, minor)												\
	(NRV_GetMajor(version) > (major) ||														\
	(NRV_GetMajor(version) == (major) && NRV_GetMinor(version) >= (minor)))

// 是否支持 Dynamic Rendering（Vulkan 1.3+）
#define NRVK_HAS_DYNAMIC_RENDERING(version)		NRVK_VERSION_GE(version, 1, 3)

// 是否支持 Synchronization2（Vulkan 1.3+）
#define NRVK_HAS_SYNCHRONIZATION2(version)		NRVK_VERSION_GE(version, 1, 3)

// 是否支持 Maintenance4（Vulkan 1.3+）
#define NRVK_HAS_MAINTENANCE4(version)			NRVK_VERSION_GE(version, 1, 3)

// 是否支持 Vulkan 1.4（实质是 1.3 + 强制扩展）
#define NRVK_IS_1_4(version)					NRVK_VERSION_GE(version, 1, 4)

// ============================================================
// Vulkan 1.4 强制扩展列表
// 当 api_target_version >= 1.4 时，这些扩展必须启用
// ============================================================
#define NRVK_1_4_REQUIRED_DEVICE_EXTENSIONS												\
	"VK_KHR_dynamic_rendering",				/* 替代传统 RenderPass */					\
	"VK_KHR_synchronization2",				/* 增强同步 API */							\
	"VK_KHR_maintenance4",					/* 设备属性增强 */							\
	"VK_KHR_shader_float_controls2",		/* 浮点控制 */								\
	"VK_KHR_swapchain"						/* 交换链（始终需要）*/

// ============================================================
// 立方体顶点数据（相对物体坐标，中心在原点，边长 1）
// 8 个顶点
// ============================================================
#define CUBE_VERTEX_COUNT 8
#define CUBE_INDEX_COUNT 36

extern const f32 g_CubeVertices[CUBE_VERTEX_COUNT * 3];
extern const u32 g_CubeIndices[CUBE_INDEX_COUNT];

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

//获取资源
NRResult nr_GetResource(NRResourceID id, struct NRResource * res);
//快捷检查资源类型是否符合要求
NRResult nr_CheckResource(NRResourceID id, NRResourceType restype);


//结果转字符串
SE_OUT(const char*) NR_ResultToString(NRResult result);
//获取上一次的错误字符串
SE_OUT(const char*) NR_GetLastError(void);


//初始化
SE_OUT(NRResult) NR_Init(u32 sdl_flags);


//创建窗口
SE_OUT(NRResult) NR_CreateWindow(const char* title, u32 width, u32 height, u32 flags);
//销毁窗口，需要先销毁渲染器
SE_OUT(NRResult) NR_DestroyWindow(void);
//创建渲染器
SE_OUT(NRResult) NR_CreateRenderer(struct NRRendererCreateInfo info);
//处理事件
SE_OUT(NRResult) NR_PollEvents(SDL_Event* event);
//销毁渲染器
SE_OUT(NRResult) NR_DestroyRenderer(void);
//在c#主线程中调用以刷新c语言库的主要数据处理
SE_OUT(NRResult) NR_MainUpdate(f64 deltatime);
//在c#的主线程中调用，来渲染图像，严格保证在调用准备函数之后调用
SE_OUT(NRResult) NR_Render(f64 deltatime);
//在c#的渲染线程中调用，来准备渲染资源，严格保证在调用渲染函数之前调用
SE_OUT(NRResult) NR_PrepareRender(f64 deltatime);

// ============================================================
// Vulkan 工具函数声明
// ============================================================

// 调试回调函数声明
VKAPI_ATTR VkBool32 VKAPI_CALL nrVkDebugCallback(
	VkDebugUtilsMessageSeverityFlagBitsEXT messageSeverity,
	VkDebugUtilsMessageTypeFlagsEXT messageType,
	const VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
	void* pUserData);

// 查找队列族索引
b32 nrVkFindQueueFamilies(VkPhysicalDevice device, VkSurfaceKHR surface,
						  u32* outGraphicsFamily, u32* outPresentFamily, u32* outComputeFamily);

// 检查设备扩展是否支持
b32 nrVkCheckDeviceExtensionSupport(VkPhysicalDevice device,
									 const char** requiredExtensions, u32 requiredCount,
									 const char** optionalExtensions, u32 optionalCount,
									 b32* outOptionalSupported);

// 选择交换链表面格式
VkSurfaceFormatKHR nrVkChooseSwapSurfaceFormat(const VkSurfaceFormatKHR* formats, u32 formatCount);

// 选择交换链呈现模式
VkPresentModeKHR nrVkChooseSwapPresentMode(const VkPresentModeKHR* modes, u32 modeCount);

// 选择交换链分辨率
VkExtent2D nrVkChooseSwapExtent(const VkSurfaceCapabilitiesKHR* capabilities, u32 windowWidth, u32 windowHeight);

// 查找内存类型
b32 nrVkFindMemoryType(VkPhysicalDevice physicalDevice, u32 typeFilter,
					   VkMemoryPropertyFlags properties, u32* outMemoryType);

// 创建深度图像和视图
NRResult nrVkCreateDepthResources(u32 width, u32 height);

// 销毁深度资源
void nrVkDestroyDepthResources(void);

// 创建顶点 Buffer
NRResult nrVkCreateVertexBuffer(void);

// 创建索引 Buffer
NRResult nrVkCreateIndexBuffer(void);

// 创建 Uniform Buffer
NRResult nrVkCreateUniformBuffer(void);

// 创建 Descriptor Set Layout
NRResult nrVkCreateDescriptorSetLayout(void);

// 创建 Descriptor Pool 和 Descriptor Set
NRResult nrVkCreateDescriptorSet(void);

// 更新 Uniform Buffer 数据（MVP 矩阵）
NRResult nrVkUpdateUniformBuffer(void);

// 销毁所有渲染资源
void nrVkDestroyRenderResources(void);

// 交换链管理
NRResult nrVkCreateSwapchain(void);
NRResult nrVkRecreateSwapchain(u32 newWidth, u32 newHeight);

// RenderPass 管理
NRResult nrVkCreateRenderPass(void);
NRResult nrVkCreateFramebuffers(void);

// Shader 模块管理
NRResult nrVkCreateShaderModule(const u32* code, u32 codeSize, VkShaderModule* outShaderModule);
void nrVkDestroyShaderModule(VkShaderModule shaderModule);
NRResult nrVkCreateShaderModules(VkShaderModule* outVertModule, VkShaderModule* outFragModule);
VkPipelineShaderStageCreateInfo nrVkCreateVertexShaderStage(VkShaderModule shaderModule);
VkPipelineShaderStageCreateInfo nrVkCreateFragmentShaderStage(VkShaderModule shaderModule);

// 图形管线管理
NRResult nrVkCreatePipelineLayout(VkPipelineLayout* outLayout);
NRResult nrVkCreateGraphicsPipeline(VkShaderModule vertShader, VkShaderModule fragShader,
                                     VkPipelineLayout layout, VkPipeline* outPipeline);

// Buffer 管理
NRResult nrVkCreateBuffer(VkDeviceSize size, VkBufferUsageFlags usage,
                           VkMemoryPropertyFlags properties,
                           VkBuffer* outBuffer, VkDeviceMemory* outMemory);
NRResult nrVkCopyBuffer(VkBuffer srcBuffer, VkBuffer dstBuffer, VkDeviceSize size,
                         VkCommandPool commandPool, VkQueue queue);
NRResult nrVkMapMemory(VkDeviceMemory memory, VkDeviceSize offset, VkDeviceSize size,
                        void** outData);
void nrVkUnmapMemory(VkDeviceMemory memory);
void nrVkDestroyBuffer(VkBuffer buffer, VkDeviceMemory memory);

// 命令池与命令缓冲管理
NRResult nrVkCreateCommandPool(VkCommandPool* outPool);
NRResult nrVkAllocateCommandBuffers(VkCommandPool pool, u32 count, VkCommandBuffer* outBuffers);
void nrVkDestroyCommandPool(VkCommandPool pool);

// 同步原语管理
NRResult nrVkCreateSemaphore(VkSemaphore* outSemaphore);
NRResult nrVkCreateFence(b32 signaled, VkFence* outFence);
NRResult nrVkCreateFrameSyncObjects(u32 maxFramesInFlight);
NRResult nrVkWaitForFence(VkFence fence, u64 timeout);
NRResult nrVkResetFence(VkFence fence);
void nrVkDestroySemaphore(VkSemaphore semaphore);
void nrVkDestroyFence(VkFence fence);

// ============================================================
// 着色器编译函数声明
// ============================================================

// 编译 HLSL 源码到 SPIR-V 二进制
NRResult nrCompileHLSLToSPIRV(const char* hlslSource,
                               const char* entryPoint,
                               const char* shaderStage,
                               u32** outSPIRV,
                               u32* outSPIRVSize);

// 从 HLSL 源码直接创建 Vulkan ShaderModule
NRResult nrVkCreateShaderFromHLSL(const char* hlslSource,
                                   const char* entryPoint,
                                   const char* shaderStage,
                                   VkShaderModule* outShaderModule);

// 释放 SPIR-V 二进制数据
void nrFreeSPIRV(u32* spirv);


