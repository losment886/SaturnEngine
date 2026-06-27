# CMakeList.txt: SENativeRenderer - 跨平台原生渲染器动态库
# 在 Windows 上生成 SENativeRenderer.dll
# 在 macOS 上生成 libSENativeRenderer.dylib
# 在 Linux 上生成 libSENativeRenderer.so
#
cmake_minimum_required(VERSION 3.10)

# 启用 MSVC 热重载策略
if(POLICY CMP0141)
  cmake_policy(SET CMP0141 NEW)
  set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT
          "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")
endif()

project(SENativeRenderer
        LANGUAGES C CXX
        VERSION 1.0.0
)


set(CMAKE_C_STANDARD 23)
set(CMAKE_CXX_STANDARD 23)

# ============================================================
# 平台检测（使用目标系统名，支持交叉编译）
# ============================================================
message(STATUS "[SENativeRenderer] Host: ${CMAKE_HOST_SYSTEM_NAME}, Target: ${CMAKE_SYSTEM_NAME}")

# Prefer explicit target system name when cross-compiling
if(DEFINED CMAKE_SYSTEM_NAME)
  string(TOLOWER "${CMAKE_SYSTEM_NAME}" SE__SYSTEM_LOWER)
  if(SE__SYSTEM_LOWER STREQUAL "windows")
    set(SE_PLATFORM_WINDOWS TRUE)
    message(STATUS "[SENativeRenderer] Platform: Windows (target)")
  elseif(SE__SYSTEM_LOWER STREQUAL "darwin" OR SE__SYSTEM_LOWER STREQUAL "macos")
    set(SE_PLATFORM_MACOS TRUE)
    message(STATUS "[SENativeRenderer] Platform: macOS (target)")
  elseif(SE__SYSTEM_LOWER STREQUAL "linux")
    set(SE_PLATFORM_LINUX TRUE)
    message(STATUS "[SENativeRenderer] Platform: Linux (target)")
  endif()
endif()

# Fallback to host-detected macros if target detection didn't set any
if(NOT DEFINED SE_PLATFORM_WINDOWS AND NOT DEFINED SE_PLATFORM_MACOS AND NOT DEFINED SE_PLATFORM_LINUX)
  if(WIN32)
    set(SE_PLATFORM_WINDOWS TRUE)
    message(STATUS "[SENativeRenderer] Platform: Windows (host)")
  elseif(APPLE)
    set(SE_PLATFORM_MACOS TRUE)
    message(STATUS "[SENativeRenderer] Platform: macOS (host)")
  elseif(UNIX AND NOT APPLE)
    set(SE_PLATFORM_LINUX TRUE)
    message(STATUS "[SENativeRenderer] Platform: Linux (host)")
  endif()
endif()

# ============================================================
# 查找 Vulkan SDK（必需）
# find_package 会设置：
#   Vulkan_INCLUDE_DIRS
#   Vulkan_LIBRARIES
# ============================================================
find_package(Vulkan REQUIRED)
message(STATUS "[SENativeRenderer] Vulkan found:")
message(STATUS "  Include: ${Vulkan_INCLUDE_DIRS}")
message(STATUS "  Library: ${Vulkan_LIBRARIES}")

# ============================================================
# 查找 SDL3
# SDL3 随 Vulkan SDK 一起安装，在同一个 Include/ 和 Lib/ 目录下
# 我们通过 Vulkan_INCLUDE_DIRS 的父目录来定位 SDL3
# ============================================================
if(Vulkan_INCLUDE_DIRS)
  # Vulkan_INCLUDE_DIRS 通常是 ".../Include" 或 ".../include/vulkan"
  # 取第一个包含路径的父目录作为 SDK 根目录
  list(GET Vulkan_INCLUDE_DIRS 0 SE_VULKAN_INCLUDE_DIR)

  # 查找 SDL3 头文件
  find_path(SDL3_INCLUDE_DIR
          NAMES SDL3/SDL.h
          HINTS
          "${SE_VULKAN_INCLUDE_DIR}"        # 直接就在 Include/ 下
          "${SE_VULKAN_INCLUDE_DIR}/.."     # 父目录
          "$ENV{VULKAN_SDK}/Include"
          "$ENV{VULKAN_SDK}/include"
          PATH_SUFFIXES
          include
          Include
  )

  # 查找 SDL3 库文件
  find_library(SDL3_LIBRARY
          NAMES SDL3 SDL3d SDL3-static SDL3-staticd
          HINTS
          "${SE_VULKAN_INCLUDE_DIR}/../Lib"
          "${SE_VULKAN_INCLUDE_DIR}/../lib"
          "$ENV{VULKAN_SDK}/Lib"
          "$ENV{VULKAN_SDK}/lib"
          PATH_SUFFIXES
          lib
          Lib
  )

  if(SDL3_INCLUDE_DIR AND SDL3_LIBRARY)
    set(SDL3_FOUND TRUE)
    message(STATUS "[SENativeRenderer] SDL3 found:")
    message(STATUS "  Include: ${SDL3_INCLUDE_DIR}")
    message(STATUS "  Library: ${SDL3_LIBRARY}")
  else()
    set(SDL3_FOUND FALSE)
    message(STATUS "[SENativeRenderer] SDL3 NOT found")
    message(STATUS "  Searched in: ${SE_VULKAN_INCLUDE_DIR}/..")
    message(STATUS "  VULKAN_SDK: $ENV{VULKAN_SDK}")
  endif()
endif()

message(STATUS "C compiler: ${CMAKE_C_COMPILER}")
message(STATUS "C++ compiler: ${CMAKE_CXX_COMPILER}")

# ============================================================
# 源文件 C
# ============================================================
set(SE_RENDERER_SOURCES
        NRWindow.c
        NRLogger.c
        NRVec.c
        NRShaderCompiler.c
        NRRenderData.c
        NRGlobal.c
        NRVA.c
)

set(SE_RENDERER_HEADERS
        NRDefine.h
        NRTypedef.h
        NRVec.h
        NRRenderData.h
        NRShaderCompiler.h
        NRShaderSource.h
        )

# ============================================================
# 源文件 CPP
# ============================================================
set(SE_RENDERER_SOURCES_CPP
        BasicMetal/BasicMetal.cpp
        )

set(SE_RENDERER_HEADERS_CPP
        BasicMetal/BasicMetal.hpp
        )

# ============================================================
# 创建动态库（SHARED）
# 输出：
#   Windows → SENativeRenderer.dll (+ .lib + .pdb)
#   macOS   → libSENativeRenderer.dylib
#   Linux   → libSENativeRenderer.so
# ============================================================
add_library(${PROJECT_NAME} SHARED
        ${SE_RENDERER_SOURCES}
        ${SE_RENDERER_HEADERS}
        ${SE_RENDERER_SOURCES_CPP}
        ${SE_RENDERER_HEADERS_CPP}
)

# 设置导出宏，让 C 代码知道它在构建动态库
target_compile_definitions(${PROJECT_NAME} PRIVATE SE_BUILD_SHARED)

# ============================================================
# 包含目录
# ============================================================
target_include_directories(${PROJECT_NAME} PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${Vulkan_INCLUDE_DIRS}
        ${CMAKE_SOURCE_DIR}/ThirdParty/metal-cpp
)



if(SDL3_FOUND)
  target_include_directories(${PROJECT_NAME} PRIVATE ${SDL3_INCLUDE_DIR})
endif()

# ============================================================
# 链接库
# ============================================================
target_link_libraries(${PROJECT_NAME} PRIVATE Vulkan::Vulkan)

if(SDL3_FOUND)
  target_link_libraries(${PROJECT_NAME} PRIVATE ${SDL3_LIBRARY})
endif()

# 平台特定链接
if(SE_PLATFORM_MACOS)
  target_link_libraries(${PROJECT_NAME} PRIVATE
          "-framework Cocoa"
          "-framework QuartzCore"
          "-framework Metal"
          "-framework IOKit"
          "-framework Foundation"
          "-framework CoreVideo"
          "-framework CoreGraphics"
  )
endif()

if(SE_PLATFORM_WINDOWS)
  target_link_libraries(${PROJECT_NAME} PRIVATE
          user32
          gdi32
          shell32
  )
endif()

# ============================================================
# 编译器选项
# ============================================================
target_compile_options(${PROJECT_NAME} PRIVATE
        $<$<C_COMPILER_ID:MSVC>:/W3 /utf-8>
        $<$<NOT:$<C_COMPILER_ID:MSVC>>:-Wall -Wextra>
)

# 设置 C 语言标准
set_target_properties(${PROJECT_NAME} PROPERTIES
        C_STANDARD 17
        C_STANDARD_REQUIRED ON
        C_EXTENSIONS OFF
        POSITION_INDEPENDENT_CODE ON
)

# ============================================================
# 输出目录设置
# 将动态库输出到 SaturnEngine/bin/$(Configuration) 目录
# 这样所有 C# 项目都可以引用此目录下的库文件
# ============================================================
# 计算项目根目录（CMakeLists.txt 的父目录）
get_filename_component(SE_PROJECT_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/.." ABSOLUTE)
set(SE_OUTPUT_BASE "${SE_PROJECT_ROOT}/SaturnEngine/bin")

# 针对多配置生成器（Visual Studio, Xcode）分别设置
foreach(OUTPUTCONFIG ${CMAKE_CONFIGURATION_TYPES})
  string(TOUPPER ${OUTPUTCONFIG} SE_CONFIG_UPPER)
  set(SE_OUTPUT_DIR "${SE_OUTPUT_BASE}/${OUTPUTCONFIG}")

  set_target_properties(${PROJECT_NAME} PROPERTIES
          LIBRARY_OUTPUT_DIRECTORY_${SE_CONFIG_UPPER} "${SE_OUTPUT_DIR}"
          RUNTIME_OUTPUT_DIRECTORY_${SE_CONFIG_UPPER} "${SE_OUTPUT_DIR}"
          ARCHIVE_OUTPUT_DIRECTORY_${SE_CONFIG_UPPER} "${SE_OUTPUT_DIR}"
  )
endforeach()

# 针对单配置生成器（Ninja, Makefile）
set_target_properties(${PROJECT_NAME} PROPERTIES
        LIBRARY_OUTPUT_DIRECTORY "${SE_OUTPUT_BASE}/$<CONFIG>"
        RUNTIME_OUTPUT_DIRECTORY "${SE_OUTPUT_BASE}/$<CONFIG>"
        ARCHIVE_OUTPUT_DIRECTORY "${SE_OUTPUT_BASE}/$<CONFIG>"
)

# macOS 上设置 install_name 以便运行时加载
if(SE_PLATFORM_MACOS)
  set_target_properties(${PROJECT_NAME} PROPERTIES
          INSTALL_NAME_DIR "@rpath"
          BUILD_WITH_INSTALL_RPATH TRUE
          INSTALL_RPATH "@executable_path;@loader_path;${SE_OUTPUT_BASE}/$<CONFIG>"
  )
endif()

# ============================================================
# 构建后命令：打印输出路径
# ============================================================
add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMAND ${CMAKE_COMMAND} -E echo "[SENativeRenderer] Build completed!"
        COMMAND ${CMAKE_COMMAND} -E echo "[SENativeRenderer] Output: $<TARGET_FILE:${PROJECT_NAME}>"
        COMMAND ${CMAKE_COMMAND} -E echo ""
        COMMENT ""
)

# ============================================================
# 安装步骤
# ============================================================
install(TARGETS ${PROJECT_NAME}
        LIBRARY DESTINATION "${SE_OUTPUT_BASE}/$<CONFIG>"
        RUNTIME DESTINATION "${SE_OUTPUT_BASE}/$<CONFIG>"
        ARCHIVE DESTINATION "${SE_OUTPUT_BASE}/$<CONFIG>"
)

# ============================================================
# 配置摘要
# ============================================================
message(STATUS "========================================")
message(STATUS "[SENativeRenderer] Configuration Summary")
message(STATUS "  Platform: ${CMAKE_SYSTEM_NAME} (${CMAKE_SYSTEM_PROCESSOR})")
message(STATUS "  Output Type: Shared Library")
message(STATUS "  Output Name: ${CMAKE_SHARED_LIBRARY_PREFIX}${PROJECT_NAME}${CMAKE_SHARED_LIBRARY_SUFFIX}")
message(STATUS "  Output Dir: ${SE_OUTPUT_BASE}/<CONFIG>")
message(STATUS "  Vulkan: ${Vulkan_FOUND}")
message(STATUS "  Vulkan Include: ${Vulkan_INCLUDE_DIRS}")
if(SDL3_FOUND)
  message(STATUS "  SDL3: ${SDL3_FOUND}")
  message(STATUS "  SDL3 Include: ${SDL3_INCLUDE_DIR}")
  message(STATUS "  SDL3 Library: ${SDL3_LIBRARY}")
else()
  message(STATUS "  SDL3: NOT found (will use search path)")
endif()
message(STATUS "========================================")