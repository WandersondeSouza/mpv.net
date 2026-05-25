
param(
    [string] $OutputDir = "$PSScriptRoot/../src/MpvNet.Windows/bin/Debug"
)

$ErrorActionPreference = 'Stop'

$PoFiles = Get-ChildItem $PSScriptRoot/po
$ExeFolder = $OutputDir

foreach ($it in $PoFiles)
{
    $folder = "$ExeFolder/Locale/$($it.BaseName)/LC_MESSAGES"

    if (-not (Test-Path $folder))
    {
        New-Item -ItemType Directory -Path $folder | Out-Null
    }

    $moPath = "$folder/mpvnet.mo"
    Write-Host "Compiling $($it.Name)"
    & msgfmt --output-file="$moPath" "$($it.FullName)"
    if ($LastExitCode) {
        Write-Warning "Skipping invalid .po file: $($it.FullName) (msgfmt exit code $LastExitCode)"
        $global:LastExitCode = 0
        continue
    }
    $moPath
}
