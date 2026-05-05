#pragma once

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