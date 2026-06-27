#include <Metal/Metal.hpp>
#include <Metal/MTLDevice.hpp>
#include <Metal/MTLCommandQueue.hpp>

#include "BasicMetal.hpp"

// ============================================================
// BasicMetal.cpp
// Metal 渲染器核心生命周期管理
// 版本感知：根据 NRRendererCreateInfo.api_target_version 动态适配
// ============================================================

// 全局 Metal 状态
struct NRMetalState nr_metal_state = {0};

// ============================================================
// 内部函数：枚举并选择最佳 Metal 设备
// ============================================================
NRResult nrMetalSelectDevice(const struct NRRendererCreateInfo* createInfo)
{
    // 获取所有可用的 Metal 设备
    NS::Array* devices = MTL::CopyAllDevices();
    if (!devices || devices->count() == 0) {
        if (devices) devices->release();
        return NRR_MakeFailure(NRR_STEP_MTL_SelectDevice, NRR_CODE_DEVICE_NOT_FOUND, 0);
    }

    // 解析目标版本对应的 Metal 功能集等级
    s32 targetFamilyLevel = NRMTL_GET_FAMILY_LEVEL(createInfo->api_target_version);
    if (targetFamilyLevel < 0) {
        devices->release();
        return NRR_MakeFailure(NRR_STEP_MTL_SelectDevice, NRR_CODE_API_VERSION_UNSUPPORTED, 0);
    }

    // 评分选择最佳设备
    MTL::Device* bestDevice = nullptr;
    s32 bestScore = -1;
    s32 bestFamilyLevel = -1;

    NS::UInteger deviceCount = devices->count();
    for (NS::UInteger i = 0; i < deviceCount; i++) {
        MTL::Device* candidate = devices->object<MTL::Device>(i);
        if (!candidate) continue;

        // 检查设备是否支持目标功能集
        // macOS_GPUFamily1 是 macOS 上最基本的 Metal 支持
        b32 familySupported = FALSE;

        // 根据目标版本检查对应的 GPU 家族支持
        // macOS 13+ 要求 GPUFamilyMac2，更早版本要求 GPUFamilyMac1
        if (targetFamilyLevel >= NRMTL_GPU_FAMILY_MACOS_13_0) {
            familySupported = candidate->supportsFamily(MTL::GPUFamilyMac2);
        } else {
            familySupported = candidate->supportsFamily(MTL::GPUFamilyMac1);
        }

        if (!familySupported) continue;

        // 评分：优先选择独立 GPU
        s32 score = 0;
        if (candidate->hasUnifiedMemory()) {
            score += 500;  // 统一内存架构（Apple Silicon）
        } else {
            score += 1000; // 独立 GPU（如 AMD）
        }

        // 根据建议工作集大小加分
        u64 workingSetSize = candidate->recommendedMaxWorkingSetSize();
        score += (s32)(workingSetSize / (1024 * 1024 * 1024)); // 每 GB 加 1 分

        if (score > bestScore) {
            bestScore = score;
            bestDevice = candidate;
            bestFamilyLevel = targetFamilyLevel;
        }
    }

    if (!bestDevice) {
        devices->release();
        return NRR_MakeFailure(NRR_STEP_MTL_SelectDevice, NRR_CODE_DEVICE_NOT_FOUND, 0);
    }

    // 保留选中的设备（retain 以增加引用计数）
    bestDevice->retain();
    nr_metal_state.device = bestDevice;

    // 填充设备属性信息
    const char* nameStr = bestDevice->name()->utf8String();
    if (nameStr) {
        strncpy(nr_metal_state.deviceName, nameStr, sizeof(nr_metal_state.deviceName) - 1);
        nr_metal_state.deviceName[sizeof(nr_metal_state.deviceName) - 1] = '\0';
    } else {
        snprintf(nr_metal_state.deviceName, sizeof(nr_metal_state.deviceName), "Unknown Metal Device");
    }

    nr_metal_state.recommendedMaxWorkingSetSize = bestDevice->recommendedMaxWorkingSetSize();
    nr_metal_state.hasUnifiedMemory = bestDevice->hasUnifiedMemory() ? TRUE : FALSE;
    nr_metal_state.maxBufferLength = bestDevice->maxBufferLength();
    nr_metal_state.metalFamilyLevel = bestFamilyLevel;

    // 获取最大线程组线程数
    MTL::Size maxThreads = bestDevice->maxThreadsPerThreadgroup();
    nr_metal_state.maxThreadsPerThreadgroup[0] = (u32)maxThreads.width;
    nr_metal_state.maxThreadsPerThreadgroup[1] = (u32)maxThreads.height;
    nr_metal_state.maxThreadsPerThreadgroup[2] = (u32)maxThreads.depth;

    // 输出设备信息（调试用）
    fprintf(stdout, "[Metal] Selected device: %s\n", nr_metal_state.deviceName);
    fprintf(stdout, "[Metal]   Unified memory: %s\n", nr_metal_state.hasUnifiedMemory ? "Yes" : "No");
    fprintf(stdout, "[Metal]   Max working set: %llu MB\n",
            (unsigned long long)(nr_metal_state.recommendedMaxWorkingSetSize / (1024 * 1024)));
    fprintf(stdout, "[Metal]   Max buffer length: %llu MB\n",
            (unsigned long long)(nr_metal_state.maxBufferLength / (1024 * 1024)));

    devices->release();
    return NRR_MakeSuccess(NRR_STEP_MTL_SelectDevice, NRR_CODE_SUCCESS);
}

// ============================================================
// 内部函数：创建命令队列（渲染队列）
// ============================================================
NRResult nrMetalCreateCommandQueue(void)
{
    if (!nr_metal_state.device) {
        return NRR_MakeFailure(NRR_STEP_MTL_CreateCommandQueue, NRR_CODE_DEVICE_NOT_FOUND, 0);
    }

    // 创建默认命令队列
    // MTL::CommandQueue 相当于 Vulkan 中的 VkQueue，用于提交命令缓冲
    MTL::CommandQueue* queue = nr_metal_state.device->newCommandQueue();
    if (!queue) {
        return NRR_MakeFailure(NRR_STEP_MTL_CreateCommandQueue, NRR_CODE_QUEUE_NOT_FOUND, 0);
    }

    // 设置队列标签（便于调试）
    NS::String* label = NS::String::string("SENativeRenderer Main Command Queue",
                                            NS::UTF8StringEncoding);
    queue->setLabel(label);

    nr_metal_state.commandQueue = queue;

    fprintf(stdout, "[Metal] Command queue created successfully\n");

    return NRR_MakeSuccess(NRR_STEP_MTL_CreateCommandQueue, NRR_CODE_SUCCESS);
}

// ============================================================
// 内部函数：销毁所有 Metal 资源
// ============================================================
void nrMetalDestroyAll(void)
{
    if (!nr_metal_state.initialized) return;

    // 销毁命令队列
    if (nr_metal_state.commandQueue) {
        nr_metal_state.commandQueue->release();
        nr_metal_state.commandQueue = nullptr;
    }

    // 销毁设备
    if (nr_metal_state.device) {
        nr_metal_state.device->release();
        nr_metal_state.device = nullptr;
    }

    // 重置状态
    memset(nr_metal_state.deviceName, 0, sizeof(nr_metal_state.deviceName));
    nr_metal_state.recommendedMaxWorkingSetSize = 0;
    nr_metal_state.hasUnifiedMemory = FALSE;
    nr_metal_state.maxBufferLength = 0;
    nr_metal_state.maxThreadsPerThreadgroup[0] = 0;
    nr_metal_state.maxThreadsPerThreadgroup[1] = 0;
    nr_metal_state.maxThreadsPerThreadgroup[2] = 0;
    nr_metal_state.apiVersion = 0;
    nr_metal_state.metalFamilyLevel = -1;
    nr_metal_state.initialized = FALSE;

    fprintf(stdout, "[Metal] All resources destroyed\n");
}
