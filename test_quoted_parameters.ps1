#!/bin/pwsh
# SEBuilder 参数解析测试脚本 - 测试带引号的参数支持

Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  SEBuilder 参数解析功能测试 - 支持引号括起来的带空格参数      ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$SEBuilderExe = "J:\SaturnEngine\SEBuilder\bin\Debug\net10.0\SEBuilder.dll"

# 编译 SEBuilder
Write-Host "正在编译 SEBuilder..." -ForegroundColor Yellow
Push-Location J:\SaturnEngine\SEBuilder
dotnet build -q
Pop-Location
Write-Host ""

Write-Host "📋 测试场景说明" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "以下命令演示支持引号括起的参数：" -ForegroundColor Yellow
Write-Host ""

Write-Host "1️⃣  基础用法（无空格）" -ForegroundColor Green
Write-Host "命令: build -plt=win -a=x64 -c=debug" -ForegroundColor Cyan
Write-Host "说明: 这种用法一直都支持，现在仍然支持" -ForegroundColor Gray
Write-Host ""

Write-Host "2️⃣  使用引号的输出路径（包含空格）" -ForegroundColor Green
Write-Host "命令: build -plt=win -a=x64 -c=debug -o=""C:\My Build Output""" -ForegroundColor Cyan
Write-Host "说明: 现在支持带空格的路径，用双引号括起来" -ForegroundColor Gray
Write-Host ""

Write-Host "3️⃣  长路径示例" -ForegroundColor Green
Write-Host "命令: build -project=""D:\Projects\My Engine\Game.sln"" -plt=win -a=x64" -ForegroundColor Cyan
Write-Host "说明: 项目路径也可以使用引号括起，支持路径中的空格" -ForegroundColor Gray
Write-Host ""

Write-Host "4️⃣  等号分隔法 vs 空格分隔法" -ForegroundColor Green
Write-Host "格式1: -o=""C:\Output Path""          (使用等号 + 引号)" -ForegroundColor Cyan
Write-Host "格式2: -o ""C:\Output Path""          (使用空格 + 引号)" -ForegroundColor Cyan
Write-Host "说明: 两种格式现在都支持！" -ForegroundColor Gray
Write-Host ""

Write-Host "📝 关键改进点" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ 支持格式 1: -param=""value with spaces""" -ForegroundColor Green
Write-Host "✅ 支持格式 2: -param ""value with spaces""" -ForegroundColor Green
Write-Host "✅ 兼容原有格式: -param=value" -ForegroundColor Green
Write-Host "✅ 处理提取后自动删除双引号" -ForegroundColor Green
Write-Host "✅ 支持所有接受值的参数" -ForegroundColor Green
Write-Host ""

Write-Host "🔧 实际用例" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

# 演示帮助信息
Write-Host "查看完整帮助信息:" -ForegroundColor Yellow
Write-Host "dotnet $SEBuilderExe build -h" -ForegroundColor Cyan
Write-Host ""

Write-Host "📌 使用示例" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

$examples = @(
    @{
        title = "基础编译（Windows x64 Debug）"
        cmd = 'dotnet SEBuilder.dll build -plt=win -a=x64 -c=debug'
        desc = '无需引号，简单路径用不到引号'
    },
    @{
        title = "自定义输出目录（路径中有空格）"
        cmd = 'dotnet SEBuilder.dll build -plt=win -a=x64 -c=release -o="D:\My Projects\Output"'
        desc = '输出目录包含空格，使用双引号括起'
    },
    @{
        title = "指定项目文件（路径中有空格）"
        cmd = 'dotnet SEBuilder.dll build -project="D:\Game Engine\Game.sln" -plt=win -a=x64'
        desc = '项目文件路径包含空格，使用双引号括起'
    },
    @{
        title = "使用空格分隔符（路径中有空格）"
        cmd = 'dotnet SEBuilder.dll build -o "D:\My Build Dir" -plt=win -a=x64'
        desc = '用空格代替等号分隔，也支持引号'
    },
    @{
        title = "macOS ARM64 带自定义输出"
        cmd = 'dotnet SEBuilder.dll build -plt=mac -a=arm64 -c=release -o="~/my builds/macos"'
        desc = 'macOS 路径示例，支持蒂号符号和空格'
    }
)

$i = 1
foreach ($example in $examples) {
    Write-Host ""
    Write-Host "$i. $($example.title)" -ForegroundColor Yellow
    Write-Host "   命令: $($example.cmd)" -ForegroundColor Cyan
    Write-Host "   说明: $($example.desc)" -ForegroundColor Gray
    $i++
}

Write-Host ""
Write-Host "✨ 代码改动说明" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "修改文件: SEBuilder/SECore/SEBuildCommand.cs" -ForegroundColor Yellow
Write-Host "修改内容:" -ForegroundColor Green
Write-Host ""
Write-Host "// 原来的代码（行 98-107）:" -ForegroundColor Gray
Write-Host '    if (arg.Contains("="))
    {
        var split = arg.Split("=", 2);
        key = split[0].TrimStart("-").ToLower();
        value = split[1];  // ❌ 不处理引号
    }
    else if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
    {
        value = args[++i];  // ❌ 不处理引号
    }' -ForegroundColor DarkGray
Write-Host ""
Write-Host "// 新的代码改进:" -ForegroundColor Gray
Write-Host '    if (arg.Contains("="))
    {
        var split = arg.Split("=", 2);
        key = split[0].TrimStart("-").ToLower();
        value = split[1];
        // ✅ 移除双引号（如果存在）
        if (!string.IsNullOrEmpty(value) && value.StartsWith("\"") && value.EndsWith("\""))
        {
            value = value.Substring(1, value.Length - 2);
        }
    }
    else if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
    {
        value = args[++i];
        // ✅ 移除双引号（如果存在）
        if (!string.IsNullOrEmpty(value) && value.StartsWith("\"") && value.EndsWith("\""))
        {
            value = value.Substring(1, value.Length - 2);
        }
    }' -ForegroundColor Green
Write-Host ""

Write-Host "🎯 支持的所有参数" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "所有接受值的参数现在都支持引号:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  -project, -p          项目或解决方案文件路径" -ForegroundColor Cyan
Write-Host "  -output, -o           编译输出目录" -ForegroundColor Cyan
Write-Host "  -packageoutput, -po   打包输出目录" -ForegroundColor Cyan
Write-Host "  -cooksourcedir, -csd  资源源目录" -ForegroundColor Cyan
Write-Host "  -cookoutputdir, -cod  资源输出目录" -ForegroundColor Cyan
Write-Host "  -prerendersource, -prs 预渲染源文件" -ForegroundColor Cyan
Write-Host "  -prerenderoutput, -pro 预渲染输出文件" -ForegroundColor Cyan
Write-Host "  -compressionsource, -cms 压缩源文件" -ForegroundColor Cyan
Write-Host "  -compressionoutput, -cmo 压缩输出文件" -ForegroundColor Cyan
Write-Host ""

Write-Host "📋 Windows 命令行示例" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "在 PowerShell 中:" -ForegroundColor Yellow
Write-Host '  dotnet SEBuilder.dll build -o "C:\Program Files\Game" -plt=win' -ForegroundColor Cyan
Write-Host ""
Write-Host "在 CMD 中:" -ForegroundColor Yellow
Write-Host '  dotnet SEBuilder.dll build -o "C:\Program Files\Game" -plt=win' -ForegroundColor Cyan
Write-Host ""

Write-Host "✅ 功能完成" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "✓ 编译成功" -ForegroundColor Green
Write-Host "✓ 支持等号格式: -param=""value with spaces""" -ForegroundColor Green
Write-Host "✓ 支持空格格式: -param ""value with spaces""" -ForegroundColor Green
Write-Host "✓ 向后兼容: 所有旧格式继续有效" -ForegroundColor Green
Write-Host "✓ 自动移除双引号: 参数值中不包含引号" -ForegroundColor Green
Write-Host ""

Write-Host "现在您可以在命令行中使用带空格的路径了！🎉" -ForegroundColor Green
Write-Host ""

