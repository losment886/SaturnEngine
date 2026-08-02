#pragma once

// ============================================================
// NRDevice.h
// Vulkan 实例 / 物理设备 / 逻辑设备 / 队列管理
// ============================================================

#include "NRVkLoader.h"
#include "NRApi.h"

SE_EXTERN_C_BEGIN

#define NR_MAX_PHYSICAL_DEVICES 16

typedef struct NRQueueFamilies
{
	u32 graphics;
	u32 present;
	u32 compute;
	u32 transfer;
	b32 has_graphics;
	b32 has_present;
	b32 has_compute;
	b32 has_transfer;
} NRQueueFamilies;

typedef struct NRDevice
{
	VkInstance instance;
	VkDebugUtilsMessengerEXT debug_messenger;
	VkSurfaceKHR surface;

	VkPhysicalDevice physical;
	VkPhysicalDeviceProperties props;
	VkPhysicalDeviceFeatures features;
	VkPhysicalDeviceMemoryProperties mem_props;

	VkDevice device;
	NRQueueFamilies families;
	VkQueue graphics_queue;
	VkQueue present_queue;
	VkQueue compute_queue;
	VkQueue transfer_queue;

	// 单帧一次性命令用的池（graphics 队列族）
	VkCommandPool transient_pool;

	u32 enabled_features;   // NR_FEATURE_* 位掩码
	u32 api_version;
	b32 validation_enabled;
	b32 update_after_bind;  // 描述符索引的 update-after-bind 系列特性是否已启用
	b32 initialized;
} NRDevice;

extern NRDevice nr_device;

// 创建实例（含 surface 扩展与可选校验层）
NRResult nrDeviceCreateInstance(const struct NRRendererCreateInfo* info, b32 enable_validation);
// 由 SDL 窗口创建 surface
NRResult nrDeviceCreateSurface(void);
// 枚举可用物理设备信息
NRResult nrDeviceEnumerate(NRDeviceInfo* out, u32* inout_count);
// 选择物理设备并创建逻辑设备（device_index 为 NR_UINT32_MAX 时自动打分选择）
NRResult nrDeviceCreateLogical(const struct NRRendererCreateInfo* info, u32 device_index);
// 填充设备信息
NRResult nrDeviceGetInfo(NRDeviceInfo* out);
// 查找内存类型
s32 nrDeviceFindMemoryType(u32 type_bits, VkMemoryPropertyFlags props);
// 一次性命令缓冲
VkCommandBuffer nrDeviceBeginOneShot(void);
NRResult nrDeviceEndOneShot(VkCommandBuffer cmd);
// 销毁全部设备对象
void nrDeviceDestroy(void);

#define NR_AUTO_DEVICE 0xFFFFFFFFu

SE_EXTERN_C_END
