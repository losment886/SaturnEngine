# SEBuilder 平台针对性编译系统

## 概述

SEBuilder 是一个类似于 UEBuild Tool 的多平台编译工具，能够识别项目内容、设置目标平台、套用目标平台的编译规则、然后进行编译打包。

## 核心功能

### 1. 项目识别与解析

SEBuilder 能够自动识别和解析以下类型的项目：

- **.NET 项目** (.csproj)
- **原生 C/C++ MSBuild 项目** (.vcxproj)
- **CMake 项目** (CMakeLists.txt)
- **解决方案** (.sln)

#### 项目发现流程

```
1. 如果指定了项目路径，直接加载该项目
2. 如果指定了目录，自动查找：
   - .sln 文件（优先）
   - CMakeLists.txt
3. 解析项目中的配置信息：
   - 输出类型（Exe/Library/WinExe）
   - 目标框架（.NET Framework）
   - 支持的平台配置
   - AOT 发布配置
```

### 2. 平台支持

SEBuilder 目前支持以下平台：

| 平台 | 架构 | 编译工具 | 说明 |
|------|------|---------|------|
| **Windows** | x86, x64, ARM64 | MSBuild/dotnet | 支持 Visual Studio 2022 |
| **macOS** | x64, ARM64 | Xcode/CMake | 需要 Xcode 工具链 |
| **Linux** | x86, x64, ARM64 | GCC/CMake | 需要 build-essential |
| **Android** | ARM64, x64, x86 | CMake + NDK | 需要 Android NDK |
| **iOS** | ARM64 | Xcode/CMake | 仅在 macOS 上可用 |

### 3. 编译规则系统

每个平台都有针对性的编译规则：

#### Windows平台
```csharp
// .NET 编译参数
- Platform: Win32/x64/ARM64
- TargetPlatformVersion: 10.0
- RuntimeIdentifier: win-x86/win-x64/win-arm64

// CMake 参数
- CMAKE_C_COMPILER: cl.exe
- CMAKE_CXX_COMPILER: cl.exe
- CMAKE_SYSTEM_NAME: Windows
```

#### macOS平台
```csharp
// .NET 编译参数
- RuntimeIdentifier: osx-x64/osx-arm64
- OSX架构: x86_64/arm64

// CMake 参数
- CMAKE_OSX_ARCHITECTURES: x86_64/arm64
- CMAKE_OSX_DEPLOYMENT_TARGET: 10.13 (x64) / 11.0 (ARM64)
```

#### Linux平台
```csharp
// CMAKE 参数
- CMAKE_C_COMPILER: gcc
- CMAKE_CXX_COMPILER: g++
- 编译并发数: -j4
```

#### Android平台
```csharp
// CMake 参数
- CMAKE_SYSTEM_NAME: Android
- CMAKE_ANDROID_NDK: $ANDROID_NDK_ROOT
- CMAKE_ANDROID_ABI: armeabi-v7a/arm64-v8a/x86/x86_64
- Android API: 21
```

#### iOS平台
```
// CMake 参数
- CMAKE_SYSTEM_NAME: iOS
- CMAKE_OSX_ARCHITECTURES: arm64
- iOS 最低部署版本: 12.0
```

## 命令行使用

### 基本语法

```
SEBuilder <Command> [Options]
```

### 支持的命令

#### Build - 编译项目
```bash
SEBuilder build [options]

选项:
  -Project, -P      <path>      .sln 或 .csproj 文件路径
  -Target, -T       <name>      要编译的子项目名（可多次指定）
  -Platform, -Plt   <name>      目标平台: Win, Mac, Linux, Android, IOS
  -Arch, -A         <arch>      架构: x64, ARM64, x86
  -Config, -C       <config>    配置: Debug, Release, Shipping
  -AOT                         启用 AOT 发布
  -Output, -O       <path>      输出目录
  -Verbose, -V                 详细输出
```

#### Package - 打包编译产物
```bash
SEBuilder package [options]

选项:
  -Platform, -Plt   <name>      目标平台
  -Arch, -A         <arch>      架构
  -Config, -C       <config>    配置
  -Output, -O       <path>      输出目录
  -PackageOutput, -PO <path>    打包输出目录
  -IncludePdb                  包含 PDB 调试符号
```

#### Cook - 资源烘焙（预留）
```bash
SEBuilder cook [options]

选项:
  -CookSourceDir, -CSD <path>   资源源目录
  -CookOutputDir, -COD <path>   资源输出目录
```

#### Prerender - 预渲染（预留）
```bash
SEBuilder prerender [options]

选项:
  -PrerenderSource, -PRS <path>  预渲染源文件
  -PrerenderOutput, -PRO <path>  预渲染输出文件
```

#### Compress - 压缩（预留）
```bash
SEBuilder compress [options]

选项:
  -CompressionSource, -CMS <path> 压缩源文件/目录
  -CompressionOutput, -CMO <path> 压缩输出文件
```

## 使用示例

### 示例 1: 编译 Windows x64 Debug
```bash
dotnet SEBuilder.dll build -plt=win -a=x64 -c=debug
```

### 示例 2: 编译并启用 AOT，输出到自定义目录
```bash
dotnet SEBuilder.dll build -plt=win -a=x64 -c=release -aot -o=./output
```

### 示例 3: 编译特定项目到 macOS
```bash
dotnet SEBuilder.dll build -plt=mac -a=arm64 -c=release -t=SaturnEngine
```

### 示例 4: 编译 Android ARM64 Release
```bash
dotnet SEBuilder.dll build -plt=android -a=arm64 -c=release
```

### 示例 5: 编译多个特定项目
```bash
dotnet SEBuilder.dll build -t=SaturnEngine -t=SEBuilder -plt=linux -a=x64 -c=release
```

### 示例 6: 详细输出编译信息
```bash
dotnet SEBuilder.dll build -plt=win -a=x64 -c=debug -verbose
```

## 架构设计

### 核心组件

```
SEBuilder/
├── SECore/
│   ├── SEBuildCommand.cs       命令行解析，参数管理
│   ├── SEPlatform.cs           平台信息定义
│   ├── SEPlatformBase.cs       平台编译基类
│   ├── SEProjectInfo.cs        项目解析和发现
│   └── SEBuildManager.cs       编译流程管理
├── SEPlatform/
│   ├── SEPlatformFactory.cs    平台工厂
│   ├── Windows/
│   │   └── WinPlatform.cs      Windows 平台实现
│   ├── Mac/
│   │   └── MacPlatform.cs      macOS 平台实现
│   ├── Linux/
│   │   └── LinuxPlatform.cs    Linux 平台实现
│   ├── Android/
│   │   └── AndroidPlatform.cs  Android 平台实现
│   └── IOS/
│       └── IOSPlatform.cs      iOS 平台实现
└── Program.cs                   主程序入口
```

### 编译流程

```
1. 命令行解析
   └─ SEBuildCommand.Parse()

2. 项目发现与解析
   └─ SEProjectResolver.ParseSolution/ParseCsproject/ParseVcxproj

3. 平台创建
   └─ SEPlatformFactory.CreatePlatform()
      └─ 返回对应平台的实现类

4. 环境验证
   └─ SEPlatformBase.ValidateToolchain()

5. 编译执行
   └─ SEBuildManager.Build()
      ├─ 获取目标项目列表
      ├─ 遍历每个项目
      ├─ 根据项目类型执行编译
      │  ├─ .NET 项目: BuildDotNetProject() / PublishDotNetProject()
      │  ├─ MSBuild 项目: BuildWithMSBuild()
      │  └─ CMake 项目: ConfigureCMake() + BuildCMake()
      └─ 输出编译结果
```

## 平台特性

### AOT（Head-of-Time）编译

当指定 `-aot` 选项时，SEBuilder 会启用 .NET AOT 发布，这提供：
- 更快的启动时间
- 更小的代码体积
- 不需要 .NET 运行时

### 交叉编译检测

SEBuilder 能够检测是否需要执行交叉编译：
```csharp
// 宿主平台与目标平台不同 → 交叉编译
// 宿主架构与目标架构不同 → 交叉编译
// 否则 → 本地编译
```

### 配置支持

- **Debug**: 用于开发，包含调试符号
- **Release**: 优化的发布版本
- **Shipping**: 最高优化级别的发布版本

## 工具链要求

### Windows
- Visual Studio 2022（包含 MSBuild）
- .NET SDK 8.0 或更高版本
- Windows Target Platform 10.0

### macOS
- Xcode 命令行工具
- CMake 3.20+
- .NET SDK 8.0 或更高版本

### Linux
- GCC/G++ 编译器
- CMake 3.20+
- .NET SDK 8.0 或更高版本
- build-essential

### Android
- Android NDK（设置 ANDROID_NDK_ROOT 环境变量）
- CMake 3.20+
- .NET SDK 8.0 或更高版本

### iOS
- macOS 系统
- Xcode
- CMake 3.20+

## 扩展与定制

### 添加新平台支持

1. 在 `SEPlatform/<PlatformName>/` 创建文件
2. 继承 `SEPlatformBase` 类
3. 实现所需的抽象方法
4. 在 `SEPlatformFactory.CreatePlatform()` 中注册

```csharp
public class MyPlatform : SEPlatformBase
{
    public override string Name => "My Platform";
    
    public override string[] GetDotNetBuildArgs() { }
    public override string[] GetCMakeConfigureArgs() { }
    public override string[] GetCMakeBuildArgs() { }
    public override Dictionary<string, string> GetNativeEnvironmentVariables() { }
    public override string GetOutputSubDir() { }
    public override bool ValidateToolchain(out string? errorMessage) { }
}
```

### 自定义编译参数

在各平台类中修改 `Get*Args()` 方法，可以自定义编译参数。

## 已知限制与计划

### 当前状态
- ✅ 平台识别与编译规则
- ✅ 项目发现与解析
- ✅ 命令行参数解析
- ✅ .NET 与 CMake 编译支持
- ✅ 多项目并行管理
- ⏳ 打包功能（Package）
- ⏳ 资源烘焙（Cook）
- ⏳ 预渲染（Prerender）
- ⏳ 压缩功能（Compress）

### 计划改进
1. 实现打包系统
2. 添加资源处理管道
3. 支持插件系统
4. 添加增量编译支持
5. 集成版本管理
6. 性能优化与分析

## 故障排除

### 问题：找不到工具链

**解决方案**：
- Windows: 安装 Visual Studio 2022 和 .NET SDK
- macOS: 运行 `xcode-select --install`
- Linux: 运行 `sudo apt-get install build-essential cmake`
- Android: 设置 `ANDROID_NDK_ROOT` 环境变量

### 问题：编译失败

**解决方案**：
1. 使用 `-verbose` 选项查看详细日志
2. 检查项目配置是否正确
3. 验证目标平台和架构是否支持

### 问题：交叉编译问题

**解决方案**：
- 确保已安装必要的交叉编译工具链
- 检查 CMake 配置是否正确
- 查看编译输出中的具体错误信息

## 常见问题（FAQ）

**Q: 能否同时为多个平台编译？**
A: 可以，但需要逐条运行命令。未来考虑支持配置文件同时指定多个目标。

**Q: 编译产物存在哪里？**
A: 默认在 `{ProjectRoot}/BuildOutput/{Platform}/{Config}/` 目录下。可使用 `-output` 指定自定义位置。

**Q: 是否支持自定义编译流程？**
A: 目前通过继承 `SEPlatformBase` 可定制平台编译规则。完整的自定义流程支持待实现。

**Q: 能否用于 CI/CD 流程？**
A: 可以，通过指定完整路径和参数，可集成到任何 CI/CD 系统。

## 参考资源

- [.NET 运行时标识符](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog)
- [CMake 文档](https://cmake.org/cmake/help/latest/)
- [MSBuild 参考](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-reference)
- [Android NDK](https://developer.android.com/ndk)

