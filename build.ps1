<#
.SYNOPSIS
    生成可独立运行的单文件 exe（目标机器不需要安装 .NET 运行时）。

.DESCRIPTION
    默认输出到 dist\BatteryMonitor.exe。使用 self-contained + PublishSingleFile
    + IncludeNativeLibrariesForSelfExtract，产物是一个 exe，不依赖本机 dotnet。

    用法：
        .\build.ps1                  # 打包
        .\build.ps1 -Run             # 打包后直接启动
        .\build.ps1 -Clean           # 先清掉 bin/obj/dist 再打包
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    [switch]$Run,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'BatteryMonitor.csproj'
$outDir = Join-Path $root 'dist'
$exe = Join-Path $outDir 'BatteryMonitor.exe'

if (-not (Test-Path $project)) {
    throw "找不到项目文件：$project"
}

if ($Clean) {
    Write-Host '清理 bin / obj / dist ...' -ForegroundColor DarkGray
    foreach ($dir in @('bin', 'obj', 'dist')) {
        $path = Join-Path $root $dir
        if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    }
}

Write-Host "打包 $Runtime 单文件版本 ..." -ForegroundColor Cyan

$publishArgs = @(
    'publish', $project
    '-c', 'Release'
    '-r', $Runtime
    '--self-contained', 'true'
    '-p:PublishSingleFile=true'
    '-p:IncludeNativeLibrariesForSelfExtract=true'
    '-p:EnableCompressionInSingleFile=true'
    '-p:DebugType=None'
    '-p:DebugSymbols=false'
    '-p:SatelliteResourceLanguages=zh-Hans'
    '-o', $outDir
    '--nologo'
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败（退出码 $LASTEXITCODE）"
}

# 单文件模式下还会有少量附属文件，清掉只留 exe
Get-ChildItem $outDir -File | Where-Object {
    $_.Name -ne 'BatteryMonitor.exe' -and
    $_.Name -notlike '*.pdb'
} | Remove-Item -Force
Get-ChildItem $outDir -Filter '*.pdb' -File | Remove-Item -Force

if (-not (Test-Path $exe)) {
    throw "打包结束但没找到 $exe"
}

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ''
Write-Host "完成：$exe（$size MB）" -ForegroundColor Green
Write-Host '这是自包含单文件，拷到任何 Windows 机器上都能直接运行。' -ForegroundColor DarkGray

if ($Run) {
    Write-Host '启动程序 ...' -ForegroundColor Cyan
    Start-Process $exe
}
