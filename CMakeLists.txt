cmake_minimum_required(VERSION 3.10)

# ============================================================
# 顶层 CMakeLists.txt
# 目的：让 JetBrains Rider for macOS（以及 CLion）能识别
# 整个 SaturnEngine 项目结构，包括：
#   - SENativeRenderer（C 原生动态库）
#   - SaturnEngine（C# 项目，Rider 自动识别）
#   - 其他 C# 子项目
#
# 在 Windows 上，Visual Studio 使用 .sln + .vcxproj
# 在 macOS 上，Rider 通过此文件识别 C/C++ 部分
# ============================================================

project(SaturnEngine
  LANGUAGES C
  VERSION 1.0.0
  DESCRIPTION "Saturn Engine - Game Engine"
)

# ============================================================
# 构建输出目录
# 所有原生输出都放到 SaturnEngine/bin/ 下
# 这样 C# 项目可以通过 P/Invoke 直接引用
# ============================================================
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/SaturnEngine/bin/${CMAKE_BUILD_TYPE}")
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/SaturnEngine/bin/${CMAKE_BUILD_TYPE}")
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/SaturnEngine/bin/${CMAKE_BUILD_TYPE}")

# 多配置支持（Debug, Release 等）
foreach(CONF ${CMAKE_CONFIGURATION_TYPES})
  string(TOUPPER ${CONF} CONF_UPPER)
  set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_${CONF_UPPER} "${CMAKE_SOURCE_DIR}/SaturnEngine/bin/${CONF}")
  set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_${CONF_UPPER} "${CMAKE_SOURCE_DIR}/SaturnEngine/bin/${CONF}")
  set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY_${CONF_UPPER} "${CMAKE_SOURCE_DIR}/SaturnEngine/bin/${CONF}")
endforeach()

# ============================================================
# 添加原生渲染器子项目
# 编译生成：
#   Windows: SaturnEngine/bin/Debug/SENativeRenderer.dll
#   macOS:   SaturnEngine/bin/Debug/libSENativeRenderer.dylib
# ============================================================
add_subdirectory(SENativeRenderer)

# ============================================================
# 自定义 target 用于组合构建
# 在 Rider for macOS 上，可以一键构建所有原生代码
# ============================================================
add_custom_target(BuildAll
  COMMAND ${CMAKE_COMMAND} --build ${CMAKE_BINARY_DIR}
  COMMENT "Building all native libraries..."
)

# 同时构建原生和 .NET 项目
add_custom_target(BuildAllWithDotNet
  COMMAND ${CMAKE_COMMAND} --build ${CMAKE_BINARY_DIR}
  COMMAND dotnet build "${CMAKE_SOURCE_DIR}/SaturnEngine.sln"
  COMMENT "Building both native library and C# projects..."
)

message(STATUS "========================================")
message(STATUS "SaturnEngine Top-Level CMake")
message(STATUS "  Source dir: ${CMAKE_SOURCE_DIR}")
message(STATUS "  Build  dir: ${CMAKE_BINARY_DIR}")
message(STATUS "  Output dir: ${CMAKE_LIBRARY_OUTPUT_DIRECTORY}")
message(STATUS "========================================")
