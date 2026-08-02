#include "NRSwapchain.h"
#include <SDL3/SDL_vulkan.h>

// ============================================================
// NRSwapchain.c
// ============================================================

NRSwapchain nr_swapchain;

// ------------------------------------------------------------
// 选择表面格式
// ------------------------------------------------------------
static void nrChooseSurfaceFormat(b32 hdr, VkFormat* outFormat, VkColorSpaceKHR* outSpace)
{
	*outFormat = VK_FORMAT_B8G8R8A8_UNORM;
	*outSpace = VK_COLOR_SPACE_SRGB_NONLINEAR_KHR;

	u32 count = 0;
	nrvk.GetPhysicalDeviceSurfaceFormatsKHR(nr_device.physical, nr_device.surface, &count, NULL);
	if (count == 0) return;

	VkSurfaceFormatKHR* fmts = (VkSurfaceFormatKHR*)malloc(sizeof(VkSurfaceFormatKHR) * count);
	if (fmts == NULL) return;
	nrvk.GetPhysicalDeviceSurfaceFormatsKHR(nr_device.physical, nr_device.surface, &count, fmts);

	// 驱动返回 UNDEFINED 表示任意格式均可
	if (count == 1 && fmts[0].format == VK_FORMAT_UNDEFINED)
	{
		free(fmts);
		return;
	}

	if (hdr)
	{
		for (u32 i = 0; i < count; i++)
		{
			if ((fmts[i].format == VK_FORMAT_A2B10G10R10_UNORM_PACK32 ||
				 fmts[i].format == VK_FORMAT_A2R10G10B10_UNORM_PACK32) &&
				fmts[i].colorSpace == VK_COLOR_SPACE_HDR10_ST2084_EXT)
			{
				*outFormat = fmts[i].format;
				*outSpace = fmts[i].colorSpace;
				free(fmts);
				return;
			}
		}
		for (u32 i = 0; i < count; i++)
		{
			if (fmts[i].format == VK_FORMAT_R16G16B16A16_SFLOAT &&
				fmts[i].colorSpace == VK_COLOR_SPACE_EXTENDED_SRGB_LINEAR_EXT)
			{
				*outFormat = fmts[i].format;
				*outSpace = fmts[i].colorSpace;
				free(fmts);
				return;
			}
		}
		// 无 HDR 支持则回退 SDR
	}

	for (u32 i = 0; i < count; i++)
	{
		if ((fmts[i].format == VK_FORMAT_B8G8R8A8_SRGB ||
			 fmts[i].format == VK_FORMAT_R8G8B8A8_SRGB) &&
			fmts[i].colorSpace == VK_COLOR_SPACE_SRGB_NONLINEAR_KHR)
		{
			*outFormat = fmts[i].format;
			*outSpace = fmts[i].colorSpace;
			free(fmts);
			return;
		}
	}

	*outFormat = fmts[0].format;
	*outSpace = fmts[0].colorSpace;
	free(fmts);
}

// ------------------------------------------------------------
// 选择呈现模式：非 vsync 优先 mailbox，其次 immediate，最后 fifo
// ------------------------------------------------------------
static VkPresentModeKHR nrChoosePresentMode(b32 vsync)
{
	if (vsync) return VK_PRESENT_MODE_FIFO_KHR;

	u32 count = 0;
	nrvk.GetPhysicalDeviceSurfacePresentModesKHR(nr_device.physical, nr_device.surface, &count, NULL);
	if (count == 0) return VK_PRESENT_MODE_FIFO_KHR;

	VkPresentModeKHR* modes = (VkPresentModeKHR*)malloc(sizeof(VkPresentModeKHR) * count);
	if (modes == NULL) return VK_PRESENT_MODE_FIFO_KHR;
	nrvk.GetPhysicalDeviceSurfacePresentModesKHR(nr_device.physical, nr_device.surface, &count, modes);

	VkPresentModeKHR chosen = VK_PRESENT_MODE_FIFO_KHR;
	for (u32 i = 0; i < count; i++)
	{
		if (modes[i] == VK_PRESENT_MODE_MAILBOX_KHR) { chosen = modes[i]; break; }
		if (modes[i] == VK_PRESENT_MODE_IMMEDIATE_KHR) chosen = modes[i];
	}
	free(modes);
	return chosen;
}

// ------------------------------------------------------------
// 深度格式
// ------------------------------------------------------------
static VkFormat nrChooseDepthFormat(void)
{
	const VkFormat candidates[] = {
		VK_FORMAT_D32_SFLOAT_S8_UINT,
		VK_FORMAT_D32_SFLOAT,
		VK_FORMAT_D24_UNORM_S8_UINT,
		VK_FORMAT_D16_UNORM_S8_UINT,
		VK_FORMAT_D16_UNORM
	};
	for (u32 i = 0; i < sizeof(candidates) / sizeof(candidates[0]); i++)
	{
		VkFormatProperties p;
		nrvk.GetPhysicalDeviceFormatProperties(nr_device.physical, candidates[i], &p);
		if (p.optimalTilingFeatures & VK_FORMAT_FEATURE_DEPTH_STENCIL_ATTACHMENT_BIT)
			return candidates[i];
	}
	return VK_FORMAT_D32_SFLOAT;
}

static VkSampleCountFlagBits nrClampSamples(u32 requested)
{
	if (requested <= 1) return VK_SAMPLE_COUNT_1_BIT;

	VkSampleCountFlags supported = nr_device.props.limits.framebufferColorSampleCounts &
								   nr_device.props.limits.framebufferDepthSampleCounts;
	VkSampleCountFlagBits want = VK_SAMPLE_COUNT_1_BIT;
	if (requested >= 8) want = VK_SAMPLE_COUNT_8_BIT;
	else if (requested >= 4) want = VK_SAMPLE_COUNT_4_BIT;
	else want = VK_SAMPLE_COUNT_2_BIT;

	while (want > VK_SAMPLE_COUNT_1_BIT && !(supported & want))
		want = (VkSampleCountFlagBits)(want >> 1);
	return want;
}

// ------------------------------------------------------------
// RenderPass
// ------------------------------------------------------------
static NRResult nrCreateRenderPass(VkFormat colorFormat, VkFormat depthFormat,
								   VkSampleCountFlagBits samples)
{
	const bool msaa = (samples != VK_SAMPLE_COUNT_1_BIT);

	VkAttachmentDescription attachments[3];
	memset(attachments, 0, sizeof(attachments));
	u32 attCount = 0;

	// 0: 颜色（MSAA 时为多重采样目标，否则为交换链图像）
	attachments[attCount].format = colorFormat;
	attachments[attCount].samples = samples;
	attachments[attCount].loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
	attachments[attCount].storeOp = msaa ? VK_ATTACHMENT_STORE_OP_DONT_CARE
										 : VK_ATTACHMENT_STORE_OP_STORE;
	attachments[attCount].stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE;
	attachments[attCount].stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
	attachments[attCount].initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
	attachments[attCount].finalLayout = msaa ? VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL
											 : VK_IMAGE_LAYOUT_PRESENT_SRC_KHR;
	const u32 colorIndex = attCount++;

	// 1: 深度
	attachments[attCount].format = depthFormat;
	attachments[attCount].samples = samples;
	attachments[attCount].loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
	attachments[attCount].storeOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
	attachments[attCount].stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE;
	attachments[attCount].stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
	attachments[attCount].initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
	attachments[attCount].finalLayout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;
	const u32 depthIndex = attCount++;

	// 2: MSAA 解析目标（交换链图像）
	u32 resolveIndex = 0;
	if (msaa)
	{
		attachments[attCount].format = colorFormat;
		attachments[attCount].samples = VK_SAMPLE_COUNT_1_BIT;
		attachments[attCount].loadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE;
		attachments[attCount].storeOp = VK_ATTACHMENT_STORE_OP_STORE;
		attachments[attCount].stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE;
		attachments[attCount].stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
		attachments[attCount].initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
		attachments[attCount].finalLayout = VK_IMAGE_LAYOUT_PRESENT_SRC_KHR;
		resolveIndex = attCount++;
	}

	VkAttachmentReference colorRef;
	colorRef.attachment = colorIndex;
	colorRef.layout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;

	VkAttachmentReference depthRef;
	depthRef.attachment = depthIndex;
	depthRef.layout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;

	VkAttachmentReference resolveRef;
	resolveRef.attachment = resolveIndex;
	resolveRef.layout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;

	VkSubpassDescription subpass;
	memset(&subpass, 0, sizeof(subpass));
	subpass.pipelineBindPoint = VK_PIPELINE_BIND_POINT_GRAPHICS;
	subpass.colorAttachmentCount = 1;
	subpass.pColorAttachments = &colorRef;
	subpass.pDepthStencilAttachment = &depthRef;
	if (msaa) subpass.pResolveAttachments = &resolveRef;

	VkSubpassDependency deps[2];
	memset(deps, 0, sizeof(deps));
	deps[0].srcSubpass = VK_SUBPASS_EXTERNAL;
	deps[0].dstSubpass = 0;
	deps[0].srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT |
						   VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT;
	deps[0].dstStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT |
						   VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT;
	deps[0].srcAccessMask = 0;
	deps[0].dstAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT |
							VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT;

	deps[1].srcSubpass = 0;
	deps[1].dstSubpass = VK_SUBPASS_EXTERNAL;
	deps[1].srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
	deps[1].dstStageMask = VK_PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT;
	deps[1].srcAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
	deps[1].dstAccessMask = 0;

	VkRenderPassCreateInfo ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO;
	ci.attachmentCount = attCount;
	ci.pAttachments = attachments;
	ci.subpassCount = 1;
	ci.pSubpasses = &subpass;
	ci.dependencyCount = 2;
	ci.pDependencies = deps;

	if (nrvk.CreateRenderPass(nr_device.device, &ci, NULL, &nr_swapchain.render_pass) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_RENDERPASS_CREATION_FAILED, 0);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 与交换链尺寸绑定的资源
// ------------------------------------------------------------
static void nrDestroySizeDependent(void)
{
	for (u32 i = 0; i < nr_swapchain.image_count; i++)
	{
		if (nr_swapchain.framebuffers[i] != VK_NULL_HANDLE)
		{
			nrvk.DestroyFramebuffer(nr_device.device, nr_swapchain.framebuffers[i], NULL);
			nr_swapchain.framebuffers[i] = VK_NULL_HANDLE;
		}
		if (nr_swapchain.views[i] != VK_NULL_HANDLE)
		{
			nrvk.DestroyImageView(nr_device.device, nr_swapchain.views[i], NULL);
			nr_swapchain.views[i] = VK_NULL_HANDLE;
		}
		nr_swapchain.images[i] = VK_NULL_HANDLE;
	}
	nrImageDestroy(&nr_swapchain.depth);
	nrImageDestroy(&nr_swapchain.color_msaa);
	nr_swapchain.image_count = 0;
}

static NRResult nrCreateSizeDependent(void)
{
	// ---- 交换链图像视图 ----
	u32 count = 0;
	nrvk.GetSwapchainImagesKHR(nr_device.device, nr_swapchain.handle, &count, NULL);
	if (count == 0)
		return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SWAPCHAIN_CREATION_FAILED, 1);
	if (count > NR_MAX_SWAPCHAIN_IMAGES) count = NR_MAX_SWAPCHAIN_IMAGES;

	nrvk.GetSwapchainImagesKHR(nr_device.device, nr_swapchain.handle, &count, nr_swapchain.images);
	nr_swapchain.image_count = count;

	for (u32 i = 0; i < count; i++)
	{
		VkImageViewCreateInfo vci;
		memset(&vci, 0, sizeof(vci));
		vci.sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
		vci.image = nr_swapchain.images[i];
		vci.viewType = VK_IMAGE_VIEW_TYPE_2D;
		vci.format = nr_swapchain.format;
		vci.subresourceRange.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
		vci.subresourceRange.levelCount = 1;
		vci.subresourceRange.layerCount = 1;
		if (nrvk.CreateImageView(nr_device.device, &vci, NULL, &nr_swapchain.views[i]) != VK_SUCCESS)
			return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SWAPCHAIN_CREATION_FAILED, 2);
		nr_swapchain.images_in_flight[i] = VK_NULL_HANDLE;
	}

	// ---- 深度附件 ----
	VkFormat depthFormat = nrChooseDepthFormat();
	VkImageAspectFlags depthAspect = VK_IMAGE_ASPECT_DEPTH_BIT;
	if (depthFormat == VK_FORMAT_D32_SFLOAT_S8_UINT ||
		depthFormat == VK_FORMAT_D24_UNORM_S8_UINT ||
		depthFormat == VK_FORMAT_D16_UNORM_S8_UINT)
	{
		depthAspect |= VK_IMAGE_ASPECT_STENCIL_BIT;
	}

	NRResult r = nrImageCreate(nr_swapchain.extent.width, nr_swapchain.extent.height, 1, 1, 1,
							   depthFormat, VK_IMAGE_TILING_OPTIMAL,
							   VK_IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT,
							   depthAspect, nr_swapchain.samples, FALSE, &nr_swapchain.depth);
	if (NRR_FAILED(r)) return r;

	// ---- MSAA 颜色目标 ----
	const bool msaa = (nr_swapchain.samples != VK_SAMPLE_COUNT_1_BIT);
	if (msaa)
	{
		r = nrImageCreate(nr_swapchain.extent.width, nr_swapchain.extent.height, 1, 1, 1,
						  nr_swapchain.format, VK_IMAGE_TILING_OPTIMAL,
						  VK_IMAGE_USAGE_TRANSIENT_ATTACHMENT_BIT |
						  VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT,
						  VK_IMAGE_ASPECT_COLOR_BIT, nr_swapchain.samples, FALSE,
						  &nr_swapchain.color_msaa);
		if (NRR_FAILED(r)) return r;
	}

	// ---- 帧缓冲 ----
	for (u32 i = 0; i < count; i++)
	{
		VkImageView atts[3];
		u32 attCount = 0;
		if (msaa)
		{
			atts[attCount++] = nr_swapchain.color_msaa.view;
			atts[attCount++] = nr_swapchain.depth.view;
			atts[attCount++] = nr_swapchain.views[i];
		}
		else
		{
			atts[attCount++] = nr_swapchain.views[i];
			atts[attCount++] = nr_swapchain.depth.view;
		}

		VkFramebufferCreateInfo fci;
		memset(&fci, 0, sizeof(fci));
		fci.sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO;
		fci.renderPass = nr_swapchain.render_pass;
		fci.attachmentCount = attCount;
		fci.pAttachments = atts;
		fci.width = nr_swapchain.extent.width;
		fci.height = nr_swapchain.extent.height;
		fci.layers = 1;

		if (nrvk.CreateFramebuffer(nr_device.device, &fci, NULL,
								   &nr_swapchain.framebuffers[i]) != VK_SUCCESS)
			return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain,
								   NRR_CODE_FRAMEBUFFER_CREATION_FAILED, 0);
	}

	return NRR_MakeSuccess(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 同步与命令对象
// ------------------------------------------------------------
static NRResult nrCreateFrameObjects(void)
{
	VkSemaphoreCreateInfo sci;
	memset(&sci, 0, sizeof(sci));
	sci.sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO;

	VkFenceCreateInfo fci;
	memset(&fci, 0, sizeof(fci));
	fci.sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
	fci.flags = VK_FENCE_CREATE_SIGNALED_BIT;   // 首帧无需等待

	for (u32 i = 0; i < NR_MAX_FRAMES_IN_FLIGHT; i++)
	{
		if (nrvk.CreateSemaphore(nr_device.device, &sci, NULL, &nr_swapchain.image_available[i]) != VK_SUCCESS ||
			nrvk.CreateSemaphore(nr_device.device, &sci, NULL, &nr_swapchain.render_finished[i]) != VK_SUCCESS ||
			nrvk.CreateFence(nr_device.device, &fci, NULL, &nr_swapchain.in_flight[i]) != VK_SUCCESS)
		{
			return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SYNC_CREATION_FAILED, 0);
		}
	}

	VkCommandPoolCreateInfo pci;
	memset(&pci, 0, sizeof(pci));
	pci.sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
	pci.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT;
	pci.queueFamilyIndex = nr_device.families.graphics;
	if (nrvk.CreateCommandPool(nr_device.device, &pci, NULL, &nr_swapchain.cmd_pool) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_COMMAND_BUFFER_FAILED, 0);

	VkCommandBufferAllocateInfo ai;
	memset(&ai, 0, sizeof(ai));
	ai.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
	ai.commandPool = nr_swapchain.cmd_pool;
	ai.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
	ai.commandBufferCount = NR_MAX_FRAMES_IN_FLIGHT;
	if (nrvk.AllocateCommandBuffers(nr_device.device, &ai, nr_swapchain.cmd_buffers) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_COMMAND_BUFFER_FAILED, 1);

	return NRR_MakeSuccess(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SUCCESS);
}

// ------------------------------------------------------------
// 交换链本体
// ------------------------------------------------------------
static NRResult nrBuildSwapchain(u32 width, u32 height, VkSwapchainKHR old)
{
	VkSurfaceCapabilitiesKHR caps;
	if (nrvk.GetPhysicalDeviceSurfaceCapabilitiesKHR(nr_device.physical, nr_device.surface, &caps) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SWAPCHAIN_CREATION_FAILED, 0);

	VkExtent2D extent;
	if (caps.currentExtent.width != 0xFFFFFFFFu)
	{
		extent = caps.currentExtent;
	}
	else
	{
		extent.width = width;
		extent.height = height;
		if (extent.width < caps.minImageExtent.width) extent.width = caps.minImageExtent.width;
		if (extent.height < caps.minImageExtent.height) extent.height = caps.minImageExtent.height;
		if (extent.width > caps.maxImageExtent.width) extent.width = caps.maxImageExtent.width;
		if (extent.height > caps.maxImageExtent.height) extent.height = caps.maxImageExtent.height;
	}
	if (extent.width == 0 || extent.height == 0)
	{
		// 窗口最小化，暂不重建
		nr_swapchain.extent = extent;
		return NRR_MakeWarning(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SWAPCHAIN_OUT_OF_DATE, 0);
	}

	u32 imageCount = caps.minImageCount + 1;
	if (caps.maxImageCount > 0 && imageCount > caps.maxImageCount) imageCount = caps.maxImageCount;
	if (imageCount > NR_MAX_SWAPCHAIN_IMAGES) imageCount = NR_MAX_SWAPCHAIN_IMAGES;

	VkSwapchainCreateInfoKHR ci;
	memset(&ci, 0, sizeof(ci));
	ci.sType = VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR;
	ci.surface = nr_device.surface;
	ci.minImageCount = imageCount;
	ci.imageFormat = nr_swapchain.format;
	ci.imageColorSpace = nr_swapchain.color_space;
	ci.imageExtent = extent;
	ci.imageArrayLayers = 1;
	ci.imageUsage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VK_IMAGE_USAGE_TRANSFER_DST_BIT;
	ci.preTransform = (caps.supportedTransforms & VK_SURFACE_TRANSFORM_IDENTITY_BIT_KHR)
					? VK_SURFACE_TRANSFORM_IDENTITY_BIT_KHR : caps.currentTransform;
	ci.compositeAlpha = VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;
	if (!(caps.supportedCompositeAlpha & VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR))
		ci.compositeAlpha = VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR;
	ci.presentMode = nr_swapchain.present_mode;
	ci.clipped = VK_TRUE;
	ci.oldSwapchain = old;

	const u32 families[2] = { nr_device.families.graphics, nr_device.families.present };
	if (families[0] != families[1])
	{
		ci.imageSharingMode = VK_SHARING_MODE_CONCURRENT;
		ci.queueFamilyIndexCount = 2;
		ci.pQueueFamilyIndices = families;
	}
	else
	{
		ci.imageSharingMode = VK_SHARING_MODE_EXCLUSIVE;
	}

	if (nrvk.CreateSwapchainKHR(nr_device.device, &ci, NULL, &nr_swapchain.handle) != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SWAPCHAIN_CREATION_FAILED, 3);

	nr_swapchain.extent = extent;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SUCCESS);
}

NRResult nrSwapchainCreate(u32 width, u32 height, b32 vsync, b32 hdr, u32 msaa_samples)
{
	if (!nr_device.initialized)
		return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_NOT_INITIALIZED, 0);
	if (nr_swapchain.created)
		return NRR_MakeWarning(NRR_STEP_NR_CreateSwapchain, NRR_CODE_RENDERER_ALREADY_CREATED, 0);

	memset(&nr_swapchain, 0, sizeof(nr_swapchain));
	nr_swapchain.vsync = vsync;
	nr_swapchain.hdr = hdr;
	nr_swapchain.samples = nrClampSamples(msaa_samples);

	nrChooseSurfaceFormat(hdr, &nr_swapchain.format, &nr_swapchain.color_space);
	nr_swapchain.present_mode = nrChoosePresentMode(vsync);

	if (width == 0 || height == 0)
	{
		int w = 0, h = 0;
		SDL_GetWindowSizeInPixels(nr_window, &w, &h);
		width = (u32)w;
		height = (u32)h;
	}

	NRResult r = nrBuildSwapchain(width, height, VK_NULL_HANDLE);
	if (NRR_FAILED(r)) return r;

	r = nrCreateRenderPass(nr_swapchain.format, nrChooseDepthFormat(), nr_swapchain.samples);
	if (NRR_FAILED(r)) return r;

	r = nrCreateSizeDependent();
	if (NRR_FAILED(r)) return r;

	r = nrCreateFrameObjects();
	if (NRR_FAILED(r)) return r;

	nr_swapchain.created = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SUCCESS);
}

NRResult nrSwapchainRecreate(u32 width, u32 height)
{
	if (!nr_swapchain.created)
		return NRR_MakeFailure(NRR_STEP_NR_CreateSwapchain, NRR_CODE_NOT_INITIALIZED, 0);

	nrvk.DeviceWaitIdle(nr_device.device);

	if (width == 0 || height == 0)
	{
		int w = 0, h = 0;
		SDL_GetWindowSizeInPixels(nr_window, &w, &h);
		width = (u32)w;
		height = (u32)h;
	}
	if (width == 0 || height == 0)
	{
		nr_swapchain.needs_rebuild = TRUE;
		return NRR_MakeWarning(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SWAPCHAIN_OUT_OF_DATE, 0);
	}

	nrDestroySizeDependent();

	VkSwapchainKHR old = nr_swapchain.handle;
	nr_swapchain.handle = VK_NULL_HANDLE;
	nr_swapchain.present_mode = nrChoosePresentMode(nr_swapchain.vsync);
	nrChooseSurfaceFormat(nr_swapchain.hdr, &nr_swapchain.format, &nr_swapchain.color_space);

	NRResult r = nrBuildSwapchain(width, height, old);
	if (old != VK_NULL_HANDLE)
		nrvk.DestroySwapchainKHR(nr_device.device, old, NULL);
	if (NRR_FAILED(r)) return r;

	r = nrCreateSizeDependent();
	if (NRR_FAILED(r)) return r;

	nr_swapchain.needs_rebuild = FALSE;
	nr_swapchain.current_frame = 0;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateSwapchain, NRR_CODE_SUCCESS);
}

void nrSwapchainDestroy(void)
{
	if (nr_device.device == VK_NULL_HANDLE) { memset(&nr_swapchain, 0, sizeof(nr_swapchain)); return; }

	nrvk.DeviceWaitIdle(nr_device.device);
	nrDestroySizeDependent();

	for (u32 i = 0; i < NR_MAX_FRAMES_IN_FLIGHT; i++)
	{
		if (nr_swapchain.image_available[i] != VK_NULL_HANDLE)
			nrvk.DestroySemaphore(nr_device.device, nr_swapchain.image_available[i], NULL);
		if (nr_swapchain.render_finished[i] != VK_NULL_HANDLE)
			nrvk.DestroySemaphore(nr_device.device, nr_swapchain.render_finished[i], NULL);
		if (nr_swapchain.in_flight[i] != VK_NULL_HANDLE)
			nrvk.DestroyFence(nr_device.device, nr_swapchain.in_flight[i], NULL);
	}
	if (nr_swapchain.cmd_pool != VK_NULL_HANDLE)
		nrvk.DestroyCommandPool(nr_device.device, nr_swapchain.cmd_pool, NULL);
	if (nr_swapchain.render_pass != VK_NULL_HANDLE)
		nrvk.DestroyRenderPass(nr_device.device, nr_swapchain.render_pass, NULL);
	if (nr_swapchain.handle != VK_NULL_HANDLE)
		nrvk.DestroySwapchainKHR(nr_device.device, nr_swapchain.handle, NULL);

	memset(&nr_swapchain, 0, sizeof(nr_swapchain));
}

// ------------------------------------------------------------
// 帧循环
// ------------------------------------------------------------
NRResult nrSwapchainAcquire(void)
{
	if (!nr_swapchain.created)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_NOT_INITIALIZED, 0);
	if (nr_swapchain.needs_rebuild)
		return NRR_MakeWarning(NRR_STEP_NR_Render, NRR_CODE_SWAPCHAIN_OUT_OF_DATE, 0);

	const u32 frame = nr_swapchain.current_frame;

	nrvk.WaitForFences(nr_device.device, 1, &nr_swapchain.in_flight[frame], VK_TRUE, UINT64_MAX);

	u32 imageIndex = 0;
	VkResult vr = nrvk.AcquireNextImageKHR(nr_device.device, nr_swapchain.handle, UINT64_MAX,
										   nr_swapchain.image_available[frame],
										   VK_NULL_HANDLE, &imageIndex);
	if (vr == VK_ERROR_OUT_OF_DATE_KHR)
	{
		nr_swapchain.needs_rebuild = TRUE;
		return NRR_MakeWarning(NRR_STEP_NR_Render, NRR_CODE_SWAPCHAIN_OUT_OF_DATE, (u32)vr);
	}
	if (vr != VK_SUCCESS && vr != VK_SUBOPTIMAL_KHR)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_PRESENT_FAILED, (u32)vr);

	// 若该图像仍被上一帧占用，先等待
	if (nr_swapchain.images_in_flight[imageIndex] != VK_NULL_HANDLE)
	{
		nrvk.WaitForFences(nr_device.device, 1,
						   &nr_swapchain.images_in_flight[imageIndex], VK_TRUE, UINT64_MAX);
	}
	nr_swapchain.images_in_flight[imageIndex] = nr_swapchain.in_flight[frame];
	nr_swapchain.current_image = imageIndex;

	nrvk.ResetFences(nr_device.device, 1, &nr_swapchain.in_flight[frame]);
	nrvk.ResetCommandBuffer(nr_swapchain.cmd_buffers[frame], 0);

	return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);
}

NRResult nrSwapchainPresent(void)
{
	const u32 frame = nr_swapchain.current_frame;

	VkPipelineStageFlags waitStage = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;

	VkSubmitInfo si;
	memset(&si, 0, sizeof(si));
	si.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
	si.waitSemaphoreCount = 1;
	si.pWaitSemaphores = &nr_swapchain.image_available[frame];
	si.pWaitDstStageMask = &waitStage;
	si.commandBufferCount = 1;
	si.pCommandBuffers = &nr_swapchain.cmd_buffers[frame];
	si.signalSemaphoreCount = 1;
	si.pSignalSemaphores = &nr_swapchain.render_finished[frame];

	VkResult vr = nrvk.QueueSubmit(nr_device.graphics_queue, 1, &si, nr_swapchain.in_flight[frame]);
	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_SUBMIT_FAILED, (u32)vr);

	VkPresentInfoKHR pi;
	memset(&pi, 0, sizeof(pi));
	pi.sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR;
	pi.waitSemaphoreCount = 1;
	pi.pWaitSemaphores = &nr_swapchain.render_finished[frame];
	pi.swapchainCount = 1;
	pi.pSwapchains = &nr_swapchain.handle;
	pi.pImageIndices = &nr_swapchain.current_image;

	vr = nrvk.QueuePresentKHR(nr_device.present_queue, &pi);

	nr_swapchain.current_frame = (frame + 1) % NR_MAX_FRAMES_IN_FLIGHT;

	if (vr == VK_ERROR_OUT_OF_DATE_KHR || vr == VK_SUBOPTIMAL_KHR)
	{
		nr_swapchain.needs_rebuild = TRUE;
		return NRR_MakeWarning(NRR_STEP_NR_Render, NRR_CODE_SWAPCHAIN_OUT_OF_DATE, (u32)vr);
	}
	if (vr != VK_SUCCESS)
		return NRR_MakeFailure(NRR_STEP_NR_Render, NRR_CODE_PRESENT_FAILED, (u32)vr);

	return NRR_MakeSuccess(NRR_STEP_NR_Render, NRR_CODE_SUCCESS);
}

VkCommandBuffer nrSwapchainCurrentCmd(void)
{
	if (!nr_swapchain.created) return VK_NULL_HANDLE;
	return nr_swapchain.cmd_buffers[nr_swapchain.current_frame];
}
