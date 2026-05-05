#!/bin/pwsh
# SEBuilder Test Script - 演示平台针对性编译功能

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "SEBuilder 平台针对性编译功能测试" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$BuildDir = "J:\SaturnEngine"
$SEBuilderExe = "J:\SaturnEngine\SEBuilder\bin\Debug\net10.0\SEBuilder.dll"

# 确保生成了 SEBuilder
if (-not (Test-Path $SEBuilderExe)) {
    Write-Host "正在编译 SEBuilder..." -ForegroundColor Yellow
    Push-Location $BuildDir
    dotnet build SEBuilder\SEBuilder.csproj -c Debug
    Pop-Location
}

Write-Host ""
Write-Host "测试 1: 显示使用帮助" -ForegroundColor Green
Write-Host "命令: dotnet $SEBuilderExe build -h" -ForegroundColor Yellow
dotnet $SEBuilderExe build -h
Write-Host ""

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "测试 2: 编译 Windows x64 Debug" -ForegroundColor Green
Write-Host "命令: dotnet $SEBuilderExe build -plt=win -a=x64 -c=debug" -ForegroundColor Yellow
# dotnet $SEBuilderExe build -plt=win -a=x64 -c=debug -v
Write-Host ""

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "测试 3: 编译特定项目" -ForegroundColor Green
Write-Host "命令: dotnet $SEBuilderExe build -plt=win -a=x64 -c=debug -t=SaturnEngine -v" -ForegroundColor Yellow
# dotnet $SEBuilderExe build -plt=win -a=x64 -c=debug -t=SaturnEngine -v
Write-Host ""

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "功能测试尚未完全实现，以下命令可用于您的项目：" -ForegroundColor Green
Write-Host ""
Write-Host "1. 编译所有项目到 Windows x64 Release:" -ForegroundColor Yellow
Write-Host "   dotnet $SEBuilderExe build -plt=win -a=x64 -c=release" -ForegroundColor Gray
Write-Host ""
Write-Host "2. 编译 macOS ARM64 Release:" -ForegroundColor Yellow
Write-Host "   dotnet $SEBuilderExe build -plt=mac -a=arm64 -c=release -aot" -ForegroundColor Gray
Write-Host ""
Write-Host "3. 编译特定项目到 Linux:" -ForegroundColor Yellow
Write-Host "   dotnet $SEBuilderExe build -plt=linux -a=x64 -c=release -t=SaturnEngine" -ForegroundColor Gray
Write-Host ""
Write-Host "4. 编译 Android ARM64:" -ForegroundColor Yellow
Write-Host "   dotnet $SEBuilderExe build -plt=android -a=arm64 -c=release" -ForegroundColor Gray
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan

