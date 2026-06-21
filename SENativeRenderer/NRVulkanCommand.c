#include "NRDefine.h"
#include "NRVulkanUtils.h"

// ============================================================
// NRVulkanCommand.c
// 命令池与命令缓冲管理
// ============================================================

// ============================================================
// 创建命令池
// ============================================================
NRResult nrVkCreateCommandPool(VkCommandPool* outPool)
{
    if (!outPool) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateCommandPool, NRR_CODE_INVALID_PARAMETER, 0);
    }

    VkCommandPoolCreateInfo poolCI = {0};
    poolCI.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
    poolCI.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT;
    poolCI.queueFamilyIndex = nr_vk_state.queueFamilies.graphicsFamily;

    VkResult result = vkCreateCommandPool(nr_vk_state.device, &poolCI, NULL, outPool);
    VK_CHECK(result, NRR_STEP_VK_CreateCommandPool, NRR_CODE_COMMAND_BUFFER_FAILED);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateCommandPool, NRR_CODE_SUCCESS);
}

// ============================================================
// 分配命令缓冲
// ============================================================
NRResult nrVkAllocateCommandBuffers(VkCommandPool pool, u32 count, VkCommandBuffer* outBuffers)
{
    if (!outBuffers || count == 0) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateCommandPool, NRR_CODE_INVALID_PARAMETER, 0);
    }

    VkCommandBufferAllocateInfo allocInfo = {0};
    allocInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
    allocInfo.commandPool = pool;
    allocInfo.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
    allocInfo.commandBufferCount = count;

    VkResult result = vkAllocateCommandBuffers(nr_vk_state.device, &allocInfo, outBuffers);
    VK_CHECK(result, NRR_STEP_VK_CreateCommandPool, NRR_CODE_COMMAND_BUFFER_FAILED);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateCommandPool, NRR_CODE_SUCCESS);
}

// ============================================================
// 销毁命令池
// ============================================================
void nrVkDestroyCommandPool(VkCommandPool pool)
{
    if (pool != VK_NULL_HANDLE && nr_vk_state.device != VK_NULL_HANDLE) {
        vkDestroyCommandPool(nr_vk_state.device, pool, NULL);
    }
}
