<#

Offline checks for published SHA-256 parsing and downloaded-file validation.

#>

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'native-dependencies-config.ps1')

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("mpvnet-native-digest-test-" + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Force $testRoot | Out-Null
    $file = Join-Path $testRoot 'asset.bin'
    [System.IO.File]::WriteAllText($file, 'abc', [System.Text.Encoding]::ASCII)

    $expected = 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad'
    if ((Get-NativeDependenciesPublishedSha256 "sha256:$expected") -ne $expected) {
        throw 'A valid GitHub SHA-256 digest was not normalized correctly.'
    }

    if (-not (Assert-NativeDependenciesFileSha256 $file "sha256:$expected")) {
        throw 'A valid downloaded file was not accepted.'
    }

    if (Assert-NativeDependenciesFileSha256 $file '') {
        throw 'A missing upstream digest was reported as validated.'
    }

    $mismatchRejected = $false
    try {
        Assert-NativeDependenciesFileSha256 $file ('0' * 64) | Out-Null
    }
    catch {
        $mismatchRejected = $true
    }
    if (-not $mismatchRejected) {
        throw 'A mismatched downloaded file was accepted.'
    }

    $malformedDigestRejected = $false
    try {
        Get-NativeDependenciesPublishedSha256 'sha512:bad' | Out-Null
    }
    catch {
        $malformedDigestRejected = $true
    }
    if (-not $malformedDigestRejected) {
        throw 'A malformed published digest was accepted.'
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

Write-Host 'Native dependency published SHA-256 validation passed.'
