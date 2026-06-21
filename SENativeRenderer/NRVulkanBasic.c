#include "NRDefine.h"
#include "NRVulkanUtils.h"

// ============================================================
// NRVulkanBasic.c
// Vulkan 渲染器核心生命周期管理
// 版本感知：根据 NRRendererCreateInfo.api_target_version 动态适配
// ============================================================

// 全局 Vulkan 状态
struct NRVkState nr_vk_state = {0};

// ============================================================
// 内部辅助函数实现
// ============================================================

VKAPI_ATTR VkBool32 VKAPI_CALL nrVkDebugCallback(
    VkDebugUtilsMessageSeverityFlagBitsEXT messageSeverity,
    VkDebugUtilsMessageTypeFlagsEXT messageType,
    const VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
    void* pUserData)
{
    (void)messageType;
    (void)pUserData;

    if (messageSeverity >= VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) {
        fprintf(stderr, "[Vulkan ERROR] %s\n", pCallbackData->pMessage);
    } else if (messageSeverity >= VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT) {
        fprintf(stderr, "[Vulkan WARNING] %s\n", pCallbackData->pMessage);
    } else {
        fprintf(stdout, "[Vulkan INFO] %s\n", pCallbackData->pMessage);
    }
    return VK_FALSE;
}

b32 nrVkFindQueueFamilies(VkPhysicalDevice device, VkSurfaceKHR surface,
                          u32* outGraphicsFamily, u32* outPresentFamily, u32* outComputeFamily)
{
    u32 queueFamilyCount = 0;
    vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, NULL);
    if (queueFamilyCount == 0) return FALSE;

    VkQueueFamilyProperties* families = (VkQueueFamilyProperties*)malloc(
        sizeof(VkQueueFamilyProperties) * queueFamilyCount);
    if (!families) return FALSE;

    vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, families);

    b32 graphicsFound = FALSE;
    b32 presentFound = FALSE;
    b32 computeFound = FALSE;

    for (u32 i = 0; i < queueFamilyCount; i++) {
        // 图形队列
        if (!graphicsFound && (families[i].queueFlags & VK_QUEUE_GRAPHICS_BIT)) {
            *outGraphicsFamily = i;
            graphicsFound = TRUE;
        }
        // 计算队列（优先找单独的，否则复用图形队列）
        if (!computeFound && (families[i].queueFlags & VK_QUEUE_COMPUTE_BIT) &&
            !(families[i].queueFlags & VK_QUEUE_GRAPHICS_BIT)) {
            *outComputeFamily = i;
            computeFound = TRUE;
        }
        // 呈现队列
        if (!presentFound && surface != VK_NULL_HANDLE) {
            VkBool32 presentSupport = VK_FALSE;
            vkGetPhysicalDeviceSurfaceSupportKHR(device, i, surface, &presentSupport);
            if (presentSupport) {
                *outPresentFamily = i;
                presentFound = TRUE;
            }
        }
    }

    // 如果没找到独立计算队列，复用图形队列
    if (!computeFound && graphicsFound) {
        *outComputeFamily = *outGraphicsFamily;
        computeFound = TRUE;
    }

    free(families);

    return graphicsFound && presentFound && computeFound;
}

b32 nrVkCheckDeviceExtensionSupport(VkPhysicalDevice device,
                                    const char** requiredExtensions, u32 requiredCount,
                                    const char** optionalExtensions, u32 optionalCount,
                                    b32* outOptionalSupported)
{
    u32 extensionCount;
    vkEnumerateDeviceExtensionProperties(device, NULL, &extensionCount, NULL);

    VkExtensionProperties* availableExtensions = (VkExtensionProperties*)malloc(
        sizeof(VkExtensionProperties) * extensionCount);
    if (!availableExtensions) return FALSE;

    vkEnumerateDeviceExtensionProperties(device, NULL, &extensionCount, availableExtensions);

    // 检查必需扩展
    for (u32 i = 0; i < requiredCount; i++) {
        b32 found = FALSE;
        for (u32 j = 0; j < extensionCount; j++) {
            if (strcmp(requiredExtensions[i], availableExtensions[j].extensionName) == 0) {
                found = TRUE;
                break;
            }
        }
        if (!found) {
            free(availableExtensions);
            return FALSE;
        }
    }

    // 检查可选扩展
    if (outOptionalSupported) {
        *outOptionalSupported = TRUE;
        for (u32 i = 0; i < optionalCount; i++) {
            b32 found = FALSE;
            for (u32 j = 0; j < extensionCount; j++) {
                if (strcmp(optionalExtensions[i], availableExtensions[j].extensionName) == 0) {
                    found = TRUE;
                    break;
                }
            }
            if (!found) {
                *outOptionalSupported = FALSE;
                break;
            }
        }
    }

    free(availableExtensions);
    return TRUE;
}

VkSurfaceFormatKHR nrVkChooseSwapSurfaceFormat(const VkSurfaceFormatKHR* formats, u32 formatCount)
{
    // 优先选择 8-bit sRGB
    for (u32 i = 0; i < formatCount; i++) {
        if (formats[i].format == VK_FORMAT_B8G8R8A8_SRGB &&
            formats[i].colorSpace == VK_COLOR_SPACE_SRGB_NONLINEAR_KHR) {
            return formats[i];
        }
    }
    // 回退到第一个
    return formats[0];
}

VkPresentModeKHR nrVkChooseSwapPresentMode(const VkPresentModeKHR* modes, u32 modeCount)
{
    // 优先 Mailbox（三重缓冲）
    for (u32 i = 0; i < modeCount; i++) {
        if (modes[i] == VK_PRESENT_MODE_MAILBOX_KHR) {
            return modes[i];
        }
    }
    // 其次 FIFO（Vulkan 保证支持）
    for (u32 i = 0; i < modeCount; i++) {
        if (modes[i] == VK_PRESENT_MODE_FIFO_KHR) {
            return modes[i];
        }
    }
    // 回退到 Immediate
    return VK_PRESENT_MODE_IMMEDIATE_KHR;
}

VkExtent2D nrVkChooseSwapExtent(const VkSurfaceCapabilitiesKHR* capabilities, u32 windowWidth, u32 windowHeight)
{
    if (capabilities->currentExtent.width != UINT32_MAX) {
        return capabilities->currentExtent;
    }
    VkExtent2D extent = {
        .width = windowWidth > capabilities->maxImageExtent.width
                     ? capabilities->maxImageExtent.width
                     : (windowWidth < capabilities->minImageExtent.width
                            ? capabilities->minImageExtent.width
                            : windowWidth),
        .height = windowHeight > capabilities->maxImageExtent.height
                      ? capabilities->maxImageExtent.height
                      : (windowHeight < capabilities->minImageExtent.height
                             ? capabilities->minImageExtent.height
                             : windowHeight),
    };
    return extent;
}

b32 nrVkFindMemoryType(VkPhysicalDevice physicalDevice, u32 typeFilter,
                       VkMemoryPropertyFlags properties, u32* outMemoryType)
{
    VkPhysicalDeviceMemoryProperties memProperties;
    vkGetPhysicalDeviceMemoryProperties(physicalDevice, &memProperties);

    for (u32 i = 0; i < memProperties.memoryTypeCount; i++) {
        if ((typeFilter & (1 << i)) &&
            (memProperties.memoryTypes[i].propertyFlags & properties) == properties) {
            *outMemoryType = i;
            return TRUE;
        }
    }
    return FALSE;
}

// ============================================================
// 内部函数：创建 VkInstance（版本感知）
// ============================================================
static NRResult nrVkCreateInstance(const struct NRRendererCreateInfo* createInfo)
{
    // 确定 Vulkan 应用版本
    u32 vulkanApiVersion;
    NRVersion targetVer = createInfo->api_target_version;
    u16 major = NRV_GetMajor(targetVer);
    u16 minor = NRV_GetMinor(targetVer);

    if (major == 1 && minor >= 4) {
        vulkanApiVersion = VK_API_VERSION_1_3; // Vulkan 1.4 基于 1.3
    } else if (major == 1 && minor == 3) {
        vulkanApiVersion = VK_API_VERSION_1_3;
    } else if (major == 1 && minor == 2) {
        vulkanApiVersion = VK_API_VERSION_1_2;
    } else if (major == 1 && minor == 1) {
        vulkanApiVersion = VK_API_VERSION_1_1;
    } else {
        vulkanApiVersion = VK_API_VERSION_1_0;
    }

    // 收集实例扩展
    u32 extensionCount = 0;
    const char* extensionNames[64];

    // SDL3 要求的 Vulkan 实例扩展
    u32 sdlExtensionCount = 0;
    const char* const* sdlExtensions = NULL;
    if (nr_window) {
        sdlExtensions = SDL_Vulkan_GetInstanceExtensions(&sdlExtensionCount);
    }

    for (u32 i = 0; i < sdlExtensionCount && extensionCount < 64; i++) {
        extensionNames[extensionCount++] = sdlExtensions[i];
    }

    // 调试扩展（可选）
    b32 debugUtilsSupported = FALSE;
    {
        u32 layerCount;
        vkEnumerateInstanceLayerProperties(&layerCount, NULL);
        VkLayerProperties* layers = (VkLayerProperties*)malloc(sizeof(VkLayerProperties) * layerCount);
        if (layers) {
            vkEnumerateInstanceLayerProperties(&layerCount, layers);
            for (u32 i = 0; i < layerCount; i++) {
                if (strcmp(layers[i].layerName, "VK_LAYER_KHRONOS_validation") == 0) {
                    debugUtilsSupported = TRUE;
                    break;
                }
            }
            free(layers);
        }
    }

    if (debugUtilsSupported && extensionCount < 64) {
        extensionNames[extensionCount++] = VK_EXT_DEBUG_UTILS_EXTENSION_NAME;
    }

    // 用户指定的额外实例扩展
    for (s32 i = 0; i < createInfo->required_instance_extensions_count && extensionCount < 64; i++) {
        // 去重
        b32 alreadyAdded = FALSE;
        for (u32 j = 0; j < extensionCount; j++) {
            if (strcmp(extensionNames[j], createInfo->required_instance_extensions[i]) == 0) {
                alreadyAdded = TRUE;
                break;
            }
        }
        if (!alreadyAdded) {
            extensionNames[extensionCount++] = createInfo->required_instance_extensions[i];
        }
    }

    // 可选扩展
    for (s32 i = 0; i < createInfo->optional_instance_extensions_count && extensionCount < 64; i++) {
        b32 alreadyAdded = FALSE;
        for (u32 j = 0; j < extensionCount; j++) {
            if (strcmp(extensionNames[j], createInfo->optional_instance_extensions[i]) == 0) {
                alreadyAdded = TRUE;
                break;
            }
        }
        if (!alreadyAdded) {
            extensionNames[extensionCount++] = createInfo->optional_instance_extensions[i];
        }
    }

    // 应用信息
    VkApplicationInfo appInfo = {0};
    appInfo.sType = VK_STRUCTURE_TYPE_APPLICATION_INFO;
    appInfo.pApplicationName = createInfo->app_name ? createInfo->app_name : "SaturnEngine";
    appInfo.applicationVersion = (u32)createInfo->app_version;
    appInfo.pEngineName = createInfo->renderer_name ? createInfo->renderer_name : "SENativeRenderer";
    appInfo.engineVersion = VK_MAKE_VERSION(1, 0, 0);
    appInfo.apiVersion = vulkanApiVersion;

    // 实例创建信息
    VkInstanceCreateInfo instanceCI = {0};
    instanceCI.sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO;
    instanceCI.pApplicationInfo = &appInfo;
    instanceCI.enabledExtensionCount = extensionCount;
    instanceCI.ppEnabledExtensionNames = extensionNames;

    // 调试层
    const char* validationLayers[] = {"VK_LAYER_KHRONOS_validation"};
    VkDebugUtilsMessengerCreateInfoEXT debugCI = {0};
    if (debugUtilsSupported) {
        instanceCI.enabledLayerCount = 1;
        instanceCI.ppEnabledLayerNames = validationLayers;

        debugCI.sType = VK_STRUCTURE_TYPE_DEBUG_UTILS_MESSENGER_CREATE_INFO_EXT;
        debugCI.messageSeverity = VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT |
                                  VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT;
        debugCI.messageType = VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT |
                              VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT |
                              VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT;
        debugCI.pfnUserCallback = nrVkDebugCallback;
        instanceCI.pNext = &debugCI;
    }

    VkResult result = vkCreateInstance(&instanceCI, NULL, &nr_vk_state.instance);
    VK_CHECK(result, NRR_STEP_VK_CreateInstance, NRR_CODE_NOT_INITIALIZED);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateInstance, NRR_CODE_SUCCESS);
}

// ============================================================
// 内部函数：创建 Surface
// ============================================================
static NRResult nrVkCreateSurface(void)
{
    if (!nr_window) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSurface, NRR_CODE_WINDOW_NOT_CREATED, 0);
    }

    VkResult result;
    if (SDL_Vulkan_CreateSurface(nr_window, nr_vk_state.instance, NULL, &result)) {
        nr_vk_state.surface = (VkSurfaceKHR)result;
        // 注意：SDL_Vulkan_CreateSurface 的第四个参数是 VkSurfaceKHR*，但 SDL3 的 API 可能不同
        // 使用标准方式
    }

    // 标准方式：通过 SDL3 获取 surface
    if (!SDL_Vulkan_CreateSurface(nr_window, nr_vk_state.instance, NULL, &nr_vk_state.surface)) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSurface, NRR_CODE_SURFACE_CREATION_FAILED, 0);
    }

    if (nr_vk_state.surface == VK_NULL_HANDLE) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateSurface, NRR_CODE_SURFACE_CREATION_FAILED, 0);
    }

    return NRR_MakeSuccess(NRR_STEP_VK_CreateSurface, NRR_CODE_SUCCESS);
}

// ============================================================
// 内部函数：选择物理设备
// ============================================================
static NRResult nrVkSelectPhysicalDevice(const struct NRRendererCreateInfo* createInfo)
{
    u32 deviceCount = 0;
    vkEnumeratePhysicalDevices(nr_vk_state.instance, &deviceCount, NULL);
    if (deviceCount == 0) {
        return NRR_MakeFailure(NRR_STEP_VK_SelectPhysicalDevice, NRR_CODE_DEVICE_NOT_FOUND, 0);
    }

    VkPhysicalDevice* devices = (VkPhysicalDevice*)malloc(sizeof(VkPhysicalDevice) * deviceCount);
    if (!devices) {
        return NRR_MakeFailure(NRR_STEP_VK_SelectPhysicalDevice, NRR_CODE_OUT_OF_MEMORY, 0);
    }
    vkEnumeratePhysicalDevices(nr_vk_state.instance, &deviceCount, devices);

    // 收集设备扩展列表
    // 基础必需扩展：交换链
    const char* baseRequiredExtensions[] = {"VK_KHR_swapchain"};
    u32 baseRequiredCount = 1;

    // 根据版本添加扩展
    const char* versionRequiredExtensions[8];
    u32 versionRequiredCount = 0;

    NRVersion targetVer = createInfo->api_target_version;
    if (NRVK_IS_1_4(targetVer)) {
        versionRequiredExtensions[versionRequiredCount++] = "VK_KHR_dynamic_rendering";
        versionRequiredExtensions[versionRequiredCount++] = "VK_KHR_synchronization2";
        versionRequiredExtensions[versionRequiredCount++] = "VK_KHR_maintenance4";
        versionRequiredExtensions[versionRequiredCount++] = "VK_KHR_shader_float_controls2";
    } else if (NRVK_HAS_DYNAMIC_RENDERING(targetVer)) {
        // 1.3 可选扩展，不作为必需
    }

    // 合并扩展列表
    const char* allRequiredExtensions[16];
    u32 allRequiredCount = 0;
    for (u32 i = 0; i < baseRequiredCount && allRequiredCount < 16; i++) {
        allRequiredExtensions[allRequiredCount++] = baseRequiredExtensions[i];
    }
    for (u32 i = 0; i < versionRequiredCount && allRequiredCount < 16; i++) {
        allRequiredExtensions[allRequiredCount++] = versionRequiredExtensions[i];
    }
    // 用户指定的必需设备扩展
    for (s32 i = 0; i < createInfo->required_device_extensions_count && allRequiredCount < 16; i++) {
        allRequiredExtensions[allRequiredCount++] = createInfo->required_device_extensions[i];
    }

    // 评分选择最佳设备
    s32 bestScore = -1;
    s32 bestIndex = -1;

    for (u32 i = 0; i < deviceCount; i++) {
        VkPhysicalDeviceProperties props;
        vkGetPhysicalDeviceProperties(devices[i], &props);

        // 检查扩展支持
        b32 optionalSupported = FALSE;
        if (!nrVkCheckDeviceExtensionSupport(devices[i],
                allRequiredExtensions, allRequiredCount,
                createInfo->optional_device_extensions,
                createInfo->optional_device_extensions_count,
                &optionalSupported)) {
            continue;
        }

        // 检查队列族
        struct NRVkQueueFamilies families = {0};
        if (!nrVkFindQueueFamilies(devices[i], nr_vk_state.surface,
                                   &families.graphicsFamily,
                                   &families.presentFamily,
                                   &families.computeFamily)) {
            continue;
        }

        // 检查交换链支持
        VkSurfaceCapabilitiesKHR capabilities;
        vkGetPhysicalDeviceSurfaceCapabilitiesKHR(devices[i], nr_vk_state.surface, &capabilities);

        u32 formatCount, modeCount;
        vkGetPhysicalDeviceSurfaceFormatsKHR(devices[i], nr_vk_state.surface, &formatCount, NULL);
        vkGetPhysicalDeviceSurfacePresentModesKHR(devices[i], nr_vk_state.surface, &modeCount, NULL);

        if (formatCount == 0 || modeCount == 0) continue;

        // 评分
        s32 score = 0;
        if (props.deviceType == VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU) score += 1000;
        if (props.deviceType == VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU) score += 500;
        score += (s32)(props.limits.maxImageDimension2D / 1024);

        if (score > bestScore) {
            bestScore = score;
            bestIndex = (s32)i;
        }
    }

    if (bestIndex < 0) {
        free(devices);
        return NRR_MakeFailure(NRR_STEP_VK_SelectPhysicalDevice, NRR_CODE_DEVICE_NOT_FOUND, 0);
    }

    nr_vk_state.physicalDevice = devices[bestIndex];

    // 获取队列族索引
    nrVkFindQueueFamilies(nr_vk_state.physicalDevice, nr_vk_state.surface,
                          &nr_vk_state.queueFamilies.graphicsFamily,
                          &nr_vk_state.queueFamilies.presentFamily,
                          &nr_vk_state.queueFamilies.computeFamily);
    nr_vk_state.queueFamilies.graphicsFound = TRUE;
    nr_vk_state.queueFamilies.presentFound = TRUE;
    nr_vk_state.queueFamilies.computeFound = TRUE;

    free(devices);
    return NRR_MakeSuccess(NRR_STEP_VK_SelectPhysicalDevice, NRR_CODE_SUCCESS);
}

// ============================================================
// 内部函数：创建逻辑设备
// ============================================================
static NRResult nrVkCreateDevice(const struct NRRendererCreateInfo* createInfo)
{
    // 队列优先级
    f32 queuePriority = 1.0f;

    // 收集唯一队列族
    u32 uniqueFamilies[3];
    u32 uniqueCount = 0;

    u32 graphicsFamily = nr_vk_state.queueFamilies.graphicsFamily;
    u32 presentFamily = nr_vk_state.queueFamilies.presentFamily;
    u32 computeFamily = nr_vk_state.queueFamilies.computeFamily;

    uniqueFamilies[uniqueCount++] = graphicsFamily;
    if (presentFamily != graphicsFamily) uniqueFamilies[uniqueCount++] = presentFamily;
    if (computeFamily != graphicsFamily && computeFamily != presentFamily)
        uniqueFamilies[uniqueCount++] = computeFamily;

    // 队列创建信息
    VkDeviceQueueCreateInfo queueCIs[3];
    for (u32 i = 0; i < uniqueCount; i++) {
        queueCIs[i].sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
        queueCIs[i].queueFamilyIndex = uniqueFamilies[i];
        queueCIs[i].queueCount = 1;
        queueCIs[i].pQueuePriorities = &queuePriority;
        queueCIs[i].flags = 0;
        queueCIs[i].pNext = NULL;
    }

    // 收集设备扩展
    const char* deviceExtensions[16];
    u32 deviceExtensionCount = 0;

    deviceExtensions[deviceExtensionCount++] = "VK_KHR_swapchain";

    NRVersion targetVer = createInfo->api_target_version;
    if (NRVK_IS_1_4(targetVer)) {
        deviceExtensions[deviceExtensionCount++] = "VK_KHR_dynamic_rendering";
        deviceExtensions[deviceExtensionCount++] = "VK_KHR_synchronization2";
        deviceExtensions[deviceExtensionCount++] = "VK_KHR_maintenance4";
        deviceExtensions[deviceExtensionCount++] = "VK_KHR_shader_float_controls2";
        nr_vk_state.useDynamicRendering = TRUE;
        nr_vk_state.useSynchronization2 = TRUE;
    } else if (NRVK_HAS_DYNAMIC_RENDERING(targetVer)) {
        // 1.3：尝试启用 Dynamic Rendering 和 Synchronization2（可选）
        // 检查是否支持
        b32 optionalSupported = FALSE;
        const char* optionalExts[] = {
            "VK_KHR_dynamic_rendering",
            "VK_KHR_synchronization2",
            "VK_KHR_maintenance4"
        };
        if (nrVkCheckDeviceExtensionSupport(nr_vk_state.physicalDevice,
                NULL, 0, optionalExts, 3, &optionalSupported) && optionalSupported) {
            deviceExtensions[deviceExtensionCount++] = "VK_KHR_dynamic_rendering";
            deviceExtensions[deviceExtensionCount++] = "VK_KHR_synchronization2";
            deviceExtensions[deviceExtensionCount++] = "VK_KHR_maintenance4";
            nr_vk_state.useDynamicRendering = TRUE;
            nr_vk_state.useSynchronization2 = TRUE;
        }
    }

    // 用户指定的设备扩展
    for (s32 i = 0; i < createInfo->required_device_extensions_count && deviceExtensionCount < 16; i++) {
        b32 alreadyAdded = FALSE;
        for (u32 j = 0; j < deviceExtensionCount; j++) {
            if (strcmp(deviceExtensions[j], createInfo->required_device_extensions[i]) == 0) {
                alreadyAdded = TRUE;
                break;
            }
        }
        if (!alreadyAdded) {
            deviceExtensions[deviceExtensionCount++] = createInfo->required_device_extensions[i];
        }
    }
    for (s32 i = 0; i < createInfo->optional_device_extensions_count && deviceExtensionCount < 16; i++) {
        b32 alreadyAdded = FALSE;
        for (u32 j = 0; j < deviceExtensionCount; j++) {
            if (strcmp(deviceExtensions[j], createInfo->optional_device_extensions[i]) == 0) {
                alreadyAdded = TRUE;
                break;
            }
        }
        if (!alreadyAdded) {
            deviceExtensions[deviceExtensionCount++] = createInfo->optional_device_extensions[i];
        }
    }

    // 设备特性
    VkPhysicalDeviceFeatures deviceFeatures = {0};
    deviceFeatures.samplerAnisotropy = VK_TRUE;

    // 设备创建信息
    VkDeviceCreateInfo deviceCI = {0};
    deviceCI.sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO;
    deviceCI.queueCreateInfoCount = uniqueCount;
    deviceCI.pQueueCreateInfos = queueCIs;
    deviceCI.enabledExtensionCount = deviceExtensionCount;
    deviceCI.ppEnabledExtensionNames = deviceExtensions;
    deviceCI.pEnabledFeatures = &deviceFeatures;

    // Vulkan 1.3/1.4 特性链
    VkPhysicalDeviceVulkan13Features features13 = {0};
    VkPhysicalDeviceDynamicRenderingFeatures dynamicRenderingFeatures = {0};
    VkPhysicalDeviceSynchronization2Features synchronization2Features = {0};
    VkPhysicalDeviceMaintenance4Features maintenance4Features = {0};

    void** nextPtr = (void**)&deviceCI.pNext;

    if (nr_vk_state.useDynamicRendering) {
        dynamicRenderingFeatures.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_DYNAMIC_RENDERING_FEATURES;
        dynamicRenderingFeatures.dynamicRendering = VK_TRUE;
        *nextPtr = &dynamicRenderingFeatures;
        nextPtr = &dynamicRenderingFeatures.pNext;
    }

    if (nr_vk_state.useSynchronization2) {
        synchronization2Features.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_SYNCHRONIZATION_2_FEATURES;
        synchronization2Features.synchronization2 = VK_TRUE;
        *nextPtr = &synchronization2Features;
        nextPtr = &synchronization2Features.pNext;
    }

    if (NRVK_HAS_MAINTENANCE4(targetVer)) {
        maintenance4Features.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_MAINTENANCE_4_FEATURES;
        maintenance4Features.maintenance4 = VK_TRUE;
        *nextPtr = &maintenance4Features;
        nextPtr = &maintenance4Features.pNext;
    }

    VkResult result = vkCreateDevice(nr_vk_state.physicalDevice, &deviceCI, NULL, &nr_vk_state.device);
    VK_CHECK(result, NRR_STEP_VK_CreateDevice, NRR_CODE_NOT_INITIALIZED);

    // 获取队列
    vkGetDeviceQueue(nr_vk_state.device, graphicsFamily, 0, &nr_vk_state.graphicsQueue);
    vkGetDeviceQueue(nr_vk_state.device, presentFamily, 0, &nr_vk_state.presentQueue);
    vkGetDeviceQueue(nr_vk_state.device, computeFamily, 0, &nr_vk_state.computeQueue);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateDevice, NRR_CODE_SUCCESS);
}

// ============================================================
// 内部函数：销毁所有 Vulkan 资源
// ============================================================
static void nrVkDestroyAll(void)
{
    if (!nr_vk_state.initialized) return;

    // 等待设备空闲
    if (nr_vk_state.device != VK_NULL_HANDLE) {
        vkDeviceWaitIdle(nr_vk_state.device);
    }

    // 销毁渲染资源（深度、Buffer、Descriptor、Shader）
    nrVkDestroyRenderResources();

    // 销毁同步对象
    if (nr_vk_state.imageAvailableSemaphores) {
        for (u32 i = 0; i < nr_vk_state.maxFramesInFlight; i++) {
            if (nr_vk_state.imageAvailableSemaphores[i] != VK_NULL_HANDLE)
                vkDestroySemaphore(nr_vk_state.device, nr_vk_state.imageAvailableSemaphores[i], NULL);
            if (nr_vk_state.renderFinishedSemaphores[i] != VK_NULL_HANDLE)
                vkDestroySemaphore(nr_vk_state.device, nr_vk_state.renderFinishedSemaphores[i], NULL);
            if (nr_vk_state.inFlightFences[i] != VK_NULL_HANDLE)
                vkDestroyFence(nr_vk_state.device, nr_vk_state.inFlightFences[i], NULL);
        }
        free(nr_vk_state.imageAvailableSemaphores);
        free(nr_vk_state.renderFinishedSemaphores);
        free(nr_vk_state.inFlightFences);
        nr_vk_state.imageAvailableSemaphores = NULL;
        nr_vk_state.renderFinishedSemaphores = NULL;
        nr_vk_state.inFlightFences = NULL;
    }

    if (nr_vk_state.imagesInFlight) {
        free(nr_vk_state.imagesInFlight);
        nr_vk_state.imagesInFlight = NULL;
    }

    // 销毁命令缓冲和池
    if (nr_vk_state.commandPool != VK_NULL_HANDLE) {
        vkDestroyCommandPool(nr_vk_state.device, nr_vk_state.commandPool, NULL);
        nr_vk_state.commandPool = VK_NULL_HANDLE;
    }

    // 销毁管线
    if (nr_vk_state.graphicsPipeline != VK_NULL_HANDLE) {
        vkDestroyPipeline(nr_vk_state.device, nr_vk_state.graphicsPipeline, NULL);
        nr_vk_state.graphicsPipeline = VK_NULL_HANDLE;
    }
    if (nr_vk_state.pipelineLayout != VK_NULL_HANDLE) {
        vkDestroyPipelineLayout(nr_vk_state.device, nr_vk_state.pipelineLayout, NULL);
        nr_vk_state.pipelineLayout = VK_NULL_HANDLE;
    }

    // 销毁 Framebuffers
    if (nr_vk_state.framebuffers) {
        for (u32 i = 0; i < nr_vk_state.swapchainInfo.imageCount; i++) {
            if (nr_vk_state.framebuffers[i] != VK_NULL_HANDLE)
                vkDestroyFramebuffer(nr_vk_state.device, nr_vk_state.framebuffers[i], NULL);
        }
        free(nr_vk_state.framebuffers);
        nr_vk_state.framebuffers = NULL;
    }

    // 销毁 RenderPass
    if (nr_vk_state.renderPass != VK_NULL_HANDLE) {
        vkDestroyRenderPass(nr_vk_state.device, nr_vk_state.renderPass, NULL);
        nr_vk_state.renderPass = VK_NULL_HANDLE;
    }

    // 销毁交换链 ImageViews
    if (nr_vk_state.swapchainInfo.imageViews) {
        for (u32 i = 0; i < nr_vk_state.swapchainInfo.imageCount; i++) {
            if (nr_vk_state.swapchainInfo.imageViews[i] != VK_NULL_HANDLE)
                vkDestroyImageView(nr_vk_state.device, nr_vk_state.swapchainInfo.imageViews[i], NULL);
        }
        free(nr_vk_state.swapchainInfo.imageViews);
        nr_vk_state.swapchainInfo.imageViews = NULL;
    }

    // 销毁交换链
    if (nr_vk_state.swapchainInfo.swapchain != VK_NULL_HANDLE) {
        vkDestroySwapchainKHR(nr_vk_state.device, nr_vk_state.swapchainInfo.swapchain, NULL);
        nr_vk_state.swapchainInfo.swapchain = VK_NULL_HANDLE;
    }

    if (nr_vk_state.swapchainInfo.images) {
        free(nr_vk_state.swapchainInfo.images);
        nr_vk_state.swapchainInfo.images = NULL;
    }

    // 销毁 Surface
    if (nr_vk_state.surface != VK_NULL_HANDLE) {
        vkDestroySurfaceKHR(nr_vk_state.instance, nr_vk_state.surface, NULL);
        nr_vk_state.surface = VK_NULL_HANDLE;
    }

    // 销毁 Device
    if (nr_vk_state.device != VK_NULL_HANDLE) {
        vkDestroyDevice(nr_vk_state.device, NULL);
        nr_vk_state.device = VK_NULL_HANDLE;
    }

    // 销毁 Instance
    if (nr_vk_state.instance != VK_NULL_HANDLE) {
        vkDestroyInstance(nr_vk_state.instance, NULL);
        nr_vk_state.instance = VK_NULL_HANDLE;
    }

    // 重置状态
    memset(&nr_vk_state.queueFamilies, 0, sizeof(nr_vk_state.queueFamilies));
    nr_vk_state.graphicsQueue = VK_NULL_HANDLE;
    nr_vk_state.presentQueue = VK_NULL_HANDLE;
    nr_vk_state.computeQueue = VK_NULL_HANDLE;
    nr_vk_state.currentFrame = 0;
    nr_vk_state.initialized = FALSE;
}

// ============================================================
// 顶层 API：创建渲染器
// ============================================================
SE_API NRResult NR_CreateRenderer(struct NRRendererCreateInfo info)
{
    if (nr_vk_state.initialized) {
        return NRR_MakeWarning(NRR_STEP_NR_CreateRenderer, NRR_CODE_RENDERER_ALREADY_CREATED, 0);
    }

    // 检查 API 类型
    if (info.api != NR_GRAPHICS_API_VULKAN) {
        return NRR_MakeFailure(NRR_STEP_NR_CreateRenderer, NRR_CODE_INVALID_API, 0);
    }

    // 检查窗口是否已创建
    if (!nr_window) {
        return NRR_MakeFailure(NRR_STEP_NR_CreateRenderer, NRR_CODE_WINDOW_NOT_CREATED, 0);
    }

    // 获取窗口尺寸
    int w, h;
    SDL_GetWindowSize(nr_window, &w, &h);
    nr_vk_state.windowWidth = (u32)w;
    nr_vk_state.windowHeight = (u32)h;

    // 设置版本信息
    nr_vk_state.apiVersion = info.api_target_version;
    nr_vk_state.maxFramesInFlight = NRVK_MAX_FRAMES_IN_FLIGHT;

    // 1. 创建 Instance
    NRResult result = nrVkCreateInstance(&info);
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 2. 创建 Surface
    result = nrVkCreateSurface();
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 3. 选择物理设备
    result = nrVkSelectPhysicalDevice(&info);
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 4. 创建逻辑设备
    result = nrVkCreateDevice(&info);
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 5. 创建交换链
    result = nrVkCreateSwapchain();
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 6. 创建 RenderPass（传统方式）或跳过（Dynamic Rendering）
    result = nrVkCreateRenderPass();
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 7. 创建深度资源
    result = nrVkCreateDepthResources(nr_vk_state.windowWidth, nr_vk_state.windowHeight);
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 8. 创建 Descriptor Set Layout
    result = nrVkCreateDescriptorSetLayout();
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 9. 创建 Uniform Buffer
    result = nrVkCreateUniformBuffer();
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 10. 创建 Descriptor Pool 和 Descriptor Set
    result = nrVkCreateDescriptorSet();
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 11. 创建顶点 Buffer
    result = nrVkCreateVertexBuffer();
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 12. 创建索引 Buffer
    result = nrVkCreateIndexBuffer();
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 13. 创建 Shader Modules
    result = nrVkCreateShaderModules(&nr_vk_state.vertShaderModule, &nr_vk_state.fragShaderModule);
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 14. 创建 Pipeline Layout
    result = nrVkCreatePipelineLayout(&nr_vk_state.pipelineLayout);
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 15. 创建图形管线
    result = nrVkCreateGraphicsPipeline(nr_vk_state.vertShaderModule,
                                         nr_vk_state.fragShaderModule,
                                         nr_vk_state.pipelineLayout,
                                         &nr_vk_state.graphicsPipeline);
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 16. 创建 Framebuffers（传统方式）
    result = nrVkCreateFramebuffers();
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 17. 创建命令池
    result = nrVkCreateCommandPool(&nr_vk_state.commandPool);
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 18. 分配命令缓冲
    nr_vk_state.commandBuffers = (VkCommandBuffer*)malloc(sizeof(VkCommandBuffer) * nr_vk_state.maxFramesInFlight);
    if (!nr_vk_state.commandBuffers) {
        nrVkDestroyAll();
        return NRR_MakeFailure(NRR_STEP_NR_CreateRenderer, NRR_CODE_OUT_OF_MEMORY, 0);
    }
    result = nrVkAllocateCommandBuffers(nr_vk_state.commandPool, nr_vk_state.maxFramesInFlight,
                                         nr_vk_state.commandBuffers);
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 19. 创建同步对象
    result = nrVkCreateFrameSyncObjects(nr_vk_state.maxFramesInFlight);
    if (NRR_FAILED(result)) {
        nrVkDestroyAll();
        return result;
    }

    // 20. 更新 Uniform Buffer（初始 MVP 矩阵）
    nrVkUpdateUniformBuffer();

    // 标记为已初始化
    nr_vk_state.initialized = TRUE;

    return NRR_MakeSuccess(NRR_STEP_NR_CreateRenderer, NRR_CODE_SUCCESS);
}

// ============================================================
// 顶层 API：销毁渲染器
// ============================================================
SE_API NRResult NR_DestroyRenderer(void)
{
    if (!nr_vk_state.initialized) {
        return NRR_MakeWarning(NRR_STEP_NR_DestroyRenderer, NRR_CODE_RENDERER_NOT_CREATED, 0);
    }

    nrVkDestroyAll();
    return NRR_MakeSuccess(NRR_STEP_NR_DestroyRenderer, NRR_CODE_SUCCESS);
}

// ============================================================
// 顶层 API：准备渲染（渲染线程调用）
// ============================================================
SE_API NRResult NR_PrepareRender(f64 deltatime)
{
    (void)deltatime;

    if (!nr_vk_state.initialized) {
        return NRR_MakeFailure(NRR_STEP_NR_PrepareRender, NRR_CODE_RENDERER_NOT_CREATED, 0);
    }

    // 等待当前帧的 Fence
    VkFence fence = nr_vk_state.inFlightFences[nr_vk_state.currentFrame];
    vkWaitForFences(nr_vk_state.device, 1, &fence, VK_TRUE, UINT64_MAX);
    vkResetFences(nr_vk_state.device, 1, &fence);

    // 获取交换链图像
    u32 imageIndex;
    VkResult result = vkAcquireNextImageKHR(
        nr_vk_state.device,
        nr_vk_state.swapchainInfo.swapchain,
        UINT64_MAX,
        nr_vk_state.imageAvailableSemaphores[nr_vk_state.currentFrame],
        VK_NULL_HANDLE,
        &imageIndex);

    if (result == VK_ERROR_OUT_OF_DATE_KHR) {
        // 交换链过期，需要重建
        return NRR_MakeFailure(NRR_STEP_NR_PrepareRender, NRR_CODE_SWAPCHAIN_OUT_OF_DATE, (u32)result);
    }
    VK_CHECK(result, NRR_STEP_NR_PrepareRender, NRR_CODE_SWAPCHAIN_CREATION_FAILED);

    // 记录命令缓冲
    VkCommandBuffer cmdBuffer = nr_vk_state.commandBuffers[nr_vk_state.currentFrame];
    VkCommandBufferBeginInfo beginInfo = {0};
    beginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
    beginInfo.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;

    result = vkBeginCommandBuffer(cmdBuffer, &beginInfo);
    VK_CHECK(result, NRR_STEP_NR_PrepareRender, NRR_CODE_COMMAND_BUFFER_FAILED);

    // 使用 Dynamic Rendering 或传统 RenderPass
    if (nr_vk_state.useDynamicRendering) {
        VkRenderingAttachmentInfo colorAttachment = {0};
        colorAttachment.sType = VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO;
        colorAttachment.imageView = nr_vk_state.swapchainInfo.imageViews[imageIndex];
        colorAttachment.imageLayout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;
        colorAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
        colorAttachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
        colorAttachment.clearValue.color.float32[0] = 0.0f;
        colorAttachment.clearValue.color.float32[1] = 0.0f;
        colorAttachment.clearValue.color.float32[2] = 0.0f;
        colorAttachment.clearValue.color.float32[3] = 1.0f;

        VkRenderingAttachmentInfo depthAttachment = {0};
        depthAttachment.sType = VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO;
        depthAttachment.imageView = nr_vk_state.depthImageView;
        depthAttachment.imageLayout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;
        depthAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
        depthAttachment.storeOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
        depthAttachment.clearValue.depthStencil.depth = 1.0f;
        depthAttachment.clearValue.depthStencil.stencil = 0;

        VkRenderingInfo renderingInfo = {0};
        renderingInfo.sType = VK_STRUCTURE_TYPE_RENDERING_INFO;
        renderingInfo.renderArea.offset.x = 0;
        renderingInfo.renderArea.offset.y = 0;
        renderingInfo.renderArea.extent = nr_vk_state.swapchainInfo.extent;
        renderingInfo.layerCount = 1;
        renderingInfo.colorAttachmentCount = 1;
        renderingInfo.pColorAttachments = &colorAttachment;
        renderingInfo.pDepthAttachment = &depthAttachment;

        vkCmdBeginRendering(cmdBuffer, &renderingInfo);
    } else {
        // 传统 RenderPass
        VkRenderPassBeginInfo renderPassInfo = {0};
        renderPassInfo.sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO;
        renderPassInfo.renderPass = nr_vk_state.renderPass;
        renderPassInfo.framebuffer = nr_vk_state.framebuffers[imageIndex];
        renderPassInfo.renderArea.offset.x = 0;
        renderPassInfo.renderArea.offset.y = 0;
        renderPassInfo.renderArea.extent = nr_vk_state.swapchainInfo.extent;

        VkClearValue clearValues[2] = {0};
        clearValues[0].color.float32[0] = 0.0f;
        clearValues[0].color.float32[1] = 0.0f;
        clearValues[0].color.float32[2] = 0.0f;
        clearValues[0].color.float32[3] = 1.0f;
        clearValues[1].depthStencil.depth = 1.0f;
        clearValues[1].depthStencil.stencil = 0;
        renderPassInfo.clearValueCount = 2;
        renderPassInfo.pClearValues = clearValues;

        vkCmdBeginRenderPass(cmdBuffer, &renderPassInfo, VK_SUBPASS_CONTENTS_INLINE);
    }

    // 绑定管线
    vkCmdBindPipeline(cmdBuffer, VK_PIPELINE_BIND_POINT_GRAPHICS, nr_vk_state.graphicsPipeline);

    // 设置视口和裁剪矩形
    VkViewport viewport = {0};
    viewport.x = 0.0f;
    viewport.y = 0.0f;
    viewport.width = (f32)nr_vk_state.swapchainInfo.extent.width;
    viewport.height = (f32)nr_vk_state.swapchainInfo.extent.height;
    viewport.minDepth = 0.0f;
    viewport.maxDepth = 1.0f;
    vkCmdSetViewport(cmdBuffer, 0, 1, &viewport);

    VkRect2D scissor = {0};
    scissor.offset.x = 0;
    scissor.offset.y = 0;
    scissor.extent = nr_vk_state.swapchainInfo.extent;
    vkCmdSetScissor(cmdBuffer, 0, 1, &scissor);

    // 绑定顶点 Buffer
    VkDeviceSize offsets[] = {0};
    vkCmdBindVertexBuffers(cmdBuffer, 0, 1, &nr_vk_state.vertexBuffer, offsets);

    // 绑定索引 Buffer
    vkCmdBindIndexBuffer(cmdBuffer, nr_vk_state.indexBuffer, 0, VK_INDEX_TYPE_UINT32);

    // 绑定 Descriptor Set
    vkCmdBindDescriptorSets(cmdBuffer, VK_PIPELINE_BIND_POINT_GRAPHICS,
                            nr_vk_state.pipelineLayout, 0, 1,
                            &nr_vk_state.descriptorSet, 0, NULL);

    // 绘制立方体（36 个索引）
    vkCmdDrawIndexed(cmdBuffer, CUBE_INDEX_COUNT, 1, 0, 0, 0);

    // 结束渲染
    if (nr_vk_state.useDynamicRendering) {
        vkCmdEndRendering(cmdBuffer);
    } else {
        vkCmdEndRenderPass(cmdBuffer);
    }

    result = vkEndCommandBuffer(cmdBuffer);
    VK_CHECK(result, NRR_STEP_NR_PrepareRender, NRR_CODE_COMMAND_BUFFER_FAILED);

    // 存储 imageIndex 供 NR_Render 使用
    nr_vk_state.currentImageIndex = imageIndex;

    return NRR_MakeSuccess(NRR_STEP_NR_PrepareRender, NRR_CODE_SUCCESS);
}

// ============================================================
// 顶层 API：渲染（主线程调用）
// ============================================================
SE_API NRResult NR_Render(f64 deltatime)
{
    (void)deltatime;

    if (!nr_vk_state.initialized) {
        return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_RENDERER_NOT_CREATED, 0);
    }

    u32 frameIndex = nr_vk_state.currentFrame;
    u32 imageIndex = nr_vk_state.currentImageIndex;

    // 提交命令缓冲
    VkSubmitInfo submitInfo = {0};
    submitInfo.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;

    VkSemaphore waitSemaphores[] = {nr_vk_state.imageAvailableSemaphores[frameIndex]};
    VkPipelineStageFlags waitStages[] = {VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT};
    submitInfo.waitSemaphoreCount = 1;
    submitInfo.pWaitSemaphores = waitSemaphores;
    submitInfo.pWaitDstStageMask = waitStages;
    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = &nr_vk_state.commandBuffers[frameIndex];

    VkSemaphore signalSemaphores[] = {nr_vk_state.renderFinishedSemaphores[frameIndex]};
    submitInfo.signalSemaphoreCount = 1;
    submitInfo.pSignalSemaphores = signalSemaphores;

    VkResult result = vkQueueSubmit(nr_vk_state.graphicsQueue, 1, &submitInfo,
                                     nr_vk_state.inFlightFences[frameIndex]);
    VK_CHECK(result, NRR_STEP_NR_Render, NRR_CODE_SUBMIT_FAILED);

    // 呈现
    VkPresentInfoKHR presentInfo = {0};
    presentInfo.sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR;
    presentInfo.waitSemaphoreCount = 1;
    presentInfo.pWaitSemaphores = signalSemaphores;
    presentInfo.swapchainCount = 1;
    presentInfo.pSwapchains = &nr_vk_state.swapchainInfo.swapchain;
    presentInfo.pImageIndices = &imageIndex;

    result = vkQueuePresentKHR(nr_vk_state.presentQueue, &presentInfo);

    if (result == VK_ERROR_OUT_OF_DATE_KHR || result == VK_SUBOPTIMAL_KHR) {
        // 交换链需要重建
        return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_SWAPCHAIN_OUT_OF_DATE, (u32)result);
    }
    VK_CHECK(result, NRR_STEP_NR_Render, NRR_CODE_PRESENT_FAILED);

    // 更新帧索引
    nr_vk_state.currentFrame = (nr_vk_state.currentFrame + 1) % nr_vk_state.maxFramesInFlight;

    return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);
}

// ============================================================
// 顶层 API：处理事件
// ============================================================
SE_API NRResult NR_PollEvents(SDL_Event* event)
{
    if (!event) {
        return NRR_MakeFailure(NRR_STEP_NR_Init, NRR_CODE_INVALID_PARAMETER, 0);
    }
    return SDL_PollEvent(event)
               ? NRR_MakeSuccess(NRR_STEP_NR_Init, NRR_CODE_SUCCESS)
               : NRR_MakeLog(NRR_STEP_NR_Init, NRR_CODE_SUCCESS, 0);
}

// ============================================================
// 顶层 API：主更新
// ============================================================
SE_API NRResult NR_MainUpdate(f64 deltatime)
{
    (void)deltatime;
    // TODO: 场景更新、资源管理等
    return NRR_MakeSuccess(NRR_STEP_NR_Init, NRR_CODE_SUCCESS);
}
