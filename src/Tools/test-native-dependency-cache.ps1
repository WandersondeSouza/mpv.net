<#

Offline smoke test for the repository-wide native dependency cache contract.

#>

param(
    [string] $SourceDir = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'native-dependencies-config.ps1')

$SourceDir = (Resolve-Path -LiteralPath $SourceDir).Path
$expectedCacheDir = Join-Path (Get-NativeDependenciesRepositoryRoot $SourceDir) $NativeDependenciesCacheRelativePath
$actualCacheDir = Get-NativeDependenciesDownloadCacheDir $SourceDir

if ($actualCacheDir -ne $expectedCacheDir) {
    throw "Unexpected native dependency cache path: $actualCacheDir"
}

if ($NativeDependenciesCacheMaxAgeDays -ne 2) {
    throw "Unexpected native dependency cache age: $NativeDependenciesCacheMaxAgeDays"
}

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("mpvnet-native-cache-test-" + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Force $testRoot | Out-Null
    $recentFile = Join-Path $testRoot 'recent.bin'
    $oldFile = Join-Path $testRoot 'old.bin'
    $emptyFile = Join-Path $testRoot 'empty.bin'
    [System.IO.File]::WriteAllBytes($recentFile, [byte[]](1, 2, 3))
    [System.IO.File]::WriteAllBytes($oldFile, [byte[]](1, 2, 3))
    [System.IO.File]::WriteAllBytes($emptyFile, [byte[]]::new(0))

    $now = Get-Date
    [System.IO.File]::SetLastWriteTime($recentFile, $now.AddDays(-1))
    [System.IO.File]::SetLastWriteTime($oldFile, $now.AddDays(-2).AddMinutes(-1))

    if (-not (Test-NativeDependencyCacheFileFresh $recentFile -Now $now)) {
        throw 'A one-day-old cache file was incorrectly considered stale.'
    }

    if (Test-NativeDependencyCacheFileFresh $oldFile -Now $now) {
        throw 'A cache file older than two days was incorrectly considered fresh.'
    }

    if (Test-NativeDependencyCacheFileFresh $emptyFile -Now $now) {
        throw 'An empty cache file was incorrectly considered fresh.'
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

Write-Host "Native dependency cache contract validated: $actualCacheDir (max age: $NativeDependenciesCacheMaxAgeDays days)"
