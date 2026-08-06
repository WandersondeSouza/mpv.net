<#

Validates the offline contract shared by the libmpv preparation and artifact
validators. This test deliberately has no network dependency.

#>

[CmdletBinding()]
param(
    [string] $ContractFile
)

$ErrorActionPreference = 'Stop'

if (-not $ContractFile) {
    $ContractFile = Join-Path $PSScriptRoot 'libmpv-build-contract.psd1'
}

if (-not (Test-Path -LiteralPath $ContractFile -PathType Leaf)) {
    throw "libmpv build contract was not found: $ContractFile"
}

$contract = Import-PowerShellDataFile -LiteralPath $ContractFile

if ($contract.SchemaVersion -ne 1) {
    throw "Unsupported libmpv build contract schema: $($contract.SchemaVersion)"
}

foreach ($variantName in @('Normal', 'X86_64V3')) {
    $variant = $contract[$variantName]
    if (-not $variant) {
        throw "Missing libmpv build contract entry: $variantName"
    }

    foreach ($propertyName in @('FileName', 'AssetRegex', 'CachePattern')) {
        if ([string]::IsNullOrWhiteSpace([string] $variant[$propertyName])) {
            throw "Missing $propertyName for libmpv build contract entry: $variantName"
        }
    }
}

if ($contract.Normal.FileName -ne 'libmpv-2.dll') {
    throw "The normal libmpv file must keep its compatibility name. Got: $($contract.Normal.FileName)"
}

if ($contract.X86_64V3.FileName -ne 'libmpv-2-v3.dll') {
    throw "Unexpected x86-64-v3 libmpv file name: $($contract.X86_64V3.FileName)"
}

$normalAsset = 'mpv-dev-x86_64-20260610-git-304426c.7z'
$v3Asset = 'mpv-dev-x86_64-v3-20260610-git-304426c.7z'

if ($normalAsset -notmatch $contract.Normal.AssetRegex) {
    throw "The normal asset regex does not accept a normal asset: $normalAsset"
}

if ($v3Asset -match $contract.Normal.AssetRegex) {
    throw "The normal asset regex must not accept the x86-64-v3 asset: $v3Asset"
}

if ($v3Asset -notmatch $contract.X86_64V3.AssetRegex) {
    throw "The x86-64-v3 asset regex does not accept a v3 asset: $v3Asset"
}

$requiredExports = @($contract.RequiredExports)
if ($requiredExports.Count -lt 9) {
    throw 'The libmpv build contract must list all required exports.'
}

foreach ($export in @(
    'mpv_client_api_version',
    'mpv_create',
    'mpv_initialize',
    'mpv_command',
    'mpv_command_string',
    'mpv_get_property',
    'mpv_set_property',
    'mpv_wait_event',
    'mpv_terminate_destroy')) {
    if ($requiredExports -notcontains $export) {
        throw "Required libmpv export is missing from the contract: $export"
    }
}

Write-Host "libmpv dual-build contract is valid: normal=$($contract.Normal.FileName), v3=$($contract.X86_64V3.FileName)"
