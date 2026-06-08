<#

Deprecated compatibility wrapper.

Use publish-store-package.ps1 as the single supported entry point.

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
Write-Warning 'build-store-package-auto.ps1 is deprecated. Use publish-store-package.ps1 instead.'

& (Join-Path $PSScriptRoot 'publish-store-package.ps1') `
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
