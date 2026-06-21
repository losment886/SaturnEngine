#include "NRShaderCompiler.h"
#include "NRVulkanUtils.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// ============================================================
// NRShaderCompiler.c
// 着色器编译实现
// 尝试调用外部编译器 (dxc.exe) 将 HLSL 编译为 SPIR-V
// ============================================================

// ============================================================
// 尝试使用 dxc.exe 编译 HLSL 到 SPIR-V
// dxc 是 Microsoft 的 DirectX Shader Compiler，支持 -spirv 输出
// ============================================================
static NRResult nrTryCompileWithDXC(const char* hlslSource,
                                     const char* entryPoint,
                                     const char* shaderStage,
                                     u32** outSPIRV,
                                     u32* outSPIRVSize)
{
    // 检查 dxc.exe 是否可用
    // 在 Windows 上，dxc 可能位于 Windows SDK 中
    // 或者在 Vulkan SDK 的 Bin 目录中
    
    // 创建临时文件来保存 HLSL 源码
    char tempHLSLPath[MAX_PATH];
    char tempSPIRVPath[MAX_PATH];
    char tempDir[MAX_PATH];
    
    // 获取临时目录
    DWORD tempDirLen = GetTempPathA(MAX_PATH, tempDir);
    if (tempDirLen == 0 || tempDirLen > MAX_PATH) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, GetLastError());
    }
    
    // 生成唯一文件名
    sprintf_s(tempHLSLPath, MAX_PATH, "%sNRShader_%u.hlsl", tempDir, (u32)GetCurrentProcessId());
    sprintf_s(tempSPIRVPath, MAX_PATH, "%sNRShader_%u.spv", tempDir, (u32)GetCurrentProcessId());
    
    // 写入 HLSL 源码到临时文件
    FILE* fp = NULL;
    fopen_s(&fp, tempHLSLPath, "w");
    if (!fp) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    fprintf_s(fp, "%s", hlslSource);
    fclose(fp);
    
    // 构建 dxc 命令行
    // dxc -T <profile> -E <entry> -spirv -fspv-target-env=vulkan1.2 -Fo <output> <input>
    char profile[16];
    sprintf_s(profile, sizeof(profile), "%s_6_0", shaderStage); // vs_6_0, ps_6_0
    
    char command[1024];
    sprintf_s(command, sizeof(command),
        "dxc.exe -T %s -E %s -spirv -fspv-target-env=vulkan1.2 -Fo \"%s\" \"%s\" 2>nul",
        profile, entryPoint, tempSPIRVPath, tempHLSLPath);
    
    // 执行编译
    int exitCode = system(command);
    
    // 清理临时 HLSL 文件
    remove(tempHLSLPath);
    
    if (exitCode != 0) {
        // dxc 不可用或编译失败
        remove(tempSPIRVPath);
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, (u32)exitCode);
    }
    
    // 读取编译后的 SPIR-V 文件
    fp = NULL;
    fopen_s(&fp, tempSPIRVPath, "rb");
    if (!fp) {
        remove(tempSPIRVPath);
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    
    // 获取文件大小
    fseek(fp, 0, SEEK_END);
    long fileSize = ftell(fp);
    fseek(fp, 0, SEEK_SET);
    
    if (fileSize <= 0 || (fileSize % 4) != 0) {
        fclose(fp);
        remove(tempSPIRVPath);
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    
    // 分配内存并读取
    u32* spirvData = (u32*)malloc((size_t)fileSize);
    if (!spirvData) {
        fclose(fp);
        remove(tempSPIRVPath);
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_OUT_OF_MEMORY, 0);
    }
    
    size_t bytesRead = fread(spirvData, 1, (size_t)fileSize, fp);
    fclose(fp);
    remove(tempSPIRVPath);
    
    if (bytesRead != (size_t)fileSize) {
        free(spirvData);
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    
    *outSPIRV = spirvData;
    *outSPIRVSize = (u32)(fileSize / 4); // SPIR-V 大小以 u32 为单位
    
    return NRR_MakeSuccess(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SUCCESS);
}

// ============================================================
// 尝试使用 glslangValidator 编译 HLSL 到 SPIR-V
// glslangValidator 支持 -V 参数编译 HLSL
// ============================================================
static NRResult nrTryCompileWithGlslang(const char* hlslSource,
                                         const char* entryPoint,
                                         const char* shaderStage,
                                         u32** outSPIRV,
                                         u32* outSPIRVSize)
{
    (void)entryPoint;
    
    // 创建临时文件
    char tempHLSLPath[MAX_PATH];
    char tempSPIRVPath[MAX_PATH];
    char tempDir[MAX_PATH];
    
    DWORD tempDirLen = GetTempPathA(MAX_PATH, tempDir);
    if (tempDirLen == 0 || tempDirLen > MAX_PATH) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, GetLastError());
    }
    
    sprintf_s(tempHLSLPath, MAX_PATH, "%sNRShader_%u.%s", tempDir, (u32)GetCurrentProcessId(), shaderStage);
    sprintf_s(tempSPIRVPath, MAX_PATH, "%sNRShader_%u.spv", tempDir, (u32)GetCurrentProcessId());
    
    // 写入 HLSL 源码
    FILE* fp = NULL;
    fopen_s(&fp, tempHLSLPath, "w");
    if (!fp) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    fprintf_s(fp, "%s", hlslSource);
    fclose(fp);
    
    // glslangValidator -V -o <output> <input>
    char command[1024];
    sprintf_s(command, sizeof(command),
        "glslangValidator.exe -V -o \"%s\" \"%s\" 2>nul",
        tempSPIRVPath, tempHLSLPath);
    
    int exitCode = system(command);
    remove(tempHLSLPath);
    
    if (exitCode != 0) {
        remove(tempSPIRVPath);
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, (u32)exitCode);
    }
    
    // 读取 SPIR-V 文件
    fp = NULL;
    fopen_s(&fp, tempSPIRVPath, "rb");
    if (!fp) {
        remove(tempSPIRVPath);
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    
    fseek(fp, 0, SEEK_END);
    long fileSize = ftell(fp);
    fseek(fp, 0, SEEK_SET);
    
    if (fileSize <= 0 || (fileSize % 4) != 0) {
        fclose(fp);
        remove(tempSPIRVPath);
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    
    u32* spirvData = (u32*)malloc((size_t)fileSize);
    if (!spirvData) {
        fclose(fp);
        remove(tempSPIRVPath);
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_OUT_OF_MEMORY, 0);
    }
    
    size_t bytesRead = fread(spirvData, 1, (size_t)fileSize, fp);
    fclose(fp);
    remove(tempSPIRVPath);
    
    if (bytesRead != (size_t)fileSize) {
        free(spirvData);
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    
    *outSPIRV = spirvData;
    *outSPIRVSize = (u32)(fileSize / 4);
    
    return NRR_MakeSuccess(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SUCCESS);
}

// ============================================================
// 编译 HLSL 到 SPIR-V
// 依次尝试 dxc.exe 和 glslangValidator.exe
// ============================================================
NRResult nrCompileHLSLToSPIRV(const char* hlslSource,
                               const char* entryPoint,
                               const char* shaderStage,
                               u32** outSPIRV,
                               u32* outSPIRVSize)
{
    if (!hlslSource || !entryPoint || !shaderStage || !outSPIRV || !outSPIRVSize) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_INVALID_PARAMETER, 0);
    }
    
    *outSPIRV = NULL;
    *outSPIRVSize = 0;
    
    // 首先尝试 dxc.exe（首选，HLSL 原生编译器）
    NRResult result = nrTryCompileWithDXC(hlslSource, entryPoint, shaderStage, outSPIRV, outSPIRVSize);
    if (NRR_SUCCESS(result) && *outSPIRV != NULL) {
        return result;
    }
    
    // 回退到 glslangValidator
    result = nrTryCompileWithGlslang(hlslSource, entryPoint, shaderStage, outSPIRV, outSPIRVSize);
    if (NRR_SUCCESS(result) && *outSPIRV != NULL) {
        return result;
    }
    
    return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
}

// ============================================================
// 从 HLSL 源码直接创建 Vulkan ShaderModule
// ============================================================
NRResult nrVkCreateShaderFromHLSL(const char* hlslSource,
                                   const char* entryPoint,
                                   const char* shaderStage,
                                   VkShaderModule* outShaderModule)
{
    if (!hlslSource || !entryPoint || !shaderStage || !outShaderModule) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_INVALID_PARAMETER, 0);
    }
    
    *outShaderModule = VK_NULL_HANDLE;
    
    // 编译 HLSL 到 SPIR-V
    u32* spirvCode = NULL;
    u32 spirvSize = 0;
    
    NRResult result = nrCompileHLSLToSPIRV(hlslSource, entryPoint, shaderStage, &spirvCode, &spirvSize);
    if (NRR_FAILED(result)) {
        return result;
    }
    
    // 创建 ShaderModule
    result = nrVkCreateShaderModule(spirvCode, spirvSize * sizeof(u32), outShaderModule);
    
    // 释放 SPIR-V 数据
    nrFreeSPIRV(spirvCode);
    
    return result;
}

// ============================================================
// 释放 SPIR-V 二进制数据
// ============================================================
void nrFreeSPIRV(u32* spirv)
{
    free(spirv);
}
