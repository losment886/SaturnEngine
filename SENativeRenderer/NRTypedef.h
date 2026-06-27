#pragma once

#if defined(__APPLE__)
typedef int8_t			s8;
typedef int16_t			s16;
typedef int				s32;
typedef long long		s64;
typedef uint8_t			u8;
typedef uint16_t		u16;
typedef unsigned int	u32;
typedef uint64_t		u64;
typedef float			f32;
typedef double			f64;
typedef uint8_t			byte;
typedef uint32_t		b32;
#else
typedef int8_t      s8;
typedef int16_t     s16;
typedef int32_t     s32;
typedef int64_t     s64;
typedef uint8_t     u8;
typedef uint16_t    u16;
typedef uint32_t    u32;
typedef uint64_t    u64;
typedef float       f32;
typedef double      f64;
typedef uint8_t     byte;
typedef uint32_t    b32;
#endif


//64位宽的结果类型，包含步骤码、严重性、错误码和系统错误码，便于溯源与纠错
typedef u64 	    NRResult;
//渲染API类型，如Vulkan、OpenGL等
typedef s32		    NRGraphicsAPI;
typedef u64 	    NRVersion;
typedef u64			NRResourceID;
typedef u32			NRResourceType;
typedef u32			NRGameObjectType;
//渲染类型，比如2d与3d
typedef u32			NRGraphicsType;

