<#

Validates managed runtime files that must be present in every distribution.

Directories are checked directly. ZIP-compatible packages (ZIP, APPX, MSIX,
bundles and Store upload containers) are inspected recursively so payloads
inside nested Store archives are validated as well.

#>

param(
    [string] $Path,
    [string] $ArchiveFile,
    [string[]] $RequiredRelativePaths = @('mpvnet.exe', 'Scripts\osc.lua', 'libmpv-2.dll', 'libmpv-2-v3.dll'),
    [string[]] $OptionalComponentRelativePaths = @('ffmpeg.exe', 'ffplay.exe', 'ffprobe.exe', 'mpvnet.com', 'yt-dlp.exe')
)

$ErrorActionPreference = 'Stop'

if (($Path -and $ArchiveFile) -or (-not $Path -and -not $ArchiveFile)) {
    throw 'Specify exactly one input: -Path or -ArchiveFile.'
}

function Normalize-RelativePath([string] $Value) {
    return $Value.Replace('\', '/').TrimStart('/')
}

function Test-RequiredDirectoryContent([string] $Root, [string[]] $RequiredPaths) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        throw "Package directory not found: $Root"
    }

    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    foreach ($relativePath in $RequiredPaths) {
        $candidate = Join-Path $resolvedRoot $relativePath
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw "Required package file not found: $candidate"
        }
        if ((Get-Item -LiteralPath $candidate).Length -le 0) {
            throw "Required package file is empty: $candidate"
        }
    }

    Write-Host "Required package content validated: $resolvedRoot"
}

function Test-OptionalDirectoryComponents([string] $Root, [string[]] $OptionalPaths) {
    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $ffmpegPaths = @($OptionalPaths | Where-Object { $_ -in @('ffmpeg.exe', 'ffplay.exe', 'ffprobe.exe') })
    $presentFfmpegPaths = @($ffmpegPaths | Where-Object { Test-Path -LiteralPath (Join-Path $resolvedRoot $_) -PathType Leaf })
    if ($presentFfmpegPaths.Count -and $presentFfmpegPaths.Count -ne $ffmpegPaths.Count) {
        throw "Portable/package payload contains a partial FFmpeg bundle: $($presentFfmpegPaths -join ', ')"
    }

    foreach ($relativePath in $OptionalPaths) {
        $candidate = Join-Path $resolvedRoot $relativePath
        if ((Test-Path -LiteralPath $candidate -PathType Leaf) -and (Get-Item -LiteralPath $candidate).Length -le 0) {
            throw "Optional component is empty: $candidate"
        }
    }
}

function Get-ArchiveEntryNames([System.IO.Stream] $Stream, [int] $Depth = 0) {
    if ($Depth -gt 4) {
        throw 'Package archive nesting exceeds the supported validation depth.'
    }

    $archive = [System.IO.Compression.ZipArchive]::new(
        $Stream,
        [System.IO.Compression.ZipArchiveMode]::Read,
        $true)
    try {
        $names = [System.Collections.Generic.List[object]]::new()
        foreach ($entry in $archive.Entries) {
            $entryName = Normalize-RelativePath $entry.FullName
            $names.Add([pscustomobject]@{
                Name = $entryName
                Length = $entry.Length
            })

            if ($entryName -match '(?i)\.(appx|msix|appxbundle|msixbundle|appxupload|msixupload|zip)$') {
                $nestedStream = [System.IO.MemoryStream]::new()
                $entryStream = $entry.Open()
                try {
                    $entryStream.CopyTo($nestedStream)
                    $nestedStream.Position = 0
                    foreach ($nestedName in (Get-ArchiveEntryNames $nestedStream ($Depth + 1))) {
                        $names.Add($nestedName)
                    }
                }
                finally {
                    $entryStream.Dispose()
                    $nestedStream.Dispose()
                }
            }
        }
        return $names
    }
    finally {
        $archive.Dispose()
    }
}

function Test-RequiredArchiveContent([string] $File, [string[]] $RequiredPaths) {
    if (-not (Test-Path -LiteralPath $File -PathType Leaf)) {
        throw "Package archive not found: $File"
    }

    Add-Type -AssemblyName System.IO.Compression
    $resolvedFile = (Resolve-Path -LiteralPath $File).Path
    $stream = [System.IO.File]::OpenRead($resolvedFile)
    try {
        $entryNames = @(Get-ArchiveEntryNames $stream)
    }
    finally {
        $stream.Dispose()
    }

    foreach ($relativePath in $RequiredPaths) {
        $normalizedRequiredPath = Normalize-RelativePath $relativePath
        $matched = @($entryNames | Where-Object {
            $_.Name -eq $normalizedRequiredPath -or
            $_.Name.EndsWith('/' + $normalizedRequiredPath, [StringComparison]::OrdinalIgnoreCase)
        })
        if (-not $matched.Count) {
            throw "Required package file '$normalizedRequiredPath' was not found in archive: $resolvedFile"
        }
        if (-not @($matched | Where-Object { $_.Length -gt 0 }).Count) {
            throw "Required package file '$normalizedRequiredPath' is empty in archive: $resolvedFile"
        }
    }

    Write-Host "Required package content validated: $resolvedFile"
}

function Test-OptionalArchiveComponents([string] $File, [string[]] $OptionalPaths) {
    Add-Type -AssemblyName System.IO.Compression
    $resolvedFile = (Resolve-Path -LiteralPath $File).Path
    $stream = [System.IO.File]::OpenRead($resolvedFile)
    try {
        $entryNames = @(Get-ArchiveEntryNames $stream)
    }
    finally {
        $stream.Dispose()
    }

    $present = @{}
    foreach ($relativePath in $OptionalPaths) {
        $normalized = Normalize-RelativePath $relativePath
        $matches = @($entryNames | Where-Object {
            $_.Name -eq $normalized -or $_.Name.EndsWith('/' + $normalized, [StringComparison]::OrdinalIgnoreCase)
        })
        $present[$relativePath] = $matches
        if ($matches.Count -and -not @($matches | Where-Object { $_.Length -gt 0 }).Count) {
            throw "Optional component is empty in archive: $normalized"
        }
    }

    $ffmpegPaths = @('ffmpeg.exe', 'ffplay.exe', 'ffprobe.exe')
    $presentFfmpegPaths = @($ffmpegPaths | Where-Object { @($present[$_]).Count })
    if ($presentFfmpegPaths.Count -and $presentFfmpegPaths.Count -ne $ffmpegPaths.Count) {
        throw "Portable/package archive contains a partial FFmpeg bundle: $($presentFfmpegPaths -join ', ')"
    }
}

if ($Path) {
    Test-RequiredDirectoryContent $Path $RequiredRelativePaths
    Test-OptionalDirectoryComponents $Path $OptionalComponentRelativePaths
}
else {
    Test-RequiredArchiveContent $ArchiveFile $RequiredRelativePaths
    Test-OptionalArchiveComponents $ArchiveFile $OptionalComponentRelativePaths
}
