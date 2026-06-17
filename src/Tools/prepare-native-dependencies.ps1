<#

Ensures the native and helper binaries expected beside mpvnet.exe exist.

libmpv, yt-dlp and MediaInfo are downloaded from the same sources used
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

    [string] $MediaInfoVersion = $env:MPVNET_MEDIAINFO_VERSION,

    [ValidateSet('normal', 'x86_64-v3')]
    [string] $MpvBuildVariant = $(if ($env:MPVNET_MPV_BUILD_VARIANT) { $env:MPVNET_MPV_BUILD_VARIANT } else { 'x86_64-v3' }),

    [string] $MediaInfoFile,

    [string] $MpvNetComFile,

    [switch] $UpdateExisting,

    [int] $MaxCacheAgeDays = 2,

    [string] $SevenZipPath = 'C:\Program Files\7-Zip\7z.exe'
)

$ErrorActionPreference = 'Stop'

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
    if (-not (Test-Path $path)) {
        return $false
    }

    $file = Test-RequiredFile $path
    if ($MaxCacheAgeDays -le 0) {
        return $true
    }

    return $file.LastWriteTime -gt (Get-Date).AddDays(-$MaxCacheAgeDays)
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
    Invoke-WebRequest -Uri $uri -UserAgent 'mpv.net-native-dependencies' -OutFile $outputFile -UseBasicParsing
    return Test-RequiredFile $outputFile
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

function Download-GitHubLatestAsset($apiUrl, $assetPattern, $downloadDir) {
    Write-Host "Reading latest release: $apiUrl"
    $release = Invoke-WebRequest -Uri $apiUrl -UserAgent 'mpv.net-native-dependencies' -UseBasicParsing | ConvertFrom-Json
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

function Copy-ExtractedFile($sourceRootDir, $fileName, $targetDir) {
    $matches = @(Get-ChildItem $sourceRootDir -Filter $fileName -Recurse -File)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one extracted $fileName in $sourceRootDir, found $($matches.Count)."
    }

    Copy-Item (Test-RequiredFile $matches[0].FullName).FullName (Join-Path $targetDir $fileName) -Force
    return Test-RequiredFile (Join-Path $targetDir $fileName)
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

function Ensure-LibMpv($targetDir, $downloadsDir, $extractDir) {
    $targetFile = Join-Path $targetDir 'libmpv-2.dll'
    $variantMarkerFile = Join-Path $targetDir 'libmpv-2.variant.txt'
    $currentVariant = if (Test-Path $variantMarkerFile) { (Get-Content $variantMarkerFile -Raw).Trim() } else { 'normal' }
    if ((-not $UpdateExisting) -and (Test-FreshFile $targetFile) -and ($currentVariant -eq $MpvBuildVariant)) {
        Assert-PeX64 $targetFile | Out-Null
        if (-not (Test-Path $variantMarkerFile)) {
            Set-Content -Path $variantMarkerFile -Value $MpvBuildVariant -Encoding ascii
        }
        return
    }

    $assetPattern = if ($MpvBuildVariant -eq 'x86_64-v3') {
        '^mpv-dev-x86_64-v3-[0-9]{8}-git-[0-9a-z]+\.7z$'
    } else {
        '^mpv-dev-x86_64-[0-9]{8}-git-[0-9a-z]+\.7z$'
    }
    $cachePattern = if ($MpvBuildVariant -eq 'x86_64-v3') { 'mpv-dev-x86_64-v3-*.7z' } else { 'mpv-dev-x86_64-*.7z' }

    Write-Host "Preparing libmpv build variant: $MpvBuildVariant"
    $libmpvArchive = Get-FreshCachedFileMatchingRegex $downloadsDir $cachePattern $assetPattern
    if (-not $libmpvArchive) {
        $libmpvArchive = Download-GitHubLatestAsset `
            'https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest' `
            $assetPattern `
            $downloadsDir
    }

    $libmpvExtractDir = Expand-ArchiveWith7Zip $libmpvArchive.FullName (Join-Path $extractDir 'libmpv')
    Copy-ExtractedFile $libmpvExtractDir 'libmpv-2.dll' $targetDir | Out-Null
    Assert-PeX64 $targetFile | Out-Null
    Set-Content -Path $variantMarkerFile -Value $MpvBuildVariant -Encoding ascii
}

function Ensure-YtDlp($targetDir, $downloadsDir) {
    $targetFile = Join-Path $targetDir 'yt-dlp.exe'
    if ($UpdateExisting -or (-not (Test-FreshFile $targetFile))) {
        $downloadFile = Join-Path $downloadsDir 'yt-dlp.exe'
        Invoke-FileDownload `
            'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' `
            $downloadFile | Out-Null
        Copy-Item (Test-RequiredFile $downloadFile).FullName $targetFile -Force
    }

    Assert-PeX64 $targetFile | Out-Null
}

function Ensure-MpvNetCom($targetDir, $downloadsDir) {
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
    $ArtifactsDir = Join-Path (Split-Path $SourceDir -Parent) 'artifacts\native-dependencies'
}

New-Item -ItemType Directory -Force $ArtifactsDir | Out-Null
$ArtifactsDir = Test-RequiredPath $ArtifactsDir
New-Item -ItemType Directory -Force (Join-Path $ArtifactsDir 'downloads') | Out-Null
$DownloadsDir = Test-RequiredPath (Join-Path $ArtifactsDir 'downloads')
$ExtractDir = New-CleanDir (Join-Path $ArtifactsDir 'extract')

Ensure-MediaInfo $TargetDir $DownloadsDir $ExtractDir
Ensure-LibMpv $TargetDir $DownloadsDir $ExtractDir
Ensure-YtDlp $TargetDir $DownloadsDir
Ensure-MpvNetCom $TargetDir $DownloadsDir
Ensure-DotNetNativeDlls $TargetDir $PublishDir

Write-Host "Native and helper dependencies are ready: $TargetDir"
