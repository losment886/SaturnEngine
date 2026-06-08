#include "NRDefine.h"

// ============================================================
// NRWindow.c
// 窗口管理功能实现
// 所有导出函数使用 SE_API 宏以实现跨平台动态库导出
// ============================================================



// 使用 SDL3 创建窗口
SE_API NRResult NR_Init(u32 sdl_flags)
{
	if (nr_sdl_init)
	{
		return NRR_MakeWarning(NRR_STEP_NR_Init, NRR_CODE_ALREADY_INITIALIZED, 0);
	}
	SDL_Init(sdl_flags);
	nr_sdl_init = TRUE;
	return NRR_MakeSuccess(NRR_STEP_NR_Init, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_CreateWindow(const char* title, u32 width, u32 height, u32 flags)
{
	if (!nr_sdl_init)
	{
		return NRR_MakeFailure(NRR_STEP_NR_CreateWindow, NRR_CODE_NOT_INITIALIZED, 0);
	}
	nr_window = SDL_CreateWindow(title, SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, width, height, flags);
	if (nr_window == NULL)
	{
		return NRR_MakeFailure(NRR_STEP_NR_CreateWindow, NRR_CODE_CREATE_WINDOW_FAILED, GetLastError());
	}
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}
