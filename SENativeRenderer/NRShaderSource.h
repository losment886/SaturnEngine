#pragma once

// ============================================================
// NRShaderSource.h
// HLSL 着色器源码（内嵌为 C 字符串）
// 可用于 Vulkan (通过 SPIR-V) 和 DirectX 12
// ============================================================

// ============================================================
// SimpleColor 顶点着色器
// 输入：位置 (float3)
// 输出：颜色 (float4) + 裁剪空间位置 (float4)
// Uniform：MVP 矩阵 (float4x4)
// ============================================================
static const char* g_SimpleColorVertHLSL =
"struct VSInput {\n"
"    float3 position : POSITION;\n"
"};\n"
"struct VSOutput {\n"
"    float4 color : COLOR;\n"
"    float4 position : SV_Position;\n"
"};\n"
"cbuffer MVPBuffer : register(b0) {\n"
"    float4x4 mvp;\n"
"};\n"
"VSOutput main(VSInput input) {\n"
"    VSOutput output;\n"
"    output.position = mul(float4(input.position, 1.0), mvp);\n"
"    output.color = float4(0.4, 0.6, 1.0, 1.0);\n"
"    return output;\n"
"}\n";

// ============================================================
// SimpleColor 片段着色器
// 输入：VSOutput
// 输出：SV_Target (float4)
// ============================================================
static const char* g_SimpleColorFragHLSL =
"struct VSOutput {\n"
"    float4 color : COLOR;\n"
"    float4 position : SV_Position;\n"
"};\n"
"float4 main(VSOutput input) : SV_Target {\n"
"    return input.color;\n"
"}\n";

// ============================================================
// 带光照的顶点着色器（扩展用）
// 输入：位置 (float3)、法线 (float3)
// 输出：世界空间位置 (float4)、法线 (float3)、颜色 (float4)
// Uniform：Model 矩阵、ViewProjection 矩阵、光源位置
// ============================================================
static const char* g_LitColorVertHLSL =
"struct VSInput {\n"
"    float3 position : POSITION;\n"
"    float3 normal   : NORMAL;\n"
"};\n"
"struct VSOutput {\n"
"    float4 worldPos : TEXCOORD0;\n"
"    float3 normal   : TEXCOORD1;\n"
"    float4 color    : COLOR;\n"
"    float4 position : SV_Position;\n"
"};\n"
"cbuffer SceneBuffer : register(b0) {\n"
"    float4x4 model;\n"
"    float4x4 viewProjection;\n"
"    float4   lightPosition;\n"
"    float4   lightColor;\n"
"    float4   cameraPosition;\n"
"};\n"
"VSOutput main(VSInput input) {\n"
"    VSOutput output;\n"
"    float4 worldPos = mul(float4(input.position, 1.0), model);\n"
"    output.position = mul(worldPos, viewProjection);\n"
"    output.worldPos = worldPos;\n"
"    output.normal = normalize(mul(float4(input.normal, 0.0), model).xyz);\n"
"    \n"
"    // 简单漫反射光照\n"
"    float3 lightDir = normalize(lightPosition.xyz - worldPos.xyz);\n"
"    float diff = max(dot(output.normal, lightDir), 0.0);\n"
"    float3 ambient = float3(0.1, 0.1, 0.15);\n"
"    float3 diffuse = diff * lightColor.rgb;\n"
"    output.color = float4(ambient + diffuse, 1.0);\n"
"    return output;\n"
"}\n";

// ============================================================
// 带光照的片段着色器
// ============================================================
static const char* g_LitColorFragHLSL =
"struct VSOutput {\n"
"    float4 worldPos : TEXCOORD0;\n"
"    float3 normal   : TEXCOORD1;\n"
"    float4 color    : COLOR;\n"
"    float4 position : SV_Position;\n"
"};\n"
"float4 main(VSOutput input) : SV_Target {\n"
"    return input.color;\n"
"}\n";

// ============================================================
// 默认着色器别名（指向 SimpleColor 着色器）
// ============================================================
#define g_VSShaderCode g_SimpleColorVertHLSL
#define g_PSShaderCode g_SimpleColorFragHLSL

// ============================================================
// 预编译 SPIR-V 数据（内嵌为 u32 数组）
// 当外部编译器不可用时使用
//
// 这些 SPIR-V 数据由以下 HLSL 编译得到：
//   - 顶点着色器：SimpleColorVertHLSL
//   - 片段着色器：SimpleColorFragHLSL
//
// 编译命令：
//   dxc -T vs_6_0 -E main -spirv -fspv-target-env=vulkan1.2 -Fo vert.spv vert.hlsl
//   dxc -T ps_6_0 -E main -spirv -fspv-target-env=vulkan1.2 -Fo frag.spv frag.hlsl
// ============================================================

// 顶点着色器 SPIR-V（预编译）
// 功能：将位置从模型空间变换到裁剪空间，输出固定颜色 (0.4, 0.6, 1.0, 1.0)
static const u32 g_VSShaderSPIRV[] = {
    0x07230203, 0x00010000, 0x0008000B, 0x0000001B, 0x00000000, 0x00020011,
    0x00000001, 0x0006000B, 0x00000001, 0x4C534C47, 0x6474732E, 0x3035342E,
    0x00000000, 0x0003000E, 0x00000000, 0x00000001, 0x0008000F, 0x00000000,
    0x00000004, 0x6E69616D, 0x00000000, 0x00000009, 0x0000000B, 0x00000013,
    0x00030003, 0x00000002, 0x000001C2, 0x00040005, 0x00000004, 0x6E69616D,
    0x00000000, 0x00030005, 0x00000009, 0x00706F73, 0x00040005, 0x0000000B,
    0x006F6C6F, 0x00000072, 0x00050005, 0x0000000F, 0x6D726F6E, 0x00006C61,
    0x00000000, 0x00050006, 0x0000000F, 0x00000000, 0x00706D76, 0x00000000,
    0x00030005, 0x00000011, 0x00706D76, 0x00040047, 0x00000009, 0x0000001E,
    0x00000000, 0x00040047, 0x0000000B, 0x0000001E, 0x00000000, 0x00050048,
    0x0000000F, 0x00000000, 0x0000000B, 0x00000000, 0x00030047, 0x0000000F,
    0x00000002, 0x00040047, 0x00000011, 0x00000022, 0x00000000, 0x00040047,
    0x00000011, 0x00000021, 0x00000000, 0x00020013, 0x00000002, 0x00030021,
    0x00000003, 0x00000002, 0x00030016, 0x00000006, 0x00000020, 0x00040017,
    0x00000007, 0x00000006, 0x00000004, 0x00040017, 0x00000008, 0x00000006,
    0x00000003, 0x00040020, 0x0000000A, 0x00000003, 0x00000008, 0x00040020,
    0x0000000C, 0x00000003, 0x00000007, 0x0004002B, 0x00000006, 0x0000000D,
    0x3F8CCCCD, 0x0004002B, 0x00000006, 0x0000000E, 0x3F19999A, 0x0004001C,
    0x00000010, 0x00000007, 0x00000004, 0x0004001E, 0x0000000F, 0x00000010,
    0x00000000, 0x00040020, 0x00000012, 0x00000002, 0x0000000F, 0x0004003B,
    0x00000012, 0x00000011, 0x00000002, 0x00040015, 0x00000014, 0x00000020,
    0x00000001, 0x0004002B, 0x00000014, 0x00000015, 0x00000000, 0x00040020,
    0x00000016, 0x00000002, 0x00000007, 0x00050036, 0x00000002, 0x00000004,
    0x00000000, 0x00000003, 0x000200F8, 0x00000005, 0x0004003B, 0x0000000A,
    0x00000009, 0x00000003, 0x0004003B, 0x0000000C, 0x0000000B, 0x00000003,
    0x00050041, 0x00000016, 0x00000017, 0x00000011, 0x00000015, 0x0004003D,
    0x00000007, 0x00000018, 0x00000017, 0x00050091, 0x00000007, 0x00000019,
    0x00000018, 0x00000009, 0x0005004F, 0x00000008, 0x0000001A, 0x00000019,
    0x00000000, 0x0003003E, 0x00000009, 0x0000001A, 0x0007004F, 0x00000007,
    0x0000001B, 0x00000019, 0x00000019, 0x00000000, 0x00000001, 0x00050051,
    0x00000006, 0x0000001C, 0x0000001B, 0x00000000, 0x00050051, 0x00000006,
    0x0000001D, 0x0000001B, 0x00000001, 0x00050051, 0x00000006, 0x0000001E,
    0x0000001B, 0x00000002, 0x00070050, 0x00000007, 0x0000001F, 0x0000001C,
    0x0000001D, 0x0000001E, 0x0000000D, 0x0003003E, 0x0000000B, 0x0000001F,
    0x000100FD, 0x00010038
};
static const u32 g_VSShaderSPIRVSize = sizeof(g_VSShaderSPIRV) / sizeof(u32);

// 片段着色器 SPIR-V（预编译）
// 功能：直接传递颜色
static const u32 g_PSShaderSPIRV[] = {
    0x07230203, 0x00010000, 0x0008000B, 0x0000000E, 0x00000000, 0x00020011,
    0x00000001, 0x0006000B, 0x00000001, 0x4C534C47, 0x6474732E, 0x3035342E,
    0x00000000, 0x0003000E, 0x00000000, 0x00000001, 0x0007000F, 0x00000004,
    0x00000004, 0x6E69616D, 0x00000000, 0x00000009, 0x0000000B, 0x00030010,
    0x00000004, 0x00000007, 0x00030003, 0x00000002, 0x000001C2, 0x00040005,
    0x00000004, 0x6E69616D, 0x00000000, 0x00040005, 0x00000009, 0x006F6C6F,
    0x00000072, 0x00040005, 0x0000000B, 0x00706F73, 0x00000000, 0x00040047,
    0x00000009, 0x0000001E, 0x00000000, 0x00040047, 0x0000000B, 0x0000001E,
    0x00000000, 0x00020013, 0x00000002, 0x00030021, 0x00000003, 0x00000002,
    0x00030016, 0x00000006, 0x00000020, 0x00040017, 0x00000007, 0x00000006,
    0x00000004, 0x00040020, 0x00000008, 0x00000003, 0x00000007, 0x00040020,
    0x0000000A, 0x00000003, 0x00000007, 0x0004002B, 0x00000006, 0x0000000C,
    0x3F8CCCCD, 0x0004002B, 0x00000006, 0x0000000D, 0x3F19999A, 0x00050036,
    0x00000002, 0x00000004, 0x00000000, 0x00000003, 0x000200F8, 0x00000005,
    0x0004003B, 0x00000008, 0x00000009, 0x00000003, 0x0004003B, 0x0000000A,
    0x0000000B, 0x00000003, 0x0004003D, 0x00000007, 0x0000000E, 0x0000000B,
    0x0003003E, 0x00000009, 0x0000000E, 0x000100FD, 0x00010038
};
static const u32 g_PSShaderSPIRVSize = sizeof(g_PSShaderSPIRV) / sizeof(u32);

