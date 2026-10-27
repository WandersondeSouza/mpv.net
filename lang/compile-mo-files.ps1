
param(
    [string] $OutputDir = "$PSScriptRoot/../src/MpvNet.Windows/bin/Debug"
)

$ErrorActionPreference = 'Stop'

function Get-CommandPath {
    param([string]$name)
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$GetTextBin = Join-Path $PSScriptRoot '..\artifacts\build-assets\locale\gettext-tools\tools\bin'
$MsgAttrib = Join-Path $GetTextBin 'msgattrib.exe'
$MsgUniq = Join-Path $GetTextBin 'msguniq.exe'
$MsgFmt = Join-Path $GetTextBin 'msgfmt.exe'
if (-not (Test-Path $MsgAttrib)) { $MsgAttrib = Get-CommandPath 'msgattrib' }
if (-not (Test-Path $MsgUniq)) { $MsgUniq = Get-CommandPath 'msguniq' }
if (-not (Test-Path $MsgFmt)) { $MsgFmt = Get-CommandPath 'msgfmt' }

$Python = Get-CommandPath 'python'
$hasGettextTools = $MsgAttrib -and $MsgUniq -and $MsgFmt
$hasPythonFallback = $Python -ne $null

$PoFiles = Get-ChildItem $PSScriptRoot/po -Filter '*.po' -File
$ExeFolder = $OutputDir

$validationScript = Join-Path $PSScriptRoot 'validate-po-files.ps1'
$potPath = Join-Path $PSScriptRoot 'source.pot'
if ((Test-Path $validationScript) -and (Test-Path $potPath)) {
    Write-Host "Validating PO files before compilation"
    & $validationScript -PoDirectory (Join-Path $PSScriptRoot 'po') -PotPath $potPath -ValidateOnly
    if ($LastExitCode) { throw $LastExitCode }
}

if (-not $hasGettextTools) {
    Write-Warning 'Gettext tools not available. Using Python compilation fallback.'
    if (-not $hasPythonFallback) {
        throw 'Neither gettext tools nor Python fallback are available. Cannot compile MO files.'
    }
}

& $Python "$PSScriptRoot/verify-language-expansion.py"
if ($LastExitCode) { throw 'Expanded language catalog validation failed.' }

foreach ($it in $PoFiles)
{
    $folder = "$ExeFolder/Locale/$($it.BaseName)/LC_MESSAGES"

    if (-not (Test-Path $folder))
    {
        New-Item -ItemType Directory -Path $folder | Out-Null
    }

    $moPath = "$folder/mpvnet.mo"
    if ($hasGettextTools) {
        $filteredPo = "$folder/$($it.BaseName).filtered.po"

        Write-Host "Compiling $($it.Name) using gettext tools"
        & $MsgAttrib --no-obsolete --output-file="$filteredPo" "$($it.FullName)"
        if ($LastExitCode) {
            throw "Invalid .po file: $($it.FullName) (msgattrib exit code $LastExitCode)"
        }

        $dedupedPo = "$folder/$($it.BaseName).deduped.po"
        & $MsgUniq --output-file="$dedupedPo" "$filteredPo"
        if ($LastExitCode) {
            throw "Invalid .po file: $($it.FullName) (msguniq exit code $LastExitCode)"
        }

        & $MsgFmt --check --output-file="$moPath" "$dedupedPo"
        if ($LastExitCode) {
            throw "Invalid .po file: $($it.FullName) (msgfmt exit code $LastExitCode)"
        }

        Remove-Item -Force "$filteredPo" -ErrorAction SilentlyContinue
        Remove-Item -Force "$dedupedPo" -ErrorAction SilentlyContinue
        Write-Host "Compiled $moPath"
    }
    else {
        Write-Host "Compiling $($it.Name) using Python fallback"
        & $Python "$PSScriptRoot/create-mo-files-fallback.py" --po-file "$($it.FullName)" --mo-file "$moPath"
        if ($LastExitCode) {
            throw "Invalid .po file: $($it.FullName) (Python fallback exit code $LastExitCode)"
        }
    }
}

& $Python "$PSScriptRoot/verify-language-expansion.py" --mo-directory $OutputDir
if ($LastExitCode) { throw 'Compiled expanded language catalog validation failed.' }
