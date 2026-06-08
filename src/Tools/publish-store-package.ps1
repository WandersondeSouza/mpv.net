<#

Single entry point for publishing the MPV.NET Store package.

This script resolves the local certificate automatically when possible and
falls back to the explicit certificate parameters or environment variables.

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

$SourceDir = (Resolve-Path $SourceDir).Path
$helperArgs = @($SourceDir, $OutputRootDir, $Configuration, $Platform, $PackageMode, $PackageSigningMode)

if (-not $PackageCertificateKeyFile) {
    $autoCertificate = & (Join-Path $PSScriptRoot 'find-store-certificate.ps1') $SourceDir
    if ($LASTEXITCODE -eq 0 -and $autoCertificate) {
        $PackageCertificateKeyFile = $autoCertificate.Trim()
        Write-Host "Auto-selected certificate: $PackageCertificateKeyFile"
    }
}

& (Join-Path $PSScriptRoot 'build-store-package.ps1') `
    -SourceDir $SourceDir `
    -OutputRootDir $OutputRootDir `
    -Configuration $Configuration `
    -Platform $Platform `
    -PackageMode $PackageMode `
    -PackageSigningMode $PackageSigningMode `
    -PackageCertificateKeyFile $PackageCertificateKeyFile `
    -PackageCertificatePassword $PackageCertificatePassword `
    -PackagePublisher $PackagePublisher

if ($LastExitCode) { throw $LastExitCode }
