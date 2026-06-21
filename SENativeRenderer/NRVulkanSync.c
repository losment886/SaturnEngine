#include "NRDefine.h"
#include "NRVulkanUtils.h"

// ============================================================
// NRVulkanSync.c
// 同步原语管理：Semaphore、Fence
// 支持传统同步和 Synchronization2（根据 useSynchronization2 标志）
// ============================================================

// ============================================================
// 创建 Semaphore
// ============================================================
NRResult nrVkCreateSemaphore(VkSemaphore* outSemaphore)
{
    if (!outSemaphore) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSyncObjects, NRR_CODE_INVALID_PARAMETER, 0);
    }

    VkSemaphoreCreateInfo semaphoreCI = {0};
    semaphoreCI.sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO;

    VkResult result = vkCreateSemaphore(nr_vk_state.device, &semaphoreCI, NULL, outSemaphore);
    VK_CHECK(result, NRR_STEP_VK_CreateSyncObjects, NRR_CODE_SYNC_CREATION_FAILED);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateSyncObjects, NRR_CODE_SUCCESS);
}

// ============================================================
// 创建 Fence（已 signaled 状态，用于初始帧）
// ============================================================
NRResult nrVkCreateFence(b32 signaled, VkFence* outFence)
{
    if (!outFence) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSyncObjects, NRR_CODE_INVALID_PARAMETER, 0);
    }

    VkFenceCreateInfo fenceCI = {0};
    fenceCI.sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
    fenceCI.flags = signaled ? VK_FENCE_CREATE_SIGNALED_BIT : 0;

    VkResult result = vkCreateFence(nr_vk_state.device, &fenceCI, NULL, outFence);
    VK_CHECK(result, NRR_STEP_VK_CreateSyncObjects, NRR_CODE_SYNC_CREATION_FAILED);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateSyncObjects, NRR_CODE_SUCCESS);
}

// ============================================================
// 创建帧同步对象集合（便捷函数）
// 为 maxFramesInFlight 帧创建 Semaphore 和 Fence
// ============================================================
NRResult nrVkCreateFrameSyncObjects(u32 maxFramesInFlight)
{
    if (maxFramesInFlight == 0) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSyncObjects, NRR_CODE_INVALID_PARAMETER, 0);
    }

    // 分配数组
    nr_vk_state.imageAvailableSemaphores = (VkSemaphore*)malloc(sizeof(VkSemaphore) * maxFramesInFlight);
    nr_vk_state.renderFinishedSemaphores = (VkSemaphore*)malloc(sizeof(VkSemaphore) * maxFramesInFlight);
    nr_vk_state.inFlightFences = (VkFence*)malloc(sizeof(VkFence) * maxFramesInFlight);

    if (!nr_vk_state.imageAvailableSemaphores || !nr_vk_state.renderFinishedSemaphores ||
        !nr_vk_state.inFlightFences) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSyncObjects, NRR_CODE_OUT_OF_MEMORY, 0);
    }

    for (u32 i = 0; i < maxFramesInFlight; i++) {
        NRResult result;

        result = nrVkCreateSemaphore(&nr_vk_state.imageAvailableSemaphores[i]);
        if (NRR_FAILED(result)) return result;

        result = nrVkCreateSemaphore(&nr_vk_state.renderFinishedSemaphores[i]);
        if (NRR_FAILED(result)) return result;

        result = nrVkCreateFence(TRUE, &nr_vk_state.inFlightFences[i]);
        if (NRR_FAILED(result)) return result;
    }

    // imagesInFlight 用于追踪每张交换链图像是否正在使用
    nr_vk_state.imagesInFlight = (VkFence*)malloc(sizeof(VkFence) * nr_vk_state.swapchainInfo.imageCount);
    if (nr_vk_state.imagesInFlight) {
        for (u32 i = 0; i < nr_vk_state.swapchainInfo.imageCount; i++) {
            nr_vk_state.imagesInFlight[i] = VK_NULL_HANDLE;
        }
    }

    nr_vk_state.maxFramesInFlight = maxFramesInFlight;

    return NRR_MakeSuccess(NRR_STEP_VK_CreateSyncObjects, NRR_CODE_SUCCESS);
}

// ============================================================
// 等待 Fence
// ============================================================
NRResult nrVkWaitForFence(VkFence fence, u64 timeout)
{
    if (fence == VK_NULL_HANDLE) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSyncObjects, NRR_CODE_INVALID_PARAMETER, 0);
    }

    VkResult result = vkWaitForFences(nr_vk_state.device, 1, &fence, VK_TRUE, timeout);
    VK_CHECK(result, NRR_STEP_VK_CreateSyncObjects, NRR_CODE_SYNC_CREATION_FAILED);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateSyncObjects, NRR_CODE_SUCCESS);
}

// ============================================================
// 重置 Fence
// ============================================================
NRResult nrVkResetFence(VkFence fence)
{
    if (fence == VK_NULL_HANDLE) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSyncObjects, NRR_CODE_INVALID_PARAMETER, 0);
    }

    VkResult result = vkResetFences(nr_vk_state.device, 1, &fence);
    VK_CHECK(result, NRR_STEP_VK_CreateSyncObjects, NRR_CODE_SYNC_CREATION_FAILED);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateSyncObjects, NRR_CODE_SUCCESS);
}

// ============================================================
// 销毁 Semaphore
// ============================================================
void nrVkDestroySemaphore(VkSemaphore semaphore)
{
    if (semaphore != VK_NULL_HANDLE && nr_vk_state.device != VK_NULL_HANDLE) {
        vkDestroySemaphore(nr_vk_state.device, semaphore, NULL);
    }
}

// ============================================================
// 销毁 Fence
// ============================================================
void nrVkDestroyFence(VkFence fence)
{
    if (fence != VK_NULL_HANDLE && nr_vk_state.device != VK_NULL_HANDLE) {
        vkDestroyFence(nr_vk_state.device, fence, NULL);
    }
}
