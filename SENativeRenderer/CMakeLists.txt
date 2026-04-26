# CMakeList.txt: SENativeRenderer 的 CMake 项目，在此处包括源代码并定义
# 项目特定的逻辑。
#
cmake_minimum_required (VERSION 3.10)

# 如果支持，请为 MSVC 编译器启用热重载。
if (POLICY CMP0141)
  cmake_policy(SET CMP0141 NEW)
  set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")
endif()

project ("SENativeRenderer")

# 将源代码添加到此项目的可执行文件。
add_executable (SENativeRenderer "NRWindow.c" "NRVulkanBasic.c" "NRDefine.h")

find_package(Vulkan REQUIRED)

target_include_directories(${PROJECT_NAME} PRIVATE ${Vulkan_INCLUDE_DIRS})
target_link_libraries(${PROJECT_NAME} PRIVATE Vulkan::Vulkan)


if (CMAKE_VERSION VERSION_GREATER 3.12)
  set_property(TARGET SENativeRenderer PROPERTY C_STANDARD 17)
endif()

# TODO: 如有需要，请添加测试并安装目标。
