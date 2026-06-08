<#

Finds a local certificate that can be used to sign the MPV.NET Store package.

This helper searches the most common locations used by this repository and
prints the first match. It does not modify any files.

Usage:
    powershell -ExecutionPolicy Bypass -File .\src\Tools\find-store-certificate.ps1 .\src

#>

param(
    [Parameter(Position = 0, Mandatory = $true)]
    [string] $SourceDir
)

$ErrorActionPreference = 'Stop'

function Test-PathOrThrow([string] $Path) {
    if (-not (Test-Path $Path)) {
        throw "Path not found: $Path"
    }
    return (Resolve-Path $Path).Path
}

function Find-StoreCertificate([string] $SourceDir) {
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
$certificate = Find-StoreCertificate $SourceDir

if ($certificate) {
    Write-Host $certificate
    exit 0
}

Write-Host 'No local Store certificate found.'
exit 1
