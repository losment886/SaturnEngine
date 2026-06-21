#include "NRDefine.h"
#include "NRVulkanUtils.h"

// ============================================================
// NRVulkanRenderPass.c
// RenderPass 管理：传统 RenderPass + Framebuffer
// 当 useDynamicRendering 为 TRUE 时，不需要创建传统 RenderPass
// ============================================================

// ============================================================
// 创建传统 RenderPass（当 useDynamicRendering = FALSE 时使用）
// 包含颜色附件和深度附件
// ============================================================
NRResult nrVkCreateRenderPass(void)
{
    if (nr_vk_state.useDynamicRendering) {
        // Dynamic Rendering 不需要传统 RenderPass
        nr_vk_state.renderPass = VK_NULL_HANDLE;
        return NRR_MakeSuccess(NRR_STEP_VK_CreateRenderPass, NRR_CODE_SUCCESS);
    }

    VkFormat depthFormat = VK_FORMAT_D32_SFLOAT;

    // 颜色附件
    VkAttachmentDescription colorAttachment = {0};
    colorAttachment.format = nr_vk_state.swapchainInfo.imageFormat;
    colorAttachment.samples = VK_SAMPLE_COUNT_1_BIT;
    colorAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
    colorAttachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
    colorAttachment.stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE;
    colorAttachment.stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
    colorAttachment.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
    colorAttachment.finalLayout = VK_IMAGE_LAYOUT_PRESENT_SRC_KHR;

    // 深度附件
    VkAttachmentDescription depthAttachment = {0};
    depthAttachment.format = depthFormat;
    depthAttachment.samples = VK_SAMPLE_COUNT_1_BIT;
    depthAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
    depthAttachment.storeOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
    depthAttachment.stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE;
    depthAttachment.stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
    depthAttachment.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
    depthAttachment.finalLayout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;

    VkAttachmentReference colorAttachmentRef = {0};
    colorAttachmentRef.attachment = 0;
    colorAttachmentRef.layout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;

    VkAttachmentReference depthAttachmentRef = {0};
    depthAttachmentRef.attachment = 1;
    depthAttachmentRef.layout = VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL;

    VkSubpassDescription subpass = {0};
    subpass.pipelineBindPoint = VK_PIPELINE_BIND_POINT_GRAPHICS;
    subpass.colorAttachmentCount = 1;
    subpass.pColorAttachments = &colorAttachmentRef;
    subpass.pDepthStencilAttachment = &depthAttachmentRef;

    VkSubpassDependency dependency = {0};
    dependency.srcSubpass = VK_SUBPASS_EXTERNAL;
    dependency.dstSubpass = 0;
    dependency.srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT | VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT;
    dependency.srcAccessMask = 0;
    dependency.dstStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT | VK_PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT;
    dependency.dstAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT | VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT;

    VkAttachmentDescription attachments[2] = {colorAttachment, depthAttachment};

    VkRenderPassCreateInfo renderPassCI = {0};
    renderPassCI.sType = VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO;
    renderPassCI.attachmentCount = 2;
    renderPassCI.pAttachments = attachments;
    renderPassCI.subpassCount = 1;
    renderPassCI.pSubpasses = &subpass;
    renderPassCI.dependencyCount = 1;
    renderPassCI.pDependencies = &dependency;

    VkResult result = vkCreateRenderPass(nr_vk_state.device, &renderPassCI, NULL,
                                          &nr_vk_state.renderPass);
    if (result != VK_SUCCESS) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateRenderPass, NRR_CODE_RENDERPASS_CREATION_FAILED, (u32)result);
    }

    return NRR_MakeSuccess(NRR_STEP_VK_CreateRenderPass, NRR_CODE_SUCCESS);
}

// ============================================================
// 创建 Framebuffers（当 useDynamicRendering = FALSE 时使用）
// ============================================================
NRResult nrVkCreateFramebuffers(void)
{
    if (nr_vk_state.useDynamicRendering) {
        // Dynamic Rendering 不需要 Framebuffer
        nr_vk_state.framebuffers = NULL;
        return NRR_MakeSuccess(NRR_STEP_VK_CreateRenderPass, NRR_CODE_SUCCESS);
    }

    if (nr_vk_state.renderPass == VK_NULL_HANDLE) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateRenderPass, NRR_CODE_RENDERPASS_CREATION_FAILED, 0);
    }

    u32 imageCount = nr_vk_state.swapchainInfo.imageCount;
    nr_vk_state.framebuffers = (VkFramebuffer*)malloc(sizeof(VkFramebuffer) * imageCount);
    if (!nr_vk_state.framebuffers) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateRenderPass, NRR_CODE_OUT_OF_MEMORY, 0);
    }

    for (u32 i = 0; i < imageCount; i++) {
        VkImageView attachments[2] = {
            nr_vk_state.swapchainInfo.imageViews[i],
            nr_vk_state.depthImageView
        };

        VkFramebufferCreateInfo framebufferCI = {0};
        framebufferCI.sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO;
        framebufferCI.renderPass = nr_vk_state.renderPass;
        framebufferCI.attachmentCount = 2;
        framebufferCI.pAttachments = attachments;
        framebufferCI.width = nr_vk_state.swapchainInfo.extent.width;
        framebufferCI.height = nr_vk_state.swapchainInfo.extent.height;
        framebufferCI.layers = 1;

        VkResult result = vkCreateFramebuffer(nr_vk_state.device, &framebufferCI, NULL,
                                               &nr_vk_state.framebuffers[i]);
        if (result != VK_SUCCESS) {
            return NRR_MakeFailure(NRR_STEP_VK_CreateRenderPass, NRR_CODE_FRAMEBUFFER_CREATION_FAILED, (u32)result);
        }
    }

    return NRR_MakeSuccess(NRR_STEP_VK_CreateRenderPass, NRR_CODE_SUCCESS);
}
