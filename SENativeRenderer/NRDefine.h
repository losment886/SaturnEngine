#pragma once

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>
#include <math.h>
#include "NRVec.h"
#include "NRTypedef.h"


#include <SDL3/SDL.h>


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


// 统一使用 __cdecl 调用约定
// __cdecl 在 MSVC、clang、GCC 上均支持，跨平台兼容
// C# P/Invoke 统一使用 CallingConvention.Cdecl
#define SE_EXP

#define SE_OUT(type) SE_API type SE_EXP




// TRUE/FALSE 宏定义（stdbool.h 只定义了小写的 true/false）
#ifndef TRUE
    #define TRUE 1
#endif
#ifndef FALSE
    #define FALSE 0
#endif


#ifdef _WIN32
    #include <windows.h>
#elif defined(__APPLE__)
    #include <TargetConditionals.h>

#elif defined(__linux__)
    #include <X11/Xlib.h>
    #include <X11/Xutil.h>
#endif


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


#define NR_GRAPHICS_TYPE_DEFAULT 0
#define NR_GRAPHICS_TYPE_3D 0
#define NR_GRAPHICS_TYPE_2D 1


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
	NRGraphicsType apitype; //渲染类型
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

extern struct NRRendererCreateInfo* nr_renderer_create_info;

// ============================================================
// 立方体顶点数据（相对物体坐标，中心在原点，边长 1）
// 8 个顶点
// ============================================================
#define CUBE_VERTEX_COUNT 8
#define CUBE_INDEX_COUNT 36

extern const f32 g_CubeVertices[CUBE_VERTEX_COUNT * 3];
extern const u32 g_CubeIndices[CUBE_INDEX_COUNT];


//获取资源
NRResult nr_GetResource(NRResourceID id, struct NRResource * res);
//快捷检查资源类型是否符合要求
NRResult nr_CheckResource(NRResourceID id, NRResourceType restype);



//===================== EXPORT =======================


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