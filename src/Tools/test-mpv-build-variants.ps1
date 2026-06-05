<#

Prepares native/helper dependencies for both supported mpv/libmpv build
variants and validates that the expected files are present and x64.

This is a packaging smoke test. Interactive playback checks still need to be
run manually from a generated package on the target CPU.

#>

param(
    [string] $SourceDir = (Join-Path $PSScriptRoot '..'),

    [string] $ArtifactsDir = (Join-Path $PSScriptRoot '..\..\artifacts\mpv-build-variant-tests'),

    [string[]] $Variants = @('normal', 'x86_64-v3'),

    [string] $SevenZipPath = 'C:\Program Files\7-Zip\7z.exe'
)

$ErrorActionPreference = 'Stop'

$SourceDir = (Resolve-Path $SourceDir).Path
New-Item -ItemType Directory -Force $ArtifactsDir | Out-Null
$ArtifactsDir = (Resolve-Path $ArtifactsDir).Path

$prepareScript = Join-Path $PSScriptRoot 'prepare-native-dependencies.ps1'
$requiredPreparedFiles = @(
    'libmpv-2.dll',
    'MediaInfo.dll',
    'ffmpeg.exe',
    'ffplay.exe',
    'ffprobe.exe',
    'yt-dlp.exe'
)

function Test-RequiredFile($path) {
    if (-not (Test-Path $path)) {
        throw "Required prepared file not found: $path"
    }

    $file = Get-Item $path
    if ($file.Length -le 0) {
        throw "Required prepared file is empty: $path"
    }

    return $file
}

function Assert-PeX64($path) {
    $file = Test-RequiredFile $path
    $stream = [System.IO.File]::OpenRead($file.FullName)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -le 0 -or $peOffset -gt ($stream.Length - 6)) {
            throw "Invalid PE header in $($file.FullName)"
        }

        $stream.Position = $peOffset
        $signature = $reader.ReadUInt32()
        if ($signature -ne 0x00004550) {
            throw "Invalid PE signature in $($file.FullName)"
        }

        $machine = $reader.ReadUInt16()
        if ($machine -ne 0x8664) {
            throw "Expected x64 native binary, got machine 0x$($machine.ToString('X4')): $($file.FullName)"
        }
    }
    finally {
        $stream.Dispose()
    }

    return $file
}

foreach ($variant in $Variants) {
    if ($variant -notin @('normal', 'x86_64-v3')) {
        throw "Unsupported mpv build variant: $variant"
    }

    $variantDir = Join-Path $ArtifactsDir $variant
    $nativeArtifactsDir = Join-Path $ArtifactsDir "native-$variant"
    New-Item -ItemType Directory -Force $variantDir | Out-Null

    & $prepareScript `
        -SourceDir $SourceDir `
        -TargetDir $variantDir `
        -ArtifactsDir $nativeArtifactsDir `
        -MpvBuildVariant $variant `
        -UpdateExisting `
        -SevenZipPath $SevenZipPath
    if ($LastExitCode) { throw $LastExitCode }

    foreach ($file in $requiredPreparedFiles) {
        Assert-PeX64 (Join-Path $variantDir $file) | Out-Null
    }

    $markerFile = Join-Path $variantDir 'libmpv-2.variant.txt'
    if (-not (Test-Path $markerFile)) {
        throw "Variant marker was not created: $markerFile"
    }

    $marker = (Get-Content $markerFile -Raw).Trim()
    if ($marker -ne $variant) {
        throw "Expected marker '$variant', got '$marker' in $markerFile"
    }

    Write-Host "OK mpv/libmpv build variant: $variant"
}
