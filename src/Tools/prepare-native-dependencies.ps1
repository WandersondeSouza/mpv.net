<#

Ensures the native and helper binaries expected beside mpvnet.exe exist.

libmpv and MediaInfo are downloaded from the same sources used
by the release flow. Microsoft .NET/WPF native DLLs are never downloaded from
third-party sites; when a publish directory is supplied they are copied from the
self-contained publish output.

#>

param(
    [Parameter(Mandatory = $true)]
    [string] $SourceDir,

    [Parameter(Mandatory = $true)]
    [string] $TargetDir,

    [string] $PublishDir,

    [string] $ArtifactsDir,

    # Optional persistent cache for downloaded archives. Extraction remains under
    # ArtifactsDir so parallel callers do not share temporary files.
    [string] $DownloadCacheDir,

    [string] $MediaInfoVersion = $env:MPVNET_MEDIAINFO_VERSION,

    # Retained only so existing callers keep working. Distribution outputs now
    # always contain both builds, regardless of this compatibility parameter.
    [ValidateSet('normal', 'x86_64-v3')]
    [string] $MpvBuildVariant = $(if ($env:MPVNET_MPV_BUILD_VARIANT) { $env:MPVNET_MPV_BUILD_VARIANT } else { 'normal' }),

    [string] $MediaInfoFile,

    [string] $MpvNetComFile,

    [switch] $UpdateExisting,

    [int] $MaxCacheAgeDays,

    [string] $SevenZipPath = 'C:\Program Files\7-Zip\7z.exe'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'libmpv-validation.ps1')
. (Join-Path $PSScriptRoot 'native-dependencies-config.ps1')

if (-not $PSBoundParameters.ContainsKey('MaxCacheAgeDays')) {
    $MaxCacheAgeDays = $NativeDependenciesCacheMaxAgeDays
}

$RequiredDotNetNativeDlls = @(
    'D3DCompiler_47_cor3.dll',
    'vcruntime140_cor3.dll',
    'wpfgfx_cor3.dll',
    'PenImc_cor3.dll',
    'PresentationNative_cor3.dll'
)

function Test-RequiredPath($path) {
    if (-not (Test-Path $path)) {
        throw "Required path not found: $path"
    }

    return (Resolve-Path $path).Path
}

function Test-RequiredFile($path) {
    if (-not (Test-Path $path)) {
        throw "Required file not found: $path"
    }

    $file = Get-Item $path
    if ($file.Length -le 0) {
        throw "Required file is empty: $path"
    }

    return $file
}

function Test-FreshFile($path) {
    return Test-NativeDependencyCacheFileFresh $path $MaxCacheAgeDays
}

function Assert-PeX64($path) {
    $file = Test-RequiredFile $path
    $stream = [System.IO.File]::OpenRead($file.FullName)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -le 0 -or $peOffset -gt ($stream.Length - 6)) {
            throw "Invalid PE header in $($file.FullName)"
        }

        $stream.Position = $peOffset
        $signature = $reader.ReadUInt32()
        if ($signature -ne 0x00004550) {
            throw "Invalid PE signature in $($file.FullName)"
        }

        $machine = $reader.ReadUInt16()
        if ($machine -ne 0x8664) {
            throw "Expected x64 native binary, got machine 0x$($machine.ToString('X4')): $($file.FullName)"
        }
    }
    finally {
        $stream.Dispose()
    }

    return $file
}

function New-CleanDir($path) {
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
    }

    New-Item -ItemType Directory -Force $path | Out-Null
    return Test-RequiredPath $path
}

function Invoke-FileDownload($uri, $outputFile) {
    if (Test-FreshFile $outputFile) {
        Write-Host "Using cached download: $outputFile"
        return Test-RequiredFile $outputFile
    }

    Write-Host "Downloading $uri"
    return Invoke-NativeDependenciesFileDownload $uri $outputFile 'mpv.net-native-dependencies' $MaxCacheAgeDays
}

function Get-FreshCachedFile($downloadDir, $filePattern) {
    $matches = @(Get-ChildItem $downloadDir -Filter $filePattern -File -ErrorAction SilentlyContinue |
        Where-Object { Test-FreshFile $_.FullName } |
        Sort-Object LastWriteTime -Descending)

    if ($matches.Count) {
        Write-Host "Using cached download: $($matches[0].FullName)"
        return Test-RequiredFile $matches[0].FullName
    }

    return $null
}

function Get-FreshCachedFileMatchingRegex($downloadDir, $filePattern, $namePattern) {
    $matches = @(Get-ChildItem $downloadDir -Filter $filePattern -File -ErrorAction SilentlyContinue |
        Where-Object { ($_.Name -match $namePattern) -and (Test-FreshFile $_.FullName) } |
        Sort-Object LastWriteTime -Descending)

    if ($matches.Count) {
        Write-Host "Using cached download: $($matches[0].FullName)"
        return Test-RequiredFile $matches[0].FullName
    }

    return $null
}

function Get-DownloadCacheMutexName($downloadDir) {
    return Get-NativeDependenciesDownloadMutexName $downloadDir
}

function Enter-DownloadCacheLock($downloadDir) {
    return Enter-NativeDependenciesDownloadCacheLock $downloadDir
}

function Get-GitHubLatestRelease($apiUrl) {
    Write-Host "Reading latest release: $apiUrl"
    $requestParameters = @{
        Uri = $apiUrl
        UserAgent = 'mpv.net-native-dependencies'
        UseBasicParsing = $true
    }
    if ($env:GH_TOKEN) {
        $requestParameters.Headers = @{
            Accept = 'application/vnd.github+json'
            Authorization = "Bearer $env:GH_TOKEN"
        }
    }

    return Invoke-WebRequest @requestParameters | ConvertFrom-Json
}

function Download-GitHubLatestAsset($apiUrl, $assetPattern, $downloadDir) {
    $release = Get-GitHubLatestRelease $apiUrl
    $assets = @($release.assets | Where-Object { $_.name -match $assetPattern })

    if ($assets.Count -ne 1) {
        $assetNames = @($release.assets | ForEach-Object { $_.name }) -join ', '
        throw "Expected exactly one asset matching '$assetPattern' from $apiUrl, found $($assets.Count). Assets: $assetNames"
    }

    $outputFile = Join-Path $downloadDir $assets[0].name
    return Invoke-FileDownload $assets[0].browser_download_url $outputFile
}

function Expand-ArchiveWith7Zip($archiveFile, $outputDir) {
    Test-RequiredFile $SevenZipPath | Out-Null
    New-CleanDir $outputDir | Out-Null
    $process = Start-Process $SevenZipPath @('x', $archiveFile, "-o$outputDir", '-y') -NoNewWindow -Wait -PassThru
    if ($process.ExitCode) {
        throw "7-Zip failed extracting $archiveFile with exit code $($process.ExitCode)"
    }

    return Test-RequiredPath $outputDir
}

function Copy-ExtractedFile($sourceRootDir, $fileName, $targetDir, $targetFileName = $fileName) {
    $matches = @(Get-ChildItem $sourceRootDir -Filter $fileName -Recurse -File)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one extracted $fileName in $sourceRootDir, found $($matches.Count)."
    }

    Copy-Item (Test-RequiredFile $matches[0].FullName).FullName (Join-Path $targetDir $targetFileName) -Force
    return Test-RequiredFile (Join-Path $targetDir $targetFileName)
}

function Resolve-MediaInfoDownloadUri($version) {
    if ($version) {
        return "https://mediaarea.net/download/binary/libmediainfo0/$version/MediaInfo_DLL_$($version)_Windows_x64_WithoutInstaller.7z"
    }

    $downloadPage = 'https://mediaarea.net/en/MediaInfo/Download/Windows'
    Write-Host "Reading official MediaInfo download page: $downloadPage"
    $page = Invoke-WebRequest -Uri $downloadPage -UserAgent 'mpv.net-native-dependencies' -UseBasicParsing
    $link = @($page.Links |
        Where-Object { $_.href -match '/download/binary/libmediainfo0/[0-9.]+/MediaInfo_DLL_[0-9.]+_Windows_x64_WithoutInstaller\.7z$' } |
        Select-Object -First 1)[0]

    if (-not $link) {
        throw "Could not find the latest MediaInfo x64 DLL archive on $downloadPage"
    }

    $href = [string] $link.href
    if ($href.StartsWith('//')) {
        return "https:$href"
    }

    if ($href.StartsWith('/')) {
        return "https://mediaarea.net$href"
    }

    return $href
}

function Copy-MediaInfoDll($extractDir, $targetDir) {
    $matches = @(Get-ChildItem $extractDir -Filter 'MediaInfo.dll' -Recurse -File)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one MediaInfo.dll in $extractDir, found $($matches.Count)."
    }

    Assert-PeX64 $matches[0].FullName | Out-Null
    Copy-Item $matches[0].FullName (Join-Path $targetDir 'MediaInfo.dll') -Force
    return Assert-PeX64 (Join-Path $targetDir 'MediaInfo.dll')
}

function Ensure-MediaInfo($targetDir, $downloadsDir, $extractDir) {
    $targetFile = Join-Path $targetDir 'MediaInfo.dll'
    if ($MediaInfoFile) {
        Copy-Item (Test-RequiredFile $MediaInfoFile).FullName $targetFile -Force
        Assert-PeX64 $targetFile | Out-Null
        return
    }

    if ((-not $UpdateExisting) -and (Test-FreshFile $targetFile)) {
        Assert-PeX64 $targetFile | Out-Null
        return
    }

    $mediaInfoArchive = Get-FreshCachedFile $downloadsDir 'MediaInfo_DLL_*_Windows_x64_WithoutInstaller.7z'
    if (-not $mediaInfoArchive) {
        $mediaInfoUri = Resolve-MediaInfoDownloadUri $MediaInfoVersion
        $mediaInfoArchive = Invoke-FileDownload $mediaInfoUri (Join-Path $downloadsDir (Split-Path $mediaInfoUri -Leaf))
    }

    $mediaInfoExtractDir = Expand-ArchiveWith7Zip $mediaInfoArchive.FullName (Join-Path $extractDir 'mediainfo')
    Copy-MediaInfoDll $mediaInfoExtractDir $targetDir | Out-Null
}

function Get-LibMpvAssetIdentity([string] $assetName) {
    $match = [regex]::Match(
        $assetName,
        '^mpv-dev-x86_64(?:-v3)?-(?<date>[0-9]{8})-git-(?<commit>[0-9a-z]+)\.7z$')
    if (-not $match.Success) {
        throw "Unsupported libmpv asset name: $assetName"
    }

    return "$($match.Groups['date'].Value)-git-$($match.Groups['commit'].Value)"
}

function Get-FreshCachedLibMpvArchivePair($downloadsDir, $contract) {
    $normalArchives = @(Get-ChildItem $downloadsDir -Filter $contract.Normal.CachePattern -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match $contract.Normal.AssetRegex -and (Test-FreshFile $_.FullName) } |
        Sort-Object LastWriteTime -Descending)
    $v3Archives = @(Get-ChildItem $downloadsDir -Filter $contract.X86_64V3.CachePattern -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match $contract.X86_64V3.AssetRegex -and (Test-FreshFile $_.FullName) } |
        Sort-Object LastWriteTime -Descending)

    foreach ($normalArchive in $normalArchives) {
        $identity = Get-LibMpvAssetIdentity $normalArchive.Name
        $v3Archive = @($v3Archives | Where-Object {
            (Get-LibMpvAssetIdentity $_.Name) -eq $identity
        } | Select-Object -First 1)[0]
        if ($v3Archive) {
            Write-Host "Using matching cached libmpv archives for $identity"
            return [pscustomobject]@{
                Normal = Test-RequiredFile $normalArchive.FullName
                X86_64V3 = Test-RequiredFile $v3Archive.FullName
            }
        }
    }

    return $null
}

function Get-LatestLibMpvAssets($contract) {
    $release = Get-GitHubLatestRelease $contract.ReleaseApiUrl
    $normalAssets = @($release.assets | Where-Object { $_.name -match $contract.Normal.AssetRegex })
    $v3Assets = @($release.assets | Where-Object { $_.name -match $contract.X86_64V3.AssetRegex })

    if ($normalAssets.Count -ne 1 -or $v3Assets.Count -ne 1) {
        $assetNames = @($release.assets | ForEach-Object { $_.name }) -join ', '
        throw "Expected one normal and one x86-64-v3 libmpv asset from $($contract.ReleaseApiUrl). Found normal=$($normalAssets.Count), v3=$($v3Assets.Count). Assets: $assetNames"
    }

    $normalIdentity = Get-LibMpvAssetIdentity $normalAssets[0].name
    $v3Identity = Get-LibMpvAssetIdentity $v3Assets[0].name
    if ($normalIdentity -ne $v3Identity) {
        throw "Latest libmpv release has mismatched normal and x86-64-v3 assets. normal=$($normalAssets[0].name), v3=$($v3Assets[0].name)"
    }

    return [pscustomobject]@{
        Normal = $normalAssets[0]
        X86_64V3 = $v3Assets[0]
    }
}

function Save-LibMpvBuildManifest($targetDir, $contract, $archives) {
    $normalFile = Join-Path $targetDir $contract.Normal.FileName
    $v3File = Join-Path $targetDir $contract.X86_64V3.FileName
    $manifest = [ordered]@{
        schemaVersion = $contract.SchemaVersion
        source = $contract.Source
        normal = [ordered]@{
            file = $contract.Normal.FileName
            asset = $archives.Normal.Name
            sha256 = Get-Sha256Hex $normalFile
            downloadedAtUtc = $archives.Normal.LastWriteTimeUtc.ToString('O')
        }
        'x86_64-v3' = [ordered]@{
            file = $contract.X86_64V3.FileName
            asset = $archives.X86_64V3.Name
            sha256 = Get-Sha256Hex $v3File
            downloadedAtUtc = $archives.X86_64V3.LastWriteTimeUtc.ToString('O')
        }
    }

    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $targetDir 'libmpv-builds.json') -Encoding utf8
}

function Ensure-LibMpv($targetDir, $downloadsDir, $extractDir) {
    $contract = Get-LibMpvBuildContract
    $normalTargetFile = Join-Path $targetDir $contract.Normal.FileName
    $v3TargetFile = Join-Path $targetDir $contract.X86_64V3.FileName
    $manifestFile = Join-Path $targetDir 'libmpv-builds.json'

    if ((-not $UpdateExisting) -and
        (Test-FreshFile $normalTargetFile) -and
        (Test-FreshFile $v3TargetFile) -and
        (Test-Path -LiteralPath $manifestFile -PathType Leaf)) {
        Assert-LibMpvBuilds $targetDir | Out-Null
        Write-Host "Using existing matching libmpv builds: $normalTargetFile, $v3TargetFile"
        return
    }

    if ($MpvBuildVariant -ne 'normal') {
        Write-Warning 'MpvBuildVariant is retained for script compatibility only; distribution outputs always include normal and x86-64-v3 libmpv builds.'
    }

    $archives = Get-FreshCachedLibMpvArchivePair $downloadsDir $contract
    if (-not $archives) {
        $assets = Get-LatestLibMpvAssets $contract
        $archives = [pscustomobject]@{
            Normal = Invoke-FileDownload $assets.Normal.browser_download_url (Join-Path $downloadsDir $assets.Normal.name)
            X86_64V3 = Invoke-FileDownload $assets.X86_64V3.browser_download_url (Join-Path $downloadsDir $assets.X86_64V3.name)
        }
    }

    $normalIdentity = Get-LibMpvAssetIdentity $archives.Normal.Name
    $v3Identity = Get-LibMpvAssetIdentity $archives.X86_64V3.Name
    if ($normalIdentity -ne $v3Identity) {
        throw "Cached libmpv archives do not belong to the same upstream build. normal=$($archives.Normal.Name), v3=$($archives.X86_64V3.Name)"
    }

    $normalExtractDir = Expand-ArchiveWith7Zip $archives.Normal.FullName (Join-Path $extractDir 'libmpv-normal')
    $v3ExtractDir = Expand-ArchiveWith7Zip $archives.X86_64V3.FullName (Join-Path $extractDir 'libmpv-v3')
    Copy-ExtractedFile $normalExtractDir 'libmpv-2.dll' $targetDir $contract.Normal.FileName | Out-Null
    Copy-ExtractedFile $v3ExtractDir 'libmpv-2.dll' $targetDir $contract.X86_64V3.FileName | Out-Null
    Assert-LibMpvBuilds $targetDir | Out-Null
    Save-LibMpvBuildManifest $targetDir $contract $archives

    $legacyMarkerFile = Join-Path $targetDir 'libmpv-2.variant.txt'
    if (Test-Path -LiteralPath $legacyMarkerFile) {
        Remove-Item -LiteralPath $legacyMarkerFile -Force
    }
}

function Ensure-MpvNetCom($targetDir, $downloadsDir, $publishDir) {
    $targetFile = Join-Path $targetDir 'mpvnet.com'
    if ($MpvNetComFile) {
        Copy-Item (Test-RequiredFile $MpvNetComFile).FullName $targetFile -Force
        Test-RequiredFile $targetFile | Out-Null
        return
    }

    if ($UpdateExisting -or (-not (Test-FreshFile $targetFile))) {
        $downloadFile = Join-Path $downloadsDir 'mpvnet.com'
        Invoke-FileDownload `
            'https://github.com/mpvnet-player/file-host/releases/download/tag/mpvnet.com.txt' `
            $downloadFile | Out-Null
        Copy-Item (Test-RequiredFile $downloadFile).FullName $targetFile -Force
    }

    if ($publishDir) {
        Copy-Item (Test-RequiredFile $targetFile).FullName (Join-Path $publishDir 'mpvnet.com') -Force
        Test-RequiredFile (Join-Path $publishDir 'mpvnet.com') | Out-Null
    }
}

function Ensure-DotNetNativeDlls($targetDir, $publishDir) {
    if (-not $publishDir) {
        return
    }

    $publishDir = Test-RequiredPath $publishDir
    foreach ($dll in $RequiredDotNetNativeDlls) {
        $sourceFile = Join-Path $publishDir $dll
        if (-not (Test-Path $sourceFile)) {
            $targetExistingFile = Join-Path $targetDir $dll
            if (Test-Path $targetExistingFile) {
                $sourceFile = $targetExistingFile
            }
        }

        Assert-PeX64 $sourceFile | Out-Null
        $targetFile = Join-Path $targetDir $dll
        $publishFile = Join-Path $publishDir $dll
        if ((Resolve-Path $sourceFile).Path -ne (Join-Path (Resolve-Path $targetDir).Path $dll)) {
            Copy-Item $sourceFile $targetFile -Force
        }

        if ((Resolve-Path $sourceFile).Path -ne (Join-Path (Resolve-Path $publishDir).Path $dll)) {
            Copy-Item $sourceFile $publishFile -Force
        }

        Assert-PeX64 (Join-Path $targetDir $dll) | Out-Null
        Assert-PeX64 (Join-Path $publishDir $dll) | Out-Null
    }
}

$SourceDir = Test-RequiredPath $SourceDir
New-Item -ItemType Directory -Force $TargetDir | Out-Null
$TargetDir = Test-RequiredPath $TargetDir

if (-not $ArtifactsDir) {
    $ArtifactsDir = Join-Path (Get-NativeDependenciesRepositoryRoot $SourceDir) 'artifacts\native-dependencies'
}

New-Item -ItemType Directory -Force $ArtifactsDir | Out-Null
$ArtifactsDir = Test-RequiredPath $ArtifactsDir

if (-not $DownloadCacheDir) {
    $DownloadCacheDir = Get-NativeDependenciesDownloadCacheDir $SourceDir
}

New-Item -ItemType Directory -Force $DownloadCacheDir | Out-Null
$DownloadsDir = Test-RequiredPath $DownloadCacheDir
$ExtractDir = New-CleanDir (Join-Path $ArtifactsDir ("extract-$PID-$([Guid]::NewGuid().ToString('N'))"))

$cacheMutex = Enter-DownloadCacheLock $DownloadsDir
try {
    Ensure-MediaInfo $TargetDir $DownloadsDir $ExtractDir
    Ensure-LibMpv $TargetDir $DownloadsDir $ExtractDir
    Ensure-MpvNetCom $TargetDir $DownloadsDir $PublishDir
    Ensure-DotNetNativeDlls $TargetDir $PublishDir
}
finally {
    $cacheMutex.ReleaseMutex()
    $cacheMutex.Dispose()

    if (Test-Path -LiteralPath $ExtractDir) {
        Remove-Item -LiteralPath $ExtractDir -Recurse -Force
    }
}

Write-Host "Native and helper dependencies are ready: $TargetDir"
