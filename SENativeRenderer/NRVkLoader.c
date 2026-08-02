#include "NRVkLoader.h"
#include <SDL3/SDL_vulkan.h>

// ============================================================
// NRVkLoader.c
// 通过 SDL3 取得 vkGetInstanceProcAddr，再逐级加载函数表。
// ============================================================

NRVkTable nrvk;

static bool s_loaderLoaded = false;

#define NR_LOAD_GLOBAL(name) \
	nrvk.name = (PFN_vk##name)nrvk.GetInstanceProcAddr(VK_NULL_HANDLE, "vk" #name)

#define NR_LOAD_INSTANCE(name) \
	nrvk.name = (PFN_vk##name)nrvk.GetInstanceProcAddr(instance, "vk" #name)

#define NR_LOAD_DEVICE(name) \
	do { \
		nrvk.name = (PFN_vk##name)nrvk.GetDeviceProcAddr(device, "vk" #name); \
		if (nrvk.name == NULL) { \
			nrvk.name = (PFN_vk##name)nrvk.GetInstanceProcAddr(s_instance, "vk" #name); \
		} \
	} while (0)

static VkInstance s_instance = VK_NULL_HANDLE;

NRResult nrVkLoadGlobal(void)
{
	if (s_loaderLoaded && nrvk.GetInstanceProcAddr != NULL)
	{
		return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);
	}

	if (!SDL_Vulkan_LoadLibrary(NULL))
	{
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_API, 0);
	}

	nrvk.GetInstanceProcAddr = (PFN_vkGetInstanceProcAddr)SDL_Vulkan_GetVkGetInstanceProcAddr();
	if (nrvk.GetInstanceProcAddr == NULL)
	{
		SDL_Vulkan_UnloadLibrary();
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_API, 1);
	}

	NR_LOAD_GLOBAL(CreateInstance);
	NR_LOAD_GLOBAL(EnumerateInstanceExtensionProperties);
	NR_LOAD_GLOBAL(EnumerateInstanceLayerProperties);
	NR_LOAD_GLOBAL(EnumerateInstanceVersion);

	if (nrvk.CreateInstance == NULL)
	{
		SDL_Vulkan_UnloadLibrary();
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_API, 2);
	}

	s_loaderLoaded = true;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);
}

NRResult nrVkLoadInstance(VkInstance instance)
{
	if (instance == VK_NULL_HANDLE || nrvk.GetInstanceProcAddr == NULL)
	{
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_PARAMETER, 0);
	}
	s_instance = instance;

	NR_LOAD_INSTANCE(DestroyInstance);
	NR_LOAD_INSTANCE(EnumeratePhysicalDevices);
	NR_LOAD_INSTANCE(GetPhysicalDeviceProperties);
	NR_LOAD_INSTANCE(GetPhysicalDeviceProperties2);
	NR_LOAD_INSTANCE(GetPhysicalDeviceFeatures);
	NR_LOAD_INSTANCE(GetPhysicalDeviceFeatures2);
	NR_LOAD_INSTANCE(GetPhysicalDeviceMemoryProperties);
	NR_LOAD_INSTANCE(GetPhysicalDeviceQueueFamilyProperties);
	NR_LOAD_INSTANCE(GetPhysicalDeviceFormatProperties);
	NR_LOAD_INSTANCE(GetPhysicalDeviceImageFormatProperties);
	NR_LOAD_INSTANCE(EnumerateDeviceExtensionProperties);
	NR_LOAD_INSTANCE(CreateDevice);
	NR_LOAD_INSTANCE(GetDeviceProcAddr);

	NR_LOAD_INSTANCE(DestroySurfaceKHR);
	NR_LOAD_INSTANCE(GetPhysicalDeviceSurfaceSupportKHR);
	NR_LOAD_INSTANCE(GetPhysicalDeviceSurfaceCapabilitiesKHR);
	NR_LOAD_INSTANCE(GetPhysicalDeviceSurfaceFormatsKHR);
	NR_LOAD_INSTANCE(GetPhysicalDeviceSurfacePresentModesKHR);

	// 调试扩展是可选的，缺失时保持 NULL
	NR_LOAD_INSTANCE(CreateDebugUtilsMessengerEXT);
	NR_LOAD_INSTANCE(DestroyDebugUtilsMessengerEXT);

	if (nrvk.EnumeratePhysicalDevices == NULL || nrvk.CreateDevice == NULL)
	{
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_API, 3);
	}
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);
}

NRResult nrVkLoadDevice(VkDevice device)
{
	if (device == VK_NULL_HANDLE || nrvk.GetDeviceProcAddr == NULL)
	{
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_PARAMETER, 0);
	}

	NR_LOAD_DEVICE(DestroyDevice);
	NR_LOAD_DEVICE(GetDeviceQueue);
	NR_LOAD_DEVICE(DeviceWaitIdle);
	NR_LOAD_DEVICE(QueueSubmit);
	NR_LOAD_DEVICE(QueueWaitIdle);
	NR_LOAD_DEVICE(QueuePresentKHR);

	NR_LOAD_DEVICE(AllocateMemory);
	NR_LOAD_DEVICE(FreeMemory);
	NR_LOAD_DEVICE(MapMemory);
	NR_LOAD_DEVICE(UnmapMemory);
	NR_LOAD_DEVICE(FlushMappedMemoryRanges);
	NR_LOAD_DEVICE(InvalidateMappedMemoryRanges);

	NR_LOAD_DEVICE(CreateBuffer);
	NR_LOAD_DEVICE(DestroyBuffer);
	NR_LOAD_DEVICE(GetBufferMemoryRequirements);
	NR_LOAD_DEVICE(BindBufferMemory);
	NR_LOAD_DEVICE(CreateImage);
	NR_LOAD_DEVICE(DestroyImage);
	NR_LOAD_DEVICE(GetImageMemoryRequirements);
	NR_LOAD_DEVICE(BindImageMemory);
	NR_LOAD_DEVICE(CreateImageView);
	NR_LOAD_DEVICE(DestroyImageView);
	NR_LOAD_DEVICE(CreateSampler);
	NR_LOAD_DEVICE(DestroySampler);

	NR_LOAD_DEVICE(CreateSwapchainKHR);
	NR_LOAD_DEVICE(DestroySwapchainKHR);
	NR_LOAD_DEVICE(GetSwapchainImagesKHR);
	NR_LOAD_DEVICE(AcquireNextImageKHR);

	NR_LOAD_DEVICE(CreateRenderPass);
	NR_LOAD_DEVICE(DestroyRenderPass);
	NR_LOAD_DEVICE(CreateFramebuffer);
	NR_LOAD_DEVICE(DestroyFramebuffer);

	NR_LOAD_DEVICE(CreateShaderModule);
	NR_LOAD_DEVICE(DestroyShaderModule);
	NR_LOAD_DEVICE(CreatePipelineLayout);
	NR_LOAD_DEVICE(DestroyPipelineLayout);
	NR_LOAD_DEVICE(CreateGraphicsPipelines);
	NR_LOAD_DEVICE(CreateComputePipelines);
	NR_LOAD_DEVICE(DestroyPipeline);
	NR_LOAD_DEVICE(CreatePipelineCache);
	NR_LOAD_DEVICE(DestroyPipelineCache);
	NR_LOAD_DEVICE(GetPipelineCacheData);

	NR_LOAD_DEVICE(CreateDescriptorSetLayout);
	NR_LOAD_DEVICE(DestroyDescriptorSetLayout);
	NR_LOAD_DEVICE(CreateDescriptorPool);
	NR_LOAD_DEVICE(DestroyDescriptorPool);
	NR_LOAD_DEVICE(ResetDescriptorPool);
	NR_LOAD_DEVICE(AllocateDescriptorSets);
	NR_LOAD_DEVICE(FreeDescriptorSets);
	NR_LOAD_DEVICE(UpdateDescriptorSets);

	NR_LOAD_DEVICE(CreateCommandPool);
	NR_LOAD_DEVICE(DestroyCommandPool);
	NR_LOAD_DEVICE(ResetCommandPool);
	NR_LOAD_DEVICE(AllocateCommandBuffers);
	NR_LOAD_DEVICE(FreeCommandBuffers);
	NR_LOAD_DEVICE(BeginCommandBuffer);
	NR_LOAD_DEVICE(EndCommandBuffer);
	NR_LOAD_DEVICE(ResetCommandBuffer);

	NR_LOAD_DEVICE(CreateSemaphore);
	NR_LOAD_DEVICE(DestroySemaphore);
	NR_LOAD_DEVICE(CreateFence);
	NR_LOAD_DEVICE(DestroyFence);
	NR_LOAD_DEVICE(WaitForFences);
	NR_LOAD_DEVICE(ResetFences);

	NR_LOAD_DEVICE(CreateQueryPool);
	NR_LOAD_DEVICE(DestroyQueryPool);
	NR_LOAD_DEVICE(GetQueryPoolResults);

	NR_LOAD_DEVICE(CmdBeginRenderPass);
	NR_LOAD_DEVICE(CmdEndRenderPass);
	NR_LOAD_DEVICE(CmdNextSubpass);
	NR_LOAD_DEVICE(CmdBindPipeline);
	NR_LOAD_DEVICE(CmdBindDescriptorSets);
	NR_LOAD_DEVICE(CmdBindVertexBuffers);
	NR_LOAD_DEVICE(CmdBindIndexBuffer);
	NR_LOAD_DEVICE(CmdDraw);
	NR_LOAD_DEVICE(CmdDrawIndexed);
	NR_LOAD_DEVICE(CmdDrawIndexedIndirect);
	NR_LOAD_DEVICE(CmdDispatch);
	NR_LOAD_DEVICE(CmdSetViewport);
	NR_LOAD_DEVICE(CmdSetScissor);
	NR_LOAD_DEVICE(CmdSetDepthBias);
	NR_LOAD_DEVICE(CmdPushConstants);
	NR_LOAD_DEVICE(CmdCopyBuffer);
	NR_LOAD_DEVICE(CmdCopyBufferToImage);
	NR_LOAD_DEVICE(CmdCopyImageToBuffer);
	NR_LOAD_DEVICE(CmdBlitImage);
	NR_LOAD_DEVICE(CmdPipelineBarrier);
	NR_LOAD_DEVICE(CmdResetQueryPool);
	NR_LOAD_DEVICE(CmdWriteTimestamp);

	if (nrvk.CmdDrawIndexed == NULL || nrvk.QueueSubmit == NULL)
	{
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_API, 4);
	}
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);
}

void nrVkUnload(void)
{
	if (s_loaderLoaded)
	{
		SDL_Vulkan_UnloadLibrary();
		s_loaderLoaded = false;
	}
	s_instance = VK_NULL_HANDLE;
	memset(&nrvk, 0, sizeof(nrvk));
}
