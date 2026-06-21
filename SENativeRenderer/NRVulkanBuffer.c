#include "NRDefine.h"
#include "NRVulkanUtils.h"

// ============================================================
// NRVulkanBuffer.c
// Buffer 与设备内存管理
// ============================================================

// ============================================================
// 创建 Buffer
// ============================================================
NRResult nrVkCreateBuffer(VkDeviceSize size, VkBufferUsageFlags usage,
                           VkMemoryPropertyFlags properties,
                           VkBuffer* outBuffer, VkDeviceMemory* outMemory)
{
    if (!outBuffer || !outMemory || size == 0) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateBuffer, NRR_CODE_INVALID_PARAMETER, 0);
    }

    // 创建 Buffer
    VkBufferCreateInfo bufferCI = {0};
    bufferCI.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
    bufferCI.size = size;
    bufferCI.usage = usage;
    bufferCI.sharingMode = VK_SHARING_MODE_EXCLUSIVE;

    VkResult result = vkCreateBuffer(nr_vk_state.device, &bufferCI, NULL, outBuffer);
    VK_CHECK(result, NRR_STEP_VK_CreateBuffer, NRR_CODE_BUFFER_CREATION_FAILED);

    // 获取内存需求
    VkMemoryRequirements memRequirements;
    vkGetBufferMemoryRequirements(nr_vk_state.device, *outBuffer, &memRequirements);

    // 分配内存
    VkMemoryAllocateInfo allocInfo = {0};
    allocInfo.sType = VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
    allocInfo.allocationSize = memRequirements.size;

    if (!nrVkFindMemoryType(nr_vk_state.physicalDevice, memRequirements.memoryTypeBits,
                             properties, &allocInfo.memoryTypeIndex)) {
        vkDestroyBuffer(nr_vk_state.device, *outBuffer, NULL);
        *outBuffer = VK_NULL_HANDLE;
        return NRR_MakeFailure(NRR_STEP_VK_CreateBuffer, NRR_CODE_OUT_OF_MEMORY, 0);
    }

    result = vkAllocateMemory(nr_vk_state.device, &allocInfo, NULL, outMemory);
    if (result != VK_SUCCESS) {
        vkDestroyBuffer(nr_vk_state.device, *outBuffer, NULL);
        *outBuffer = VK_NULL_HANDLE;
        VK_CHECK(result, NRR_STEP_VK_CreateBuffer, NRR_CODE_OUT_OF_MEMORY);
    }

    // 绑定内存
    vkBindBufferMemory(nr_vk_state.device, *outBuffer, *outMemory, 0);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateBuffer, NRR_CODE_SUCCESS);
}

// ============================================================
// 拷贝 Buffer 数据（从暂存 Buffer 到设备本地 Buffer）
// ============================================================
NRResult nrVkCopyBuffer(VkBuffer srcBuffer, VkBuffer dstBuffer, VkDeviceSize size,
                         VkCommandPool commandPool, VkQueue queue)
{
    if (srcBuffer == VK_NULL_HANDLE || dstBuffer == VK_NULL_HANDLE || size == 0) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateBuffer, NRR_CODE_INVALID_PARAMETER, 0);
    }

    // 创建临时命令缓冲
    VkCommandBufferAllocateInfo allocInfo = {0};
    allocInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
    allocInfo.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
    allocInfo.commandPool = commandPool;
    allocInfo.commandBufferCount = 1;

    VkCommandBuffer cmdBuffer;
    VkResult result = vkAllocateCommandBuffers(nr_vk_state.device, &allocInfo, &cmdBuffer);
    VK_CHECK(result, NRR_STEP_VK_CreateBuffer, NRR_CODE_COMMAND_BUFFER_FAILED);

    // 录制命令
    VkCommandBufferBeginInfo beginInfo = {0};
    beginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
    beginInfo.flags = VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;

    vkBeginCommandBuffer(cmdBuffer, &beginInfo);

    VkBufferCopy copyRegion = {0};
    copyRegion.srcOffset = 0;
    copyRegion.dstOffset = 0;
    copyRegion.size = size;
    vkCmdCopyBuffer(cmdBuffer, srcBuffer, dstBuffer, 1, &copyRegion);

    vkEndCommandBuffer(cmdBuffer);

    // 提交
    VkSubmitInfo submitInfo = {0};
    submitInfo.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = &cmdBuffer;

    vkQueueSubmit(queue, 1, &submitInfo, VK_NULL_HANDLE);
    vkQueueWaitIdle(queue);

    // 清理
    vkFreeCommandBuffers(nr_vk_state.device, commandPool, 1, &cmdBuffer);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateBuffer, NRR_CODE_SUCCESS);
}

// ============================================================
// 将数据映射到 Host Visible 内存
// ============================================================
NRResult nrVkMapMemory(VkDeviceMemory memory, VkDeviceSize offset, VkDeviceSize size,
                        void** outData)
{
    if (memory == VK_NULL_HANDLE || !outData) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateBuffer, NRR_CODE_INVALID_PARAMETER, 0);
    }

    VkResult result = vkMapMemory(nr_vk_state.device, memory, offset, size, 0, outData);
    VK_CHECK(result, NRR_STEP_VK_CreateBuffer, NRR_CODE_OUT_OF_MEMORY);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateBuffer, NRR_CODE_SUCCESS);
}

// ============================================================
// 取消映射内存
// ============================================================
void nrVkUnmapMemory(VkDeviceMemory memory)
{
    if (memory != VK_NULL_HANDLE) {
        vkUnmapMemory(nr_vk_state.device, memory);
    }
}

// ============================================================
// 销毁 Buffer 和内存
// ============================================================
void nrVkDestroyBuffer(VkBuffer buffer, VkDeviceMemory memory)
{
    if (nr_vk_state.device == VK_NULL_HANDLE) return;

    if (buffer != VK_NULL_HANDLE) {
        vkDestroyBuffer(nr_vk_state.device, buffer, NULL);
    }
    if (memory != VK_NULL_HANDLE) {
        vkFreeMemory(nr_vk_state.device, memory, NULL);
    }
}

// ============================================================
// 创建顶点 Buffer（从 g_CubeVertices 填充）
// ============================================================
NRResult nrVkCreateVertexBuffer(void)
{
    VkDeviceSize bufferSize = sizeof(g_CubeVertices);

    // 创建暂存 Buffer（Host Visible）
    VkBuffer stagingBuffer;
    VkDeviceMemory stagingMemory;
    NRResult result = nrVkCreateBuffer(bufferSize,
        VK_BUFFER_USAGE_TRANSFER_SRC_BIT,
        VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT,
        &stagingBuffer, &stagingMemory);
    if (NRR_FAILED(result)) return result;

    // 拷贝顶点数据到暂存 Buffer
    void* data;
    result = nrVkMapMemory(stagingMemory, 0, bufferSize, &data);
    if (NRR_FAILED(result)) {
        nrVkDestroyBuffer(stagingBuffer, stagingMemory);
        return result;
    }
    memcpy(data, g_CubeVertices, (size_t)bufferSize);
    nrVkUnmapMemory(stagingMemory);

    // 创建设备本地 Buffer
    result = nrVkCreateBuffer(bufferSize,
        VK_BUFFER_USAGE_TRANSFER_DST_BIT | VK_BUFFER_USAGE_VERTEX_BUFFER_BIT,
        VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT,
        &nr_vk_state.vertexBuffer, &nr_vk_state.vertexMemory);
    if (NRR_FAILED(result)) {
        nrVkDestroyBuffer(stagingBuffer, stagingMemory);
        return result;
    }

    // 从暂存 Buffer 拷贝到设备本地 Buffer
    result = nrVkCopyBuffer(stagingBuffer, nr_vk_state.vertexBuffer, bufferSize,
                             nr_vk_state.commandPool, nr_vk_state.graphicsQueue);

    // 销毁暂存 Buffer
    nrVkDestroyBuffer(stagingBuffer, stagingMemory);

    return result;
}

// ============================================================
// 创建索引 Buffer（从 g_CubeIndices 填充）
// ============================================================
NRResult nrVkCreateIndexBuffer(void)
{
    VkDeviceSize bufferSize = sizeof(g_CubeIndices);

    // 创建暂存 Buffer
    VkBuffer stagingBuffer;
    VkDeviceMemory stagingMemory;
    NRResult result = nrVkCreateBuffer(bufferSize,
        VK_BUFFER_USAGE_TRANSFER_SRC_BIT,
        VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT,
        &stagingBuffer, &stagingMemory);
    if (NRR_FAILED(result)) return result;

    // 拷贝索引数据
    void* data;
    result = nrVkMapMemory(stagingMemory, 0, bufferSize, &data);
    if (NRR_FAILED(result)) {
        nrVkDestroyBuffer(stagingBuffer, stagingMemory);
        return result;
    }
    memcpy(data, g_CubeIndices, (size_t)bufferSize);
    nrVkUnmapMemory(stagingMemory);

    // 创建设备本地 Buffer
    result = nrVkCreateBuffer(bufferSize,
        VK_BUFFER_USAGE_TRANSFER_DST_BIT | VK_BUFFER_USAGE_INDEX_BUFFER_BIT,
        VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT,
        &nr_vk_state.indexBuffer, &nr_vk_state.indexMemory);
    if (NRR_FAILED(result)) {
        nrVkDestroyBuffer(stagingBuffer, stagingMemory);
        return result;
    }

    // 拷贝
    result = nrVkCopyBuffer(stagingBuffer, nr_vk_state.indexBuffer, bufferSize,
                             nr_vk_state.commandPool, nr_vk_state.graphicsQueue);

    nrVkDestroyBuffer(stagingBuffer, stagingMemory);
    return result;
}

// ============================================================
// 创建 Uniform Buffer（Host Visible，用于 MVP 矩阵）
// ============================================================
NRResult nrVkCreateUniformBuffer(void)
{
    VkDeviceSize bufferSize = sizeof(MVPBuffer);

    NRResult result = nrVkCreateBuffer(bufferSize,
        VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT,
        VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT,
        &nr_vk_state.uniformBuffer, &nr_vk_state.uniformMemory);
    if (NRR_FAILED(result)) return result;

    // 持久映射
    result = nrVkMapMemory(nr_vk_state.uniformMemory, 0, bufferSize, &nr_vk_state.uniformMappedData);
    if (NRR_FAILED(result)) {
        nrVkDestroyBuffer(nr_vk_state.uniformBuffer, nr_vk_state.uniformMemory);
        nr_vk_state.uniformBuffer = VK_NULL_HANDLE;
        nr_vk_state.uniformMemory = VK_NULL_HANDLE;
        return result;
    }

    return NRR_MakeSuccess(NRR_STEP_VK_CreateBuffer, NRR_CODE_SUCCESS);
}

// ============================================================
// 创建 Descriptor Set Layout
// ============================================================
NRResult nrVkCreateDescriptorSetLayout(void)
{
    VkDescriptorSetLayoutBinding uboLayoutBinding = {0};
    uboLayoutBinding.binding = 0;
    uboLayoutBinding.descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
    uboLayoutBinding.descriptorCount = 1;
    uboLayoutBinding.stageFlags = VK_SHADER_STAGE_VERTEX_BIT;
    uboLayoutBinding.pImmutableSamplers = NULL;

    VkDescriptorSetLayoutCreateInfo layoutCI = {0};
    layoutCI.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
    layoutCI.bindingCount = 1;
    layoutCI.pBindings = &uboLayoutBinding;

    VkResult result = vkCreateDescriptorSetLayout(nr_vk_state.device, &layoutCI, NULL,
                                                   &nr_vk_state.descriptorSetLayout);
    VK_CHECK(result, NRR_STEP_VK_CreateBuffer, NRR_CODE_DESCRIPTOR_SET_FAILED);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateBuffer, NRR_CODE_SUCCESS);
}

// ============================================================
// 创建 Descriptor Pool 和 Descriptor Set
// ============================================================
NRResult nrVkCreateDescriptorSet(void)
{
    // 创建 Descriptor Pool
    VkDescriptorPoolSize poolSize = {0};
    poolSize.type = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
    poolSize.descriptorCount = 1;

    VkDescriptorPoolCreateInfo poolCI = {0};
    poolCI.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
    poolCI.poolSizeCount = 1;
    poolCI.pPoolSizes = &poolSize;
    poolCI.maxSets = 1;

    VkResult result = vkCreateDescriptorPool(nr_vk_state.device, &poolCI, NULL,
                                              &nr_vk_state.descriptorPool);
    VK_CHECK(result, NRR_STEP_VK_CreateBuffer, NRR_CODE_DESCRIPTOR_SET_FAILED);

    // 分配 Descriptor Set
    VkDescriptorSetAllocateInfo allocInfo = {0};
    allocInfo.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO;
    allocInfo.descriptorPool = nr_vk_state.descriptorPool;
    allocInfo.descriptorSetCount = 1;
    allocInfo.pSetLayouts = &nr_vk_state.descriptorSetLayout;

    result = vkAllocateDescriptorSets(nr_vk_state.device, &allocInfo, &nr_vk_state.descriptorSet);
    VK_CHECK(result, NRR_STEP_VK_CreateBuffer, NRR_CODE_DESCRIPTOR_SET_FAILED);

    // 更新 Descriptor Set
    VkDescriptorBufferInfo bufferInfo = {0};
    bufferInfo.buffer = nr_vk_state.uniformBuffer;
    bufferInfo.offset = 0;
    bufferInfo.range = sizeof(MVPBuffer);

    VkWriteDescriptorSet descriptorWrite = {0};
    descriptorWrite.sType = VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
    descriptorWrite.dstSet = nr_vk_state.descriptorSet;
    descriptorWrite.dstBinding = 0;
    descriptorWrite.dstArrayElement = 0;
    descriptorWrite.descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
    descriptorWrite.descriptorCount = 1;
    descriptorWrite.pBufferInfo = &bufferInfo;

    vkUpdateDescriptorSets(nr_vk_state.device, 1, &descriptorWrite, 0, NULL);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateBuffer, NRR_CODE_SUCCESS);
}

// ============================================================
// 更新 Uniform Buffer 数据（MVP 矩阵）
// ============================================================
NRResult nrVkUpdateUniformBuffer(void)
{
    if (!nr_vk_state.uniformMappedData) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateBuffer, NRR_CODE_INVALID_PARAMETER, 0);
    }

    // 计算 MVP 矩阵
    // 模型矩阵：绕 Y 轴旋转（让立方体旋转起来）
    f32 angle = (f32)nr_vk_state.currentFrame * 0.01f; // 随时间旋转
    Matrix4x4 model = Matrix4x4_RotateY(angle);

    // 观察矩阵：相机在 (2, 2, 2) 看向原点
    Vector3D eye = {2.0f, 2.0f, 2.0f};
    Vector3D center = {0.0f, 0.0f, 0.0f};
    Vector3D up = {0.0f, 1.0f, 0.0f};
    Matrix4x4 view = Matrix4x4_LookAt(eye, center, up);

    // 投影矩阵：透视投影
    f32 aspect = (f32)nr_vk_state.windowWidth / (f32)nr_vk_state.windowHeight;
    Matrix4x4 proj = Matrix4x4_Perspective(45.0f * 3.14159265f / 180.0f, aspect, 0.1f, 100.0f);

    // MVP = proj * view * model
    Matrix4x4 mvp = Matrix4x4_Mul(proj, Matrix4x4_Mul(view, model));

    // 拷贝到 Uniform Buffer
    memcpy(nr_vk_state.uniformMappedData, &mvp, sizeof(MVPBuffer));

    return NRR_MakeSuccess(NRR_STEP_VK_CreateBuffer, NRR_CODE_SUCCESS);
}

// ============================================================
// 销毁所有渲染资源
// ============================================================
void nrVkDestroyRenderResources(void)
{
    // 销毁 Shader Modules
    if (nr_vk_state.vertShaderModule != VK_NULL_HANDLE) {
        vkDestroyShaderModule(nr_vk_state.device, nr_vk_state.vertShaderModule, NULL);
        nr_vk_state.vertShaderModule = VK_NULL_HANDLE;
    }
    if (nr_vk_state.fragShaderModule != VK_NULL_HANDLE) {
        vkDestroyShaderModule(nr_vk_state.device, nr_vk_state.fragShaderModule, NULL);
        nr_vk_state.fragShaderModule = VK_NULL_HANDLE;
    }

    // 销毁 Descriptor Pool
    if (nr_vk_state.descriptorPool != VK_NULL_HANDLE) {
        vkDestroyDescriptorPool(nr_vk_state.device, nr_vk_state.descriptorPool, NULL);
        nr_vk_state.descriptorPool = VK_NULL_HANDLE;
    }

    // 销毁 Descriptor Set Layout
    if (nr_vk_state.descriptorSetLayout != VK_NULL_HANDLE) {
        vkDestroyDescriptorSetLayout(nr_vk_state.device, nr_vk_state.descriptorSetLayout, NULL);
        nr_vk_state.descriptorSetLayout = VK_NULL_HANDLE;
    }

    // 销毁 Uniform Buffer
    if (nr_vk_state.uniformMappedData) {
        nrVkUnmapMemory(nr_vk_state.uniformMemory);
        nr_vk_state.uniformMappedData = NULL;
    }
    nrVkDestroyBuffer(nr_vk_state.uniformBuffer, nr_vk_state.uniformMemory);
    nr_vk_state.uniformBuffer = VK_NULL_HANDLE;
    nr_vk_state.uniformMemory = VK_NULL_HANDLE;

    // 销毁索引 Buffer
    nrVkDestroyBuffer(nr_vk_state.indexBuffer, nr_vk_state.indexMemory);
    nr_vk_state.indexBuffer = VK_NULL_HANDLE;
    nr_vk_state.indexMemory = VK_NULL_HANDLE;

    // 销毁顶点 Buffer
    nrVkDestroyBuffer(nr_vk_state.vertexBuffer, nr_vk_state.vertexMemory);
    nr_vk_state.vertexBuffer = VK_NULL_HANDLE;
    nr_vk_state.vertexMemory = VK_NULL_HANDLE;

    // 销毁深度资源
    nrVkDestroyDepthResources();
}
