
<#

Script that builds mpv.net in Release configuration and releases it on GitHub.

Needs 2 positional CLI arguments:
    1. Directory where the mpv.net source code is located (mpv.net\src)
    2. Directory of the output files, for instance the desktop dir.

Optional parameters:
    -Repo Owner/repository used by GitHub CLI. Default: WandersondeSouza/mpv.net.
    -SkipPortableZip Skips portable ZIP generation.
    -SkipInstaller Skips Inno Setup package generation.
    -SkipGitHubRelease Creates local artifacts without publishing a GitHub release.
    -EnableFileLogging Builds a diagnostic package with file logging enabled. Default: disabled.
    -MpvBuildVariant mpv/libmpv build variant. Default: x86_64-v3. Use normal only when compatibility with older x64 CPUs is required.
    -MediaInfoFile Optional override path to MediaInfo.dll. Defaults to automatic MediaArea download when missing.
    -MediaInfoVersion Optional MediaInfo version pin, for example 26.05. Defaults to the latest stable x64 DLL archive listed by MediaArea.
    -MpvNetComFile Optional override path to mpvnet.com. Defaults to the upstream helper download.

Dependencies:
    7zip installation found at: 'C:\Program Files\7-Zip\7z.exe'.
    Inno Setup compiler installation found at: 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' unless -SkipInstaller is used.
    GitHub CLI https://cli.github.com, the env var GH_TOKEN must be defined unless -SkipGitHubRelease is used.
    Internet access to download FFmpeg, libmpv, yt-dlp, mpvnet.com and MediaInfo for the portable package.
    Internet access to download Gettext.Tools from NuGet when msgfmt.exe is not available on PATH.

Notes:
    The release version is read from:
        \mpv.net\src\BuildVersion.props
#>

param(
    [Parameter(Position = 0, Mandatory = $true)]
    [string] $SourceDir,

    [Parameter(Position = 1, Mandatory = $true)]
    [string] $OutputRootDir,

    [string] $Repo = 'WandersondeSouza/mpv.net',

    [switch] $SkipPortableZip,

    [switch] $SkipInstaller,

    [switch] $SkipGitHubRelease,

    [switch] $EnableFileLogging,

    [ValidateSet('normal', 'x86_64-v3')]
    [string] $MpvBuildVariant = $(if ($env:MPVNET_MPV_BUILD_VARIANT) { $env:MPVNET_MPV_BUILD_VARIANT } else { 'x86_64-v3' }),

    [string] $MediaInfoFile,

    [string] $MediaInfoVersion,

    [string] $MpvNetComFile,

    [string] $ReleaseNotes,

    [string] $ReleaseNotesFile
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

    $createMoScript = Test (Join-Path $sourceDir '..\lang\compile-mo-files.ps1')
    AddGettextToolsToPath $workDir
    & $createMoScript (Join-Path $sourceDir 'MpvNet.Windows\bin\Release\win-x64') | ForEach-Object { Write-Host $_ }
    if ($LastExitCode) { throw $LastExitCode }

    return Test $localeDir
}

function GetReleaseNotes($versionName, $explicitNotes, $notesFile) {
    if ($explicitNotes) {
        return $explicitNotes.Trim()
    }

    if ($notesFile) {
        $resolvedNotesFile = TestFile $notesFile
        $notes = (Get-Content -LiteralPath $resolvedNotesFile -Encoding UTF8 -Raw).Trim()
        if ($notes) {
            return $notes
        }

        throw "Release notes file is empty: $resolvedNotesFile"
    }

    return @"
# MPV.NET Media Player v$versionName

Release de manutencao do fork `WandersondeSouza/mpv.net`.

Preencha a descricao desta versao diretamente no corpo da publicacao do GitHub antes da publicacao final.
"@.Trim()
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

# Dotnet Publish
$BuildConfiguration = 'Release'
$EnableFileLoggingValue = if ($EnableFileLogging) { 'true' } else { 'false' }
$PublishDir64 = Join-Path $SourceDir "MpvNet.Windows\bin\$BuildConfiguration\win-x64\publish\"
$ProjectFile = Test (Join-Path $SourceDir 'MpvNet.Windows\MpvNet.Windows.csproj')
DeleteDir $PublishDir64
dotnet publish $ProjectFile --self-contained true --configuration $BuildConfiguration --runtime win-x64 --output $PublishDir64 /p:IncludeNativeLibrariesForSelfExtract=false /p:EnsureBuildAssets=false /p:EnableFileLogging=$EnableFileLoggingValue
if ($LastExitCode) { throw "dotnet publish failed with exit code $LastExitCode" }
$PublishedExeFile64 = Test ($PublishDir64 + 'mpvnet.exe')
$BinDirX64 = Test (Join-Path $SourceDir "MpvNet.Windows\bin\$BuildConfiguration\win-x64\")
$EnsureDependenciesScript = Test (Join-Path $SourceDir 'Tools\prepare-native-dependencies.ps1')
$EnsureDependenciesArgs = @{
    SourceDir = $SourceDir
    TargetDir = $BinDirX64
    PublishDir = $PublishDir64
    ArtifactsDir = Join-Path (Split-Path $SourceDir -Parent) 'artifacts\native-dependencies'
    MaxCacheAgeDays = 2
    MpvBuildVariant = $MpvBuildVariant
}
if ($MediaInfoVersion) {
    $EnsureDependenciesArgs.MediaInfoVersion = $MediaInfoVersion
}
if ($MediaInfoFile) {
    $EnsureDependenciesArgs.MediaInfoFile = $MediaInfoFile
}
if ($MpvNetComFile) {
    $EnsureDependenciesArgs.MpvNetComFile = $MpvNetComFile
}

# Create OutputName
$VersionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($PublishedExeFile64)
$IsBeta = $VersionInfo.ProductVersion -match '(?i)(^|[-+.])(alpha|beta|preview|rc)([-+.]|$)'
$BetaString = if ($IsBeta) { '-beta' } else { '' }
$DiagnosticString = if ($EnableFileLogging) { '-diagnostic' } else { '' }
$VersionName = $VersionInfo.FileVersion
$ReleaseNotes = GetReleaseNotes $VersionName $ReleaseNotes $ReleaseNotesFile
$InstallerOutputName64 = 'MPV.NET-Media-Player-v' + $VersionName
$OutputName64 = $InstallerOutputName64 + $BetaString + $DiagnosticString + '-portable-x64'

# Create OutputFolder
$OutputDir64   = Join-Path $OutputRootDir ($OutputName64 + '\')
DeleteDir $OutputDir64
mkdir $OutputDir64

# Copy Files
Copy-Item ($PublishDir64 + '*') $OutputDir64
& $EnsureDependenciesScript @EnsureDependenciesArgs
if ($LastExitCode) { throw $LastExitCode }
$ExtraFiles = 'libmpv-2.dll', 'libmpv-2.variant.txt', 'MediaInfo.dll'
CopyExtraFiles $BinDirX64 $OutputDir64 $ExtraFiles
CopyExtraFiles $BinDirX64 $PublishDir64 $ExtraFiles
$LocaleDir = EnsureLocale `
    $SourceDir `
    (Join-Path $SourceDir "MpvNet.Windows\bin\$BuildConfiguration\win-x64\Locale\") `
    (Join-Path $env:TEMP 'mpv.net-release-locale')
CopyDir $LocaleDir (Join-Path $OutputDir64 'Locale') | Out-Null
CopyDir $LocaleDir (Join-Path $PublishDir64 'Locale') | Out-Null
AddPortableConfig $OutputDir64 $DocsDir

$NativeValidationScript = Test (Join-Path $SourceDir 'Tools\validate-native-dependencies.ps1')
& $NativeValidationScript -Path $OutputDir64
if ($LastExitCode) { throw $LastExitCode }
& $NativeValidationScript -Path $PublishDir64
if ($LastExitCode) { throw $LastExitCode }

$ReleaseFiles = @()

if (-not $SkipPortableZip) {
    # Pack
    $ZipOutputFile64 = Join-Path $OutputRootDir ($OutputName64 + '.zip')
    & $7zFile a -tzip -mx9 $ZipOutputFile64 -r ($OutputDir64 + '*')
    if ($LastExitCode) { throw $LastExitCode }
    Test $ZipOutputFile64
    & $NativeValidationScript -ZipFile $ZipOutputFile64
    if ($LastExitCode) { throw $LastExitCode }

    $ReleaseFiles += $ZipOutputFile64
}

if (-not $SkipInstaller) {
    # Inno Setup
    ''; ''
    $InnoSetupScript = Test (Join-Path $SourceDir 'Setup\Inno\build-windows-installer.iss')
    & $InnoSetupCompiler "/O$OutputRootDir" $InnoSetupScript
    if ($LastExitCode) { throw $LastExitCode }
    $SetupFile = Test (Join-Path $OutputRootDir "$InstallerOutputName64-setup-x64.exe")

    if ($IsBeta -or $EnableFileLogging) {
        $NewSetupFile = Join-Path $OutputRootDir "$InstallerOutputName64$BetaString$DiagnosticString-setup-x64.exe"
        Move-Item $SetupFile $NewSetupFile
        $SetupFile = $NewSetupFile
    }

    $ReleaseFiles += $SetupFile
}

# Release
$Title = 'v' + $VersionName + $BetaString

if (-not $SkipGitHubRelease) {
    if (-not $ReleaseFiles.Count) {
        throw 'No release files were generated. Disable -SkipPortableZip or -SkipInstaller before publishing.'
    }

    if ($BetaString) {
        gh release create $Title -t $Title -n $ReleaseNotes --repo $Repo --prerelease $ReleaseFiles
    } else {
        gh release create $Title -t $Title -n $ReleaseNotes --repo $Repo $ReleaseFiles
    }

    if ($LastExitCode) { throw $LastExitCode }
}
