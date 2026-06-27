#include "NRShaderCompiler.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>

// ============================================================
// NRShaderCompiler.c
// 着色器编译实现
// 尝试调用外部编译器 (dxc / glslangValidator) 将 HLSL 编译为 SPIR-V
// 跨平台：Windows 使用 Win32 API，macOS/Linux 使用 POSIX API
// ============================================================

// ============================================================
// 平台相关的临时文件路径辅助函数
// ============================================================

#ifdef _WIN32
// ==================== Windows 实现 ====================

#include <windows.h>

// 获取临时目录路径
static int nrGetTempDir(char* outPath, int maxLen)
{
    DWORD len = GetTempPathA((DWORD)maxLen, outPath);
    return (len > 0 && len < (DWORD)maxLen) ? 0 : -1;
}

// 生成临时文件路径
static int nrMakeTempPath(char* outPath, int maxLen, const char* dir, const char* suffix)
{
    return sprintf_s(outPath, maxLen, "%sNRShader_%u.%s", dir, (u32)GetCurrentProcessId(), suffix);
}

// 安全打开文件（写）
static FILE* nrOpenFileWrite(const char* path)
{
    FILE* fp = NULL;
    fopen_s(&fp, path, "w");
    return fp;
}

// 安全打开文件（读二进制）
static FILE* nrOpenFileReadBinary(const char* path)
{
    FILE* fp = NULL;
    fopen_s(&fp, path, "rb");
    return fp;
}

// 安全写入字符串到文件
static void nrWriteString(FILE* fp, const char* str)
{
    fprintf_s(fp, "%s", str);
}

// 安全格式化字符串
static int nrFormatString(char* out, int maxLen, const char* fmt, ...)
{
    va_list args;
    va_start(args, fmt);
    int ret = vsprintf_s(out, maxLen, fmt, args);
    va_end(args);
    return ret;
}

#else
// ==================== macOS / Linux 实现（POSIX） ====================

#include <unistd.h>
#include <time.h>

// 获取临时目录路径
static int nrGetTempDir(char* outPath, int maxLen)
{
    const char* tmp = NULL;
    // 依次尝试环境变量
    tmp = getenv("TMPDIR");
    if (!tmp) tmp = getenv("TMP");
    if (!tmp) tmp = getenv("TEMP");
    if (!tmp) tmp = "/tmp";
    
    size_t len = strlen(tmp);
    if ((int)len >= maxLen) return -1;
    
    // 确保以 '/' 结尾
    if (tmp[len - 1] == '/') {
        memcpy(outPath, tmp, len + 1);
    } else {
        memcpy(outPath, tmp, len);
        outPath[len] = '/';
        outPath[len + 1] = '\0';
    }
    return 0;
}

// 生成临时文件路径（使用进程 ID + 时间戳 + 随机数确保唯一性）
static int nrMakeTempPath(char* outPath, int maxLen, const char* dir, const char* suffix)
{
    u32 pid = (u32)getpid();
    u32 timestamp = (u32)time(NULL);
    u32 randomPart = (u32)((unsigned long)outPath ^ pid ^ timestamp) & 0xFFFF;
    return snprintf(outPath, (size_t)maxLen, "%sNRShader_%u_%u_%u.%s",
                    dir, pid, timestamp, randomPart, suffix);
}

// 打开文件（写）
static FILE* nrOpenFileWrite(const char* path)
{
    return fopen(path, "w");
}

// 打开文件（读二进制）
static FILE* nrOpenFileReadBinary(const char* path)
{
    return fopen(path, "rb");
}

// 写入字符串到文件
static void nrWriteString(FILE* fp, const char* str)
{
    fprintf(fp, "%s", str);
}

// 格式化字符串
static int nrFormatString(char* out, int maxLen, const char* fmt, ...)
{
    va_list args;
    va_start(args, fmt);
    int ret = vsnprintf(out, (size_t)maxLen, fmt, args);
    va_end(args);
    return ret;
}

#endif // _WIN32

// ============================================================
// 通用临时文件路径缓冲区大小
// ============================================================
#ifndef MAX_PATH
#define MAX_PATH 260
#endif

// ============================================================
// 尝试使用 dxc 编译 HLSL 到 SPIR-V
// dxc 是 Microsoft 的 DirectX Shader Compiler，支持 -spirv 输出
// ============================================================
static NRResult nrTryCompileWithDXC(const char* hlslSource,
                                     const char* entryPoint,
                                     const char* shaderStage,
                                     u32** outSPIRV,
                                     u32* outSPIRVSize)
{
    // 创建临时文件来保存 HLSL 源码
    char tempHLSLPath[MAX_PATH];
    char tempSPIRVPath[MAX_PATH];
    char tempDir[MAX_PATH];
    
    // 获取临时目录
    if (nrGetTempDir(tempDir, MAX_PATH) != 0) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    
    // 生成唯一文件名
    nrMakeTempPath(tempHLSLPath, MAX_PATH, tempDir, "hlsl");
    nrMakeTempPath(tempSPIRVPath, MAX_PATH, tempDir, "spv");
    
    // 写入 HLSL 源码到临时文件
    FILE* fp = nrOpenFileWrite(tempHLSLPath);
    if (!fp) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    nrWriteString(fp, hlslSource);
    fclose(fp);
    
    // 构建 dxc 命令行
    // dxc -T <profile> -E <entry> -spirv -fspv-target-env=vulkan1.2 -Fo <output> <input>
    char profile[16];
    nrFormatString(profile, sizeof(profile), "%s_6_0", shaderStage); // vs_6_0, ps_6_0
    
    char command[1024];
    nrFormatString(command, sizeof(command),
        "dxc -T %s -E %s -spirv -fspv-target-env=vulkan1.2 -Fo \"%s\" \"%s\" 2>/dev/null",
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
    fp = nrOpenFileReadBinary(tempSPIRVPath);
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
    
    if (nrGetTempDir(tempDir, MAX_PATH) != 0) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    
    nrMakeTempPath(tempHLSLPath, MAX_PATH, tempDir, shaderStage);
    nrMakeTempPath(tempSPIRVPath, MAX_PATH, tempDir, "spv");
    
    // 写入 HLSL 源码
    FILE* fp = nrOpenFileWrite(tempHLSLPath);
    if (!fp) {
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
    }
    nrWriteString(fp, hlslSource);
    fclose(fp);
    
    // glslangValidator -V -o <output> <input>
    char command[1024];
    nrFormatString(command, sizeof(command),
        "glslangValidator -V -o \"%s\" \"%s\" 2>/dev/null",
        tempSPIRVPath, tempHLSLPath);
    
    int exitCode = system(command);
    remove(tempHLSLPath);
    
    if (exitCode != 0) {
        remove(tempSPIRVPath);
        return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, (u32)exitCode);
    }
    
    // 读取 SPIR-V 文件
    fp = nrOpenFileReadBinary(tempSPIRVPath);
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
// 依次尝试 dxc 和 glslangValidator
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
    
    // 首先尝试 dxc（首选，HLSL 原生编译器）
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
