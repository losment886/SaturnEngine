#pragma once

// ============================================================
// BasicMetal.hpp
// Metal 渲染器核心定义
// 使用 metal-cpp (C++ binding) 实现
// ============================================================

#include <Metal/Metal.hpp>
#include <Metal/MTLDevice.hpp>
#include <Metal/MTLCommandQueue.hpp>
#include <Metal/MTLVersion.hpp>

#include "../NRDefine.h"

// ============================================================
// Metal 内部步骤码（30~39）
// ============================================================
#define NRR_STEP_MTL_CreateDevice         30
#define NRR_STEP_MTL_SelectDevice         31
#define NRR_STEP_MTL_CreateCommandQueue   32
#define NRR_STEP_MTL_DestroyAll           33

// ============================================================
// Metal 版本映射宏
// 根据 macOS 版本确定 Metal 功能集版本
// ============================================================

// macOS 版本对应的 Metal 功能集
#define NRMTL_GPU_FAMILY_MACOS_10_15  0  // Metal 2.0 (macOS 10.15)
#define NRMTL_GPU_FAMILY_MACOS_11_0   1  // Metal 2.3 (macOS 11.0)
#define NRMTL_GPU_FAMILY_MACOS_12_0   2  // Metal 2.4 (macOS 12.0)
#define NRMTL_GPU_FAMILY_MACOS_13_0   3  // Metal 3.0 (macOS 13.0)
#define NRMTL_GPU_FAMILY_MACOS_14_0   4  // Metal 3.1 (macOS 14.0)
#define NRMTL_GPU_FAMILY_MACOS_15_0   5  // Metal 3.2 (macOS 15.0)

// 从 NRVersion (api_target_version) 获取 Metal 功能集等级
#define NRMTL_GET_FAMILY_LEVEL(version)                                     \
    ((NRV_GetMajor(version) >= 15) ? NRMTL_GPU_FAMILY_MACOS_15_0 :         \
     (NRV_GetMajor(version) >= 14) ? NRMTL_GPU_FAMILY_MACOS_14_0 :         \
     (NRV_GetMajor(version) >= 13) ? NRMTL_GPU_FAMILY_MACOS_13_0 :         \
     (NRV_GetMajor(version) >= 12) ? NRMTL_GPU_FAMILY_MACOS_12_0 :         \
     (NRV_GetMajor(version) >= 11) ? NRMTL_GPU_FAMILY_MACOS_11_0 :         \
     (NRV_GetMajor(version) >= 10 && NRV_GetMinor(version) >= 15)          \
         ? NRMTL_GPU_FAMILY_MACOS_10_15 :                                  \
     -1)

// ============================================================
// Metal 内部状态结构体
// ============================================================
struct NRMetalState {
    // 核心对象
    MTL::Device* device;
    MTL::CommandQueue* commandQueue;

    // 版本信息
    NRVersion apiVersion;
    s32 metalFamilyLevel;  // 上述 NRMTL_GPU_FAMILY_* 值

    // 设备属性
    char deviceName[256];
    u64 recommendedMaxWorkingSetSize;  // 建议最大工作集大小（字节）
    b32 hasUnifiedMemory;             // 是否统一内存架构
    u64 maxBufferLength;              // 最大 Buffer 长度
    u32 maxThreadsPerThreadgroup[3];  // 每线程组最大线程数

    // 是否已初始化
    b32 initialized;
};

// 全局 Metal 状态
extern struct NRMetalState nr_metal_state;

// ============================================================
// Metal 内部函数声明
// ============================================================

// 枚举并选择最佳 Metal 设备
NRResult nrMetalSelectDevice(const struct NRRendererCreateInfo* createInfo);

// 从设备创建命令队列（渲染队列）
NRResult nrMetalCreateCommandQueue(void);

// 销毁所有 Metal 资源
void nrMetalDestroyAll(void);
