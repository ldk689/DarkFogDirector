<#
    DarkFogDirector — one-shot build script.

    为什么不用 dotnet build：
      本机不一定装了 .NET Framework 4.7.2 targeting pack，直接用 Windows 自带的
      csc.exe 反而更省事、更可复现。csproj 仍然保留，方便在装了 VS/Rider 的机器上打开。

    用法：
      pwsh -File build.ps1
      pwsh -File build.ps1 -GameDir "D:\其他路径\Dyson Sphere Program" -BepInExDir "C:\...\profile\BepInEx"
#>
param(
    [string]$GameDir = "D:\SteamLibrary\steamapps\common\Dyson Sphere Program",
    [string]$BepInExDir = "$env:APPDATA\r2modmanPlus-local\DysonSphereProgram\profiles\Default\BepInEx"
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$managed = Join-Path $GameDir 'DSPGAME_Data\Managed'
$core = Join-Path $BepInExDir 'core'
$outDir = Join-Path $root 'bin'
$out = Join-Path $outDir 'DarkFogDirector.dll'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "找不到 csc.exe：$csc" }

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
if (Test-Path $out) { Remove-Item $out -Force }

# 游戏程序集 + Unity 模块 + BepInEx/HarmonyX
$refs = @(
    'netstandard.dll',
    'Assembly-CSharp.dll',
    'UnityEngine.dll',
    'UnityEngine.CoreModule.dll',
    'UnityEngine.InputLegacyModule.dll',
    'UnityEngine.UI.dll',
    'UnityEngine.IMGUIModule.dll',
    'UnityEngine.TextRenderingModule.dll'
) | ForEach-Object { "/r:`"$(Join-Path $managed $_)`"" }

$refs += "/r:`"$(Join-Path $core 'BepInEx.dll')`""
$refs += "/r:`"$(Join-Path $core '0Harmony.dll')`""

$srcs = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { "`"$($_.FullName)`"" }

$args = @('/nologo', '/target:library', '/optimize+', "/out:`"$out`"") + $refs + $srcs
$output = & $csc @args 2>&1
if ($LASTEXITCODE -ne 0) { $output | Write-Host; throw "编译失败" }
if ($output) { $output | Write-Host } else { Write-Host "编译成功：0 错误 / 0 警告" }

Write-Host "输出：$out ($((Get-Item $out).Length) bytes)"

# 可选：直接部署到插件目录（游戏正在运行时会失败，需先关游戏）
$pluginDir = Join-Path $BepInExDir 'plugins\DarkFogDirector'
if (Test-Path $pluginDir) {
    try {
        Copy-Item $out (Join-Path $pluginDir 'DarkFogDirector.dll') -Force
        Write-Host "已部署到 $pluginDir"
    } catch {
        Write-Warning "部署失败（游戏可能正在运行，先关游戏）：$($_.Exception.Message)"
    }
}
