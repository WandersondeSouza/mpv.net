<#

Legacy compatibility entry point.

MediaInfo and the other build-time native/helper components are now prepared
by prepare-native-dependencies.ps1 from the repository-wide shared cache.

#>

param(
    [Parameter(Mandatory = $true)]
    [string] $SourceDir,

    [Parameter(Mandatory = $true)]
    [string] $PublishDir,

    [string] $BuildOutputDir,

    [string] $ArtifactsDir,

    [string] $MediaInfoVersion = $env:MPVNET_MEDIAINFO_VERSION,

    [string] $SevenZipPath = 'C:\Program Files\7-Zip\7z.exe'
)

$ErrorActionPreference = 'Stop'

function Test-RequiredPath([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required path not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

$SourceDir = Test-RequiredPath $SourceDir
$PublishDir = Test-RequiredPath $PublishDir
$targetDir = if ($BuildOutputDir) {
    Test-RequiredPath $BuildOutputDir
}
else {
    $PublishDir
}

$prepareScript = Test-RequiredPath (Join-Path $SourceDir 'Tools\prepare-native-dependencies.ps1')
$prepareArgs = @{
    SourceDir = $SourceDir
    TargetDir = $targetDir
    PublishDir = $PublishDir
    SevenZipPath = $SevenZipPath
}

if ($ArtifactsDir) {
    $prepareArgs.ArtifactsDir = $ArtifactsDir
}

if ($MediaInfoVersion) {
    $prepareArgs.MediaInfoVersion = $MediaInfoVersion
}

& $prepareScript @prepareArgs
if ($LastExitCode) {
    throw $LastExitCode
}

# Preserve the legacy convenience copy without creating a second download
# cache or download implementation.
$legacyCacheDir = if ($ArtifactsDir) {
    Join-Path $ArtifactsDir 'win-x64'
}
else {
    Join-Path (Split-Path $SourceDir -Parent) 'artifacts\native-dependencies\win-x64'
}

New-Item -ItemType Directory -Force $legacyCacheDir | Out-Null
Copy-Item (Join-Path $targetDir 'MediaInfo.dll') (Join-Path $legacyCacheDir 'MediaInfo.dll') -Force

Write-Host "Legacy MediaInfo dependency entry point completed through the shared native dependency cache."
