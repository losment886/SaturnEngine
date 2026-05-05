# SEBuilder 快速开始指南

## 5 分钟上手

### 步骤 1: 编译 SEBuilder

```bash
cd J:\SaturnEngine\SEBuilder
dotnet build -c Release
```

### 步骤 2: 运行第一个编译命令

```bash
# 显示帮助信息
dotnet bin\Release\net10.0\SEBuilder.dll build -h

# 编译 Windows x64 Debug
dotnet bin\Release\net10.0\SEBuilder.dll build -plt=win -a=x64 -c=debug

# 编译 macOS ARM64 Release（在 macOS 上）
dotnet bin\Release\net10.0\SEBuilder.dll build -plt=mac -a=arm64 -c=release
```

### 步骤 3: 使用常见选项

```bash
# 启用详细输出
dotnet SEBuilder.dll build -plt=win -a=x64 -c=debug -verbose

# 启用 AOT 编译
dotnet SEBuilder.dll build -plt=win -a=x64 -c=release -aot

# 指定输出目录
dotnet SEBuilder.dll build -plt=win -a=x64 -c=debug -output=./MyOutput

# 只编译特定项目
dotnet SEBuilder.dll build -plt=win -a=x64 -c=debug -t=SaturnEngine

# 编译多个项目
dotnet SEBuilder.dll build -t=SaturnEngine -t=SEBuilder -plt=win -a=x64
```

## 常用命令速查表

| 目标 | 命令 |
|------|------|
| Windows Debug | `build -plt=win -a=x64 -c=debug` |
| Windows Release | `build -plt=win -a=x64 -c=release` |
| Windows AOT | `build -plt=win -a=x64 -c=release -aot` |
| macOS | `build -plt=mac -a=arm64 -c=release` |
| Linux | `build -plt=linux -a=x64 -c=release` |
| Android | `build -plt=android -a=arm64 -c=release` |
| iOS | `build -plt=ios -c=release` |

## 环境配置

### Windows
```powershell
# 可选：添加到 PATH 便于全局使用
$env:PATH += ";C:\your\path\to\SEBuilder\bin\Release\net10.0"
```

### macOS/Linux
```bash
# 可选：创建别名
alias sebuilder="dotnet /path/to/SEBuilder.dll"

# 然后可以直接使用
sebuilder build -plt=linux -a=x64 -c=release
```

## 输出目录结构

```
BuildOutput/
├── Windows/
│   ├── Debug/
│   ├── Release/
│   └── Shipping/
├── Mac/
│   └── Release/
├── Linux/
│   └── Release/
├── Android/
│   └── Release/
└── iOS/
    └── Release/
```

## 故障排除

### 编译失败常见原因

1. **工具链未安装**
   - Windows: 安装 Visual Studio 2022
   - macOS: 运行 `xcode-select --install`
   - Linux: 运行 `sudo apt-get install build-essential`

2. **项目配置问题**
   - 检查 .csproj 或 .sln 文件是否正确
   - 确保目标项目存在

3. **权限问题**
   - 确保有写入输出目录的权限
   - 确保有读取项目文件的权限

### 查看详细日志

```bash
# 使用 -verbose 或 -v 选项
dotnet SEBuilder.dll build -plt=win -a=x64 -c=debug -verbose
```

## 高级用法

### 脚本化编译

创建 `build_all.ps1`:
```powershell
$platforms = @(
    @{plt="win"; a="x64"; c="release"}
    @{plt="mac"; a="arm64"; c="release"}
    @{plt="linux"; a="x64"; c="release"}
)

foreach ($p in $platforms) {
    Write-Host "Building $($p.plt)..."
    & dotnet SEBuilder.dll build -plt=$p.plt -a=$p.a -c=$p.c -v
}
```

运行: `.\build_all.ps1`

### 结合 CI/CD

```yaml
# GitHub Actions example
- name: Build Windows
  run: dotnet SEBuilder.dll build -plt=win -a=x64 -c=release

- name: Build macOS
  run: dotnet SEBuilder.dll build -plt=mac -a=arm64 -c=release
```

## 获取帮助

### 显示完整帮助
```bash
dotnet SEBuilder.dll build -h
```

### 查看当前配置
```bash
dotnet SEBuilder.dll build -plt=win -v -c=debug --dryrun
```

## 下一步

- 📖 查看 [完整文档](./SEBuilder_Documentation.md)
- ⚙️ 参考 [配置文件示例](./SEBuilder.config.json)
- 🧪 运行 [测试脚本](./test_sebuilder.ps1)
- 🔧 了解 [平台扩展](./SEBuilder_Documentation.md#扩展与定制)

## 系统要求

### 最低要求
- .NET SDK 8.0+
- 4GB RAM
- 2GB 磁盘空间

### 推荐配置
- .NET SDK 8.0+
- 8GB+ RAM
- 平台特定工具链（见下文）

### 工具链要求

| 平台 | 工具 | 版本 |
|------|------|------|
| Windows | Visual Studio / MSBuild | 2022 17.0+ |
| macOS | Xcode | 14.0+ |
| Linux | GCC/CMake | 9.0+/3.20+ |
| Android | Android NDK | 21.0+ |
| iOS | Xcode | 14.0+ |

## 常见问题

**Q: 编译需要多长时间？**
A: 首次编译通常需要 15-30 分钟（取决于项目大小和硬件）。后续增量编译会快得多。

**Q: 是否支持分布式编译？**
A: 暂不支持，但可以考虑使用系统的编译缓存。

**Q: 能否自定义编译规则？**
A: 可以通过修改各平台类的实现来自定义编译规则。

**Q: 如何处理编译错误？**
A: 使用 `-verbose` 选项查看详细日志，或检查项目配置。

## 资源链接

- 🌐 [SaturnEngine 主页](.)
- 📚 [完整 API 文档](./SEBuilder_Documentation.md)
- 🐛 [报告问题](.)
- 💬 [社区讨论](.)

---

**最后更新**: 2026-05-02 | **版本**: 1.0.0

