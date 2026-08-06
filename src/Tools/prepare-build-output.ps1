<#

Prepares the build output folder so mpvnet.exe can be launched directly from
Visual Studio or from `dotnet build` in Debug and Release configurations.

This script ensures native/helper binaries and gettext Locale catalogs are
available beside mpvnet.exe.

#>

param(
    [Parameter(Mandatory = $true)]
    [string] $SourceDir,

    [Parameter(Mandatory = $true)]
    [string] $TargetDir,

    [string] $ArtifactsDir,

    [string] $MediaInfoVersion = $env:MPVNET_MEDIAINFO_VERSION,

    [ValidateSet('normal', 'x86_64-v3')]
    [string] $MpvBuildVariant = $(if ($env:MPVNET_MPV_BUILD_VARIANT) { $env:MPVNET_MPV_BUILD_VARIANT } else { 'normal' }),

    [string] $SevenZipPath = 'C:\Program Files\7-Zip\7z.exe'
)

$ErrorActionPreference = 'Stop'

function Test-RequiredPath($path) {
    if (-not (Test-Path $path)) {
        throw "Required path not found: $path"
    }

    return (Resolve-Path $path).Path
}

function Test-RequiredFile($path) {
    if (-not (Test-Path $path)) {
        throw "Required file not found: $path"
    }

    $file = Get-Item $path
    if ($file.Length -le 0) {
        throw "Required file is empty: $path"
    }

    return $file
}

function New-CleanDir($path) {
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
    }

    New-Item -ItemType Directory -Force $path | Out-Null
    return Test-RequiredPath $path
}

function Invoke-FileDownload($uri, $outputFile) {
    Write-Host "Downloading $uri"
    Invoke-WebRequest -Uri $uri -UserAgent 'mpv.net-build-assets' -OutFile $outputFile -UseBasicParsing
    return Test-RequiredFile $outputFile
}

function AddGettextToolsToPath($workDir) {
    if (Get-Command msgfmt -ErrorAction SilentlyContinue) {
        return
    }

    $packagesDir = New-CleanDir (Join-Path $workDir 'gettext-tools')
    $index = Invoke-WebRequest `
        -Uri 'https://api.nuget.org/v3-flatcontainer/gettext.tools/index.json' `
        -UseBasicParsing |
        ConvertFrom-Json
    $version = @($index.versions)[-1]
    if (-not $version) {
        throw 'Could not resolve the latest Gettext.Tools package version from NuGet.'
    }

    $packageFile = Join-Path $workDir "gettext.tools.$version.nupkg"
    Invoke-FileDownload `
        "https://api.nuget.org/v3-flatcontainer/gettext.tools/$version/gettext.tools.$version.nupkg" `
        $packageFile | Out-Null

    $zipFile = Join-Path $workDir "gettext.tools.$version.zip"
    Copy-Item $packageFile $zipFile -Force
    Expand-Archive -LiteralPath $zipFile -DestinationPath $packagesDir -Force

    $toolBinDir = Get-ChildItem $packagesDir -Filter 'msgfmt.exe' -Recurse -File |
        Select-Object -First 1 |
        ForEach-Object { $_.DirectoryName }

    if (-not $toolBinDir) {
        throw "Gettext.Tools was installed, but msgfmt.exe was not found in $packagesDir"
    }

    $env:Path = "$toolBinDir;$env:Path"
    Test-RequiredFile (Join-Path $toolBinDir 'msgfmt.exe') | Out-Null
}

$SourceDir = Test-RequiredPath $SourceDir
New-Item -ItemType Directory -Force $TargetDir | Out-Null
$TargetDir = Test-RequiredPath $TargetDir

if (-not $ArtifactsDir) {
    $ArtifactsDir = Join-Path (Split-Path $SourceDir -Parent) 'artifacts\build-assets'
}

$ArtifactsDir = Test-RequiredPath (New-Item -ItemType Directory -Force $ArtifactsDir).FullName
$LocaleWorkDir = New-CleanDir (Join-Path $ArtifactsDir 'locale')

$ensureNativeScript = Test-RequiredFile (Join-Path $SourceDir 'Tools\prepare-native-dependencies.ps1')
$ensureNativeArgs = @{
    SourceDir = $SourceDir
    TargetDir = $TargetDir
    ArtifactsDir = (Join-Path $ArtifactsDir "native-dependencies-$PID")
    DownloadCacheDir = (Join-Path $ArtifactsDir 'native-dependencies\downloads')
    SevenZipPath = $SevenZipPath
    MpvBuildVariant = $MpvBuildVariant
}

if ($MediaInfoVersion) {
    $ensureNativeArgs.MediaInfoVersion = $MediaInfoVersion
}

& $ensureNativeScript @ensureNativeArgs
if ($LastExitCode) { throw $LastExitCode }

AddGettextToolsToPath $LocaleWorkDir

$createMoScript = Test-RequiredFile (Join-Path (Split-Path $SourceDir -Parent) 'lang\compile-mo-files.ps1')
& $createMoScript $TargetDir
if ($LastExitCode) { throw $LastExitCode }

$localeDir = Test-RequiredPath (Join-Path $TargetDir 'Locale')
$ptBrCatalog = Test-RequiredFile (Join-Path $localeDir 'pt_BR\LC_MESSAGES\mpvnet.mo')

Write-Host "Build assets are ready: $TargetDir"
Write-Host "Brazilian Portuguese catalog: $($ptBrCatalog.FullName)"
