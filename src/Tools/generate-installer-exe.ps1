<#

Generates only the Inno Setup installer executable.

The shared release script still publishes mpv.net and prepares the same
validated x64 runtime folder used by the portable package, but skips ZIP and
GitHub release creation.

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
    SkipPortableZip = $true
    SkipGitHubRelease = $true
}

if ($MediaInfoVersion) { $argsForRelease.MediaInfoVersion = $MediaInfoVersion }
if ($MediaInfoFile) { $argsForRelease.MediaInfoFile = $MediaInfoFile }
if ($MpvNetComFile) { $argsForRelease.MpvNetComFile = $MpvNetComFile }

& (Join-Path $PSScriptRoot 'build-release-package.ps1') @argsForRelease
if ($LastExitCode) { throw $LastExitCode }
