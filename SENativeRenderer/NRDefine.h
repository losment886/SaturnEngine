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

typedef u64 	    NRResult;

typedef struct Vector3D
{
    f64 X, Y, Z;
    
};

typedef struct Vector2D
{
    f64 X, Y;
};


#define NRR_Make(stepcode, severity, code, systemcode) (((u64)(severity) << 56) | ((u64)(stepcode) << 48) | ((u64)(code) << 32) | (systemcode))
#define NRR_GetStepCode(result) ((u8)((result >> 48) & 0xFF))
#define NRR_GetSeverity(result) ((u8)((result >> 56) & 0xFF))
#define NRR_GetCode(result) ((u16)((result >> 32) & 0xFFFF))
#define NRR_GetSystemCode(result) ((u32)(result & 0xFFFFFFFF))
          
#define NRR_MakeSuccess(stepcode, code) NR_Make(stepcode, 0, code, 0)
#define NRR_MakeFailure(stepcode, code, systemcode) NR_Make(stepcode, 2, code, systemcode)
#define NRR_MakeWarning(stepcode, code, systemcode) NR_Make(stepcode, 1, code, systemcode)
#define NRR_MakeLog(stepcode, code, systemcode) NR_Make(stepcode, 0, code, systemcode)
          
#define NRR_SUCCESS(result) (NR_GetSeverity(result) == 0)
#define NRR_FAILED(result) (NR_GetSeverity(result) >= 1)
          
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
SE_API NRResult NR_PollEvents();
