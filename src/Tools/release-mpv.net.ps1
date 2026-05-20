
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

# Throw error if the file/dir don't exist
function Test($path) {
    if (-not (Test-Path $path)) {
        throw $path
    }
    return $path
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
$ExtraFiles = 'mpvnet.com', 'libmpv-2.dll', 'MediaInfo.dll'
$ExtraFiles | ForEach-Object { Copy-Item ($BinDirX64 + $_) ($OutputDir64 + $_) }
$ExtraFiles | ForEach-Object { Copy-Item ($BinDirX64 + $_) ($PublishDir64 + $_) }
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
