param(
    [string] $PoDirectory = "$PSScriptRoot/po",
    [string] $PotPath = "$PSScriptRoot/source.pot",
    [switch] $ValidateOnly
)

$ErrorActionPreference = 'Stop'

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    throw 'Python is required to normalize and validate PO files.'
}

$script = Join-Path $PSScriptRoot 'normalize-po-files.py'
if (-not (Test-Path $script)) {
    throw "Normalization script not found: $script"
}

$args = @(
    $script,
    '--po-directory', $PoDirectory,
    '--pot-path', $PotPath,
    '--fill-empty'
)

if ($ValidateOnly) {
    $args += '--validate-only'
}

& $python.Source @args
if ($LastExitCode) {
    throw "PO normalization/validation failed with exit code $LastExitCode"
}
