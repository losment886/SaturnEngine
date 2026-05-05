#pragma once
#ifdef __cplusplus
#undef __cplusplus
#endif // __cplusplus


#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>

// ============================================================
// 平台相关的库/框架头文件
// ============================================================

// 跨平台导出宏
#include "NRExport.h"

// Vulkan 头文件
#include <vulkan/vulkan.h>

// SDL3 头文件（随 Vulkan SDK 一同安装）
#include <SDL3/SDL.h>
#include <SDL3/SDL_vulkan.h>

// ============================================================ 
// 平台特定头文件
// ============================================================
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

// ============================================================
// 常用类型重定义
// ============================================================
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
