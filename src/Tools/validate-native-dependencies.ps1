<#

Validates native DLLs in a publish/package directory, or in a portable ZIP.

#>

param(
    [string] $Path,

    [string] $ZipFile,

    [string] $SevenZipPath = 'C:\Program Files\7-Zip\7z.exe'
)

$ErrorActionPreference = 'Stop'

$RequiredDlls = @(
    'libmpv-2.dll',
    'MediaInfo.dll',
    'D3DCompiler_47_cor3.dll',
    'vcruntime140_cor3.dll',
    'wpfgfx_cor3.dll',
    'PenImc_cor3.dll',
    'PresentationNative_cor3.dll'
)

function Test-RequiredFile($path) {
    if (-not (Test-Path $path)) {
        throw "Required native dependency not found: $path"
    }

    $file = Get-Item $path
    if ($file.Length -le 0) {
        throw "Required native dependency is empty: $path"
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

function Expand-ZipForValidation($zipFile) {
    if (-not (Test-Path $zipFile)) {
        throw "ZIP file not found: $zipFile"
    }

    $tempDir = Join-Path $env:TEMP ("mpv.net-native-validation-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $tempDir | Out-Null

    if (Test-Path $SevenZipPath) {
        $process = Start-Process $SevenZipPath @('x', $zipFile, "-o$tempDir", '-y') -NoNewWindow -Wait -PassThru
        if ($process.ExitCode) {
            throw "7-Zip failed extracting $zipFile with exit code $($process.ExitCode)"
        }
    }
    else {
        Expand-Archive -Path $zipFile -DestinationPath $tempDir -Force
    }

    return $tempDir
}

if ($ZipFile) {
    $Path = Expand-ZipForValidation $ZipFile
}

if (-not $Path) {
    throw 'Pass -Path <publish-or-package-dir> or -ZipFile <portable.zip>.'
}

if (-not (Test-Path $Path)) {
    throw "Validation path not found: $Path"
}

try {
    $root = (Resolve-Path $Path).Path
    foreach ($dll in $RequiredDlls) {
        $matches = @(Get-ChildItem $root -Filter $dll -Recurse -File)
        if ($matches.Count -lt 1) {
            throw "Required native dependency not found under ${root}: $dll"
        }

        $file = Assert-PeX64 $matches[0].FullName
        $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName).FileVersion
        Write-Host "OK $dll $($file.Length) bytes $version"
    }

    Write-Host "Native dependency validation completed: $root"
}
finally {
    if ($ZipFile -and $Path -and (Test-Path $Path)) {
        Remove-Item $Path -Recurse -Force
    }
}
