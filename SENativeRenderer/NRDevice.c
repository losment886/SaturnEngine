#include "NRDevice.h"
#include <SDL3/SDL_vulkan.h>

// ============================================================
// NRDevice.c
// Vulkan 设备层实现
//
// 平台无关做法：instance 扩展列表由 SDL3 的
// SDL_Vulkan_GetInstanceExtensions() 提供，它已经针对
// Windows / Linux(X11,Wayland) / macOS / iOS / Android / OHOS
// 返回正确的 VK_KHR_*_surface 扩展，无需手写平台分支。
// ============================================================

NRDevice nr_device;

#define NR_MAX_EXT 64

// ------------------------------------------------------------
// 调试回调
// ------------------------------------------------------------
static VKAPI_ATTR VkBool32 VKAPI_CALL nrDebugCallback(
	VkDebugUtilsMessageSeverityFlagBitsEXT severity,
	VkDebugUtilsMessageTypeFlagsEXT types,
	const VkDebugUtilsMessengerCallbackDataEXT* data,
	void* user)
{
	(void)types; (void)user;
	if (data == NULL || data->pMessage == NULL) return VK_FALSE;

	if (severity & VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT)
	{
		NRR_MakeWarning(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS, 0);
		SDL_Log("[Vulkan][ERROR] %s", data->pMessage);
	}
	else if (severity & VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT)
	{
		SDL_Log("[Vulkan][WARN] %s", data->pMessage);
	}
	return VK_FALSE;
}

static bool nrHasExtension(const VkExtensionProperties* list, u32 count, const char* name)
{
	for (u32 i = 0; i < count; i++)
	{
		if (strcmp(list[i].extensionName, name) == 0) return true;
	}
	return false;
}

static bool nrHasLayer(const char* name)
{
	u32 count = 0;
	if (nrvk.EnumerateInstanceLayerProperties == NULL) return false;
	nrvk.EnumerateInstanceLayerProperties(&count, NULL);
	if (count == 0) return false;
	VkLayerProperties* layers = (VkLayerProperties*)malloc(sizeof(VkLayerProperties) * count);
	if (layers == NULL) return false;
	nrvk.EnumerateInstanceLayerProperties(&count, layers);
	bool found = false;
	for (u32 i = 0; i < count; i++)
	{
		if (strcmp(layers[i].layerName, name) == 0) { found = true; break; }
	}
	free(layers);
	return found;
}

// ------------------------------------------------------------
// 实例创建
// ------------------------------------------------------------
NRResult nrDeviceCreateInstance(const struct NRRendererCreateInfo* info, b32 enable_validation)
{
	NRResult r = nrVkLoadGlobal();
	if (NRR_FAILED(r)) return r;

	if (nr_device.instance != VK_NULL_HANDLE)
	{
		return NRR_MakeWarning(NRR_STEP_NR_CreateDevice, NRR_CODE_RENDERER_ALREADY_CREATED, 0);
	}

	// ---- 可用实例扩展 ----
	u32 availCount = 0;
	nrvk.EnumerateInstanceExtensionProperties(NULL, &availCount, NULL);
	VkExtensionProperties* avail = NULL;
	if (availCount > 0)
	{
		avail = (VkExtensionProperties*)malloc(sizeof(VkExtensionProperties) * availCount);
		if (avail == NULL) return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_OUT_OF_MEMORY, 0);
		nrvk.EnumerateInstanceExtensionProperties(NULL, &availCount, avail);
	}

	const char* exts[NR_MAX_EXT];
	u32 extCount = 0;

	// SDL 提供的平台 surface 扩展
	Uint32 sdlCount = 0;
	char const* const* sdlExts = SDL_Vulkan_GetInstanceExtensions(&sdlCount);
	for (Uint32 i = 0; i < sdlCount && extCount < NR_MAX_EXT; i++)
	{
		exts[extCount++] = sdlExts[i];
	}

	// 便携枚举（MoltenVK 必需）
	VkInstanceCreateFlags flags = 0;
#if defined(SE_PLATFORM_MACOS) || defined(SE_PLATFORM_IOS)
	if (nrHasExtension(avail, availCount, VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME) &&
		extCount < NR_MAX_EXT)
	{
		exts[extCount++] = VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME;
		flags |= VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR;
	}
#endif

	// 物理设备属性2（IBL/描述符索引特性查询依赖）
	if (nrHasExtension(avail, availCount, VK_KHR_GET_PHYSICAL_DEVICE_PROPERTIES_2_EXTENSION_NAME) &&
		extCount < NR_MAX_EXT)
	{
		exts[extCount++] = VK_KHR_GET_PHYSICAL_DEVICE_PROPERTIES_2_EXTENSION_NAME;
	}

	// 调用方追加的必需/可选扩展
	if (info != NULL)
	{
		for (s32 i = 0; i < info->required_instance_extensions_count && extCount < NR_MAX_EXT; i++)
		{
			exts[extCount++] = info->required_instance_extensions[i];
		}
		for (s32 i = 0; i < info->optional_instance_extensions_count && extCount < NR_MAX_EXT; i++)
		{
			if (nrHasExtension(avail, availCount, info->optional_instance_extensions[i]))
				exts[extCount++] = info->optional_instance_extensions[i];
		}
	}

	// 调试扩展
	b32 debugAvailable = FALSE;
	if (enable_validation &&
		nrHasExtension(avail, availCount, VK_EXT_DEBUG_UTILS_EXTENSION_NAME) &&
		extCount < NR_MAX_EXT)
	{
		exts[extCount++] = VK_EXT_DEBUG_UTILS_EXTENSION_NAME;
		debugAvailable = TRUE;
	}

	const char* layers[4];
	u32 layerCount = 0;
	if (enable_validation && nrHasLayer("VK_LAYER_KHRONOS_validation"))
	{
		layers[layerCount++] = "VK_LAYER_KHRONOS_validation";
	}

	// ---- API 版本 ----
	u32 instanceVersion = VK_API_VERSION_1_0;
	if (nrvk.EnumerateInstanceVersion != NULL)
	{
		nrvk.EnumerateInstanceVersion(&instanceVersion);
	}
	u32 wanted = VK_API_VERSION_1_3;
	if (info != NULL && info->api_target_version != 0)
	{
		wanted = VK_MAKE_API_VERSION(0,
			(u32)NRV_GetMajor(info->api_target_version),
			(u32)NRV_GetMinor(info->api_target_version),
			(u32)NRV_GetPatch(info->api_target_version));
	}
	if (wanted > instanceVersion) wanted = instanceVersion;

	VkApplicationInfo app;
	memset(&app, 0, sizeof(app));
	app.sType = VK_STRUCTURE_TYPE_APPLICATION_INFO;
	app.pApplicationName = (info != NULL && info->app_name != NULL) ? info->app_name : "SaturnEngine";
	app.applicationVersion = VK_MAKE_VERSION(1, 0, 0);
	app.pEngineName = "SENativeRenderer";
	app.engineVersion = VK_MAKE_VERSION(1, 0, 0);
	app.apiVersion = wanted;

	VkInstanceCreateInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO;
	ci.flags = flags;
	ci.pApplicationInfo = &app;
	ci.enabledExtensionCount = extCount;
	ci.ppEnabledExtensionNames = exts;
	ci.enabledLayerCount = layerCount;
	ci.ppEnabledLayerNames = (layerCount > 0) ? layers : NULL;

	VkResult vr = nrvk.CreateInstance(&ci, NULL, &nr_device.instance);
	free(avail);
	if (vr != VK_SUCCESS)
	{
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_API, (u32)vr);
	}

	nr_device.api_version = wanted;
	nr_device.validation_enabled = (layerCount > 0);

	r = nrVkLoadInstance(nr_device.instance);
	if (NRR_FAILED(r)) return r;

	if (debugAvailable && nrvk.CreateDebugUtilsMessengerEXT != NULL)
	{
		VkDebugUtilsMessengerCreateInfoEXT dci;
		memset(&dci, 0, sizeof(dci));
		dci.sType = VK_STRUCTURE_TYPE_DEBUG_UTILS_MESSENGER_CREATE_INFO_EXT;
		dci.messageSeverity = VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT |
							  VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT;
		dci.messageType = VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT |
						  VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT |
						  VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT;
		dci.pfnUserCallback = nrDebugCallback;
		nrvk.CreateDebugUtilsMessengerEXT(nr_device.instance, &dci, NULL, &nr_device.debug_messenger);
	}

	return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// Surface
// ------------------------------------------------------------
NRResult nrDeviceCreateSurface(void)
{
	if (nr_window == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_WINDOW_NOT_CREATED, 0);
	if (nr_device.instance == VK_NULL_HANDLE)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_NOT_INITIALIZED, 0);
	if (nr_device.surface != VK_NULL_HANDLE)
		return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);

	if (!SDL_Vulkan_CreateSurface(nr_window, nr_device.instance, NULL, &nr_device.surface))
	{
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_SURFACE_CREATION_FAILED, 0);
	}
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 队列族查找
// ------------------------------------------------------------
static NRQueueFamilies nrFindQueueFamilies(VkPhysicalDevice pd)
{
	NRQueueFamilies f;
	memset(&f, 0, sizeof(f));

	u32 count = 0;
	nrvk.GetPhysicalDeviceQueueFamilyProperties(pd, &count, NULL);
	if (count == 0) return f;

	VkQueueFamilyProperties* qs =
		(VkQueueFamilyProperties*)malloc(sizeof(VkQueueFamilyProperties) * count);
	if (qs == NULL) return f;
	nrvk.GetPhysicalDeviceQueueFamilyProperties(pd, &count, qs);

	for (u32 i = 0; i < count; i++)
	{
		const VkQueueFlags fl = qs[i].queueFlags;

		if (!f.has_graphics && (fl & VK_QUEUE_GRAPHICS_BIT))
		{
			f.graphics = i; f.has_graphics = TRUE;
		}
		// 优先选择独立的 compute 队列（异步计算：粒子模拟）
		if ((fl & VK_QUEUE_COMPUTE_BIT) && !(fl & VK_QUEUE_GRAPHICS_BIT))
		{
			f.compute = i; f.has_compute = TRUE;
		}
		// 优先选择独立的 transfer 队列（异步资源上传）
		if ((fl & VK_QUEUE_TRANSFER_BIT) &&
			!(fl & VK_QUEUE_GRAPHICS_BIT) && !(fl & VK_QUEUE_COMPUTE_BIT))
		{
			f.transfer = i; f.has_transfer = TRUE;
		}

		if (!f.has_present && nr_device.surface != VK_NULL_HANDLE)
		{
			VkBool32 support = VK_FALSE;
			nrvk.GetPhysicalDeviceSurfaceSupportKHR(pd, i, nr_device.surface, &support);
			if (support) { f.present = i; f.has_present = TRUE; }
		}
	}

	// 回退：没有独立队列时复用 graphics
	if (!f.has_compute && f.has_graphics) { f.compute = f.graphics; f.has_compute = TRUE; }
	if (!f.has_transfer && f.has_graphics) { f.transfer = f.graphics; f.has_transfer = TRUE; }
	if (!f.has_present && f.has_graphics) { f.present = f.graphics; f.has_present = TRUE; }

	free(qs);
	return f;
}

// ------------------------------------------------------------
// 设备打分
// ------------------------------------------------------------
static u64 nrScoreDevice(VkPhysicalDevice pd, const VkPhysicalDeviceProperties* p)
{
	u64 score = 0;
	switch (p->deviceType)
	{
		case VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU:   score += 100000; break;
		case VK_PHYSICAL_DEVICE_TYPE_INTEGRATED_GPU: score += 50000;  break;
		case VK_PHYSICAL_DEVICE_TYPE_VIRTUAL_GPU:    score += 10000;  break;
		default: break;
	}
	score += p->limits.maxImageDimension2D;

	VkPhysicalDeviceMemoryProperties mp;
	nrvk.GetPhysicalDeviceMemoryProperties(pd, &mp);
	for (u32 i = 0; i < mp.memoryHeapCount; i++)
	{
		if (mp.memoryHeaps[i].flags & VK_MEMORY_HEAP_DEVICE_LOCAL_BIT)
			score += mp.memoryHeaps[i].size / (1024ull * 1024ull);
	}

	NRQueueFamilies f = nrFindQueueFamilies(pd);
	if (!f.has_graphics) return 0;   // 不可用
	return score;
}

static u32 nrCollectFeatureMask(VkPhysicalDevice pd, const VkPhysicalDeviceFeatures* feat)
{
	u32 mask = 0;
	if (feat->samplerAnisotropy)  mask |= NR_FEATURE_ANISOTROPY;
	if (feat->sampleRateShading)  mask |= NR_FEATURE_SAMPLE_RATE_SHADING;
	if (feat->geometryShader)     mask |= NR_FEATURE_GEOMETRY_SHADER;
	if (feat->tessellationShader) mask |= NR_FEATURE_TESSELLATION;
	if (feat->multiDrawIndirect)  mask |= NR_FEATURE_MULTI_DRAW_INDIRECT;
	mask |= NR_FEATURE_COMPUTE;   // Vulkan 强制要求支持计算

	u32 extCount = 0;
	nrvk.EnumerateDeviceExtensionProperties(pd, NULL, &extCount, NULL);
	if (extCount > 0)
	{
		VkExtensionProperties* exts =
			(VkExtensionProperties*)malloc(sizeof(VkExtensionProperties) * extCount);
		if (exts != NULL)
		{
			nrvk.EnumerateDeviceExtensionProperties(pd, NULL, &extCount, exts);
			if (nrHasExtension(exts, extCount, VK_EXT_DESCRIPTOR_INDEXING_EXTENSION_NAME))
				mask |= NR_FEATURE_DESCRIPTOR_INDEXING;
			if (nrHasExtension(exts, extCount, VK_KHR_RAY_TRACING_PIPELINE_EXTENSION_NAME))
				mask |= NR_FEATURE_RAY_TRACING;
			free(exts);
		}
	}
	return mask;
}

// ------------------------------------------------------------
// 枚举
// ------------------------------------------------------------
static NRResult nrDeviceEnumerateInternal(NRDeviceInfo* out, u32* inout_count)
{
	if (inout_count == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_PARAMETER, 0);
	if (nr_device.instance == VK_NULL_HANDLE)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_NOT_INITIALIZED, 0);

	u32 count = 0;
	nrvk.EnumeratePhysicalDevices(nr_device.instance, &count, NULL);
	if (count == 0)
	{
		*inout_count = 0;
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_DEVICE_NOT_FOUND, 0);
	}
	if (count > NR_MAX_PHYSICAL_DEVICES) count = NR_MAX_PHYSICAL_DEVICES;

	if (out == NULL)
	{
		*inout_count = count;
		return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);
	}

	VkPhysicalDevice pds[NR_MAX_PHYSICAL_DEVICES];
	u32 query = NR_MAX_PHYSICAL_DEVICES;
	nrvk.EnumeratePhysicalDevices(nr_device.instance, &query, pds);
	if (query < count) count = query;

	u32 write = (*inout_count < count) ? *inout_count : count;
	for (u32 i = 0; i < write; i++)
	{
		VkPhysicalDeviceProperties p;
		VkPhysicalDeviceFeatures f;
		VkPhysicalDeviceMemoryProperties mp;
		nrvk.GetPhysicalDeviceProperties(pds[i], &p);
		nrvk.GetPhysicalDeviceFeatures(pds[i], &f);
		nrvk.GetPhysicalDeviceMemoryProperties(pds[i], &mp);

		memset(&out[i], 0, sizeof(NRDeviceInfo));
		strncpy(out[i].name, p.deviceName, sizeof(out[i].name) - 1);
		out[i].vendor_id = p.vendorID;
		out[i].device_id = p.deviceID;
		out[i].device_type = (u32)p.deviceType;
		out[i].api_version = p.apiVersion;
		out[i].driver_version = p.driverVersion;
		out[i].features = nrCollectFeatureMask(pds[i], &f);

		u64 vram = 0;
		for (u32 h = 0; h < mp.memoryHeapCount; h++)
		{
			if (mp.memoryHeaps[h].flags & VK_MEMORY_HEAP_DEVICE_LOCAL_BIT)
				vram += mp.memoryHeaps[h].size;
		}
		out[i].vram_bytes = vram;

		VkSampleCountFlags samples = p.limits.framebufferColorSampleCounts &
									 p.limits.framebufferDepthSampleCounts;
		u32 maxSamples = 1;
		if (samples & VK_SAMPLE_COUNT_64_BIT) maxSamples = 64;
		else if (samples & VK_SAMPLE_COUNT_32_BIT) maxSamples = 32;
		else if (samples & VK_SAMPLE_COUNT_16_BIT) maxSamples = 16;
		else if (samples & VK_SAMPLE_COUNT_8_BIT) maxSamples = 8;
		else if (samples & VK_SAMPLE_COUNT_4_BIT) maxSamples = 4;
		else if (samples & VK_SAMPLE_COUNT_2_BIT) maxSamples = 2;
		out[i].max_msaa_samples = maxSamples;
	}

	*inout_count = count;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);
}

// 枚举可能发生在渲染器创建之前（宿主需要先拿到设备列表再决定用哪块卡），
// 此时还没有 VkInstance。这里按需建立一个临时实例，枚举完立刻销毁，
// 保证不影响后续 nrRendererCreateInternal 的正常实例创建流程。
NRResult nrDeviceEnumerate(NRDeviceInfo* out, u32* inout_count)
{
	if (inout_count == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_PARAMETER, 0);

	if (nr_device.instance != VK_NULL_HANDLE)
		return nrDeviceEnumerateInternal(out, inout_count);

	NRResult r = nrDeviceCreateInstance(NULL, FALSE);
	if (NRR_FAILED(r)) return r;

	NRResult e = nrDeviceEnumerateInternal(out, inout_count);

	if (nr_device.instance != VK_NULL_HANDLE && nrvk.DestroyInstance != NULL)
		nrvk.DestroyInstance(nr_device.instance, NULL);
	nr_device.instance = VK_NULL_HANDLE;
	nr_device.debug_messenger = VK_NULL_HANDLE;
	nr_device.api_version = 0;
	nr_device.validation_enabled = FALSE;

	return e;
}

// ------------------------------------------------------------
// 逻辑设备
// ------------------------------------------------------------
NRResult nrDeviceCreateLogical(const struct NRRendererCreateInfo* info, u32 device_index)
{
	if (nr_device.instance == VK_NULL_HANDLE)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_NOT_INITIALIZED, 0);
	if (nr_device.device != VK_NULL_HANDLE)
		return NRR_MakeWarning(NRR_STEP_NR_CreateDevice, NRR_CODE_RENDERER_ALREADY_CREATED, 0);

	u32 count = NR_MAX_PHYSICAL_DEVICES;
	VkPhysicalDevice pds[NR_MAX_PHYSICAL_DEVICES];
	nrvk.EnumeratePhysicalDevices(nr_device.instance, &count, pds);
	if (count == 0)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_DEVICE_NOT_FOUND, 0);

	// ---- 选择物理设备 ----
	VkPhysicalDevice chosen = VK_NULL_HANDLE;
	if (device_index != NR_AUTO_DEVICE && device_index < count)
	{
		chosen = pds[device_index];
	}
	else
	{
		u64 best = 0;
		for (u32 i = 0; i < count; i++)
		{
			VkPhysicalDeviceProperties p;
			nrvk.GetPhysicalDeviceProperties(pds[i], &p);
			u64 s = nrScoreDevice(pds[i], &p);
			if (s > best) { best = s; chosen = pds[i]; }
		}
	}
	if (chosen == VK_NULL_HANDLE)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_DEVICE_NOT_FOUND, 1);

	nr_device.physical = chosen;
	nrvk.GetPhysicalDeviceProperties(chosen, &nr_device.props);
	nrvk.GetPhysicalDeviceFeatures(chosen, &nr_device.features);
	nrvk.GetPhysicalDeviceMemoryProperties(chosen, &nr_device.mem_props);
	nr_device.enabled_features = nrCollectFeatureMask(chosen, &nr_device.features);

	nr_device.families = nrFindQueueFamilies(chosen);
	if (!nr_device.families.has_graphics)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_QUEUE_NOT_FOUND, 0);

	// ---- 去重的队列创建信息 ----
	u32 uniq[4];
	u32 uniqCount = 0;
	const u32 wantFamilies[4] = {
		nr_device.families.graphics,
		nr_device.families.present,
		nr_device.families.compute,
		nr_device.families.transfer
	};
	for (u32 i = 0; i < 4; i++)
	{
		bool dup = false;
		for (u32 j = 0; j < uniqCount; j++)
			if (uniq[j] == wantFamilies[i]) { dup = true; break; }
		if (!dup) uniq[uniqCount++] = wantFamilies[i];
	}

	const f32 priority = 1.0f;
	VkDeviceQueueCreateInfo qcis[4];
	for (u32 i = 0; i < uniqCount; i++)
	{
		memset(&qcis[i], 0, sizeof(VkDeviceQueueCreateInfo));
		qcis[i].sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
		qcis[i].queueFamilyIndex = uniq[i];
		qcis[i].queueCount = 1;
		qcis[i].pQueuePriorities = &priority;
	}

	// ---- 设备扩展 ----
	u32 availCount = 0;
	nrvk.EnumerateDeviceExtensionProperties(chosen, NULL, &availCount, NULL);
	VkExtensionProperties* avail = NULL;
	if (availCount > 0)
	{
		avail = (VkExtensionProperties*)malloc(sizeof(VkExtensionProperties) * availCount);
		if (avail == NULL) return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_OUT_OF_MEMORY, 0);
		nrvk.EnumerateDeviceExtensionProperties(chosen, NULL, &availCount, avail);
	}

	const char* devExts[NR_MAX_EXT];
	u32 devExtCount = 0;

	if (!nrHasExtension(avail, availCount, VK_KHR_SWAPCHAIN_EXTENSION_NAME))
	{
		free(avail);
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_DEVICE_EXTENSION_MISSING, 0);
	}
	devExts[devExtCount++] = VK_KHR_SWAPCHAIN_EXTENSION_NAME;

	// MoltenVK 便携子集
	if (nrHasExtension(avail, availCount, "VK_KHR_portability_subset") && devExtCount < NR_MAX_EXT)
		devExts[devExtCount++] = "VK_KHR_portability_subset";

	if ((nr_device.enabled_features & NR_FEATURE_DESCRIPTOR_INDEXING) && devExtCount < NR_MAX_EXT)
		devExts[devExtCount++] = VK_EXT_DESCRIPTOR_INDEXING_EXTENSION_NAME;

	if (info != NULL)
	{
		for (s32 i = 0; i < info->required_device_extensions_count && devExtCount < NR_MAX_EXT; i++)
			devExts[devExtCount++] = info->required_device_extensions[i];
		for (s32 i = 0; i < info->optional_device_extensions_count && devExtCount < NR_MAX_EXT; i++)
		{
			if (nrHasExtension(avail, availCount, info->optional_device_extensions[i]))
				devExts[devExtCount++] = info->optional_device_extensions[i];
		}
	}

	// ---- 启用特性 ----
	VkPhysicalDeviceFeatures enabled;
	memset(&enabled, 0, sizeof(enabled));
	enabled.samplerAnisotropy  = nr_device.features.samplerAnisotropy;
	enabled.sampleRateShading  = nr_device.features.sampleRateShading;
	enabled.fillModeNonSolid   = nr_device.features.fillModeNonSolid;
	enabled.geometryShader     = nr_device.features.geometryShader;
	enabled.multiDrawIndirect  = nr_device.features.multiDrawIndirect;
	enabled.independentBlend   = nr_device.features.independentBlend;
	enabled.depthClamp         = nr_device.features.depthClamp;
	enabled.depthBiasClamp     = nr_device.features.depthBiasClamp;
	enabled.shaderSampledImageArrayDynamicIndexing =
		nr_device.features.shaderSampledImageArrayDynamicIndexing;

	VkPhysicalDeviceDescriptorIndexingFeaturesEXT diFeatures;
	memset(&diFeatures, 0, sizeof(diFeatures));
	diFeatures.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_DESCRIPTOR_INDEXING_FEATURES_EXT;

	nr_device.update_after_bind = FALSE;
	if (nr_device.enabled_features & NR_FEATURE_DESCRIPTOR_INDEXING)
	{
		// 先查硬件实际支持的子特性，避免启用未支持项导致创建失败或
		// 后续 vkCreateDescriptorSetLayout 校验报错
		VkPhysicalDeviceDescriptorIndexingFeaturesEXT supported;
		memset(&supported, 0, sizeof(supported));
		supported.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_DESCRIPTOR_INDEXING_FEATURES_EXT;

		if (nrvk.GetPhysicalDeviceFeatures2 != NULL)
		{
			VkPhysicalDeviceFeatures2 f2;
			memset(&f2, 0, sizeof(f2));
			f2.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2;
			f2.pNext = &supported;
			nrvk.GetPhysicalDeviceFeatures2(chosen, &f2);
		}

		diFeatures.shaderSampledImageArrayNonUniformIndexing =
			supported.shaderSampledImageArrayNonUniformIndexing;
		diFeatures.runtimeDescriptorArray = supported.runtimeDescriptorArray;
		diFeatures.descriptorBindingPartiallyBound = supported.descriptorBindingPartiallyBound;
		diFeatures.descriptorBindingVariableDescriptorCount =
			supported.descriptorBindingVariableDescriptorCount;
		diFeatures.descriptorBindingSampledImageUpdateAfterBind =
			supported.descriptorBindingSampledImageUpdateAfterBind;
		diFeatures.descriptorBindingUpdateUnusedWhilePending =
			supported.descriptorBindingUpdateUnusedWhilePending;

		nr_device.update_after_bind =
			(diFeatures.runtimeDescriptorArray &&
			 diFeatures.descriptorBindingPartiallyBound &&
			 diFeatures.descriptorBindingSampledImageUpdateAfterBind &&
			 diFeatures.descriptorBindingUpdateUnusedWhilePending) ? TRUE : FALSE;
	}

	VkDeviceCreateInfo dci;
	memset(&dci, 0, sizeof(dci));
	dci.sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO;
	dci.queueCreateInfoCount = uniqCount;
	dci.pQueueCreateInfos = qcis;
	dci.enabledExtensionCount = devExtCount;
	dci.ppEnabledExtensionNames = devExts;
	dci.pEnabledFeatures = &enabled;
	if (nr_device.enabled_features & NR_FEATURE_DESCRIPTOR_INDEXING)
		dci.pNext = &diFeatures;

	VkResult vr = nrvk.CreateDevice(chosen, &dci, NULL, &nr_device.device);
	free(avail);
	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_DEVICE_NOT_FOUND, (u32)vr);

	NRResult r = nrVkLoadDevice(nr_device.device);
	if (NRR_FAILED(r)) return r;

	nrvk.GetDeviceQueue(nr_device.device, nr_device.families.graphics, 0, &nr_device.graphics_queue);
	nrvk.GetDeviceQueue(nr_device.device, nr_device.families.present,  0, &nr_device.present_queue);
	nrvk.GetDeviceQueue(nr_device.device, nr_device.families.compute,  0, &nr_device.compute_queue);
	nrvk.GetDeviceQueue(nr_device.device, nr_device.families.transfer, 0, &nr_device.transfer_queue);

	VkCommandPoolCreateInfo pci;
	memset(&pci, 0, sizeof(pci));
	pci.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
	pci.flags = VK_COMMAND_POOL_CREATE_TRANSIENT_BIT |
				VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT;
	pci.queueFamilyIndex = nr_device.families.graphics;
	if (nrvk.CreateCommandPool(nr_device.device, &pci, NULL, &nr_device.transient_pool) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_COMMAND_BUFFER_FAILED, 0);

	nr_device.initialized = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);
}

NRResult nrDeviceGetInfo(NRDeviceInfo* out)
{
	if (out == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_PARAMETER, 0);
	if (nr_device.physical == VK_NULL_HANDLE)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_NOT_INITIALIZED, 0);

	memset(out, 0, sizeof(NRDeviceInfo));
	strncpy(out->name, nr_device.props.deviceName, sizeof(out->name) - 1);
	out->vendor_id = nr_device.props.vendorID;
	out->device_id = nr_device.props.deviceID;
	out->device_type = (u32)nr_device.props.deviceType;
	out->api_version = nr_device.props.apiVersion;
	out->driver_version = nr_device.props.driverVersion;
	out->features = nr_device.enabled_features;

	u64 vram = 0;
	for (u32 h = 0; h < nr_device.mem_props.memoryHeapCount; h++)
	{
		if (nr_device.mem_props.memoryHeaps[h].flags & VK_MEMORY_HEAP_DEVICE_LOCAL_BIT)
			vram += nr_device.mem_props.memoryHeaps[h].size;
	}
	out->vram_bytes = vram;

	VkSampleCountFlags samples = nr_device.props.limits.framebufferColorSampleCounts &
								 nr_device.props.limits.framebufferDepthSampleCounts;
	u32 maxSamples = 1;
	if (samples & VK_SAMPLE_COUNT_8_BIT) maxSamples = 8;
	else if (samples & VK_SAMPLE_COUNT_4_BIT) maxSamples = 4;
	else if (samples & VK_SAMPLE_COUNT_2_BIT) maxSamples = 2;
	out->max_msaa_samples = maxSamples;

	return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);
}

s32 nrDeviceFindMemoryType(u32 type_bits, VkMemoryPropertyFlags props)
{
	for (u32 i = 0; i < nr_device.mem_props.memoryTypeCount; i++)
	{
		if ((type_bits & (1u << i)) &&
			(nr_device.mem_props.memoryTypes[i].propertyFlags & props) == props)
		{
			return (s32)i;
		}
	}
	return -1;
}

VkCommandBuffer nrDeviceBeginOneShot(void)
{
	if (!nr_device.initialized) return VK_NULL_HANDLE;

	VkCommandBufferAllocateInfo ai;
	memset(&ai, 0, sizeof(ai));
	ai.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
	ai.commandPool = nr_device.transient_pool;
	ai.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
	ai.commandBufferCount = 1;

	VkCommandBuffer cmd = VK_NULL_HANDLE;
	if (nrvk.AllocateCommandBuffers(nr_device.device, &ai, &cmd) != VK_SUCCESS)
		return VK_NULL_HANDLE;

	VkCommandBufferBeginInfo bi;
	memset(&bi, 0, sizeof(bi));
	bi.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
	bi.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
	nrvk.BeginCommandBuffer(cmd, &bi);
	return cmd;
}

NRResult nrDeviceEndOneShot(VkCommandBuffer cmd)
{
	if (cmd == VK_NULL_HANDLE)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_INVALID_PARAMETER, 0);

	nrvk.EndCommandBuffer(cmd);

	VkSubmitInfo si;
	memset(&si, 0, sizeof(si));
	si.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
	si.commandBufferCount = 1;
	si.pCommandBuffers = &cmd;

	VkFenceCreateInfo fci;
	memset(&fci, 0, sizeof(fci));
	fci.sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
	VkFence fence = VK_NULL_HANDLE;
	nrvk.CreateFence(nr_device.device, &fci, NULL, &fence);

	VkResult vr = nrvk.QueueSubmit(nr_device.graphics_queue, 1, &si, fence);
	if (vr == VK_SUCCESS)
	{
		nrvk.WaitForFences(nr_device.device, 1, &fence, VK_TRUE, UINT64_MAX);
	}
	nrvk.DestroyFence(nr_device.device, fence, NULL);
	nrvk.FreeCommandBuffers(nr_device.device, nr_device.transient_pool, 1, &cmd);

	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateDevice, NRR_CODE_SUBMIT_FAILED, (u32)vr);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateDevice, NRR_CODE_SUCCESS);
}

void nrDeviceDestroy(void)
{
	if (nr_device.device != VK_NULL_HANDLE)
	{
		nrvk.DeviceWaitIdle(nr_device.device);
		if (nr_device.transient_pool != VK_NULL_HANDLE)
		{
			nrvk.DestroyCommandPool(nr_device.device, nr_device.transient_pool, NULL);
			nr_device.transient_pool = VK_NULL_HANDLE;
		}
		nrvk.DestroyDevice(nr_device.device, NULL);
		nr_device.device = VK_NULL_HANDLE;
	}
	if (nr_device.surface != VK_NULL_HANDLE && nrvk.DestroySurfaceKHR != NULL)
	{
		nrvk.DestroySurfaceKHR(nr_device.instance, nr_device.surface, NULL);
		nr_device.surface = VK_NULL_HANDLE;
	}
	if (nr_device.debug_messenger != VK_NULL_HANDLE && nrvk.DestroyDebugUtilsMessengerEXT != NULL)
	{
		nrvk.DestroyDebugUtilsMessengerEXT(nr_device.instance, nr_device.debug_messenger, NULL);
		nr_device.debug_messenger = VK_NULL_HANDLE;
	}
	if (nr_device.instance != VK_NULL_HANDLE && nrvk.DestroyInstance != NULL)
	{
		nrvk.DestroyInstance(nr_device.instance, NULL);
		nr_device.instance = VK_NULL_HANDLE;
	}
	memset(&nr_device, 0, sizeof(nr_device));
}
