#include "NRDefine.h"
#include "NRVulkanUtils.h"
#include "NRShaderSource.h"
#include "NRShaderCompiler.h"

// ============================================================
// NRVulkanShader.c
// Shader 模块管理：从 SPIR-V 二进制创建 ShaderModule
// ============================================================

// ============================================================
// 从 SPIR-V 二进制数据创建 ShaderModule
// ============================================================
NRResult nrVkCreateShaderModule(const u32* code, u32 codeSize, VkShaderModule* outShaderModule)
{
    if (!code || !outShaderModule || codeSize == 0) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_INVALID_PARAMETER, 0);
    }

    VkShaderModuleCreateInfo shaderCI = {0};
    shaderCI.sType = VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO;
    shaderCI.codeSize = codeSize;
    shaderCI.pCode = code;

    VkResult result = vkCreateShaderModule(nr_vk_state.device, &shaderCI, NULL, outShaderModule);
    VK_CHECK(result, NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED);

    return NRR_MakeSuccess(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SUCCESS);
}

// ============================================================
// 销毁 ShaderModule
// ============================================================
void nrVkDestroyShaderModule(VkShaderModule shaderModule)
{
    if (shaderModule != VK_NULL_HANDLE && nr_vk_state.device != VK_NULL_HANDLE) {
        vkDestroyShaderModule(nr_vk_state.device, shaderModule, NULL);
    }
}

// ============================================================
// 创建顶点着色器阶段信息（便捷函数）
// ============================================================
VkPipelineShaderStageCreateInfo nrVkCreateVertexShaderStage(VkShaderModule shaderModule)
{
    VkPipelineShaderStageCreateInfo stage = {0};
    stage.sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
    stage.stage = VK_SHADER_STAGE_VERTEX_BIT;
    stage.module = shaderModule;
    stage.pName = "main";
    return stage;
}

// ============================================================
// 创建片段着色器阶段信息（便捷函数）
// ============================================================
VkPipelineShaderStageCreateInfo nrVkCreateFragmentShaderStage(VkShaderModule shaderModule)
{
    VkPipelineShaderStageCreateInfo stage = {0};
    stage.sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
    stage.stage = VK_SHADER_STAGE_FRAGMENT_BIT;
    stage.module = shaderModule;
    stage.pName = "main";
    return stage;
}

// ============================================================
// 从 HLSL 源码创建顶点和片段 Shader Modules
// ============================================================
NRResult nrVkCreateShaderModules(VkShaderModule* outVertModule, VkShaderModule* outFragModule)
{
    if (!outVertModule || !outFragModule) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_INVALID_PARAMETER, 0);
    }

    // 编译顶点着色器
    u32* vertSPIRV = NULL;
    u32 vertSPIRVSize = 0;
    NRResult result = nrCompileHLSLToSPIRV(g_VSShaderCode, "main", "vs", &vertSPIRV, &vertSPIRVSize);
    if (NRR_FAILED(result)) {
        // 如果编译失败，尝试使用内置 SPIR-V
        // 这里我们使用预编译的 SPIR-V（内嵌在 NRShaderSource.h 中）
        if (g_VSShaderSPIRV != NULL && g_VSShaderSPIRVSize > 0) {
            result = nrVkCreateShaderModule(g_VSShaderSPIRV, g_VSShaderSPIRVSize * sizeof(u32), outVertModule);
            if (NRR_FAILED(result)) return result;
        } else {
            return result;
        }
    } else {
        result = nrVkCreateShaderModule(vertSPIRV, vertSPIRVSize * sizeof(u32), outVertModule);
        nrFreeSPIRV(vertSPIRV);
        if (NRR_FAILED(result)) return result;
    }

    // 编译片段着色器
    u32* fragSPIRV = NULL;
    u32 fragSPIRVSize = 0;
    result = nrCompileHLSLToSPIRV(g_PSShaderCode, "main", "ps", &fragSPIRV, &fragSPIRVSize);
    if (NRR_FAILED(result)) {
        // 使用预编译 SPIR-V
        if (g_PSShaderSPIRV != NULL && g_PSShaderSPIRVSize > 0) {
            result = nrVkCreateShaderModule(g_PSShaderSPIRV, g_PSShaderSPIRVSize * sizeof(u32), outFragModule);
            if (NRR_FAILED(result)) {
                vkDestroyShaderModule(nr_vk_state.device, *outVertModule, NULL);
                *outVertModule = VK_NULL_HANDLE;
                return result;
            }
        } else {
            vkDestroyShaderModule(nr_vk_state.device, *outVertModule, NULL);
            *outVertModule = VK_NULL_HANDLE;
            return result;
        }
    } else {
        result = nrVkCreateShaderModule(fragSPIRV, fragSPIRVSize * sizeof(u32), outFragModule);
        nrFreeSPIRV(fragSPIRV);
        if (NRR_FAILED(result)) {
            vkDestroyShaderModule(nr_vk_state.device, *outVertModule, NULL);
            *outVertModule = VK_NULL_HANDLE;
            return result;
        }
    }

    return NRR_MakeSuccess(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SUCCESS);
}
