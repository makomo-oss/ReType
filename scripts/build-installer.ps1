# Builds a self-contained single-file exe and compiles the Inno Setup installer.
# Output: dist\ReType-Setup-<version>.exe
param([string]$Version = "0.1.5")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# 1. Conversion tools (ffmpeg / pandoc) into thirdparty\bin.
if (-not (Test-Path "$root\thirdparty\bin\ffmpeg.exe") -or
    -not (Test-Path "$root\thirdparty\bin\pandoc.exe")) {
    & "$root\scripts\fetch-tools.ps1"
}

# 2. Self-contained publish (no .NET runtime needed on the target machine).
dotnet publish src/ReType.App -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -o "$root\publish"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Single-file publish does not bundle None items; place the tools next to the exe.
New-Item -ItemType Directory -Force "$root\publish\thirdparty\bin" | Out-Null
Copy-Item "$root\thirdparty\bin\*.exe" "$root\publish\thirdparty\bin\" -Force

# 3. Compile the installer.
$iscc = @(
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup"
}

& $iscc "installer\ReType.iss" "/DMyAppVersion=$Version"
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

Get-ChildItem "$root\dist\ReType-Setup-*.exe" | Select-Object Name, Length
