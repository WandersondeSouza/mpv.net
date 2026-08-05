<#

Validates managed runtime files that must be present in every distribution.

Directories are checked directly. ZIP-compatible packages (ZIP, APPX, MSIX,
bundles and Store upload containers) are inspected recursively so payloads
inside nested Store archives are validated as well.

#>

param(
    [string] $Path,
    [string] $ArchiveFile,
    [string[]] $RequiredRelativePaths = @('mpvnet.exe', 'Scripts\osc.lua')
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

function Get-ArchiveEntryNames([System.IO.Stream] $Stream, [int] $Depth = 0) {
    if ($Depth -gt 4) {
        throw 'Package archive nesting exceeds the supported validation depth.'
    }

    $archive = [System.IO.Compression.ZipArchive]::new(
        $Stream,
        [System.IO.Compression.ZipArchiveMode]::Read,
        $true)
    try {
        $names = [System.Collections.Generic.List[string]]::new()
        foreach ($entry in $archive.Entries) {
            $entryName = Normalize-RelativePath $entry.FullName
            $names.Add($entryName)

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
            $_ -eq $normalizedRequiredPath -or
            $_.EndsWith('/' + $normalizedRequiredPath, [StringComparison]::OrdinalIgnoreCase)
        })
        if (-not $matched.Count) {
            throw "Required package file '$normalizedRequiredPath' was not found in archive: $resolvedFile"
        }
    }

    Write-Host "Required package content validated: $resolvedFile"
}

if ($Path) {
    Test-RequiredDirectoryContent $Path $RequiredRelativePaths
}
else {
    Test-RequiredArchiveContent $ArchiveFile $RequiredRelativePaths
}
