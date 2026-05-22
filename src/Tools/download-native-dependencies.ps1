<#

Downloads and validates native dependencies that are not restored by dotnet.

MediaInfo.dll is downloaded from the official MediaArea download page by
default. Microsoft .NET/WPF native DLLs are not downloaded manually; they must
come from the self-contained win-x64 dotnet publish output.

#>

param(
    [Parameter(Mandatory = $true)]
    [string] $SourceDir,

    [Parameter(Mandatory = $true)]
    [string] $PublishDir,

    [string] $BuildOutputDir,

    [string] $ArtifactsDir,

    [string] $MediaInfoVersion = $env:MPVNET_MEDIAINFO_VERSION,

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
    $file = Get-Item (Test-RequiredPath $path)
    if ($file.Length -le 0) {
        throw "Required file is empty: $path"
    }

    return $file
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

function Invoke-FileDownload($uri, $outputFile) {
    Write-Host "Downloading $uri"
    Invoke-WebRequest -Uri $uri -UserAgent 'mpv.net-native-dependencies' -OutFile $outputFile -UseBasicParsing
    return Test-RequiredFile $outputFile
}

function New-CleanDir($path) {
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
    }

    New-Item -ItemType Directory -Force $path | Out-Null
    return Test-RequiredPath $path
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

function Expand-ArchiveWith7Zip($archiveFile, $outputDir) {
    Test-RequiredFile $SevenZipPath | Out-Null
    New-CleanDir $outputDir | Out-Null
    $process = Start-Process $SevenZipPath @('x', $archiveFile, "-o$outputDir", '-y') -NoNewWindow -Wait -PassThru
    if ($process.ExitCode) {
        throw "7-Zip failed extracting $archiveFile with exit code $($process.ExitCode)"
    }

    return Test-RequiredPath $outputDir
}

function Copy-MediaInfoDll($extractDir, $targetDir) {
    $matches = @(Get-ChildItem $extractDir -Filter 'MediaInfo.dll' -Recurse -File)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one MediaInfo.dll in $extractDir, found $($matches.Count)."
    }

    Assert-PeX64 $matches[0].FullName | Out-Null
    New-Item -ItemType Directory -Force $targetDir | Out-Null
    $targetFile = Join-Path $targetDir 'MediaInfo.dll'
    Copy-Item $matches[0].FullName $targetFile -Force
    Assert-PeX64 $targetFile | Out-Null
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($targetFile).FileVersion
    Write-Host "MediaInfo.dll ready: $targetFile ($version)"
    return Get-Item $targetFile
}

$SourceDir = Test-RequiredPath $SourceDir
$PublishDir = Test-RequiredPath $PublishDir
if ($BuildOutputDir) {
    $BuildOutputDir = Test-RequiredPath $BuildOutputDir
}

if (-not $ArtifactsDir) {
    $ArtifactsDir = Join-Path (Split-Path $SourceDir -Parent) 'artifacts\native-dependencies'
}

$ArtifactsDir = New-CleanDir $ArtifactsDir
$DownloadsDir = New-CleanDir (Join-Path $ArtifactsDir 'downloads')
$ExtractDir = New-CleanDir (Join-Path $ArtifactsDir 'extract')
$NativeCacheDir = Join-Path $ArtifactsDir 'win-x64'

$mediaInfoUri = Resolve-MediaInfoDownloadUri $MediaInfoVersion
$mediaInfoArchive = Invoke-FileDownload $mediaInfoUri (Join-Path $DownloadsDir (Split-Path $mediaInfoUri -Leaf))
$mediaInfoExtractDir = Expand-ArchiveWith7Zip $mediaInfoArchive.FullName (Join-Path $ExtractDir 'mediainfo')
$mediaInfoFile = Copy-MediaInfoDll $mediaInfoExtractDir $NativeCacheDir

Copy-Item $mediaInfoFile.FullName (Join-Path $PublishDir 'MediaInfo.dll') -Force
Assert-PeX64 (Join-Path $PublishDir 'MediaInfo.dll') | Out-Null

if ($BuildOutputDir) {
    Copy-Item $mediaInfoFile.FullName (Join-Path $BuildOutputDir 'MediaInfo.dll') -Force
    Assert-PeX64 (Join-Path $BuildOutputDir 'MediaInfo.dll') | Out-Null
}

foreach ($dll in $RequiredDotNetNativeDlls) {
    $path = Join-Path $PublishDir $dll
    if (-not (Test-Path $path)) {
        throw "Required .NET/WPF native DLL not found: $path. Publish the project as self-contained win-x64; do not download Microsoft runtime DLLs from third-party DLL sites."
    }

    Assert-PeX64 $path | Out-Null
    Write-Host "Validated .NET/WPF native DLL from publish output: $dll"
}

Write-Host "Native dependency download and validation completed."
