#include "NRDefine.h"
#include "NRVulkanUtils.h"

// ============================================================
// NRVulkanUtils.c
// Vulkan 工具函数实现：深度资源创建/销毁
// ============================================================

// ============================================================
// 创建深度图像和视图
// ============================================================
NRResult nrVkCreateDepthResources(u32 width, u32 height)
{
    VkFormat depthFormat = VK_FORMAT_D32_SFLOAT;

    // 创建深度图像
    VkImageCreateInfo imageCI = {0};
    imageCI.sType = VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO;
    imageCI.imageType = VK_IMAGE_TYPE_2D;
    imageCI.extent.width = width;
    imageCI.extent.height = height;
    imageCI.extent.depth = 1;
    imageCI.mipLevels = 1;
    imageCI.arrayLayers = 1;
    imageCI.format = depthFormat;
    imageCI.tiling = VK_IMAGE_TILING_OPTIMAL;
    imageCI.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
    imageCI.usage = VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT;
    imageCI.samples = VK_SAMPLE_COUNT_1_BIT;
    imageCI.sharingMode = VK_SHARING_MODE_EXCLUSIVE;

    VkResult result = vkCreateImage(nr_vk_state.device, &imageCI, NULL, &nr_vk_state.depthImage);
    VK_CHECK(result, NRR_STEP_VK_CreateImage, NRR_CODE_IMAGE_CREATION_FAILED);

    // 获取内存需求
    VkMemoryRequirements memRequirements;
    vkGetImageMemoryRequirements(nr_vk_state.device, nr_vk_state.depthImage, &memRequirements);

    // 分配内存
    VkMemoryAllocateInfo allocInfo = {0};
    allocInfo.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
    allocInfo.allocationSize = memRequirements.size;
    if (!nrVkFindMemoryType(nr_vk_state.physicalDevice, memRequirements.memoryTypeBits,
                             VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT, &allocInfo.memoryTypeIndex)) {
        vkDestroyImage(nr_vk_state.device, nr_vk_state.depthImage, NULL);
        nr_vk_state.depthImage = VK_NULL_HANDLE;
        return NRR_MakeFailure(NRR_STEP_VK_CreateImage, NRR_CODE_OUT_OF_MEMORY, 0);
    }

    result = vkAllocateMemory(nr_vk_state.device, &allocInfo, NULL, &nr_vk_state.depthMemory);
    if (result != VK_SUCCESS) {
        vkDestroyImage(nr_vk_state.device, nr_vk_state.depthImage, NULL);
        nr_vk_state.depthImage = VK_NULL_HANDLE;
        VK_CHECK(result, NRR_STEP_VK_CreateImage, NRR_CODE_OUT_OF_MEMORY);
    }

    vkBindImageMemory(nr_vk_state.device, nr_vk_state.depthImage, nr_vk_state.depthMemory, 0);

    // 创建 Image View
    VkImageViewCreateInfo viewCI = {0};
    viewCI.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
    viewCI.image = nr_vk_state.depthImage;
    viewCI.viewType = VK_IMAGE_VIEW_TYPE_2D;
    viewCI.format = depthFormat;
    viewCI.subresourceRange.aspectMask = VK_IMAGE_ASPECT_DEPTH_BIT;
    viewCI.subresourceRange.baseMipLevel = 0;
    viewCI.subresourceRange.levelCount = 1;
    viewCI.subresourceRange.baseArrayLayer = 0;
    viewCI.subresourceRange.layerCount = 1;

    result = vkCreateImageView(nr_vk_state.device, &viewCI, NULL, &nr_vk_state.depthImageView);
    if (result != VK_SUCCESS) {
        vkDestroyImage(nr_vk_state.device, nr_vk_state.depthImage, NULL);
        vkFreeMemory(nr_vk_state.device, nr_vk_state.depthMemory, NULL);
        nr_vk_state.depthImage = VK_NULL_HANDLE;
        nr_vk_state.depthMemory = VK_NULL_HANDLE;
        VK_CHECK(result, NRR_STEP_VK_CreateImage, NRR_CODE_IMAGE_CREATION_FAILED);
    }

    return NRR_MakeSuccess(NRR_STEP_VK_CreateImage, NRR_CODE_SUCCESS);
}

// ============================================================
// 销毁深度资源
// ============================================================
void nrVkDestroyDepthResources(void)
{
    if (nr_vk_state.device == VK_NULL_HANDLE) return;

    if (nr_vk_state.depthImageView != VK_NULL_HANDLE) {
        vkDestroyImageView(nr_vk_state.device, nr_vk_state.depthImageView, NULL);
        nr_vk_state.depthImageView = VK_NULL_HANDLE;
    }
    if (nr_vk_state.depthImage != VK_NULL_HANDLE) {
        vkDestroyImage(nr_vk_state.device, nr_vk_state.depthImage, NULL);
        nr_vk_state.depthImage = VK_NULL_HANDLE;
    }
    if (nr_vk_state.depthMemory != VK_NULL_HANDLE) {
        vkFreeMemory(nr_vk_state.device, nr_vk_state.depthMemory, NULL);
        nr_vk_state.depthMemory = VK_NULL_HANDLE;
    }
}
