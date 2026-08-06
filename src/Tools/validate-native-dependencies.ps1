<#

Validates native DLLs in a publish/package directory or a recursively nested
ZIP-compatible artifact (ZIP, APPX, MSIX, bundles and Store uploads).

#>

param(
    [string] $Path,

    [string] $ZipFile,

    [string] $ArchiveFile,

    [string] $SevenZipPath = 'C:\Program Files\7-Zip\7z.exe'
)

$ErrorActionPreference = 'Stop'

$RequiredRuntimeDlls = @(
    'MediaInfo.dll',
    'D3DCompiler_47_cor3.dll',
    'vcruntime140_cor3.dll',
    'wpfgfx_cor3.dll',
    'PenImc_cor3.dll',
    'PresentationNative_cor3.dll'
)

. (Join-Path $PSScriptRoot 'libmpv-validation.ps1')

$archiveExtensions = @('.zip', '.appx', '.msix', '.appxbundle', '.msixbundle', '.appxupload', '.msixupload')
$providedInputs = @(@($Path, $ZipFile, $ArchiveFile) | Where-Object { $_ })
if ($providedInputs.Count -ne 1) {
    throw 'Pass exactly one input: -Path <publish-or-package-dir>, -ZipFile <portable.zip>, or -ArchiveFile <package>.'
}

function Expand-ArchiveForValidation([string] $SourceFile, [string] $Destination) {
    if (-not (Test-Path -LiteralPath $SourceFile -PathType Leaf)) {
        throw "Package archive not found: $SourceFile"
    }

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    if (Test-Path -LiteralPath $SevenZipPath -PathType Leaf) {
        $process = Start-Process -FilePath $SevenZipPath -ArgumentList @('x', $SourceFile, "-o$Destination", '-y') -NoNewWindow -Wait -PassThru
        if ($process.ExitCode) {
            throw "7-Zip failed extracting $SourceFile with exit code $($process.ExitCode)"
        }
    }
    else {
        Expand-Archive -LiteralPath $SourceFile -DestinationPath $Destination -Force
    }

    return (Resolve-Path -LiteralPath $Destination).Path
}

function Get-ArchivePayloadDirectories([string] $SourceFile, [string] $Destination, [int] $Depth = 0) {
    if ($Depth -gt 4) {
        throw "Package archive nesting exceeds the supported validation depth: $SourceFile"
    }

    $root = Expand-ArchiveForValidation $SourceFile $Destination
    $directories = [System.Collections.Generic.List[string]]::new()
    $directories.Add($root)
    $nestedArchives = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
        $archiveExtensions -contains $_.Extension.ToLowerInvariant()
    })

    foreach ($nestedArchive in $nestedArchives) {
        $nestedDestination = Join-Path $root ('.nested-' + [Guid]::NewGuid().ToString('N'))
        foreach ($directory in (Get-ArchivePayloadDirectories $nestedArchive.FullName $nestedDestination ($Depth + 1))) {
            $directories.Add($directory)
        }
    }

    return $directories
}

function Get-DualLibMpvRoots([string] $Root) {
    $roots = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($normal in @(Get-ChildItem -LiteralPath $Root -Filter 'libmpv-2.dll' -Recurse -File)) {
        $candidateRoot = $normal.DirectoryName
        if (Test-Path -LiteralPath (Join-Path $candidateRoot 'libmpv-2-v3.dll') -PathType Leaf) {
            $roots.Add($candidateRoot) | Out-Null
        }
    }

    return @($roots)
}

$temporaryRoot = $null
try {
    if ($Path) {
        if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
            throw "Validation path not found: $Path"
        }
        $searchRoots = @((Resolve-Path -LiteralPath $Path).Path)
        $description = "directory '$Path'"
    }
    else {
        $sourceArchive = if ($ZipFile) { $ZipFile } else { $ArchiveFile }
        $temporaryRoot = Join-Path $env:TEMP ('mpv.net-native-validation-' + [Guid]::NewGuid().ToString('N'))
        $searchRoots = @(Get-ArchivePayloadDirectories $sourceArchive $temporaryRoot)
        $description = "archive '$sourceArchive'"
    }

    $payloadRoots = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($searchRoot in $searchRoots) {
        foreach ($payloadRoot in (Get-DualLibMpvRoots $searchRoot)) {
            $payloadRoots.Add($payloadRoot) | Out-Null
        }
    }

    if (-not $payloadRoots.Count) {
        throw "No payload containing both libmpv-2.dll and libmpv-2-v3.dll was found in $description."
    }

    foreach ($payloadRoot in $payloadRoots) {
        $libMpv = Assert-LibMpvBuilds -Root $payloadRoot
        Write-Host "OK libmpv-2.dll $($libMpv.Normal.Length) bytes sha256=$($libMpv.Normal.Sha256)"
        Write-Host "OK libmpv-2-v3.dll $($libMpv.X86_64V3.Length) bytes sha256=$($libMpv.X86_64V3.Sha256)"

        foreach ($dll in $RequiredRuntimeDlls) {
            $file = Assert-PeX64 (Join-Path $payloadRoot $dll)
            $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName).FileVersion
            Write-Host "OK $dll $($file.Length) bytes $version"
        }

        Write-Host "Native dependency validation completed: $payloadRoot"
    }
}
finally {
    if ($temporaryRoot -and (Test-Path -LiteralPath $temporaryRoot)) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
