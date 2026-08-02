#include "NRShaderc.h"
#include "NRApi.h"

#if defined(NR_HAS_SHADERC)
	#include <shaderc/shaderc.h>
#endif

// ============================================================
// NRShaderc.c
// ============================================================

static char s_cacheDir[512];
static char s_lastError[2048];
static b32  s_inited = FALSE;

#if defined(NR_HAS_SHADERC)
static shaderc_compiler_t s_compiler = NULL;
#endif

const char* nrShadercLastError(void) { return s_lastError; }

// ------------------------------------------------------------
// FNV-1a 64
// ------------------------------------------------------------
static u64 nrHashUpdate(u64 h, const char* data, size_t len)
{
	for (size_t i = 0; i < len; i++)
	{
		h ^= (u64)(u8)data[i];
		h *= 1099511628211ull;
	}
	return h;
}

static u64 nrHashShader(const char* source, u32 stage, const char* entry,
						const NRShaderMacro* macros, u32 macro_count)
{
	u64 h = 14695981039346656037ull;
	h = nrHashUpdate(h, source, strlen(source));
	h = nrHashUpdate(h, (const char*)&stage, sizeof(stage));
	if (entry != NULL) h = nrHashUpdate(h, entry, strlen(entry));
	for (u32 i = 0; i < macro_count; i++)
	{
		if (macros[i].name != NULL) h = nrHashUpdate(h, macros[i].name, strlen(macros[i].name));
		if (macros[i].value != NULL) h = nrHashUpdate(h, macros[i].value, strlen(macros[i].value));
	}
	return h;
}

// ------------------------------------------------------------
// 缓存读写
// ------------------------------------------------------------
static void nrCachePath(u64 hash, char* out, size_t outSize)
{
	if (s_cacheDir[0] == '\0') { out[0] = '\0'; return; }
	SDL_snprintf(out, outSize, "%s%016llx.spv", s_cacheDir, (unsigned long long)hash);
}

static bool nrCacheLoad(u64 hash, u32** out_spirv, u64* out_size)
{
	char path[600];
	nrCachePath(hash, path, sizeof(path));
	if (path[0] == '\0') return false;

	SDL_IOStream* io = SDL_IOFromFile(path, "rb");
	if (io == NULL) return false;

	Sint64 size = SDL_GetIOSize(io);
	if (size <= 0 || (size % 4) != 0) { SDL_CloseIO(io); return false; }

	u32* buf = (u32*)malloc((size_t)size);
	if (buf == NULL) { SDL_CloseIO(io); return false; }

	size_t read = SDL_ReadIO(io, buf, (size_t)size);
	SDL_CloseIO(io);
	if (read != (size_t)size) { free(buf); return false; }

	// 校验 SPIR-V 魔数，防止读到损坏文件
	if (buf[0] != 0x07230203u) { free(buf); return false; }

	*out_spirv = buf;
	*out_size = (u64)size;
	return true;
}

static void nrCacheStore(u64 hash, const u32* spirv, u64 size)
{
	char path[600];
	nrCachePath(hash, path, sizeof(path));
	if (path[0] == '\0') return;

	SDL_IOStream* io = SDL_IOFromFile(path, "wb");
	if (io == NULL) return;
	SDL_WriteIO(io, spirv, (size_t)size);
	SDL_CloseIO(io);
}

// ------------------------------------------------------------
// 初始化
// ------------------------------------------------------------
NRResult nrShadercInit(const char* cache_directory)
{
	if (s_inited) return NRR_MakeSuccess(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SUCCESS);

	memset(s_cacheDir, 0, sizeof(s_cacheDir));
	memset(s_lastError, 0, sizeof(s_lastError));

	if (cache_directory != NULL && cache_directory[0] != '\0')
	{
		size_t len = strlen(cache_directory);
		const char* sep = (cache_directory[len - 1] == '/' || cache_directory[len - 1] == '\\')
						? "" : "/";
		SDL_snprintf(s_cacheDir, sizeof(s_cacheDir), "%s%s", cache_directory, sep);
	}
	else
	{
		// SDL_GetPrefPath 返回的字符串归调用方所有，必须 SDL_free
		char* pref = SDL_GetPrefPath("SaturnEngine", "Shaders");
		if (pref != NULL)
		{
			SDL_snprintf(s_cacheDir, sizeof(s_cacheDir), "%s", pref);
			SDL_free(pref);
		}
	}
	if (s_cacheDir[0] != '\0')
		SDL_CreateDirectory(s_cacheDir);

#if defined(NR_HAS_SHADERC)
	s_compiler = shaderc_compiler_initialize();
	if (s_compiler == NULL)
		return NRR_MakeWarning(NRR_STEP_VK_CreateShaderModule, NRR_CODE_NOT_IMPLEMENTED, 0);
#endif

	s_inited = TRUE;
	return NRR_MakeSuccess(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SUCCESS);
}

void nrShadercShutdown(void)
{
#if defined(NR_HAS_SHADERC)
	if (s_compiler != NULL)
	{
		shaderc_compiler_release(s_compiler);
		s_compiler = NULL;
	}
#endif
	s_inited = FALSE;
}

void nrShadercFree(u32* spirv)
{
	free(spirv);
}

// ------------------------------------------------------------
// 编译
// ------------------------------------------------------------
NRResult nrShadercCompile(const char* source, const char* name, u32 stage,
						  const char* entry_point,
						  const NRShaderMacro* macros, u32 macro_count,
						  u32** out_spirv, u64* out_size_bytes)
{
	if (source == NULL || out_spirv == NULL || out_size_bytes == NULL)
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_INVALID_PARAMETER, 0);

	*out_spirv = NULL;
	*out_size_bytes = 0;
	if (entry_point == NULL) entry_point = "main";
	if (name == NULL) name = "shader";

	// ---- 缓存命中 ----
	u64 hash = nrHashShader(source, stage, entry_point, macros, macro_count);
	if (nrCacheLoad(hash, out_spirv, out_size_bytes))
		return NRR_MakeSuccess(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SUCCESS);

#if !defined(NR_HAS_SHADERC)
	SDL_snprintf(s_lastError, sizeof(s_lastError),
				 "shaderc 未编入，且缓存未命中：%s。请预编译 SPIR-V 并放入 %s",
				 name, s_cacheDir);
	return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 0);
#else
	if (s_compiler == NULL)
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_NOT_INITIALIZED, 0);

	shaderc_shader_kind kind;
	switch (stage)
	{
		case NR_SHADER_STAGE_VERTEX:   kind = shaderc_vertex_shader; break;
		case NR_SHADER_STAGE_FRAGMENT: kind = shaderc_fragment_shader; break;
		case NR_SHADER_STAGE_COMPUTE:  kind = shaderc_compute_shader; break;
		case NR_SHADER_STAGE_GEOMETRY: kind = shaderc_geometry_shader; break;
		default:
			return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_INVALID_PARAMETER, 1);
	}

	shaderc_compile_options_t opts = shaderc_compile_options_initialize();
	if (opts == NULL)
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_OUT_OF_MEMORY, 0);

	// 目标环境：Vulkan 1.1，覆盖所有目标平台（含 Android/iOS via MoltenVK）的最低要求
	shaderc_compile_options_set_target_env(opts, shaderc_target_env_vulkan,
										   shaderc_env_version_vulkan_1_1);
	shaderc_compile_options_set_optimization_level(opts, shaderc_optimization_level_performance);
	shaderc_compile_options_set_source_language(opts, shaderc_source_language_glsl);

	for (u32 i = 0; i < macro_count; i++)
	{
		if (macros[i].name == NULL) continue;
		const char* v = (macros[i].value != NULL) ? macros[i].value : "1";
		shaderc_compile_options_add_macro_definition(opts, macros[i].name, strlen(macros[i].name),
													 v, strlen(v));
	}

	shaderc_compilation_result_t result =
		shaderc_compile_into_spv(s_compiler, source, strlen(source), kind, name, entry_point, opts);
	shaderc_compile_options_release(opts);

	if (result == NULL)
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 1);

	if (shaderc_result_get_compilation_status(result) != shaderc_compilation_status_success)
	{
		SDL_snprintf(s_lastError, sizeof(s_lastError), "%s: %s",
					 name, shaderc_result_get_error_message(result));
		shaderc_result_release(result);
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 2);
	}

	size_t size = shaderc_result_get_length(result);
	if (size == 0 || (size % 4) != 0)
	{
		shaderc_result_release(result);
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SHADER_COMPILATION_FAILED, 3);
	}

	u32* code = (u32*)malloc(size);
	if (code == NULL)
	{
		shaderc_result_release(result);
		return NRR_MakeFailure(NRR_STEP_VK_CreateShaderModule, NRR_CODE_OUT_OF_MEMORY, 1);
	}
	memcpy(code, shaderc_result_get_bytes(result), size);
	shaderc_result_release(result);

	nrCacheStore(hash, code, (u64)size);

	*out_spirv = code;
	*out_size_bytes = (u64)size;
	s_lastError[0] = '\0';
	return NRR_MakeSuccess(NRR_STEP_VK_CreateShaderModule, NRR_CODE_SUCCESS);
#endif
}
