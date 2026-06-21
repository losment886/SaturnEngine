#include "NRDefine.h"

// ============================================================
// NRGlobal.c
// 全局变量定义
// 所有在 NRDefine.h 中以 extern 声明的全局变量在此处定义
// ============================================================

// SDL 初始化状态
bool nr_sdl_init = false;

// SDL 窗口指针
SDL_Window* nr_window = NULL;

// 上一次 SDL 错误消息
const char* nr_last_sdl_error_msg = "No error.";

// 上一次操作结果
NRResult nr_last_result = 0;
