
<#

Script that builds mpv.net and releases it on GitHub.
Please note that debug builds are built and released,
for release builds, scripts need to be rewritten.

Needs 2 positional CLI arguments:
    1. Directory where the mpv.net source code is located (mpv.net\src)
    2. Directory of the output files, for instance the desktop dir.

Optional parameters:
    -Repo Owner/repository used by GitHub CLI. Default: WandersondeSouza/mpv.net.
    -SkipInstaller Skips Inno Setup package generation.
    -SkipGitHubRelease Creates local artifacts without publishing a GitHub release.
    -MediaInfoFile Optional override path to MediaInfo.dll. Defaults to src\Native\win-x64\MediaInfo.dll.
    -MediaInfoVersion Optional MediaInfo version pin, for example 26.05. Defaults to the latest stable x64 DLL archive listed by MediaArea.
    -MpvNetComFile Optional override path to mpvnet.com. Defaults to the upstream helper download.

Dependencies:
    7zip installation found at: 'C:\Program Files\7-Zip\7z.exe'.
    Inno Setup compiler installation found at: 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' unless -SkipInstaller is used.
    GitHub CLI https://cli.github.com, the env var GH_TOKEN must be defined unless -SkipGitHubRelease is used.
    Internet access to download FFmpeg, libmpv, yt-dlp and MediaInfo for the portable package.
    Internet access to download mpvnet.com when -MpvNetComFile is not provided and the build output does not already contain it.
    Internet access to download Gettext.Tools from NuGet when msgfmt.exe is not available on PATH.

Notes:
    Before you run the script you need to update the versions found in the file:
        \mpv.net\src\MpvNet.Windows\MpvNet.Windows.csproj
#>

param(
    [Parameter(Position = 0, Mandatory = $true)]
    [string] $SourceDir,

    [Parameter(Position = 1, Mandatory = $true)]
    [string] $OutputRootDir,

    [string] $Repo = 'WandersondeSouza/mpv.net',

    [switch] $SkipInstaller,

    [switch] $SkipGitHubRelease,

    [string] $MediaInfoFile,

    [string] $MediaInfoVersion,

    [string] $MpvNetComFile
)

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

function CopyDir($sourceDir, $targetDir) {
    DeleteDir $targetDir
    Copy-Item (Test $sourceDir) $targetDir -Recurse -Force
    return Test $targetDir
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

function AddGettextToolsToPath($workDir) {
    if (Get-Command msgfmt -ErrorAction SilentlyContinue) {
        return
    }

    $packagesDir = NewCleanDir (Join-Path $workDir 'gettext-tools')
    $index = Invoke-WebRequest `
        -Uri 'https://api.nuget.org/v3-flatcontainer/gettext.tools/index.json' `
        -UseBasicParsing |
        ConvertFrom-Json
    $version = @($index.versions)[-1]
    if (-not $version) {
        throw 'Could not resolve the latest Gettext.Tools package version from NuGet.'
    }

    $packageFile = Join-Path $workDir "gettext.tools.$version.nupkg"
    InvokeFileDownload `
        "https://api.nuget.org/v3-flatcontainer/gettext.tools/$version/gettext.tools.$version.nupkg" `
        $packageFile | Out-Null
    ExpandReleaseArchive $packageFile $packagesDir | Out-Null

    $toolBinDir = Get-ChildItem $packagesDir -Filter 'msgfmt.exe' -Recurse -File |
        Select-Object -First 1 |
        ForEach-Object { $_.DirectoryName }

    if (-not $toolBinDir) {
        throw "Gettext.Tools was installed, but msgfmt.exe was not found in $packagesDir"
    }

    $env:Path = "$toolBinDir;$env:Path"
    TestFile (Join-Path $toolBinDir 'msgfmt.exe') | Out-Null
}

function EnsureLocale($sourceDir, $localeDir, $workDir) {
    if ((Test-Path $localeDir) -and @(Get-ChildItem $localeDir -Filter 'mpvnet.mo' -Recurse -File).Count) {
        return Test $localeDir
    }

    $createMoScript = Test (Join-Path $sourceDir '..\lang\create-mo-files.ps1')
    AddGettextToolsToPath $workDir
    & $createMoScript (Join-Path $sourceDir 'MpvNet.Windows\bin\Debug\win-x64')
    if ($LastExitCode) { throw $LastExitCode }

    return Test $localeDir
}

function UpdatePortableDependencies($binDir, $workDir) {
    try {
        $downloadsDir = NewCleanDir (Join-Path $workDir 'downloads')
        $extractDir = NewCleanDir (Join-Path $workDir 'extract')

        $ffmpegArchive = DownloadGitHubLatestAsset `
            'https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest' `
            '^ffmpeg-N-[0-9]+-g[0-9a-f]+-win64-gpl\.zip$' `
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
$SourceDir     = Test $SourceDir
New-Item -ItemType Directory -Force $OutputRootDir | Out-Null
$OutputRootDir = Test $OutputRootDir
$DocsDir       = Test (Join-Path $SourceDir '..\docs')

Test (Join-Path $SourceDir 'MpvNet.sln')

$7zFile            = Test 'C:\Program Files\7-Zip\7z.exe'
if (-not $SkipInstaller) {
    $InnoSetupCompiler = Test 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
}

$ReleaseNotes = "- [Changelog](https://github.com/$Repo/blob/main/docs/changelog.md)"

# Dotnet Publish
$PublishDir64 = Join-Path $SourceDir 'MpvNet.Windows\bin\Debug\win-x64\publish\'
$ProjectFile = Test (Join-Path $SourceDir 'MpvNet.Windows\MpvNet.Windows.csproj')
DeleteDir $PublishDir64
dotnet publish $ProjectFile --self-contained true --configuration Debug --runtime win-x64 --output $PublishDir64 /p:IncludeNativeLibrariesForSelfExtract=false
$PublishedExeFile64 = Test ($PublishDir64 + 'mpvnet.exe')
$BinDirX64 = Test (Join-Path $SourceDir 'MpvNet.Windows\bin\Debug\win-x64\')
$NativeDependenciesScript = Test (Join-Path $SourceDir 'Tools\download-native-dependencies.ps1')
$NativeDependenciesArgs = @{
    SourceDir = $SourceDir
    PublishDir = $PublishDir64
    BuildOutputDir = $BinDirX64
    ArtifactsDir = Join-Path (Split-Path $SourceDir -Parent) 'artifacts\native-dependencies'
}
if ($MediaInfoVersion) {
    $NativeDependenciesArgs.MediaInfoVersion = $MediaInfoVersion
}
& $NativeDependenciesScript @NativeDependenciesArgs
if ($LastExitCode) { throw $LastExitCode }
if ($MediaInfoFile) {
    Copy-Item (TestFile $MediaInfoFile) (Join-Path $PublishDir64 'MediaInfo.dll') -Force
    Copy-Item (TestFile $MediaInfoFile) (Join-Path $BinDirX64 'MediaInfo.dll') -Force
}

# Create OutputName
$VersionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($PublishedExeFile64)
$IsBeta = $VersionInfo.ProductVersion -match '(?i)(^|[-+.])(alpha|beta|preview|rc)([-+.]|$)'
$BetaString = if ($IsBeta) { '-beta' } else { '' }
$VersionName = $VersionInfo.FileVersion
$OutputName64 = 'mpv.net-v' + $VersionName + $BetaString + '-portable-x64'

# Create OutputFolder
$OutputDir64   = Join-Path $OutputRootDir ($OutputName64 + '\')
DeleteDir $OutputDir64
mkdir $OutputDir64

# Copy Files
Copy-Item ($PublishDir64 + '*') $OutputDir64
$DependencyWorkDir = Join-Path $env:TEMP 'mpv.net-release-dependencies'
UpdatePortableDependencies $BinDirX64 $DependencyWorkDir
if ($MpvNetComFile) {
    Copy-Item (TestFile $MpvNetComFile) (Join-Path $BinDirX64 'mpvnet.com') -Force
}
elseif (-not (Test-Path (Join-Path $BinDirX64 'mpvnet.com'))) {
    InvokeFileDownload `
        'https://github.com/mpvnet-player/file-host/releases/download/tag/mpvnet.com.txt' `
        (Join-Path $BinDirX64 'mpvnet.com') | Out-Null
}
$ExtraFiles = 'mpvnet.com', 'MediaInfo.dll', 'libmpv-2.dll', 'ffmpeg.exe', 'ffplay.exe', 'ffprobe.exe', 'yt-dlp.exe'
CopyExtraFiles $BinDirX64 $OutputDir64 $ExtraFiles
CopyExtraFiles $BinDirX64 $PublishDir64 $ExtraFiles
$LocaleDir = EnsureLocale `
    $SourceDir `
    (Join-Path $SourceDir 'MpvNet.Windows\bin\Debug\win-x64\Locale\') `
    (Join-Path $env:TEMP 'mpv.net-release-locale')
CopyDir $LocaleDir (Join-Path $OutputDir64 'Locale') | Out-Null
CopyDir $LocaleDir (Join-Path $PublishDir64 'Locale') | Out-Null
AddPortableConfig $OutputDir64 $DocsDir

$NativeValidationScript = Test (Join-Path $SourceDir 'Tools\test-native-dependencies.ps1')
& $NativeValidationScript -Path $OutputDir64
if ($LastExitCode) { throw $LastExitCode }
& $NativeValidationScript -Path $PublishDir64
if ($LastExitCode) { throw $LastExitCode }

# Pack
$ZipOutputFile64 = Join-Path $OutputRootDir ($OutputName64 + '.zip')
& $7zFile a -tzip -mx9 $ZipOutputFile64 -r ($OutputDir64 + '*')
if ($LastExitCode) { throw $LastExitCode }
Test $ZipOutputFile64
& $NativeValidationScript -ZipFile $ZipOutputFile64
if ($LastExitCode) { throw $LastExitCode }

$ReleaseFiles = @($ZipOutputFile64)

if (-not $SkipInstaller) {
    # Inno Setup
    ''; ''
    $InnoSetupScript = Test (Join-Path $SourceDir 'Setup\Inno\inno-setup.iss')
    & $InnoSetupCompiler "/O$OutputRootDir" $InnoSetupScript
    if ($LastExitCode) { throw $LastExitCode }
    $SetupFile = Test (Join-Path $OutputRootDir "mpv.net-v$VersionName-setup-x64.exe")

    if ($IsBeta) {
        $NewSetupFile = Join-Path $OutputRootDir "mpv.net-v$VersionName-beta-setup-x64.exe"
        Move-Item $SetupFile $NewSetupFile
        $SetupFile = $NewSetupFile
    }

    $ReleaseFiles += $SetupFile
}

# Release
$Title = 'v' + $VersionName + $BetaString

if (-not $SkipGitHubRelease) {
    if ($BetaString) {
        gh release create $Title -t $Title -n $ReleaseNotes --repo $Repo --prerelease $ReleaseFiles
    } else {
        gh release create $Title -t $Title -n $ReleaseNotes --repo $Repo $ReleaseFiles
    }

    if ($LastExitCode) { throw $LastExitCode }
}
