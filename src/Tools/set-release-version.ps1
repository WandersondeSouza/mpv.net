<#

Sets the public MPV.NET release version in every version surface that must
store a literal value.

BuildVersion.props remains the canonical source for the Windows executable,
portable ZIP and Inno Setup installer. The MSIX manifest also needs a literal
Identity Version, so this script updates both files together.

#>

param(
    [string] $Version,

    [switch] $IncrementRevision
)

$ErrorActionPreference = 'Stop'

function Get-VersionFile {
    return (Resolve-Path (Join-Path $PSScriptRoot '..\BuildVersion.props')).Path
}

function Get-PackageManifestFile {
    return (Resolve-Path (Join-Path $PSScriptRoot '..\MpvNet.Pacote\Package.appxmanifest')).Path
}

function Read-MpvNetVersion([string] $VersionFile) {
    [xml] $versionXml = Get-Content -LiteralPath $VersionFile
    return [version] $versionXml.Project.PropertyGroup.MpvNetVersion
}

function Assert-FourPartVersion([version] $ParsedVersion) {
    if ($ParsedVersion.Build -lt 0 -or $ParsedVersion.Revision -lt 0) {
        throw "Release version must have four numeric parts, for example 7.1.3.14."
    }
}

if ($Version -and $IncrementRevision) {
    throw 'Use either -Version or -IncrementRevision, not both.'
}

if (-not $Version -and -not $IncrementRevision) {
    throw 'Pass -Version <major.minor.build.revision> or -IncrementRevision.'
}

$versionFile = Get-VersionFile
$manifestFile = Get-PackageManifestFile

if ($IncrementRevision) {
    $currentVersion = Read-MpvNetVersion $versionFile
    Assert-FourPartVersion $currentVersion
    $nextVersion = [version]::new(
        $currentVersion.Major,
        $currentVersion.Minor,
        $currentVersion.Build,
        $currentVersion.Revision + 1)
}
else {
    $nextVersion = [version] $Version
    Assert-FourPartVersion $nextVersion
}

$nextVersionText = $nextVersion.ToString()

$versionText = [System.IO.File]::ReadAllText($versionFile)
$updatedVersionText = [regex]::Replace(
    $versionText,
    '<MpvNetVersion>[^<]+</MpvNetVersion>',
    "<MpvNetVersion>$nextVersionText</MpvNetVersion>",
    1)
if ($updatedVersionText -eq $versionText -and $versionText -notmatch "<MpvNetVersion>$nextVersionText</MpvNetVersion>") {
    throw "MpvNetVersion node not found in $versionFile."
}

[System.IO.File]::WriteAllText($versionFile, $updatedVersionText, [System.Text.UTF8Encoding]::new($true))

$manifestText = [System.IO.File]::ReadAllText($manifestFile)
if ($manifestText -notmatch '(?s)<Identity\b[^>]*\bVersion="[^"]+"') {
    throw "Package Identity Version attribute not found in $manifestFile."
}

$updatedManifestText = [regex]::Replace(
    $manifestText,
    '(?s)(<Identity\b[^>]*\bVersion=")[^"]+(")',
    "`${1}$nextVersionText`${2}",
    1)
[System.IO.File]::WriteAllText($manifestFile, $updatedManifestText, [System.Text.UTF8Encoding]::new($true))

Write-Host "Release version set to $nextVersionText"
Write-Output $nextVersionText
