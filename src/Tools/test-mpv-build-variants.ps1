<#

Validates the dual libmpv build contract without network access by default.
Pass -Online to prepare both upstream archives in one output directory and
verify their PE metadata, exports, hashes and descriptive manifest.

#>

[CmdletBinding()]
param(
    [string] $SourceDir,

    [string] $ArtifactsDir,

    [string] $SevenZipPath = 'C:\Program Files\7-Zip\7z.exe',

    [switch] $Online
)

$ErrorActionPreference = 'Stop'

if (-not $SourceDir) {
    $SourceDir = Join-Path $PSScriptRoot '..'
}

if (-not $ArtifactsDir) {
    $ArtifactsDir = Join-Path $PSScriptRoot '..\..\artifacts\mpv-build-variant-tests'
}

$SourceDir = (Resolve-Path $SourceDir).Path
New-Item -ItemType Directory -Force $ArtifactsDir | Out-Null
$ArtifactsDir = (Resolve-Path $ArtifactsDir).Path

$contractTest = Join-Path $PSScriptRoot 'test-libmpv-build-contract.ps1'
& $contractTest
if ($LastExitCode) { throw $LastExitCode }

if (-not $Online) {
    Write-Host 'Offline libmpv build contract validation completed. Pass -Online to download and validate both upstream builds.'
    return
}

. (Join-Path $PSScriptRoot 'libmpv-validation.ps1')
. (Join-Path $PSScriptRoot 'native-dependencies-config.ps1')

$preparedDir = Join-Path $ArtifactsDir 'dual-runtime'
$nativeArtifactsDir = Join-Path $ArtifactsDir 'native-dependencies'
$prepareScript = Join-Path $PSScriptRoot 'prepare-native-dependencies.ps1'

& $prepareScript `
    -SourceDir $SourceDir `
    -TargetDir $preparedDir `
    -ArtifactsDir $nativeArtifactsDir `
    -DownloadCacheDir (Get-NativeDependenciesDownloadCacheDir $SourceDir) `
    -MaxCacheAgeDays $NativeDependenciesCacheMaxAgeDays `
    -UpdateExisting `
    -SevenZipPath $SevenZipPath
if ($LastExitCode) { throw $LastExitCode }

$result = Assert-LibMpvBuilds $preparedDir
$manifestFile = Join-Path $preparedDir 'libmpv-builds.json'
if (-not (Test-Path -LiteralPath $manifestFile -PathType Leaf)) {
    throw "libmpv build manifest was not created: $manifestFile"
}

$manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
if ($manifest.normal.file -ne 'libmpv-2.dll' -or $manifest.'x86_64-v3'.file -ne 'libmpv-2-v3.dll') {
    throw "libmpv build manifest has unexpected distribution file names: $manifestFile"
}

if ($manifest.normal.sha256 -ne $result.Normal.Sha256 -or $manifest.'x86_64-v3'.sha256 -ne $result.X86_64V3.Sha256) {
    throw "libmpv build manifest hashes do not match the prepared DLLs: $manifestFile"
}

Write-Host "OK libmpv normal: $($result.Normal.File) sha256=$($result.Normal.Sha256)"
Write-Host "OK libmpv x86-64-v3: $($result.X86_64V3.File) sha256=$($result.X86_64V3.Sha256)"
