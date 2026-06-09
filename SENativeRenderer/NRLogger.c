#include "NRDefine.h"


SE_API const char* NR_ResultToString(NRResult result)
{
	u8 severity = NRR_GetSeverity(result);
	u8 stepcode = NRR_GetStepCode(result);
	u16 code = NRR_GetCode(result);
	u32 systemcode = NRR_GetSystemCode(result);
	static char buffer[256];
	snprintf(buffer, sizeof(buffer), "NRResult: Severity=%u, StepCode=%u, Code=%u, SystemCode=%u", severity, stepcode, code, systemcode);
	return buffer;
}
SE_API const char* NR_GetLastError()
{
	if (NRR_SUCCESS(nr_last_result))
	{
		return "No error.";
	}
	else
	{
		static char buffer[512];
		snprintf(buffer, sizeof(buffer), "Last SDL error: %s (%s)", nr_last_sdl_error_msg, NR_ResultToString(nr_last_result));
		return buffer;
	}
}



NRResult nr_OnError(NRResult result)
{
	nr_last_result = result;
	if (NRR_FAILED(result))
	{
		nr_last_sdl_error_msg = SDL_GetError();
	}
	else
	{
		nr_last_sdl_error_msg = "No error.";
	}
	return result;
}