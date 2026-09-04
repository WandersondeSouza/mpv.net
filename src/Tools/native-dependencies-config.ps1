<#

Shared policy for build-time native/helper dependency downloads.

The cache contains downloaded archives/files only. Each caller keeps its own
output and extraction directory so Debug, Release, packaging and release
processes can reuse the same source without sharing mutable extracted files.

#>

$NativeDependenciesCacheMaxAgeDays = 2
$NativeDependenciesCacheRelativePath = 'artifacts\native-dependencies\downloads'

function Get-NativeDependenciesRepositoryRoot([string] $SourceDir) {
    $resolvedSourceDir = (Resolve-Path -LiteralPath $SourceDir).Path
    return [System.IO.Path]::GetDirectoryName($resolvedSourceDir)
}

function Get-NativeDependenciesDownloadCacheDir([string] $SourceDir) {
    return Join-Path (Get-NativeDependenciesRepositoryRoot $SourceDir) $NativeDependenciesCacheRelativePath
}

function Test-NativeDependencyCacheFileFresh(
    [string] $Path,
    [int] $MaxAgeDays = $NativeDependenciesCacheMaxAgeDays,
    [datetime] $Now = (Get-Date)) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0) {
        return $false
    }

    if ($MaxAgeDays -le 0) {
        return $true
    }

    return $file.LastWriteTime -gt $Now.AddDays(-$MaxAgeDays)
}

function Get-NativeDependenciesFreshCachedFile(
    [string] $DownloadDir,
    [string] $FilePattern,
    [int] $MaxAgeDays = $NativeDependenciesCacheMaxAgeDays) {
    $matches = @(Get-ChildItem -LiteralPath $DownloadDir -Filter $FilePattern -File -ErrorAction SilentlyContinue |
        Where-Object { Test-NativeDependencyCacheFileFresh $_.FullName $MaxAgeDays } |
        Sort-Object LastWriteTime -Descending)

    if ($matches.Count) {
        return $matches[0]
    }

    return $null
}

function Get-NativeDependenciesDownloadMutexName([string] $DownloadDir) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($DownloadDir.ToUpperInvariant())
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $algorithm.ComputeHash($bytes)
    }
    finally {
        $algorithm.Dispose()
    }

    $identifier = [System.BitConverter]::ToString($hash).Replace('-', '').Substring(0, 24)
    return "Local\mpvnet-native-download-cache-$identifier"
}

function Enter-NativeDependenciesDownloadCacheLock([string] $DownloadDir) {
    $mutexName = Get-NativeDependenciesDownloadMutexName $DownloadDir
    $mutex = [System.Threading.Mutex]::new($false, $mutexName)

    try {
        if (-not $mutex.WaitOne([TimeSpan]::FromMinutes(10))) {
            throw "Timed out waiting for the native download cache: $DownloadDir"
        }
    }
    catch [System.Threading.AbandonedMutexException] {
        Write-Warning "Previous native download cache owner ended unexpectedly. Continuing with cache: $DownloadDir"
    }
    catch {
        $mutex.Dispose()
        throw
    }

    return $mutex
}

function Invoke-NativeDependenciesFileDownload(
    [string] $Uri,
    [string] $OutputFile,
    [string] $UserAgent = 'mpv.net-native-dependencies',
    [int] $MaxAgeDays = $NativeDependenciesCacheMaxAgeDays) {
    if (Test-NativeDependencyCacheFileFresh $OutputFile $MaxAgeDays) {
        return Get-Item -LiteralPath $OutputFile
    }

    $temporaryFile = "$OutputFile.$PID.$([Guid]::NewGuid().ToString('N')).download"
    try {
        Invoke-WebRequest -Uri $Uri -UserAgent $UserAgent -OutFile $temporaryFile -UseBasicParsing
        $temporaryItem = Get-Item -LiteralPath $temporaryFile
        if ($temporaryItem.Length -le 0) {
            throw "Downloaded file is empty: $Uri"
        }

        Move-Item -LiteralPath $temporaryFile -Destination $OutputFile -Force
        return Get-Item -LiteralPath $OutputFile
    }
    finally {
        if (Test-Path -LiteralPath $temporaryFile -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryFile -Force
        }
    }
}
