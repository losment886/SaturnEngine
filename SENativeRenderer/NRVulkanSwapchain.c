#include "NRDefine.h"
#include "NRVulkanUtils.h"

// ============================================================
// NRVulkanSwapchain.c
// 交换链管理：创建、重建、销毁
// ============================================================

// 前向声明
static NRResult nrVkCreateSwapchainEx(u32 windowWidth, u32 windowHeight);

// ============================================================
// 创建交换链
// ============================================================
NRResult nrVkCreateSwapchain(void)
{
    u32 windowWidth = nr_vk_state.windowWidth;
    u32 windowHeight = nr_vk_state.windowHeight;
    
    return nrVkCreateSwapchainEx(windowWidth, windowHeight);
}

// ============================================================
// 创建交换链（内部实现，带尺寸参数）
// ============================================================
static NRResult nrVkCreateSwapchainEx(u32 windowWidth, u32 windowHeight)
{
    if (nr_vk_state.physicalDevice == VK_NULL_HANDLE || nr_vk_state.device == VK_NULL_HANDLE) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSwapchain, NRR_CODE_RENDERER_NOT_CREATED, 0);
    }

    // 查询表面能力
    VkSurfaceCapabilitiesKHR capabilities;
    vkGetPhysicalDeviceSurfaceCapabilitiesKHR(nr_vk_state.physicalDevice,
                                               nr_vk_state.surface, &capabilities);

    u32 formatCount;
    vkGetPhysicalDeviceSurfaceFormatsKHR(nr_vk_state.physicalDevice,
                                          nr_vk_state.surface, &formatCount, NULL);
    VkSurfaceFormatKHR* formats = (VkSurfaceFormatKHR*)malloc(sizeof(VkSurfaceFormatKHR) * formatCount);
    if (!formats) return NRR_MakeFailure(NRR_STEP_VK_CreateSwapchain, NRR_CODE_OUT_OF_MEMORY, 0);
    vkGetPhysicalDeviceSurfaceFormatsKHR(nr_vk_state.physicalDevice,
                                          nr_vk_state.surface, &formatCount, formats);

    u32 modeCount;
    vkGetPhysicalDeviceSurfacePresentModesKHR(nr_vk_state.physicalDevice,
                                               nr_vk_state.surface, &modeCount, NULL);
    VkPresentModeKHR* modes = (VkPresentModeKHR*)malloc(sizeof(VkPresentModeKHR) * modeCount);
    if (!modes) {
        free(formats);
        return NRR_MakeFailure(NRR_STEP_VK_CreateSwapchain, NRR_CODE_OUT_OF_MEMORY, 0);
    }
    vkGetPhysicalDeviceSurfacePresentModesKHR(nr_vk_state.physicalDevice,
                                               nr_vk_state.surface, &modeCount, modes);

    // 选择格式、呈现模式、分辨率
    VkSurfaceFormatKHR surfaceFormat = nrVkChooseSwapSurfaceFormat(formats, formatCount);
    VkPresentModeKHR presentMode = nrVkChooseSwapPresentMode(modes, modeCount);
    VkExtent2D extent = nrVkChooseSwapExtent(&capabilities, windowWidth, windowHeight);

    free(formats);
    free(modes);

    // 图像数量
    u32 imageCount = capabilities.minImageCount + 1;
    if (capabilities.maxImageCount > 0 && imageCount > capabilities.maxImageCount) {
        imageCount = capabilities.maxImageCount;
    }

    // 交换链创建信息
    VkSwapchainCreateInfoKHR swapchainCI = {0};
    swapchainCI.sType = VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR;
    swapchainCI.surface = nr_vk_state.surface;
    swapchainCI.minImageCount = imageCount;
    swapchainCI.imageFormat = surfaceFormat.format;
    swapchainCI.imageColorSpace = surfaceFormat.colorSpace;
    swapchainCI.imageExtent = extent;
    swapchainCI.imageArrayLayers = 1;
    swapchainCI.imageUsage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT;

    // 队列族共享
    u32 graphicsFamily = nr_vk_state.queueFamilies.graphicsFamily;
    u32 presentFamily = nr_vk_state.queueFamilies.presentFamily;
    u32 queueFamilyIndices[] = {graphicsFamily, presentFamily};

    if (graphicsFamily != presentFamily) {
        swapchainCI.imageSharingMode = VK_SHARING_MODE_CONCURRENT;
        swapchainCI.queueFamilyIndexCount = 2;
        swapchainCI.pQueueFamilyIndices = queueFamilyIndices;
    } else {
        swapchainCI.imageSharingMode = VK_SHARING_MODE_EXCLUSIVE;
        swapchainCI.queueFamilyIndexCount = 0;
        swapchainCI.pQueueFamilyIndices = NULL;
    }

    swapchainCI.preTransform = capabilities.currentTransform;
    swapchainCI.compositeAlpha = VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;
    swapchainCI.presentMode = presentMode;
    swapchainCI.clipped = VK_TRUE;
    swapchainCI.oldSwapchain = VK_NULL_HANDLE;

    VkResult result = vkCreateSwapchainKHR(nr_vk_state.device, &swapchainCI, NULL,
                                            &nr_vk_state.swapchainInfo.swapchain);
    if (result != VK_SUCCESS) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSwapchain, NRR_CODE_SWAPCHAIN_CREATION_FAILED, (u32)result);
    }

    // 获取交换链图像
    vkGetSwapchainImagesKHR(nr_vk_state.device, nr_vk_state.swapchainInfo.swapchain,
                             &imageCount, NULL);
    nr_vk_state.swapchainInfo.images = (VkImage*)malloc(sizeof(VkImage) * imageCount);
    if (!nr_vk_state.swapchainInfo.images) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSwapchain, NRR_CODE_OUT_OF_MEMORY, 0);
    }
    vkGetSwapchainImagesKHR(nr_vk_state.device, nr_vk_state.swapchainInfo.swapchain,
                             &imageCount, nr_vk_state.swapchainInfo.images);

    nr_vk_state.swapchainInfo.imageCount = imageCount;
    nr_vk_state.swapchainInfo.imageFormat = surfaceFormat.format;
    nr_vk_state.swapchainInfo.extent = extent;

    // 创建 ImageViews
    nr_vk_state.swapchainInfo.imageViews = (VkImageView*)malloc(sizeof(VkImageView) * imageCount);
    if (!nr_vk_state.swapchainInfo.imageViews) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSwapchain, NRR_CODE_OUT_OF_MEMORY, 0);
    }

    for (u32 i = 0; i < imageCount; i++) {
        VkImageViewCreateInfo viewCI = {0};
        viewCI.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
        viewCI.image = nr_vk_state.swapchainInfo.images[i];
        viewCI.viewType = VK_IMAGE_VIEW_TYPE_2D;
        viewCI.format = surfaceFormat.format;
        viewCI.components.r = VK_COMPONENT_SWIZZLE_IDENTITY;
        viewCI.components.g = VK_COMPONENT_SWIZZLE_IDENTITY;
        viewCI.components.b = VK_COMPONENT_SWIZZLE_IDENTITY;
        viewCI.components.a = VK_COMPONENT_SWIZZLE_IDENTITY;
        viewCI.subresourceRange.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
        viewCI.subresourceRange.baseMipLevel = 0;
        viewCI.subresourceRange.levelCount = 1;
        viewCI.subresourceRange.baseArrayLayer = 0;
        viewCI.subresourceRange.layerCount = 1;

        result = vkCreateImageView(nr_vk_state.device, &viewCI, NULL,
                                    &nr_vk_state.swapchainInfo.imageViews[i]);
        if (result != VK_SUCCESS) {
            return NRR_MakeFailure(NRR_STEP_VK_CreateSwapchain, NRR_CODE_IMAGE_CREATION_FAILED, (u32)result);
        }
    }

    return NRR_MakeSuccess(NRR_STEP_VK_CreateSwapchain, NRR_CODE_SUCCESS);
}

// ============================================================
// 重建交换链（窗口大小变化时调用）
// ============================================================
NRResult nrVkRecreateSwapchain(u32 newWidth, u32 newHeight)
{
    if (nr_vk_state.device == VK_NULL_HANDLE) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSwapchain, NRR_CODE_RENDERER_NOT_CREATED, 0);
    }

    vkDeviceWaitIdle(nr_vk_state.device);

    // 销毁旧的 ImageViews
    if (nr_vk_state.swapchainInfo.imageViews) {
        for (u32 i = 0; i < nr_vk_state.swapchainInfo.imageCount; i++) {
            if (nr_vk_state.swapchainInfo.imageViews[i] != VK_NULL_HANDLE) {
                vkDestroyImageView(nr_vk_state.device, nr_vk_state.swapchainInfo.imageViews[i], NULL);
            }
        }
        free(nr_vk_state.swapchainInfo.imageViews);
        nr_vk_state.swapchainInfo.imageViews = NULL;
    }

    if (nr_vk_state.swapchainInfo.images) {
        free(nr_vk_state.swapchainInfo.images);
        nr_vk_state.swapchainInfo.images = NULL;
    }

    // 销毁旧的交换链
    if (nr_vk_state.swapchainInfo.swapchain != VK_NULL_HANDLE) {
        vkDestroySwapchainKHR(nr_vk_state.device, nr_vk_state.swapchainInfo.swapchain, NULL);
        nr_vk_state.swapchainInfo.swapchain = VK_NULL_HANDLE;
    }

    // 更新窗口尺寸
    nr_vk_state.windowWidth = newWidth;
    nr_vk_state.windowHeight = newHeight;

    // 重新创建
    return nrVkCreateSwapchainEx(newWidth, newHeight);
}
