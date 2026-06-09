#pragma once
#ifdef __cplusplus
#undef __cplusplus
#endif // __cplusplus


#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>




#include "NRExport.h"


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

//64位宽的结果类型，包含步骤码、严重性、错误码和系统错误码，便于溯源与纠错
typedef u64 	    NRResult;
//渲染API类型，如Vulkan、OpenGL等
typedef s32		    NRGraphicsAPI;
typedef u64 	    NRVersion;

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

//与C#共用的渲染器结构体，不涉及具体实现细节，抽象于Vulkan或其他渲染API
typedef struct NRRenderer
{
	const char* name; //渲染器名称
};

typedef struct NRRendererCreateInfo
{
	const char* renderer_name; //渲染器名称
	const char* app_name; //应用程序名称
    NRVersion app_version; //应用程序版本
    NRGraphicsAPI api; //渲染API类型
	NRVersion api_base_version; //渲染API基础版本
	NRVersion api_target_version; //渲染API目标版本
	const char** required_instance_extensions; //必需的实例扩展列表（额外）
	const char** optional_instance_extensions; //可选的实例扩展列表（额外）
	const char** required_device_extensions; //必需的设备扩展列表（额外）
	const char** optional_device_extensions; //可选的设备扩展列表（额外）
	const char** required_renderer_features; //必需的渲染器功能列表（额外）
	const char** optional_renderer_features; //可选的渲染器功能列表（额外）
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



#define NRR_CODE_SUCCESS 0
#define NRR_CODE_ALREADY_INITIALIZED 1
#define NRR_CODE_NOT_INITIALIZED 2
#define NRR_CODE_CREATE_WINDOW_FAILED 3
#define NRR_CODE_WINDOW_NOT_CREATED 4


#define NRV_Make(major, minor, patch, user) (((u64)(major) << 48) | ((u64)(minor) << 32) | ((u64)(patch) << 16) | (user))
#define NRV_GetMajor(version) ((u16)((version >> 48) & 0xFFFF))
#define NRV_GetMinor(version) ((u16)((version >> 32) & 0xFFFF))
#define NRV_GetPatch(version) ((u16)((version >> 16) & 0xFFFF))
#define NRV_GetUser(version) ((u16)(version & 0xFFFF))


extern bool nr_sdl_init = FALSE;
extern SDL_Window* nr_window;


extern const char* nr_last_sdl_error_msg;
extern NRResult nr_last_result;






SE_API const char* NR_ResultToString(NRResult result);
SE_API const char* NR_GetLastError();




//初始化
SE_API NRResult NR_Init(u32 sdl_flags);
//创建窗口
SE_API NRResult NR_CreateWindow(const char* title, u32 width, u32 height, u32 flags);
SE_API NRResult NR_DestroyWindow();
SE_API NRResult NR_CreateRenderer();
SE_API NRResult NR_PollEvents();
