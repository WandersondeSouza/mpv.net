<#

Generates only the portable x64 ZIP package.

The shared release script publishes mpv.net, prepares native/helper binaries,
reuses downloads cached for up to two days in artifacts/native-dependencies,
and validates the final ZIP.

#>

param(
    [string] $SourceDir = (Join-Path $PSScriptRoot '..'),

    [string] $OutputRootDir = (Join-Path $PSScriptRoot '..\..\artifacts\release'),

    [string] $MediaInfoVersion = $env:MPVNET_MEDIAINFO_VERSION,

    [string] $MediaInfoFile,

    [string] $MpvNetComFile
)

$ErrorActionPreference = 'Stop'

$SourceDir = (Resolve-Path $SourceDir).Path
New-Item -ItemType Directory -Force $OutputRootDir | Out-Null
$OutputRootDir = (Resolve-Path $OutputRootDir).Path

$argsForRelease = @{
    SourceDir = $SourceDir
    OutputRootDir = $OutputRootDir
    SkipInstaller = $true
    SkipGitHubRelease = $true
}

if ($MediaInfoVersion) { $argsForRelease.MediaInfoVersion = $MediaInfoVersion }
if ($MediaInfoFile) { $argsForRelease.MediaInfoFile = $MediaInfoFile }
if ($MpvNetComFile) { $argsForRelease.MpvNetComFile = $MpvNetComFile }

& (Join-Path $PSScriptRoot 'build-release-package.ps1') @argsForRelease
if ($LastExitCode) { throw $LastExitCode }
