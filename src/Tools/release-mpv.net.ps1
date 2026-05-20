
<#

Script that builds mpv.net and releases it on GitHub.
Please note that debug builds are built and released,
for release builds, scripts need to be rewritten.

Needs 2 positional CLI arguments:
    1. Directory where the mpv.net source code is located (mpv.net\src)
    2. Directory of the output files, for instance the desktop dir.

Dependencies:
    7zip installation found at: 'C:\Program Files\7-Zip\7z.exe'.
    Inno Setup compiler installation found at: 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'.
    GitHub CLI https://cli.github.com, the env var GH_TOKEN must be defined.
    Internet access to download FFmpeg, libmpv and yt-dlp for the portable package.

Notes:
    Before you run the script you need to update the versions found in the file:
        \mpv.net\src\MpvNet.Windows\MpvNet.Windows.csproj
#>

# Stop when the first error occurs
$ErrorActionPreference = 'Stop'

function DeleteDir($path) {
    if (Test-Path $path) {
        Remove-Item $path -Recurse
    }
}

function NewCleanDir($path) {
    DeleteDir $path
    New-Item -ItemType Directory -Force $path | Out-Null
    return Test $path
}

# Throw error if the file/dir don't exist
function Test($path) {
    if (-not (Test-Path $path)) {
        throw $path
    }
    return $path
}

function TestFile($path) {
    $file = Get-Item (Test $path)
    if ($file.Length -le 0) {
        throw "File is empty: $path"
    }
    return $file.FullName
}

function AddPortableConfig($outputDir, $docsDir) {
    $portableConfigDir = Join-Path $outputDir 'portable_config'
    $scriptsDir = Join-Path $portableConfigDir 'scripts'
    $scriptOptsDir = Join-Path $portableConfigDir 'script-opts'
    New-Item -ItemType Directory -Force $scriptsDir | Out-Null
    New-Item -ItemType Directory -Force $scriptOptsDir | Out-Null
    Copy-Item (Test (Join-Path $docsDir 'exemplos\portable_config\mpv.conf')) (Join-Path $portableConfigDir 'mpv.conf')
    Copy-Item (Test (Join-Path $docsDir 'exemplos\portable_config\input.conf')) (Join-Path $portableConfigDir 'input.conf')
}

function CopyExtraFiles($sourceDir, $targetDir, $files) {
    foreach ($file in $files) {
        $sourceFile = Join-Path $sourceDir $file
        TestFile $sourceFile | Out-Null
        Copy-Item $sourceFile (Join-Path $targetDir $file)
    }
}

function InvokeFileDownload($uri, $outputFile) {
    Write-Host "Downloading $uri"
    Invoke-WebRequest -Uri $uri -UserAgent 'mpv.net-release-script' -OutFile $outputFile -UseBasicParsing
    return TestFile $outputFile
}

function DownloadGitHubLatestAsset($apiUrl, $assetPattern, $downloadDir) {
    Write-Host "Reading latest release: $apiUrl"
    $release = Invoke-WebRequest -Uri $apiUrl -UserAgent 'mpv.net-release-script' -UseBasicParsing | ConvertFrom-Json
    $assets = @($release.assets | Where-Object { $_.name -match $assetPattern })

    if ($assets.Count -ne 1) {
        $assetNames = @($release.assets | ForEach-Object { $_.name }) -join ', '
        throw "Expected exactly one asset matching '$assetPattern' from $apiUrl, found $($assets.Count). Assets: $assetNames"
    }

    $outputFile = Join-Path $downloadDir $assets[0].name
    return InvokeFileDownload $assets[0].browser_download_url $outputFile
}

function ExpandReleaseArchive($archiveFile, $outputDir) {
    NewCleanDir $outputDir | Out-Null
    $process = Start-Process $7zFile @('x', $archiveFile, "-o$outputDir", '-y') -NoNewWindow -Wait -PassThru
    if ($process.ExitCode) {
        throw "7-Zip failed extracting $archiveFile with exit code $($process.ExitCode)"
    }

    return Test $outputDir
}

function CopyExtractedFile($sourceRootDir, $fileName, $targetDir) {
    $matches = @(Get-ChildItem $sourceRootDir -Filter $fileName -Recurse -File)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one extracted $fileName in $sourceRootDir, found $($matches.Count)."
    }

    TestFile $matches[0].FullName | Out-Null
    $targetFile = Join-Path $targetDir $fileName
    Copy-Item $matches[0].FullName $targetFile -Force
    TestFile $targetFile | Out-Null
}

function UpdatePortableDependencies($binDir, $workDir) {
    try {
        $downloadsDir = NewCleanDir (Join-Path $workDir 'downloads')
        $extractDir = NewCleanDir (Join-Path $workDir 'extract')

        $ffmpegArchive = DownloadGitHubLatestAsset `
            'https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest' `
            '^ffmpeg-master-latest-win64-gpl\.zip$' `
            $downloadsDir
        $ffmpegExtractDir = ExpandReleaseArchive $ffmpegArchive (Join-Path $extractDir 'ffmpeg')
        CopyExtractedFile $ffmpegExtractDir 'ffmpeg.exe' $binDir
        CopyExtractedFile $ffmpegExtractDir 'ffplay.exe' $binDir
        CopyExtractedFile $ffmpegExtractDir 'ffprobe.exe' $binDir

        $libmpvArchive = DownloadGitHubLatestAsset `
            'https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest' `
            '^mpv-dev-x86_64-[0-9]{8}-git-[0-9a-z]+\.7z$' `
            $downloadsDir
        $libmpvExtractDir = ExpandReleaseArchive $libmpvArchive (Join-Path $extractDir 'libmpv')
        CopyExtractedFile $libmpvExtractDir 'libmpv-2.dll' $binDir

        InvokeFileDownload `
            'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' `
            (Join-Path $binDir 'yt-dlp.exe') | Out-Null
    }
    finally {
        DeleteDir $workDir
    }
}

# Variables
$SourceDir     = Test $args[0]
$OutputRootDir = Test $args[1]
$DocsDir       = Test (Join-Path $SourceDir '..\docs')

Test (Join-Path $SourceDir 'MpvNet.sln')

$7zFile            = Test 'C:\Program Files\7-Zip\7z.exe'
$InnoSetupCompiler = Test 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'

$ReleaseNotes = "- [.NET Desktop Runtime 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)`n- [Changelog](https://github.com/mpvnet-player/mpv.net/blob/main/docs/changelog.md)"
$Repo = 'github.com/mpvnet-player/mpv.net'

# Dotnet Publish
$PublishDir64 = Join-Path $SourceDir 'MpvNet.Windows\bin\Debug\win-x64\publish\'
$ProjectFile = Test (Join-Path $SourceDir 'MpvNet.Windows\MpvNet.Windows.csproj')
dotnet publish $ProjectFile --self-contained false --configuration Debug --runtime win-x64
$PublishedExeFile64 = Test ($PublishDir64 + 'mpvnet.exe')

# Create OutputName
$VersionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($PublishedExeFile64)
$IsBeta = $VersionInfo.FilePrivatePart -ne 0
$BetaString = if ($IsBeta) { '-beta' } else { '' }
$VersionName = $VersionInfo.FileVersion
$OutputName64 = 'mpv.net-v' + $VersionName + $BetaString + '-portable-x64'

# Create OutputFolder
$OutputDir64   = Join-Path $OutputRootDir ($OutputName64 + '\')
DeleteDir $OutputDir64
mkdir $OutputDir64

# Copy Files
Copy-Item ($PublishDir64 + '*') $OutputDir64
$BinDirX64 = Test (Join-Path $SourceDir 'MpvNet.Windows\bin\Debug\win-x64\')
$DependencyWorkDir = Join-Path $env:TEMP 'mpv.net-release-dependencies'
UpdatePortableDependencies $BinDirX64 $DependencyWorkDir
$ExtraFiles = 'mpvnet.com', 'MediaInfo.dll', 'libmpv-2.dll', 'ffmpeg.exe', 'ffplay.exe', 'ffprobe.exe', 'yt-dlp.exe'
CopyExtraFiles $BinDirX64 $OutputDir64 $ExtraFiles
CopyExtraFiles $BinDirX64 $PublishDir64 $ExtraFiles
$LocaleDir = Test (Join-Path $SourceDir 'MpvNet.Windows\bin\Debug\win-x64\Locale\')
Copy-Item $LocaleDir ($OutputDir64 + 'Locale') -Recurse
Copy-Item $LocaleDir ($PublishDir64 + 'Locale') -Recurse -Force
AddPortableConfig $OutputDir64 $DocsDir

# Pack
$ZipOutputFile64 = Join-Path $OutputRootDir ($OutputName64 + '.zip')
& $7zFile a -tzip -mx9 $ZipOutputFile64 -r ($OutputDir64 + '*')
if ($LastExitCode) { throw $LastExitCode }
Test $ZipOutputFile64

# Inno Setup
''; ''
$InnoSetupScript = Test (Join-Path $SourceDir 'Setup\Inno\inno-setup.iss')
& $InnoSetupCompiler $InnoSetupScript
if ($LastExitCode) { throw $LastExitCode }
$SetupFile = Test (Join-Path $OutputRootDir "mpv.net-v$VersionName-setup-x64.exe")

if ($IsBeta) {
    $NewSetupFile = Join-Path $OutputRootDir "mpv.net-v$VersionName-beta-setup-x64.exe"
    Move-Item $SetupFile $NewSetupFile
    $SetupFile = $NewSetupFile
}

# Release
$Title = 'v' + $VersionName + $BetaString

if ($BetaString) {
    gh release create $Title -t $Title -n $ReleaseNotes --repo $Repo --prerelease $ZipOutputFile64 $SetupFile
} else {
    gh release create $Title -t $Title -n $ReleaseNotes --repo $Repo $ZipOutputFile64 $SetupFile
}

if ($LastExitCode) { throw $LastExitCode }
