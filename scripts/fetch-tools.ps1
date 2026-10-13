# Downloads the ffmpeg and pandoc executables into thirdparty\bin.
# ReType finds them there at runtime (see ToolRunner.Locate).
param(
    [string]$FfmpegVersion = "9.0.2",
    [string]$PandocVersion = "3.12.1"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$binDir = Join-Path $repoRoot "thirdparty\bin"
New-Item -ItemType Directory -Force $binDir | Out-Null

function Get-ExecutableFromZip([string]$url, [string]$entrySuffix, [string]$destName) {
    $zip = Join-Path $env:TEMP ("retype-tools-" + [guid]::NewGuid() + ".zip")
    Write-Host "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -like "*$entrySuffix" } | Select-Object -First 1
        if (-not $entry) { throw "Entry *$entrySuffix not found in $url" }
        $target = Join-Path $binDir $destName
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        Write-Host "  -> $target"
    } finally {
        $archive.Dispose()
        Remove-Item $zip -ErrorAction SilentlyContinue
    }
}

Get-ExecutableFromZip `
    "https://github.com/GyanD/codexffmpeg/releases/download/$FfmpegVersion/ffmpeg-$FfmpegVersion-essentials_build.zip" `
    "bin/ffmpeg.exe" "ffmpeg.exe"

Get-ExecutableFromZip `
    "https://github.com/jgm/pandoc/releases/download/$PandocVersion/pandoc-$PandocVersion-windows-x86_64.zip" `
    "pandoc.exe" "pandoc.exe"

Write-Host "Done. Tools are in $binDir"
