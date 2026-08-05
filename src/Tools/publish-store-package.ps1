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

    [string] $PackagePublisher = $env:MPVNET_STORE_PUBLISHER,

    [switch] $AllowTemporarySigningFallback
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

function New-TemporarySigningCertificate([string] $SourceDir) {
    $certDir = Join-Path $SourceDir 'artifacts\store-temp'
    New-Item -ItemType Directory -Force $certDir | Out-Null

    $certPath = Join-Path $certDir 'MpvNet.Pacote_TemporaryKey.pfx'
    $passwordText = 'MpvNetTemporarySigning!123'
    $password = ConvertTo-SecureString $passwordText -AsPlainText -Force

    $subject = 'CN=MPV.NET Temporary Signing'
    $cert = New-SelfSignedCertificate `
        -Subject $subject `
        -Type Custom `
        -KeyUsage DigitalSignature `
        -KeyExportPolicy Exportable `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears(5)

    Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $password | Out-Null
    return @{
        Path = $certPath
        Password = $passwordText
        Subject = $subject
        Thumbprint = $cert.Thumbprint
    }
}

$SourceDir = Test-PathOrThrow $SourceDir
New-Item -ItemType Directory -Force $OutputRootDir | Out-Null
$OutputRootDir = (Resolve-Path $OutputRootDir).Path

if ($PackageCertificateKeyFile) {
    $PackageCertificateKeyFile = Test-PathOrThrow $PackageCertificateKeyFile
}

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
    if ($AllowTemporarySigningFallback) {
        Write-Warning 'Distribution signing was requested, but no certificate or publisher was provided. Falling back to Temporary signing for local MSIX generation.'
        $PackageSigningMode = 'Temporary'
        $temporaryCert = New-TemporarySigningCertificate $SourceDir
        $PackageCertificateKeyFile = $temporaryCert.Path
        $PackageCertificatePassword = $temporaryCert.Password
        $PackageCertificateThumbprint = $temporaryCert.Thumbprint
        $PackagePublisher = $null
        Write-Host "Temporary signing certificate created: $($temporaryCert.Path)"
    }
    else {
        throw 'Distribution signing requires PackageCertificateKeyFile and PackagePublisher or a Packaging.Distribution.props file.'
    }
}

if ($PackageSigningMode -eq 'Temporary' -and -not $PackageCertificateKeyFile) {
    $temporaryCert = New-TemporarySigningCertificate $SourceDir
    $PackageCertificateKeyFile = $temporaryCert.Path
    $PackageCertificatePassword = $temporaryCert.Password
    $PackageCertificateThumbprint = $temporaryCert.Thumbprint
    Write-Host "Temporary signing certificate created: $($temporaryCert.Path)"
}

$wapProject = Join-Path $SourceDir 'MpvNet.Pacote\MpvNet.Pacote.wapproj'
Test-PathOrThrow $wapProject | Out-Null

$buildArgs = @(
    $wapProject,
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

if ($PackageCertificateThumbprint) {
    $buildArgs += "/p:PackageCertificateThumbprint=$PackageCertificateThumbprint"
}

if ($PackagePublisher) {
    $buildArgs += "/p:PackagePublisher=$PackagePublisher"
}

$msbuild = Get-MsBuildExe
Write-Host "Validating Store package with $msbuild"
& $msbuild @($buildArgs + '/t:ValidateStorePackage')
if ($LastExitCode) { throw $LastExitCode }

Write-Host "Building Store package with $msbuild"
& $msbuild @($buildArgs + '/t:Build')
if ($LastExitCode) { throw $LastExitCode }

$packageContentValidationScript = Test-PathOrThrow (Join-Path $SourceDir 'Tools\validate-package-contents.ps1')
$storePackages = @(Get-ChildItem -LiteralPath $OutputRootDir -Recurse -File |
    Where-Object { $_.Extension -in @('.msixupload', '.appxupload', '.msixbundle', '.appxbundle') })

if (-not $storePackages.Count) {
    $storePackages = @(Get-ChildItem -LiteralPath $OutputRootDir -Recurse -File |
        Where-Object {
            $_.Extension -in @('.msix', '.appx') -and
            $_.FullName -notmatch '[\\/]Dependencies[\\/]'
        })
}

if (-not $storePackages.Count) {
    throw "No Microsoft Store package was generated under $OutputRootDir"
}

foreach ($storePackage in $storePackages) {
    & $packageContentValidationScript `
        -ArchiveFile $storePackage.FullName `
        -RequiredRelativePaths 'MpvNet.Windows\mpvnet.exe', 'MpvNet.Windows\Scripts\osc.lua'
    if ($LastExitCode) { throw $LastExitCode }
}
