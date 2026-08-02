#pragma once

// ============================================================
// NRVkLoader.h
// Vulkan 函数动态加载表
//
// 为什么不直接链接 Vulkan loader：
//   Android / iOS(MoltenVK) / HarmonyOS NEXT 上没有可静态链接的
//   loader 导入库，必须运行时 dlopen。SDL3 的 SDL_Vulkan_LoadLibrary +
//   SDL_Vulkan_GetVkGetInstanceProcAddr 在所有平台上都可用，
//   因此统一走这条路径，桌面端也一致，避免平台分支。
//
// 用法：所有 Vulkan 调用一律写成 nrvk.XxxYyy(...)，不要直接写 vkXxxYyy。
// ============================================================

#define VK_NO_PROTOTYPES 1
#include "NRDefine.h"
#include <vulkan/vulkan.h>

SE_EXTERN_C_BEGIN

typedef struct NRVkTable
{
	// ---- 全局级（instance == VK_NULL_HANDLE 即可取得）----
	PFN_vkGetInstanceProcAddr                       GetInstanceProcAddr;
	PFN_vkCreateInstance                            CreateInstance;
	PFN_vkEnumerateInstanceExtensionProperties      EnumerateInstanceExtensionProperties;
	PFN_vkEnumerateInstanceLayerProperties          EnumerateInstanceLayerProperties;
	PFN_vkEnumerateInstanceVersion                  EnumerateInstanceVersion;

	// ---- 实例级 ----
	PFN_vkDestroyInstance                           DestroyInstance;
	PFN_vkEnumeratePhysicalDevices                  EnumeratePhysicalDevices;
	PFN_vkGetPhysicalDeviceProperties               GetPhysicalDeviceProperties;
	PFN_vkGetPhysicalDeviceProperties2              GetPhysicalDeviceProperties2;
	PFN_vkGetPhysicalDeviceFeatures                 GetPhysicalDeviceFeatures;
	PFN_vkGetPhysicalDeviceFeatures2                GetPhysicalDeviceFeatures2;
	PFN_vkGetPhysicalDeviceMemoryProperties         GetPhysicalDeviceMemoryProperties;
	PFN_vkGetPhysicalDeviceQueueFamilyProperties    GetPhysicalDeviceQueueFamilyProperties;
	PFN_vkGetPhysicalDeviceFormatProperties         GetPhysicalDeviceFormatProperties;
	PFN_vkGetPhysicalDeviceImageFormatProperties    GetPhysicalDeviceImageFormatProperties;
	PFN_vkEnumerateDeviceExtensionProperties        EnumerateDeviceExtensionProperties;
	PFN_vkCreateDevice                              CreateDevice;
	PFN_vkGetDeviceProcAddr                         GetDeviceProcAddr;

	PFN_vkDestroySurfaceKHR                         DestroySurfaceKHR;
	PFN_vkGetPhysicalDeviceSurfaceSupportKHR        GetPhysicalDeviceSurfaceSupportKHR;
	PFN_vkGetPhysicalDeviceSurfaceCapabilitiesKHR   GetPhysicalDeviceSurfaceCapabilitiesKHR;
	PFN_vkGetPhysicalDeviceSurfaceFormatsKHR        GetPhysicalDeviceSurfaceFormatsKHR;
	PFN_vkGetPhysicalDeviceSurfacePresentModesKHR   GetPhysicalDeviceSurfacePresentModesKHR;

	PFN_vkCreateDebugUtilsMessengerEXT              CreateDebugUtilsMessengerEXT;
	PFN_vkDestroyDebugUtilsMessengerEXT             DestroyDebugUtilsMessengerEXT;

	// ---- 设备级：核心 ----
	PFN_vkDestroyDevice                             DestroyDevice;
	PFN_vkGetDeviceQueue                            GetDeviceQueue;
	PFN_vkDeviceWaitIdle                            DeviceWaitIdle;
	PFN_vkQueueSubmit                               QueueSubmit;
	PFN_vkQueueWaitIdle                             QueueWaitIdle;
	PFN_vkQueuePresentKHR                           QueuePresentKHR;

	// 内存
	PFN_vkAllocateMemory                            AllocateMemory;
	PFN_vkFreeMemory                                FreeMemory;
	PFN_vkMapMemory                                 MapMemory;
	PFN_vkUnmapMemory                               UnmapMemory;
	PFN_vkFlushMappedMemoryRanges                   FlushMappedMemoryRanges;
	PFN_vkInvalidateMappedMemoryRanges              InvalidateMappedMemoryRanges;

	// 缓冲/图像
	PFN_vkCreateBuffer                              CreateBuffer;
	PFN_vkDestroyBuffer                             DestroyBuffer;
	PFN_vkGetBufferMemoryRequirements               GetBufferMemoryRequirements;
	PFN_vkBindBufferMemory                          BindBufferMemory;
	PFN_vkCreateImage                               CreateImage;
	PFN_vkDestroyImage                              DestroyImage;
	PFN_vkGetImageMemoryRequirements                GetImageMemoryRequirements;
	PFN_vkBindImageMemory                           BindImageMemory;
	PFN_vkCreateImageView                           CreateImageView;
	PFN_vkDestroyImageView                          DestroyImageView;
	PFN_vkCreateSampler                             CreateSampler;
	PFN_vkDestroySampler                            DestroySampler;

	// 交换链
	PFN_vkCreateSwapchainKHR                        CreateSwapchainKHR;
	PFN_vkDestroySwapchainKHR                       DestroySwapchainKHR;
	PFN_vkGetSwapchainImagesKHR                     GetSwapchainImagesKHR;
	PFN_vkAcquireNextImageKHR                       AcquireNextImageKHR;

	// 渲染通道/帧缓冲
	PFN_vkCreateRenderPass                          CreateRenderPass;
	PFN_vkDestroyRenderPass                         DestroyRenderPass;
	PFN_vkCreateFramebuffer                         CreateFramebuffer;
	PFN_vkDestroyFramebuffer                        DestroyFramebuffer;

	// 管线
	PFN_vkCreateShaderModule                        CreateShaderModule;
	PFN_vkDestroyShaderModule                       DestroyShaderModule;
	PFN_vkCreatePipelineLayout                      CreatePipelineLayout;
	PFN_vkDestroyPipelineLayout                     DestroyPipelineLayout;
	PFN_vkCreateGraphicsPipelines                   CreateGraphicsPipelines;
	PFN_vkCreateComputePipelines                    CreateComputePipelines;
	PFN_vkDestroyPipeline                           DestroyPipeline;
	PFN_vkCreatePipelineCache                       CreatePipelineCache;
	PFN_vkDestroyPipelineCache                      DestroyPipelineCache;
	PFN_vkGetPipelineCacheData                      GetPipelineCacheData;

	// 描述符
	PFN_vkCreateDescriptorSetLayout                 CreateDescriptorSetLayout;
	PFN_vkDestroyDescriptorSetLayout                DestroyDescriptorSetLayout;
	PFN_vkCreateDescriptorPool                      CreateDescriptorPool;
	PFN_vkDestroyDescriptorPool                     DestroyDescriptorPool;
	PFN_vkResetDescriptorPool                       ResetDescriptorPool;
	PFN_vkAllocateDescriptorSets                    AllocateDescriptorSets;
	PFN_vkFreeDescriptorSets                        FreeDescriptorSets;
	PFN_vkUpdateDescriptorSets                      UpdateDescriptorSets;

	// 命令
	PFN_vkCreateCommandPool                         CreateCommandPool;
	PFN_vkDestroyCommandPool                        DestroyCommandPool;
	PFN_vkResetCommandPool                          ResetCommandPool;
	PFN_vkAllocateCommandBuffers                    AllocateCommandBuffers;
	PFN_vkFreeCommandBuffers                        FreeCommandBuffers;
	PFN_vkBeginCommandBuffer                        BeginCommandBuffer;
	PFN_vkEndCommandBuffer                          EndCommandBuffer;
	PFN_vkResetCommandBuffer                        ResetCommandBuffer;

	// 同步
	PFN_vkCreateSemaphore                           CreateSemaphore;
	PFN_vkDestroySemaphore                          DestroySemaphore;
	PFN_vkCreateFence                               CreateFence;
	PFN_vkDestroyFence                              DestroyFence;
	PFN_vkWaitForFences                             WaitForFences;
	PFN_vkResetFences                               ResetFences;

	// 查询
	PFN_vkCreateQueryPool                           CreateQueryPool;
	PFN_vkDestroyQueryPool                          DestroyQueryPool;
	PFN_vkGetQueryPoolResults                       GetQueryPoolResults;

	// 命令记录
	PFN_vkCmdBeginRenderPass                        CmdBeginRenderPass;
	PFN_vkCmdEndRenderPass                          CmdEndRenderPass;
	PFN_vkCmdNextSubpass                            CmdNextSubpass;
	PFN_vkCmdBindPipeline                           CmdBindPipeline;
	PFN_vkCmdBindDescriptorSets                     CmdBindDescriptorSets;
	PFN_vkCmdBindVertexBuffers                      CmdBindVertexBuffers;
	PFN_vkCmdBindIndexBuffer                        CmdBindIndexBuffer;
	PFN_vkCmdDraw                                   CmdDraw;
	PFN_vkCmdDrawIndexed                            CmdDrawIndexed;
	PFN_vkCmdDrawIndexedIndirect                    CmdDrawIndexedIndirect;
	PFN_vkCmdDispatch                               CmdDispatch;
	PFN_vkCmdSetViewport                            CmdSetViewport;
	PFN_vkCmdSetScissor                             CmdSetScissor;
	PFN_vkCmdSetDepthBias                           CmdSetDepthBias;
	PFN_vkCmdPushConstants                          CmdPushConstants;
	PFN_vkCmdCopyBuffer                             CmdCopyBuffer;
	PFN_vkCmdCopyBufferToImage                      CmdCopyBufferToImage;
	PFN_vkCmdCopyImageToBuffer                      CmdCopyImageToBuffer;
	PFN_vkCmdBlitImage                              CmdBlitImage;
	PFN_vkCmdPipelineBarrier                        CmdPipelineBarrier;
	PFN_vkCmdResetQueryPool                         CmdResetQueryPool;
	PFN_vkCmdWriteTimestamp                         CmdWriteTimestamp;
} NRVkTable;

extern NRVkTable nrvk;

// 加载 loader 并取得全局级函数（幂等）
NRResult nrVkLoadGlobal(void);
// 取得实例级函数
NRResult nrVkLoadInstance(VkInstance instance);
// 取得设备级函数（优先 vkGetDeviceProcAddr，减少 loader 跳转开销）
NRResult nrVkLoadDevice(VkDevice device);
// 卸载 loader
void nrVkUnload(void);

SE_EXTERN_C_END
