<#

Updates mpv (x64) and libmpv (x64, ARM64).

Files are downloaded from:
    https://github.com/shinchiro/mpv-winbuild-cmake/releases

Requires 7zip being installed at 'C:\Program Files\7-Zip\7z.exe'.

Needs 3 positional CLI arguments:
    1. Directory where mpv x64 is located. To skip pass '-'.
    2. Directory where libmpv x64 is located. To skip pass '-'.
    3. Directory where libmpv ARM64 is located. To skip pass '-'.

The script validates the GitHub SHA-256 digest and the expected PE machine
before touching an installation. The mpv directory is replaced through a
same-volume backup/swap so a failed promotion can restore the previous copy.
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $MpvDirX64 = '-',

    [Parameter(Position = 1)]
    [string] $LibmpvDirX64 = '-',

    [Parameter(Position = 2)]
    [string] $LibmpvDirARM64 = '-'
)

$ErrorActionPreference = 'Stop'
$7ZipPath = 'C:\Program Files\7-Zip\7z.exe'
$UpdateRoot = $null

function Resolve-UpdateTarget([string] $path, [string] $label, [string] $requiredFile) {
    if ([string]::IsNullOrWhiteSpace($path) -or $path -eq '-') {
        return $null
    }

    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "$label location not found: $path. Pass '-' to skip it."
    }

    $directory = Get-Item -LiteralPath $path -ErrorAction Stop
    if (($directory.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$label location must not be a reparse point: $($directory.FullName)"
    }

    $requiredPath = Join-Path $directory.FullName $requiredFile
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "$label required file not found: $requiredPath"
    }

    return $directory.FullName
}

function Get-Sha256Hex([string] $path) {
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($path)
        try {
            return ([System.BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $sha256.Dispose()
    }
}

function Assert-PortableExecutable([string] $path, [uint16] $expectedMachine, [string] $architecture) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required $architecture runtime file not found: $path"
    }

    $file = Get-Item -LiteralPath $path
    if ($file.Length -le 0) {
        throw "Required $architecture runtime file is empty: $path"
    }

    $stream = [System.IO.File]::OpenRead($file.FullName)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        if ($stream.Length -lt 0x40) {
            throw "Invalid PE header in $($file.FullName)"
        }

        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -le 0 -or $peOffset -gt ($stream.Length - 6)) {
            throw "Invalid PE header in $($file.FullName)"
        }

        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) {
            throw "Invalid PE signature in $($file.FullName)"
        }

        $machine = $reader.ReadUInt16()
        if ($machine -ne $expectedMachine) {
            throw "Expected $architecture native binary, got machine 0x$($machine.ToString('X4')): $($file.FullName)"
        }
    }
    finally {
        $stream.Dispose()
    }

    return $file
}

function Get-GitHubRelease() {
    $apiUrl = 'https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest'
    $response = Invoke-WebRequest -Uri $apiUrl -Headers @{ Accept = 'application/vnd.github+json' } -UserAgent 'mpv-net-runtime-updater' -UseBasicParsing -ErrorAction Stop
    return $response.Content | ConvertFrom-Json
}

function Get-ReleaseAsset($release, [string] $pattern) {
    $assets = @($release.assets | Where-Object { $_.name -match $pattern })
    if ($assets.Count -ne 1) {
        throw "Expected exactly one GitHub release asset matching '$pattern', found $($assets.Count)."
    }

    $asset = $assets[0]
    if ($asset.name -ne [System.IO.Path]::GetFileName($asset.name)) {
        throw "GitHub release asset has an unsafe filename: $($asset.name)"
    }

    $digestProperty = $asset.PSObject.Properties['digest']
    $digest = if ($digestProperty) { [string] $digestProperty.Value } else { $null }
    if ($digest -notmatch '^sha256:[0-9a-fA-F]{64}$') {
        throw "GitHub release asset has no valid SHA-256 digest: $($asset.name)"
    }

    $uri = [System.Uri]::new([string] $asset.browser_download_url)
    if ($uri.Scheme -ne 'https' -or $uri.Host -ine 'github.com') {
        throw "GitHub release asset URL is not a trusted HTTPS GitHub URL: $($uri.AbsoluteUri)"
    }

    return [pscustomobject]@{
        Name = [string] $asset.name
        Uri = $uri
        Digest = $digest.Substring(7).ToLowerInvariant()
    }
}

function Download-ReleaseAsset($asset) {
    $path = Join-Path $UpdateRoot ($asset.Name + '.download')
    Invoke-WebRequest -Uri $asset.Uri.AbsoluteUri -UserAgent 'mpv-net-runtime-updater' -OutFile $path -UseBasicParsing -ErrorAction Stop

    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "GitHub release asset was not downloaded: $($asset.Name)"
    }

    $actualDigest = Get-Sha256Hex $path
    if ($actualDigest -ne $asset.Digest) {
        throw "SHA-256 mismatch for $($asset.Name): expected $($asset.Digest), got $actualDigest"
    }

    Write-Host "Downloaded $($asset.Name) sha256=$actualDigest"
    return Get-Item -LiteralPath $path
}

function Unpack-Archive([System.IO.FileInfo] $archive) {
    if (-not (Test-Path -LiteralPath $7ZipPath -PathType Leaf)) {
        throw "7-Zip was not found: $7ZipPath"
    }

    $outputDir = Join-Path $UpdateRoot ('extract-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    $process = Start-Process -FilePath $7ZipPath -ArgumentList @('x', ('"{0}"' -f $archive.FullName), ('-o"{0}"' -f $outputDir), '-y') -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "7-Zip failed extracting $($archive.Name) with exit code $($process.ExitCode)"
    }

    return Get-Item -LiteralPath $outputDir
}

function Get-PayloadFile([System.IO.DirectoryInfo] $root, [string] $name) {
    $files = @(Get-ChildItem -LiteralPath $root.FullName -Filter $name -Recurse -File)
    if ($files.Count -ne 1) {
        throw "Expected exactly one $name in $($root.FullName), found $($files.Count)."
    }

    return $files[0]
}

function Get-PayloadRoot([System.IO.DirectoryInfo] $root, [string] $requiredFile) {
    return (Get-PayloadFile $root $requiredFile).Directory
}

function New-SiblingPath([string] $target, [string] $prefix) {
    $parent = [System.IO.Path]::GetDirectoryName($target)
    if ([string]::IsNullOrWhiteSpace($parent)) {
        throw "Cannot create a staging path without a target parent: $target"
    }

    return Join-Path $parent (".$prefix-" + [Guid]::NewGuid().ToString('N'))
}

function Copy-DirectoryContents([string] $source, [string] $destination) {
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($entry in Get-ChildItem -LiteralPath $source -Force) {
        Copy-Item -LiteralPath $entry.FullName -Destination $destination -Recurse -Force
    }
}

function Install-MpvDirectory([string] $source, [string] $target) {
    $stage = New-SiblingPath $target 'mpvnet-runtime-stage'
    $backup = New-SiblingPath $target 'mpvnet-runtime-backup'
    $oldMoved = $false

    try {
        Copy-DirectoryContents $source $stage
        Assert-PortableExecutable (Join-Path $stage 'mpv.exe') 0x8664 'x64 mpv' | Out-Null

        Move-Item -LiteralPath $target -Destination $backup
        $oldMoved = $true
        Move-Item -LiteralPath $stage -Destination $target
        Assert-PortableExecutable (Join-Path $target 'mpv.exe') 0x8664 'x64 mpv' | Out-Null
    }
    catch {
        if ($oldMoved) {
            if (Test-Path -LiteralPath $target) {
                Remove-Item -LiteralPath $target -Recurse -Force
            }
            if (Test-Path -LiteralPath $backup) {
                Move-Item -LiteralPath $backup -Destination $target
            }
        }
        throw
    }
    finally {
        if (Test-Path -LiteralPath $stage) {
            Remove-Item -LiteralPath $stage -Recurse -Force
        }
    }

    if (Test-Path -LiteralPath $backup) {
        try {
            Remove-Item -LiteralPath $backup -Recurse -Force
        }
        catch {
            Write-Warning "The previous mpv directory was kept as a backup: $backup"
        }
    }
}

function Install-LibMpvFile([string] $source, [string] $targetDirectory, [uint16] $expectedMachine, [string] $architecture) {
    $target = Join-Path $targetDirectory 'libmpv-2.dll'
    $stage = New-SiblingPath $target 'mpvnet-libmpv-stage'
    $backup = New-SiblingPath $target 'mpvnet-libmpv-backup'
    $oldMoved = $false

    try {
        Copy-Item -LiteralPath $source -Destination $stage -Force
        Assert-PortableExecutable $stage $expectedMachine $architecture | Out-Null

        Move-Item -LiteralPath $target -Destination $backup
        $oldMoved = $true
        Move-Item -LiteralPath $stage -Destination $target
        Assert-PortableExecutable $target $expectedMachine $architecture | Out-Null
    }
    catch {
        if ($oldMoved) {
            if (Test-Path -LiteralPath $target) {
                Remove-Item -LiteralPath $target -Force
            }
            if (Test-Path -LiteralPath $backup) {
                Move-Item -LiteralPath $backup -Destination $target
            }
        }
        throw
    }
    finally {
        if (Test-Path -LiteralPath $stage) {
            Remove-Item -LiteralPath $stage -Force
        }
    }

    if (Test-Path -LiteralPath $backup) {
        try {
            Remove-Item -LiteralPath $backup -Force
        }
        catch {
            Write-Warning "The previous libmpv file was kept as a backup: $backup"
        }
    }
}

$MpvTarget = Resolve-UpdateTarget $MpvDirX64 'mpv x64' 'mpv.exe'
$LibmpvTargetX64 = Resolve-UpdateTarget $LibmpvDirX64 'libmpv x64' 'libmpv-2.dll'
$LibmpvTargetARM64 = Resolve-UpdateTarget $LibmpvDirARM64 'libmpv ARM64' 'libmpv-2.dll'

if (-not $MpvTarget -and -not $LibmpvTargetX64 -and -not $LibmpvTargetARM64) {
    Write-Host "No runtime target selected. Pass a directory or '-' for each positional argument."
    return
}

$UpdateRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('mpvnet-runtime-update-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $UpdateRoot -Force | Out-Null

try {
    $release = Get-GitHubRelease
    $mpvPayloadRoot = $null
    $libmpvPayloadX64 = $null
    $libmpvPayloadARM64 = $null

    if ($MpvTarget) {
        $mpvArchive = Download-ReleaseAsset (Get-ReleaseAsset $release '^mpv-x86_64-[0-9]{8}-git-[0-9a-z]+\.7z$')
        $mpvPayloadRoot = Get-PayloadRoot (Unpack-Archive $mpvArchive) 'mpv.exe'
        Assert-PortableExecutable (Join-Path $mpvPayloadRoot.FullName 'mpv.exe') 0x8664 'x64 mpv' | Out-Null
    }

    if ($LibmpvTargetX64) {
        $libmpvArchiveX64 = Download-ReleaseAsset (Get-ReleaseAsset $release '^mpv-dev-x86_64-[0-9]{8}-git-[0-9a-z]+\.7z$')
        $libmpvPayloadX64 = Get-PayloadFile (Unpack-Archive $libmpvArchiveX64) 'libmpv-2.dll'
        Assert-PortableExecutable $libmpvPayloadX64.FullName 0x8664 'x64 libmpv' | Out-Null
    }

    if ($LibmpvTargetARM64) {
        $libmpvArchiveARM64 = Download-ReleaseAsset (Get-ReleaseAsset $release '^mpv-dev-aarch64-[0-9]{8}-git-[0-9a-z]+\.7z$')
        $libmpvPayloadARM64 = Get-PayloadFile (Unpack-Archive $libmpvArchiveARM64) 'libmpv-2.dll'
        Assert-PortableExecutable $libmpvPayloadARM64.FullName 0xAA64 'ARM64 libmpv' | Out-Null
    }

    if ($MpvTarget) {
        Install-MpvDirectory $mpvPayloadRoot.FullName $MpvTarget
        Write-Host "Updated mpv x64: $MpvTarget"
    }

    if ($LibmpvTargetX64) {
        Install-LibMpvFile $libmpvPayloadX64.FullName $LibmpvTargetX64 0x8664 'x64 libmpv'
        Write-Host "Updated libmpv x64: $LibmpvTargetX64"
    }

    if ($LibmpvTargetARM64) {
        Install-LibMpvFile $libmpvPayloadARM64.FullName $LibmpvTargetARM64 0xAA64 'ARM64 libmpv'
        Write-Host "Updated libmpv ARM64: $LibmpvTargetARM64"
    }
}
finally {
    if ($UpdateRoot -and (Test-Path -LiteralPath $UpdateRoot)) {
        Remove-Item -LiteralPath $UpdateRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
