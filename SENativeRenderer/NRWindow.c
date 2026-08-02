#include "NRDefine.h"
#include "NRApi.h"

// ============================================================
// NRWindow.c
// 窗口管理功能实现
// 所有导出函数使用 SE_API 宏以实现跨平台动态库导出
// ============================================================

// ------------------------------------------------------------
// 事件与日志回调状态
// 托管侧通过 NR_SetEventCallback 注册 [UnmanagedCallersOnly] 函数指针，
// NR_PumpEvents 抽干 SDL 队列并逐个翻译为定长 NREvent 后回调。
// 之所以不直接暴露 SDL_Event，是为了避免 SDL 的变长联合体布局
// 跨语言边界产生歧义，同时让托管层不依赖任何 SDL 绑定。
// ------------------------------------------------------------
static NREventCallback nr_event_cb = NULL;
static void* nr_event_cb_user = NULL;
static NRLogCallback nr_log_cb = NULL;
static void* nr_log_cb_user = NULL;

// 打开的手柄列表，用于 NR_RumbleGamepad 按 device_id 反查
#define NR_MAX_GAMEPADS 16
static SDL_Gamepad* nr_gamepads[NR_MAX_GAMEPADS];
static SDL_JoystickID nr_gamepad_ids[NR_MAX_GAMEPADS];

// 窗口未创建时统一返回该错误，避免每个函数重复写
#define NR_REQUIRE_WINDOW(step)                                            \
	do {                                                                   \
		if (!nr_sdl_init)                                                  \
			return NRR_MakeFailure((step), NRR_CODE_NOT_INITIALIZED, 0);   \
		if (nr_window == NULL)                                             \
			return NRR_MakeFailure((step), NRR_CODE_WINDOW_NOT_CREATED, 0);\
	} while (0)



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
	

	nr_window = SDL_CreateWindow(title, width, height, flags);
	if (nr_window == NULL)
	{
#ifdef _WIN32
		return NRR_MakeFailure(NRR_STEP_NR_CreateWindow, NRR_CODE_CREATE_WINDOW_FAILED, GetLastError());
#else
		return NRR_MakeFailure(NRR_STEP_NR_CreateWindow, NRR_CODE_CREATE_WINDOW_FAILED, 0);
#endif
		
	}
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_DestroyWindow()
{
	if (!nr_sdl_init)
	{
		return NRR_MakeFailure(NRR_STEP_NR_DestroyWindow, NRR_CODE_NOT_INITIALIZED, 0);
	}
	if (nr_window == NULL)
	{
		return NRR_MakeFailure(NRR_STEP_NR_DestroyWindow, NRR_CODE_WINDOW_NOT_CREATED, 0);
	}
	SDL_DestroyWindow(nr_window);
	nr_window = NULL;
	return NRR_MakeSuccess(NRR_STEP_NR_DestroyWindow, NRR_CODE_SUCCESS);
}

// ============================================================
// 窗口控制
// ============================================================

SE_API NRResult NR_ShowWindow(void)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	SDL_ShowWindow(nr_window);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_HideWindow(void)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	SDL_HideWindow(nr_window);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_SetWindowTitle(const char* title)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	if (title == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateWindow, NRR_CODE_INVALID_PARAMETER, 0);
	SDL_SetWindowTitle(nr_window, title);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_SetWindowSize(u32 width, u32 height)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	if (width == 0 || height == 0)
		return NRR_MakeFailure(NRR_STEP_NR_CreateWindow, NRR_CODE_INVALID_PARAMETER, 0);
	SDL_SetWindowSize(nr_window, (int)width, (int)height);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_GetWindowSize(u32* out_width, u32* out_height)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	int w = 0, h = 0;
	SDL_GetWindowSize(nr_window, &w, &h);
	if (out_width)  *out_width = (u32)w;
	if (out_height) *out_height = (u32)h;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

// 像素尺寸在高 DPI 显示器上与逻辑尺寸不同，交换链必须使用像素尺寸
SE_API NRResult NR_GetWindowPixelSize(u32* out_width, u32* out_height)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	int w = 0, h = 0;
	SDL_GetWindowSizeInPixels(nr_window, &w, &h);
	if (out_width)  *out_width = (u32)w;
	if (out_height) *out_height = (u32)h;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_SetWindowPosition(s32 x, s32 y)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	SDL_SetWindowPosition(nr_window, (int)x, (int)y);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_GetWindowPosition(s32* out_x, s32* out_y)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	int x = 0, y = 0;
	SDL_GetWindowPosition(nr_window, &x, &y);
	if (out_x) *out_x = (s32)x;
	if (out_y) *out_y = (s32)y;
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_SetWindowFullscreen(b32 fullscreen)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	if (!SDL_SetWindowFullscreen(nr_window, fullscreen ? true : false))
		return NRR_MakeWarning(NRR_STEP_NR_CreateWindow, NRR_CODE_INVALID_PARAMETER, 0);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_SetWindowResizable(b32 resizable)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	SDL_SetWindowResizable(nr_window, resizable ? true : false);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_SetWindowIcon(const void* rgba_pixels, u32 width, u32 height)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	if (rgba_pixels == NULL || width == 0 || height == 0)
		return NRR_MakeFailure(NRR_STEP_NR_CreateWindow, NRR_CODE_INVALID_PARAMETER, 0);

	// SDL 不接管像素内存，创建的 surface 仅在本次调用内有效
	SDL_Surface* surf = SDL_CreateSurfaceFrom((int)width, (int)height,
											  SDL_PIXELFORMAT_RGBA32,
											  (void*)rgba_pixels, (int)(width * 4));
	if (surf == NULL)
		return NRR_MakeFailure(NRR_STEP_NR_CreateWindow, NRR_CODE_INVALID_PARAMETER, 0);

	bool ok = SDL_SetWindowIcon(nr_window, surf);
	SDL_DestroySurface(surf);

	if (!ok)
		return NRR_MakeWarning(NRR_STEP_NR_CreateWindow, NRR_CODE_INVALID_PARAMETER, 0);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API f32 NR_GetWindowDisplayScale(void)
{
	if (!nr_sdl_init || nr_window == NULL)
		return 1.0f;
	f32 scale = SDL_GetWindowDisplayScale(nr_window);
	// SDL 失败时返回 0，此时退回 1.0 避免上层用 0 参与除法
	return (scale > 0.0f) ? scale : 1.0f;
}

// 返回平台原生句柄（Windows HWND / X11 Window / Wayland surface / macOS NSWindow）
SE_API void* NR_GetNativeWindowHandle(void)
{
	if (!nr_sdl_init || nr_window == NULL)
		return NULL;

	SDL_PropertiesID props = SDL_GetWindowProperties(nr_window);
	if (props == 0)
		return NULL;

#if defined(SE_PLATFORM_WINDOWS)
	return SDL_GetPointerProperty(props, SDL_PROP_WINDOW_WIN32_HWND_POINTER, NULL);
#elif defined(SE_PLATFORM_MACOS) || defined(SE_PLATFORM_IOS)
	return SDL_GetPointerProperty(props, SDL_PROP_WINDOW_COCOA_WINDOW_POINTER, NULL);
#elif defined(SE_PLATFORM_ANDROID)
	return SDL_GetPointerProperty(props, SDL_PROP_WINDOW_ANDROID_WINDOW_POINTER, NULL);
#else
	// Linux/HarmonyOS：优先 Wayland，回退 X11
	void* p = SDL_GetPointerProperty(props, SDL_PROP_WINDOW_WAYLAND_SURFACE_POINTER, NULL);
	if (p != NULL) return p;
	return (void*)(uintptr_t)SDL_GetNumberProperty(props, SDL_PROP_WINDOW_X11_WINDOW_NUMBER, 0);
#endif
}

SE_API void* NR_GetSDLWindow(void)
{
	return (void*)nr_window;
}

// ============================================================
// 事件
// ============================================================

SE_API NRResult NR_SetEventCallback(NREventCallback cb, void* user_data)
{
	nr_event_cb = cb;
	nr_event_cb_user = user_data;
	return NRR_MakeSuccess(NRR_STEP_NR_Init, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_SetLogCallback(NRLogCallback cb, void* user_data)
{
	nr_log_cb = cb;
	nr_log_cb_user = user_data;
	return NRR_MakeSuccess(NRR_STEP_NR_Init, NRR_CODE_SUCCESS);
}

// 内部：把日志转发给托管侧（若已注册）
void nr_LogToManaged(s32 severity, const char* message)
{
	if (nr_log_cb != NULL && message != NULL)
		nr_log_cb(severity, message, nr_log_cb_user);
}

// 记录新接入的手柄，返回其 device_id
static void nrGamepadAdded(SDL_JoystickID id)
{
	for (u32 i = 0; i < NR_MAX_GAMEPADS; ++i)
	{
		if (nr_gamepads[i] == NULL)
		{
			nr_gamepads[i] = SDL_OpenGamepad(id);
			nr_gamepad_ids[i] = id;
			return;
		}
	}
}

static void nrGamepadRemoved(SDL_JoystickID id)
{
	for (u32 i = 0; i < NR_MAX_GAMEPADS; ++i)
	{
		if (nr_gamepad_ids[i] == id && nr_gamepads[i] != NULL)
		{
			SDL_CloseGamepad(nr_gamepads[i]);
			nr_gamepads[i] = NULL;
			nr_gamepad_ids[i] = 0;
			return;
		}
	}
}

// 把 SDL_Event 翻译为定长 NREvent。
// 返回 FALSE 表示该事件无需上报托管层（例如内部已消化的手柄热插拔）。
static b32 nrTranslateEvent(const SDL_Event* e, NREvent* out)
{
	memset(out, 0, sizeof(NREvent));
	out->timestamp = e->common.timestamp;

	switch (e->type)
	{
	case SDL_EVENT_QUIT:
		out->type = NR_EVENT_QUIT;
		return TRUE;

	// 尺寸变化用 PIXEL_SIZE_CHANGED 而非 RESIZED：
	// 交换链需要的是像素尺寸，高 DPI 下二者不等
	case SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED:
		out->type = NR_EVENT_WINDOW_RESIZE;
		out->i0 = e->window.data1;
		out->i1 = e->window.data2;
		return TRUE;

	case SDL_EVENT_WINDOW_MOVED:
		out->type = NR_EVENT_WINDOW_MOVE;
		out->i0 = e->window.data1;
		out->i1 = e->window.data2;
		return TRUE;

	case SDL_EVENT_WINDOW_FOCUS_GAINED:
		out->type = NR_EVENT_WINDOW_FOCUS;
		out->i0 = 1;
		return TRUE;

	case SDL_EVENT_WINDOW_FOCUS_LOST:
		out->type = NR_EVENT_WINDOW_FOCUS;
		out->i0 = 0;
		return TRUE;

	case SDL_EVENT_WINDOW_MINIMIZED:
		out->type = NR_EVENT_WINDOW_MINIMIZE;
		out->i0 = 1;
		return TRUE;

	case SDL_EVENT_WINDOW_RESTORED:
		out->type = NR_EVENT_WINDOW_MINIMIZE;
		out->i0 = 0;
		return TRUE;

	case SDL_EVENT_KEY_DOWN:
	case SDL_EVENT_KEY_UP:
		out->type = (e->type == SDL_EVENT_KEY_DOWN) ? NR_EVENT_KEY_DOWN : NR_EVENT_KEY_UP;
		out->i0 = (s32)e->key.scancode;   // 物理位置，布局无关
		out->i1 = (s32)e->key.key;        // 逻辑键值，受布局影响
		out->i2 = (s32)e->key.mod;
		out->i3 = e->key.repeat ? 1 : 0;
		return TRUE;

	case SDL_EVENT_TEXT_INPUT:
		out->type = NR_EVENT_TEXT_INPUT;
		if (e->text.text != NULL)
		{
			// text 字段固定 16 字节，须保证以 0 结尾
			size_t n = SDL_strlen(e->text.text);
			if (n > sizeof(out->text) - 1) n = sizeof(out->text) - 1;
			memcpy(out->text, e->text.text, n);
			out->text[n] = '\0';
		}
		return TRUE;

	case SDL_EVENT_MOUSE_MOTION:
		out->type = NR_EVENT_MOUSE_MOVE;
		out->i0 = (s32)e->motion.x;
		out->i1 = (s32)e->motion.y;
		out->f0 = e->motion.x;
		out->f1 = e->motion.y;
		out->f2 = e->motion.xrel;  // 相对位移，相对鼠标模式下必需
		out->f3 = e->motion.yrel;
		return TRUE;

	case SDL_EVENT_MOUSE_BUTTON_DOWN:
	case SDL_EVENT_MOUSE_BUTTON_UP:
		out->type = (e->type == SDL_EVENT_MOUSE_BUTTON_DOWN)
					? NR_EVENT_MOUSE_DOWN : NR_EVENT_MOUSE_UP;
		out->i0 = (s32)e->button.button;
		out->i1 = (s32)e->button.clicks;
		out->i2 = (s32)e->button.x;
		out->i3 = (s32)e->button.y;
		out->f0 = e->button.x;
		out->f1 = e->button.y;
		return TRUE;

	case SDL_EVENT_MOUSE_WHEEL:
		out->type = NR_EVENT_MOUSE_WHEEL;
		out->f0 = e->wheel.x;
		out->f1 = e->wheel.y;
		// SDL 可能上报翻转的滚轮方向，这里原样传递由上层决定是否取反
		out->i0 = (e->wheel.direction == SDL_MOUSEWHEEL_FLIPPED) ? 1 : 0;
		return TRUE;

	case SDL_EVENT_FINGER_DOWN:
	case SDL_EVENT_FINGER_UP:
	case SDL_EVENT_FINGER_MOTION:
		out->type = (e->type == SDL_EVENT_FINGER_DOWN) ? NR_EVENT_TOUCH_DOWN
				  : (e->type == SDL_EVENT_FINGER_UP)   ? NR_EVENT_TOUCH_UP
														: NR_EVENT_TOUCH_MOVE;
		out->device_id = (u32)e->tfinger.fingerID;
		out->f0 = e->tfinger.x;        // 归一化 0..1
		out->f1 = e->tfinger.y;
		out->f2 = e->tfinger.pressure;
		out->f3 = e->tfinger.dx;
		return TRUE;

	case SDL_EVENT_GAMEPAD_ADDED:
		nrGamepadAdded(e->gdevice.which);
		out->type = NR_EVENT_GAMEPAD_ADDED;
		out->device_id = (u32)e->gdevice.which;
		return TRUE;

	case SDL_EVENT_GAMEPAD_REMOVED:
		nrGamepadRemoved(e->gdevice.which);
		out->type = NR_EVENT_GAMEPAD_REMOVED;
		out->device_id = (u32)e->gdevice.which;
		return TRUE;

	case SDL_EVENT_GAMEPAD_BUTTON_DOWN:
	case SDL_EVENT_GAMEPAD_BUTTON_UP:
		out->type = NR_EVENT_GAMEPAD_BUTTON;
		out->device_id = (u32)e->gbutton.which;
		out->i0 = (s32)e->gbutton.button;
		out->i1 = (e->type == SDL_EVENT_GAMEPAD_BUTTON_DOWN) ? 1 : 0;
		return TRUE;

	case SDL_EVENT_GAMEPAD_AXIS_MOTION:
		out->type = NR_EVENT_GAMEPAD_AXIS;
		out->device_id = (u32)e->gaxis.which;
		out->i0 = (s32)e->gaxis.axis;
		out->i1 = (s32)e->gaxis.value;
		// 归一化到 -1..1，扳机轴 SDL 只输出 0..32767，故仍落在 0..1
		out->f0 = (f32)e->gaxis.value / 32767.0f;
		return TRUE;

	case SDL_EVENT_SENSOR_UPDATE:
		out->type = NR_EVENT_SENSOR;
		out->device_id = (u32)e->sensor.which;
		out->f0 = e->sensor.data[0];
		out->f1 = e->sensor.data[1];
		out->f2 = e->sensor.data[2];
		return TRUE;

	default:
		return FALSE;
	}
}

SE_API NRResult NR_PumpEvents(u32* out_count)
{
	if (!nr_sdl_init)
		return NRR_MakeFailure(NRR_STEP_NR_Init, NRR_CODE_NOT_INITIALIZED, 0);

	u32 count = 0;
	SDL_Event e;
	NREvent evt;

	while (SDL_PollEvent(&e))
	{
		if (!nrTranslateEvent(&e, &evt))
			continue;

		++count;
		if (nr_event_cb != NULL)
			nr_event_cb(&evt, nr_event_cb_user);
	}

	if (out_count) *out_count = count;
	return NRR_MakeSuccess(NRR_STEP_NR_Init, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_SetRelativeMouseMode(b32 enable)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	if (!SDL_SetWindowRelativeMouseMode(nr_window, enable ? true : false))
		return NRR_MakeWarning(NRR_STEP_NR_CreateWindow, NRR_CODE_NOT_IMPLEMENTED, 0);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_SetCursorVisible(b32 visible)
{
	if (!nr_sdl_init)
		return NRR_MakeFailure(NRR_STEP_NR_Init, NRR_CODE_NOT_INITIALIZED, 0);

	bool ok = visible ? SDL_ShowCursor() : SDL_HideCursor();
	if (!ok)
		return NRR_MakeWarning(NRR_STEP_NR_Init, NRR_CODE_NOT_IMPLEMENTED, 0);
	return NRR_MakeSuccess(NRR_STEP_NR_Init, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_StartTextInput(void)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	SDL_StartTextInput(nr_window);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_StopTextInput(void)
{
	NR_REQUIRE_WINDOW(NRR_STEP_NR_CreateWindow);
	SDL_StopTextInput(nr_window);
	return NRR_MakeSuccess(NRR_STEP_NR_CreateWindow, NRR_CODE_SUCCESS);
}

SE_API NRResult NR_RumbleGamepad(u32 device_id, f32 low_freq, f32 high_freq, u32 duration_ms)
{
	if (!nr_sdl_init)
		return NRR_MakeFailure(NRR_STEP_NR_Init, NRR_CODE_NOT_INITIALIZED, 0);

	for (u32 i = 0; i < NR_MAX_GAMEPADS; ++i)
	{
		if (nr_gamepads[i] != NULL && (u32)nr_gamepad_ids[i] == device_id)
		{
			// ABI 用 0..1 浮点表达强度，SDL 需要 0..65535 整数
			f32 lo = (low_freq  < 0.0f) ? 0.0f : (low_freq  > 1.0f ? 1.0f : low_freq);
			f32 hi = (high_freq < 0.0f) ? 0.0f : (high_freq > 1.0f ? 1.0f : high_freq);

			if (!SDL_RumbleGamepad(nr_gamepads[i], (Uint16)(lo * 65535.0f),
								   (Uint16)(hi * 65535.0f), duration_ms))
				return NRR_MakeWarning(NRR_STEP_NR_Init, NRR_CODE_NOT_IMPLEMENTED, 0);

			return NRR_MakeSuccess(NRR_STEP_NR_Init, NRR_CODE_SUCCESS);
		}
	}

	return NRR_MakeWarning(NRR_STEP_NR_Init, NRR_CODE_INVALID_HANDLE, device_id);
}


