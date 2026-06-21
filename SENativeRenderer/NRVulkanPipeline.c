#include "NRDefine.h"
#include "NRVulkanUtils.h"

// ============================================================
// NRVulkanPipeline.c
// 图形管线管理：创建默认图形管线
// ============================================================

// ============================================================
// 创建 Pipeline Layout（带 Descriptor Set Layout）
// ============================================================
NRResult nrVkCreatePipelineLayout(VkPipelineLayout* outLayout)
{
    if (!outLayout) {
        return NRR_MakeFailure(NRR_STEP_VK_CreatePipeline, NRR_CODE_INVALID_PARAMETER, 0);
    }

    VkPipelineLayoutCreateInfo layoutCI = {0};
    layoutCI.sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
    layoutCI.setLayoutCount = 1;
    layoutCI.pSetLayouts = &nr_vk_state.descriptorSetLayout;
    layoutCI.pushConstantRangeCount = 0;
    layoutCI.pPushConstantRanges = NULL;

    VkResult result = vkCreatePipelineLayout(nr_vk_state.device, &layoutCI, NULL, outLayout);
    VK_CHECK(result, NRR_STEP_VK_CreatePipeline, NRR_CODE_PIPELINE_CREATION_FAILED);

    return NRR_MakeSuccess(NRR_STEP_VK_CreatePipeline, NRR_CODE_SUCCESS);
}

// ============================================================
// 创建默认图形管线
// 使用 Dynamic Rendering 或传统 RenderPass
// ============================================================
NRResult nrVkCreateGraphicsPipeline(VkShaderModule vertShader, VkShaderModule fragShader,
                                     VkPipelineLayout layout, VkPipeline* outPipeline)
{
    if (!outPipeline) {
        return NRR_MakeFailure(NRR_STEP_VK_CreatePipeline, NRR_CODE_INVALID_PARAMETER, 0);
    }

    // Shader 阶段
    VkPipelineShaderStageCreateInfo vertStage = nrVkCreateVertexShaderStage(vertShader);
    VkPipelineShaderStageCreateInfo fragStage = nrVkCreateFragmentShaderStage(fragShader);
    VkPipelineShaderStageCreateInfo shaderStages[] = {vertStage, fragStage};

    // 顶点输入绑定描述
    // binding 0: 每顶点 float3 位置（12 字节）
    VkVertexInputBindingDescription bindingDesc = {0};
    bindingDesc.binding = 0;
    bindingDesc.stride = sizeof(f32) * 3; // 3 floats per vertex
    bindingDesc.inputRate = VK_VERTEX_INPUT_RATE_VERTEX;

    // 顶点属性描述
    VkVertexInputAttributeDescription attributeDesc = {0};
    attributeDesc.location = 0; // HLSL 中的 POSITION
    attributeDesc.binding = 0;
    attributeDesc.format = VK_FORMAT_R32G32B32_SFLOAT; // float3
    attributeDesc.offset = 0;

    VkPipelineVertexInputStateCreateInfo vertexInput = {0};
    vertexInput.sType = VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO;
    vertexInput.vertexBindingDescriptionCount = 1;
    vertexInput.pVertexBindingDescriptions = &bindingDesc;
    vertexInput.vertexAttributeDescriptionCount = 1;
    vertexInput.pVertexAttributeDescriptions = &attributeDesc;

    // 输入装配
    VkPipelineInputAssemblyStateCreateInfo inputAssembly = {0};
    inputAssembly.sType = VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO;
    inputAssembly.topology = VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
    inputAssembly.primitiveRestartEnable = VK_FALSE;

    // 视口（动态状态）
    VkPipelineViewportStateCreateInfo viewportState = {0};
    viewportState.sType = VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO;
    viewportState.viewportCount = 1;
    viewportState.scissorCount = 1;

    // 光栅化
    VkPipelineRasterizationStateCreateInfo rasterizer = {0};
    rasterizer.sType = VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO;
    rasterizer.depthClampEnable = VK_FALSE;
    rasterizer.rasterizerDiscardEnable = VK_FALSE;
    rasterizer.polygonMode = VK_POLYGON_MODE_FILL;
    rasterizer.lineWidth = 1.0f;
    rasterizer.cullMode = VK_CULL_MODE_BACK_BIT;
    rasterizer.frontFace = VK_FRONT_FACE_CLOCKWISE;
    rasterizer.depthBiasEnable = VK_FALSE;

    // 多重采样
    VkPipelineMultisampleStateCreateInfo multisampling = {0};
    multisampling.sType = VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO;
    multisampling.sampleShadingEnable = VK_FALSE;
    multisampling.rasterizationSamples = VK_SAMPLE_COUNT_1_BIT;

    // 深度/模板（启用深度测试）
    VkPipelineDepthStencilStateCreateInfo depthStencil = {0};
    depthStencil.sType = VK_STRUCTURE_TYPE_PIPELINE_DEPTH_STENCIL_STATE_CREATE_INFO;
    depthStencil.depthTestEnable = VK_TRUE;
    depthStencil.depthWriteEnable = VK_TRUE;
    depthStencil.depthCompareOp = VK_COMPARE_OP_LESS;
    depthStencil.depthBoundsTestEnable = VK_FALSE;
    depthStencil.stencilTestEnable = VK_FALSE;

    // 颜色混合
    VkPipelineColorBlendAttachmentState colorBlendAttachment = {0};
    colorBlendAttachment.colorWriteMask = VK_COLOR_COMPONENT_R_BIT |
                                          VK_COLOR_COMPONENT_G_BIT |
                                          VK_COLOR_COMPONENT_B_BIT |
                                          VK_COLOR_COMPONENT_A_BIT;
    colorBlendAttachment.blendEnable = VK_FALSE;

    VkPipelineColorBlendStateCreateInfo colorBlending = {0};
    colorBlending.sType = VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO;
    colorBlending.logicOpEnable = VK_FALSE;
    colorBlending.logicOp = VK_LOGIC_OP_COPY;
    colorBlending.attachmentCount = 1;
    colorBlending.pAttachments = &colorBlendAttachment;
    colorBlending.blendConstants[0] = 0.0f;
    colorBlending.blendConstants[1] = 0.0f;
    colorBlending.blendConstants[2] = 0.0f;
    colorBlending.blendConstants[3] = 0.0f;

    // 动态状态（视口和裁剪矩形）
    VkDynamicState dynamicStates[] = {
        VK_DYNAMIC_STATE_VIEWPORT,
        VK_DYNAMIC_STATE_SCISSOR
    };
    VkPipelineDynamicStateCreateInfo dynamicState = {0};
    dynamicState.sType = VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO;
    dynamicState.dynamicStateCount = 2;
    dynamicState.pDynamicStates = dynamicStates;

    // 管线创建信息
    VkGraphicsPipelineCreateInfo pipelineCI = {0};
    pipelineCI.sType = VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO;
    pipelineCI.stageCount = 2;
    pipelineCI.pStages = shaderStages;
    pipelineCI.pVertexInputState = &vertexInput;
    pipelineCI.pInputAssemblyState = &inputAssembly;
    pipelineCI.pViewportState = &viewportState;
    pipelineCI.pRasterizationState = &rasterizer;
    pipelineCI.pMultisampleState = &multisampling;
    pipelineCI.pDepthStencilState = &depthStencil;
    pipelineCI.pColorBlendState = &colorBlending;
    pipelineCI.pDynamicState = &dynamicState;
    pipelineCI.layout = layout;
    pipelineCI.renderPass = nr_vk_state.renderPass;
    pipelineCI.subpass = 0;
    pipelineCI.basePipelineHandle = VK_NULL_HANDLE;
    pipelineCI.basePipelineIndex = -1;

    // 深度格式
    VkFormat depthFormat = VK_FORMAT_D32_SFLOAT;

    // 如果使用 Dynamic Rendering，通过 pNext 传递颜色附件格式和深度格式
    VkPipelineRenderingCreateInfo renderingCI = {0};
    if (nr_vk_state.useDynamicRendering) {
        renderingCI.sType = VK_STRUCTURE_TYPE_PIPELINE_RENDERING_CREATE_INFO;
        renderingCI.colorAttachmentCount = 1;
        renderingCI.pColorAttachmentFormats = &nr_vk_state.swapchainInfo.imageFormat;
        renderingCI.depthAttachmentFormat = depthFormat;
        renderingCI.stencilAttachmentFormat = VK_FORMAT_UNDEFINED;
        pipelineCI.pNext = &renderingCI;
        // Dynamic Rendering 不需要 renderPass
        pipelineCI.renderPass = VK_NULL_HANDLE;
    }

    VkResult result = vkCreateGraphicsPipelines(nr_vk_state.device, VK_NULL_HANDLE,
                                                 1, &pipelineCI, NULL, outPipeline);
    VK_CHECK(result, NRR_STEP_VK_CreatePipeline, NRR_CODE_PIPELINE_CREATION_FAILED);

    return NRR_MakeSuccess(NRR_STEP_VK_CreatePipeline, NRR_CODE_SUCCESS);
}
