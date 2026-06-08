<#

Builds the MPV.NET Store package using a locally discovered certificate when
no explicit certificate path is provided.

This is a convenience wrapper around build-store-package.ps1 for local use.

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
$autoCertificate = & (Join-Path $PSScriptRoot 'find-store-certificate.ps1') $SourceDir
if (-not $PackageCertificateKeyFile -and $LASTEXITCODE -eq 0 -and $autoCertificate) {
    $PackageCertificateKeyFile = $autoCertificate.Trim()
    Write-Host "Auto-selected certificate: $PackageCertificateKeyFile"
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
