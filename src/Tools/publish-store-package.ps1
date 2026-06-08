<#

Publishes the MPV.NET Store package using one clear entry point.

The script resolves a local certificate automatically when possible and falls
back to explicit certificate parameters or environment variables.

#>

param(
    [Parameter(Position = 0, Mandatory = $true)]
    [string] $SourceDir,

    [Parameter(Position = 1, Mandatory = $true)]
    [string] $OutputRootDir,

    [string] $Configuration = 'Release',

    [string] $Platform = 'x64',

    [string] $PackageMode = 'StoreUpload',

    [ValidateSet('Temporary', 'Distribution')]
    [string] $PackageSigningMode = 'Distribution',

    [string] $PackageCertificateKeyFile = $env:MPVNET_STORE_CERTIFICATE_KEYFILE,

    [string] $PackageCertificatePassword = $env:MPVNET_STORE_CERTIFICATE_PASSWORD,

    [string] $PackagePublisher = $env:MPVNET_STORE_PUBLISHER
)

$ErrorActionPreference = 'Stop'

function Test-PathOrThrow([string] $Path) {
    if (-not (Test-Path $Path)) {
        throw "Path not found: $Path"
    }
    return (Resolve-Path $Path).Path
}

function Get-MsBuildExe {
    $vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vsWhere) {
        $instance = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\Current\Bin\MSBuild.exe
        if ($instance) {
            return $instance | Select-Object -First 1
        }
    }

    $fallback = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    if (Test-Path $fallback) { return $fallback }

    $fallback = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
    if (Test-Path $fallback) { return $fallback }

    throw 'MSBuild.exe not found. Install Visual Studio or Build Tools with Desktop Bridge/MSIX components.'
}

function Resolve-LocalCertificate([string] $SourceDir) {
    $candidateNames = @(
        'Packaging.Distribution.pfx',
        'MpvNet.Store.pfx',
        'MpvNet.Pacote.pfx',
        'SigningCertificate.pfx',
        'GitHubActionsWorkflow.pfx'
    )

    $searchRoots = @(
        (Join-Path $SourceDir 'MpvNet.Pacote'),
        $SourceDir,
        (Split-Path $SourceDir -Parent)
    ) | Select-Object -Unique

    foreach ($root in $searchRoots) {
        foreach ($candidate in $candidateNames) {
            $path = Join-Path $root $candidate
            if (Test-Path $path) {
                return (Resolve-Path $path).Path
            }
        }
    }

    return $null
}

$SourceDir = Test-PathOrThrow $SourceDir
New-Item -ItemType Directory -Force $OutputRootDir | Out-Null
$OutputRootDir = (Resolve-Path $OutputRootDir).Path

$distributionProps = Join-Path $SourceDir 'MpvNet.Pacote\Packaging.Distribution.props'
if (Test-Path $distributionProps) {
    Write-Host "Using distribution props from $distributionProps"
}
elseif (-not $PackageCertificateKeyFile) {
    $discoveredCertificate = Resolve-LocalCertificate $SourceDir
    if ($discoveredCertificate) {
        $PackageCertificateKeyFile = $discoveredCertificate
        Write-Host "Using discovered local certificate: $PackageCertificateKeyFile"
    }
}

if ($PackageSigningMode -eq 'Distribution' -and (-not $PackageCertificateKeyFile -or -not $PackagePublisher)) {
    throw 'Distribution signing requires PackageCertificateKeyFile and PackagePublisher or a Packaging.Distribution.props file.'
}

$wapProject = Join-Path $SourceDir 'MpvNet.Pacote\MpvNet.Pacote.wapproj'
Test-PathOrThrow $wapProject | Out-Null

$buildArgs = @(
    $wapProject,
    '/t:Build',
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:UapAppxPackageBuildMode=$PackageMode",
    '/p:AppxBundle=Always',
    '/p:AppxBundlePlatforms=x64',
    "/p:PackageSigningMode=$PackageSigningMode",
    "/p:OutDir=$OutputRootDir\"
)

if ($PackageCertificateKeyFile) {
    $buildArgs += "/p:PackageCertificateKeyFile=$PackageCertificateKeyFile"
}

if ($PackageCertificatePassword) {
    $buildArgs += "/p:PackageCertificatePassword=$PackageCertificatePassword"
}

if ($PackagePublisher) {
    $buildArgs += "/p:PackagePublisher=$PackagePublisher"
}

$msbuild = Get-MsBuildExe
Write-Host "Building Store package with $msbuild"
& $msbuild @buildArgs
if ($LastExitCode) { throw $LastExitCode }
