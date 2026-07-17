#pragma once

#include "NRDefine.h"
#include <vulkan/vulkan.h>

SE_EXTERN_C_BEGIN

NRResult nrCompileHLSLToSPIRV(const char* hlslSource,
	const char* entryPoint,
	const char* shaderStage,
	u32** outSPIRV,
	u32* outSPIRVSize);

NRResult nrVkCreateShaderFromHLSL(const char* hlslSource,
	const char* entryPoint,
	const char* shaderStage,
	VkShaderModule* outShaderModule);

void nrFreeSPIRV(u32* spirv);

SE_EXTERN_C_END
