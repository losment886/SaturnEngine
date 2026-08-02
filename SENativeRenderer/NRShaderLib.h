#pragma once

// ============================================================
// NRShaderLib.h
// 内置 GLSL 着色器源（供 shaderc 运行时编译）
//
// 为什么新建而不是改 NRShaderSource.h：
//   那份文件是 HLSL，面向 DirectX12 路径且仍被旧代码引用；
//   原生 Vulkan 后端统一走 GLSL + shaderc，两者并存互不影响。
//
// 集合布局（与 NRDescriptor.h 严格一致）：
//   set 0 : b0 CameraUBO, b1 LightSSBO, b2 irradiance, b3 prefiltered,
//           b4 brdfLUT, b5 shadowMap
//   set 1 : b0 MaterialUBO, b1..b5 baseColor/mr/normal/occlusion/emissive
//   set 2 : b0 ObjectUBO(dynamic), b1 SkinSSBO
//   set 3 : b0 bindless texture array
//
// 顶点属性 location 与 nrPipelineVertexInput 一一对应：
//   0 position, 1 normal, 2 tangent, 3 uv0, 4 uv1, 5 color, 6 joints, 7 weights
// ============================================================

// ------------------------------------------------------------
// 公共声明片段
// ------------------------------------------------------------
#define NR_GLSL_HEADER \
"#version 450 core\n" \
"#extension GL_ARB_separate_shader_objects : enable\n"

#define NR_GLSL_COMMON \
"struct NRLight {\n" \
"    vec4 position_range;\n" \
"    vec4 direction_type;\n" \
"    vec4 color_intensity;\n" \
"    vec4 cone_bias;\n" \
"};\n" \
"layout(set = 0, binding = 0) uniform CameraUBO {\n" \
"    mat4 view;\n" \
"    mat4 proj;\n" \
"    mat4 viewProj;\n" \
"    mat4 lightViewProj;\n" \
"    vec4 cameraPos;\n" \
"    vec4 params;      // x=near y=far z=time w=exposure\n" \
"    uvec4 counts;     // x=lightCount\n" \
"} uCamera;\n" \
"layout(std430, set = 0, binding = 1) readonly buffer LightSSBO {\n" \
"    NRLight lights[];\n" \
"} uLights;\n" \
"layout(push_constant) uniform PushBlock {\n" \
"    mat4 model;\n" \
"    uint materialIndex;\n" \
"    uint objectFlags;\n" \
"    float time;\n" \
"    float pad;\n" \
"} uPush;\n"

// ============================================================
// PBR — 顶点着色器
// ============================================================
static const char* g_NRPbrVertGLSL =
NR_GLSL_HEADER
NR_GLSL_COMMON
"layout(location = 0) in vec3 inPosition;\n"
"layout(location = 1) in vec3 inNormal;\n"
"layout(location = 2) in vec4 inTangent;\n"
"layout(location = 3) in vec2 inUV0;\n"
"layout(location = 4) in vec2 inUV1;\n"
"layout(location = 5) in vec4 inColor;\n"
"layout(location = 6) in uvec4 inJoints;\n"
"layout(location = 7) in vec4 inWeights;\n"
"#ifdef NR_SKINNED\n"
"layout(std430, set = 2, binding = 1) readonly buffer SkinSSBO { mat4 joints[]; } uSkin;\n"
"#endif\n"
"layout(location = 0) out vec3 vWorldPos;\n"
"layout(location = 1) out vec3 vNormal;\n"
"layout(location = 2) out vec4 vTangent;\n"
"layout(location = 3) out vec2 vUV0;\n"
"layout(location = 4) out vec2 vUV1;\n"
"layout(location = 5) out vec4 vColor;\n"
"layout(location = 6) out vec4 vLightSpacePos;\n"
"void main() {\n"
"    mat4 model = uPush.model;\n"
"#ifdef NR_SKINNED\n"
"    mat4 skin = inWeights.x * uSkin.joints[inJoints.x]\n"
"              + inWeights.y * uSkin.joints[inJoints.y]\n"
"              + inWeights.z * uSkin.joints[inJoints.z]\n"
"              + inWeights.w * uSkin.joints[inJoints.w];\n"
"    model = model * skin;\n"
"#endif\n"
"    vec4 worldPos = model * vec4(inPosition, 1.0);\n"
"    vWorldPos = worldPos.xyz;\n"
"    mat3 nrmMat = mat3(transpose(inverse(model)));\n"
"    vNormal = normalize(nrmMat * inNormal);\n"
"    vTangent = vec4(normalize(nrmMat * inTangent.xyz), inTangent.w);\n"
"    vUV0 = inUV0;\n"
"    vUV1 = inUV1;\n"
"    vColor = inColor;\n"
"    vLightSpacePos = uCamera.lightViewProj * worldPos;\n"
"    gl_Position = uCamera.viewProj * worldPos;\n"
"}\n";

// ============================================================
// PBR — 片段着色器（Cook-Torrance + IBL + 阴影）
// ============================================================
static const char* g_NRPbrFragGLSL =
NR_GLSL_HEADER
NR_GLSL_COMMON
"layout(set = 0, binding = 2) uniform samplerCube uIrradiance;\n"
"layout(set = 0, binding = 3) uniform samplerCube uPrefiltered;\n"
"layout(set = 0, binding = 4) uniform sampler2D uBrdfLUT;\n"
"layout(set = 0, binding = 5) uniform sampler2D uShadowMap;\n"
"layout(set = 1, binding = 0) uniform MaterialUBO {\n"
"    vec4 baseColorFactor;\n"
"    vec4 emissiveFactor;   // w = alphaCutoff\n"
"    vec4 pbrFactors;       // x=metallic y=roughness z=normalScale w=occlusion\n"
"    uvec4 texIndices;      // bindless: x=base y=mr z=normal w=occlusion\n"
"    uvec4 texIndices2;     // x=emissive\n"
"} uMat;\n"
"layout(set = 1, binding = 1) uniform sampler2D uBaseColor;\n"
"layout(set = 1, binding = 2) uniform sampler2D uMetalRough;\n"
"layout(set = 1, binding = 3) uniform sampler2D uNormalMap;\n"
"layout(set = 1, binding = 4) uniform sampler2D uOcclusion;\n"
"layout(set = 1, binding = 5) uniform sampler2D uEmissive;\n"
"layout(location = 0) in vec3 vWorldPos;\n"
"layout(location = 1) in vec3 vNormal;\n"
"layout(location = 2) in vec4 vTangent;\n"
"layout(location = 3) in vec2 vUV0;\n"
"layout(location = 4) in vec2 vUV1;\n"
"layout(location = 5) in vec4 vColor;\n"
"layout(location = 6) in vec4 vLightSpacePos;\n"
"layout(location = 0) out vec4 outColor;\n"
"const float PI = 3.14159265359;\n"
"vec3 getNormal() {\n"
"    vec3 N = normalize(vNormal);\n"
"    vec3 T = normalize(vTangent.xyz - N * dot(N, vTangent.xyz));\n"
"    if (length(T) < 1e-5) return N;\n"
"    vec3 B = cross(N, T) * vTangent.w;\n"
"    vec3 tn = texture(uNormalMap, vUV0).xyz * 2.0 - 1.0;\n"
"    tn.xy *= uMat.pbrFactors.z;\n"
"    return normalize(mat3(T, B, N) * tn);\n"
"}\n"
"float distributionGGX(float NdotH, float a) {\n"
"    float a2 = a * a;\n"
"    float d = NdotH * NdotH * (a2 - 1.0) + 1.0;\n"
"    return a2 / max(PI * d * d, 1e-7);\n"
"}\n"
"float geometrySmith(float NdotV, float NdotL, float rough) {\n"
"    float k = (rough + 1.0) * (rough + 1.0) / 8.0;\n"
"    float gv = NdotV / (NdotV * (1.0 - k) + k);\n"
"    float gl = NdotL / (NdotL * (1.0 - k) + k);\n"
"    return gv * gl;\n"
"}\n"
"vec3 fresnelSchlick(float cosT, vec3 F0) {\n"
"    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosT, 0.0, 1.0), 5.0);\n"
"}\n"
"vec3 fresnelSchlickRough(float cosT, vec3 F0, float rough) {\n"
"    return F0 + (max(vec3(1.0 - rough), F0) - F0) * pow(clamp(1.0 - cosT, 0.0, 1.0), 5.0);\n"
"}\n"
"float shadowFactor() {\n"
"    vec3 pc = vLightSpacePos.xyz / max(vLightSpacePos.w, 1e-6);\n"
"    if (pc.z > 1.0 || pc.z < 0.0) return 1.0;\n"
"    vec2 uv = pc.xy * 0.5 + 0.5;\n"
"    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 1.0;\n"
"    float bias = 0.0015;\n"
"    float shadow = 0.0;\n"
"    vec2 texel = 1.0 / vec2(textureSize(uShadowMap, 0));\n"
"    for (int x = -1; x <= 1; ++x)\n"
"        for (int y = -1; y <= 1; ++y) {\n"
"            float d = texture(uShadowMap, uv + vec2(x, y) * texel).r;\n"
"            shadow += (pc.z - bias > d) ? 0.0 : 1.0;\n"
"        }\n"
"    return shadow / 9.0;\n"
"}\n"
"void main() {\n"
"    vec4 baseColor = texture(uBaseColor, vUV0) * uMat.baseColorFactor * vColor;\n"
"#ifdef NR_ALPHA_MASK\n"
"    if (baseColor.a < uMat.emissiveFactor.w) discard;\n"
"#endif\n"
"    vec4 mr = texture(uMetalRough, vUV0);\n"
"    float metallic = clamp(mr.b * uMat.pbrFactors.x, 0.0, 1.0);\n"
"    float rough = clamp(mr.g * uMat.pbrFactors.y, 0.04, 1.0);\n"
"    float ao = mix(1.0, texture(uOcclusion, vUV0).r, uMat.pbrFactors.w);\n"
"    vec3 N = getNormal();\n"
"    vec3 V = normalize(uCamera.cameraPos.xyz - vWorldPos);\n"
"    float NdotV = max(dot(N, V), 1e-4);\n"
"    vec3 F0 = mix(vec3(0.04), baseColor.rgb, metallic);\n"
"    vec3 Lo = vec3(0.0);\n"
"    uint count = uCamera.counts.x;\n"
"    for (uint i = 0u; i < count; ++i) {\n"
"        NRLight lt = uLights.lights[i];\n"
"        uint type = uint(lt.direction_type.w);\n"
"        vec3 L; float atten = 1.0;\n"
"        if (type == 0u) { L = normalize(-lt.direction_type.xyz); }\n"
"        else {\n"
"            vec3 d = lt.position_range.xyz - vWorldPos;\n"
"            float dist = length(d);\n"
"            L = d / max(dist, 1e-5);\n"
"            float range = max(lt.position_range.w, 1e-4);\n"
"            float f = clamp(1.0 - pow(dist / range, 4.0), 0.0, 1.0);\n"
"            atten = f * f / max(dist * dist, 1e-4);\n"
"            if (type == 2u) {\n"
"                float cd = dot(normalize(-lt.direction_type.xyz), L);\n"
"                float inner = lt.cone_bias.x, outer = lt.cone_bias.y;\n"
"                atten *= clamp((cd - outer) / max(inner - outer, 1e-4), 0.0, 1.0);\n"
"            }\n"
"        }\n"
"        vec3 H = normalize(V + L);\n"
"        float NdotL = max(dot(N, L), 0.0);\n"
"        if (NdotL <= 0.0 || atten <= 0.0) continue;\n"
"        float D = distributionGGX(max(dot(N, H), 0.0), rough * rough);\n"
"        float G = geometrySmith(NdotV, NdotL, rough);\n"
"        vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);\n"
"        vec3 spec = (D * G * F) / max(4.0 * NdotV * NdotL, 1e-5);\n"
"        vec3 kd = (vec3(1.0) - F) * (1.0 - metallic);\n"
"        vec3 radiance = lt.color_intensity.rgb * lt.color_intensity.w * atten;\n"
"        float sh = (type == 0u) ? shadowFactor() : 1.0;\n"
"        Lo += (kd * baseColor.rgb / PI + spec) * radiance * NdotL * sh;\n"
"    }\n"
"    vec3 F = fresnelSchlickRough(NdotV, F0, rough);\n"
"    vec3 kd = (vec3(1.0) - F) * (1.0 - metallic);\n"
"    vec3 irradiance = texture(uIrradiance, N).rgb;\n"
"    vec3 diffuseIBL = irradiance * baseColor.rgb;\n"
"    vec3 R = reflect(-V, N);\n"
"    const float MAX_LOD = 6.0;\n"
"    vec3 prefiltered = textureLod(uPrefiltered, R, rough * MAX_LOD).rgb;\n"
"    vec2 brdf = texture(uBrdfLUT, vec2(NdotV, rough)).rg;\n"
"    vec3 specIBL = prefiltered * (F * brdf.x + brdf.y);\n"
"    vec3 ambient = (kd * diffuseIBL + specIBL) * ao;\n"
"    vec3 emissive = texture(uEmissive, vUV0).rgb * uMat.emissiveFactor.rgb;\n"
"    vec3 color = ambient + Lo + emissive;\n"
"    outColor = vec4(color, baseColor.a);\n"
"}\n";

// ============================================================
// 天空盒
// ============================================================
static const char* g_NRSkyboxVertGLSL =
NR_GLSL_HEADER
NR_GLSL_COMMON
"layout(location = 0) in vec3 inPosition;\n"
"layout(location = 0) out vec3 vDir;\n"
"void main() {\n"
"    vDir = inPosition;\n"
"    mat4 rotView = mat4(mat3(uCamera.view));\n"
"    vec4 pos = uCamera.proj * rotView * vec4(inPosition, 1.0);\n"
"    gl_Position = pos.xyww;\n"   // z=w => 深度恒为 1，始终在最远处
"}\n";

static const char* g_NRSkyboxFragGLSL =
NR_GLSL_HEADER
"layout(set = 0, binding = 2) uniform samplerCube uEnvMap;\n"
"layout(location = 0) in vec3 vDir;\n"
"layout(location = 0) out vec4 outColor;\n"
"void main() {\n"
"    outColor = vec4(texture(uEnvMap, normalize(vDir)).rgb, 1.0);\n"
"}\n";

// ============================================================
// 阴影（深度-only）
// ============================================================
static const char* g_NRShadowVertGLSL =
NR_GLSL_HEADER
NR_GLSL_COMMON
"layout(location = 0) in vec3 inPosition;\n"
"layout(location = 6) in uvec4 inJoints;\n"
"layout(location = 7) in vec4 inWeights;\n"
"#ifdef NR_SKINNED\n"
"layout(std430, set = 2, binding = 1) readonly buffer SkinSSBO { mat4 joints[]; } uSkin;\n"
"#endif\n"
"void main() {\n"
"    mat4 model = uPush.model;\n"
"#ifdef NR_SKINNED\n"
"    model = model * (inWeights.x * uSkin.joints[inJoints.x]\n"
"                   + inWeights.y * uSkin.joints[inJoints.y]\n"
"                   + inWeights.z * uSkin.joints[inJoints.z]\n"
"                   + inWeights.w * uSkin.joints[inJoints.w]);\n"
"#endif\n"
"    gl_Position = uCamera.lightViewProj * model * vec4(inPosition, 1.0);\n"
"}\n";

static const char* g_NRShadowFragGLSL =
NR_GLSL_HEADER
"void main() { }\n";

// ============================================================
// 全屏三角形（后处理通用顶点着色器，无顶点缓冲）
// ============================================================
static const char* g_NRFullscreenVertGLSL =
NR_GLSL_HEADER
"layout(location = 0) out vec2 vUV;\n"
"void main() {\n"
"    vUV = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);\n"
"    gl_Position = vec4(vUV * 2.0 - 1.0, 0.0, 1.0);\n"
"}\n";

// ============================================================
// 后处理：色调映射 + 泛光合成 + 伽马
// ============================================================
static const char* g_NRPostFragGLSL =
NR_GLSL_HEADER
"layout(set = 0, binding = 0) uniform sampler2D uScene;\n"
"layout(set = 0, binding = 1) uniform sampler2D uBloom;\n"
"layout(push_constant) uniform PostBlock {\n"
"    float exposure;\n"
"    float gamma;\n"
"    float bloomIntensity;\n"
"    float vignette;\n"
"    float contrast;\n"
"    float saturation;\n"
"    uint  tonemapMode;   // 0=None 1=Reinhard 2=ACES 3=Filmic\n"
"    uint  flags;\n"
"} uPost;\n"
"layout(location = 0) in vec2 vUV;\n"
"layout(location = 0) out vec4 outColor;\n"
"vec3 tonemapACES(vec3 x) {\n"
"    const float a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;\n"
"    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);\n"
"}\n"
"vec3 tonemapFilmic(vec3 x) {\n"
"    vec3 X = max(vec3(0.0), x - 0.004);\n"
"    return (X * (6.2 * X + 0.5)) / (X * (6.2 * X + 1.7) + 0.06);\n"
"}\n"
"void main() {\n"
"    vec3 color = texture(uScene, vUV).rgb;\n"
"    color += texture(uBloom, vUV).rgb * uPost.bloomIntensity;\n"
"    color *= uPost.exposure;\n"
"    if (uPost.tonemapMode == 1u) color = color / (color + vec3(1.0));\n"
"    else if (uPost.tonemapMode == 2u) color = tonemapACES(color);\n"
"    else if (uPost.tonemapMode == 3u) { outColor = vec4(tonemapFilmic(color), 1.0); return; }\n"
"    float luma = dot(color, vec3(0.2126, 0.7152, 0.0722));\n"
"    color = mix(vec3(luma), color, uPost.saturation);\n"
"    color = (color - 0.5) * uPost.contrast + 0.5;\n"
"    vec2 d = vUV - 0.5;\n"
"    color *= mix(1.0, 1.0 - dot(d, d) * 1.5, uPost.vignette);\n"
"    color = pow(max(color, vec3(0.0)), vec3(1.0 / max(uPost.gamma, 1e-3)));\n"
"    outColor = vec4(color, 1.0);\n"
"}\n";

// ============================================================
// 泛光提取（亮度阈值）
// ============================================================
static const char* g_NRBloomExtractFragGLSL =
NR_GLSL_HEADER
"layout(set = 0, binding = 0) uniform sampler2D uScene;\n"
"layout(push_constant) uniform BloomBlock { float threshold; float knee; } uB;\n"
"layout(location = 0) in vec2 vUV;\n"
"layout(location = 0) out vec4 outColor;\n"
"void main() {\n"
"    vec3 c = texture(uScene, vUV).rgb;\n"
"    float luma = dot(c, vec3(0.2126, 0.7152, 0.0722));\n"
"    float soft = clamp((luma - uB.threshold + uB.knee) / max(2.0 * uB.knee, 1e-4), 0.0, 1.0);\n"
"    float w = max(luma - uB.threshold, luma * soft) / max(luma, 1e-4);\n"
"    outColor = vec4(c * w, 1.0);\n"
"}\n";

// ============================================================
// 高斯模糊（单方向，水平/垂直两次调用）
// ============================================================
static const char* g_NRBlurFragGLSL =
NR_GLSL_HEADER
"layout(set = 0, binding = 0) uniform sampler2D uSrc;\n"
"layout(push_constant) uniform BlurBlock { vec2 direction; } uBlur;\n"
"layout(location = 0) in vec2 vUV;\n"
"layout(location = 0) out vec4 outColor;\n"
"void main() {\n"
"    vec2 texel = 1.0 / vec2(textureSize(uSrc, 0));\n"
"    const float w[5] = float[](0.227027, 0.194594, 0.121621, 0.054054, 0.016216);\n"
"    vec3 sum = texture(uSrc, vUV).rgb * w[0];\n"
"    for (int i = 1; i < 5; ++i) {\n"
"        vec2 off = uBlur.direction * texel * float(i);\n"
"        sum += texture(uSrc, vUV + off).rgb * w[i];\n"
"        sum += texture(uSrc, vUV - off).rgb * w[i];\n"
"    }\n"
"    outColor = vec4(sum, 1.0);\n"
"}\n";

// ============================================================
// 粒子（billboard，实例化）
// ============================================================
static const char* g_NRParticleVertGLSL =
NR_GLSL_HEADER
NR_GLSL_COMMON
"struct NRParticle {\n"
"    vec4 position_size;\n"
"    vec4 color;\n"
"    vec4 velocity_life;\n"
"};\n"
"layout(std430, set = 2, binding = 0) readonly buffer ParticleSSBO {\n"
"    NRParticle particles[];\n"
"} uParticles;\n"
"layout(location = 0) out vec2 vUV;\n"
"layout(location = 1) out vec4 vColor;\n"
"void main() {\n"
"    NRParticle p = uParticles.particles[gl_InstanceIndex];\n"
"    vec2 corner = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);\n"
"    vUV = corner;\n"
"    vColor = p.color;\n"
"    vec2 offset = (corner * 2.0 - 1.0) * p.position_size.w;\n"
"    vec3 right = vec3(uCamera.view[0][0], uCamera.view[1][0], uCamera.view[2][0]);\n"
"    vec3 up    = vec3(uCamera.view[0][1], uCamera.view[1][1], uCamera.view[2][1]);\n"
"    vec3 world = p.position_size.xyz + right * offset.x + up * offset.y;\n"
"    gl_Position = uCamera.viewProj * vec4(world, 1.0);\n"
"}\n";

static const char* g_NRParticleFragGLSL =
NR_GLSL_HEADER
"layout(set = 1, binding = 1) uniform sampler2D uParticleTex;\n"
"layout(location = 0) in vec2 vUV;\n"
"layout(location = 1) in vec4 vColor;\n"
"layout(location = 0) out vec4 outColor;\n"
"void main() {\n"
"    vec4 tex = texture(uParticleTex, vUV);\n"
"    outColor = tex * vColor;\n"
"    if (outColor.a < 0.003) discard;\n"
"}\n";

// ============================================================
// 粒子模拟（计算着色器）
// ============================================================
static const char* g_NRParticleCompGLSL =
NR_GLSL_HEADER
"layout(local_size_x = 256) in;\n"
"struct NRParticle {\n"
"    vec4 position_size;\n"
"    vec4 color;\n"
"    vec4 velocity_life;\n"
"};\n"
"layout(std430, set = 0, binding = 0) buffer ParticleSSBO {\n"
"    NRParticle particles[];\n"
"} uParticles;\n"
"layout(push_constant) uniform SimBlock {\n"
"    vec4 gravity_dt;    // xyz=gravity w=deltaTime\n"
"    uint count;\n"
"    float drag;\n"
"} uSim;\n"
"void main() {\n"
"    uint i = gl_GlobalInvocationID.x;\n"
"    if (i >= uSim.count) return;\n"
"    NRParticle p = uParticles.particles[i];\n"
"    float dt = uSim.gravity_dt.w;\n"
"    p.velocity_life.w -= dt;\n"
"    if (p.velocity_life.w <= 0.0) { p.color.a = 0.0; uParticles.particles[i] = p; return; }\n"
"    vec3 vel = p.velocity_life.xyz + uSim.gravity_dt.xyz * dt;\n"
"    vel *= max(1.0 - uSim.drag * dt, 0.0);\n"
"    p.velocity_life.xyz = vel;\n"
"    p.position_size.xyz += vel * dt;\n"
"    uParticles.particles[i] = p;\n"
"}\n";
