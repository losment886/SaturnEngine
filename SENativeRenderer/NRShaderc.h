#pragma once

// ============================================================
// NRShaderc.h
// 基于 shaderc 的 GLSL -> SPIR-V 运行时编译 + 磁盘缓存
//
// 为什么不用现有的 NRShaderCompiler.c：
//   那份实现通过 CreateProcess/fork 调用外部 dxc / glslangValidator，
//   在 Android / iOS / HarmonyOS NEXT 上无法执行子进程，也没有可用的
//   命令行工具。shaderc 是静态库，可随 .so/.a 一起打包，全平台可用。
//
// 缓存策略：以 (源码 + 宏定义 + stage + entry) 的 FNV-1a 64 位哈希为
// 文件名存放 SPIR-V，命中即跳过编译。首次启动后冷启动时间大幅下降。
// ============================================================

#include "NRDefine.h"

SE_EXTERN_C_BEGIN

typedef struct NRShaderMacro
{
	const char* name;
	const char* value;   // NULL 表示仅定义
} NRShaderMacro;

// 初始化编译器与缓存目录（cache_directory 为 NULL 时使用应用首选目录）
NRResult nrShadercInit(const char* cache_directory);
void     nrShadercShutdown(void);

// GLSL -> SPIR-V。stage 取 NR_SHADER_STAGE_*。
// 返回的 out_spirv 需用 nrShadercFree 释放。
NRResult nrShadercCompile(const char* source, const char* name, u32 stage,
						  const char* entry_point,
						  const NRShaderMacro* macros, u32 macro_count,
						  u32** out_spirv, u64* out_size_bytes);

void     nrShadercFree(u32* spirv);
// 上一次编译的错误日志（无错误时返回空串）
const char* nrShadercLastError(void);

SE_EXTERN_C_END
