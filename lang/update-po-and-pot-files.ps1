
$ErrorActionPreference = 'Stop'

# Write list of .cs files into cs-files.txt file
$csFiles = Get-ChildItem $PSScriptRoot/.. -Recurse -File -Filter '*.cs' |
    Where-Object { $_ -notmatch '[/\\]obj[/\\]' } |
    ForEach-Object { $_.FullName }
$utf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false
[System.IO.File]::WriteAllLines("$PSScriptRoot/cs-files.txt", $csFiles, $utf8)

# Create .pot file
xgettext -k_ -k_n:1,2 -k_p:1c,2 -k_pn:1c,2,3 --force-po --from-code=UTF-8 '--language=c#' -o $PSScriptRoot/source.pot --files-from=$PSScriptRoot/cs-files.txt --keyword=_
if ($LastExitCode) { throw $LastExitCode }

function Escape-PotString {
    param([string]$value)
    return $value -replace '\\', '\\\\' -replace '"', '\\"'
}

function Get-XamlGettextStrings {
    param([string]$sourceRoot)

    $results = @{}
    $regexGettext = [regex]'\{ngettext:Gettext\s+([^}]*)\}'
    $regexPlural = [regex]'\{ngettext:PluralGettext\s+([^,}]*)\s*,\s*([^}]*)\}'

    $xamlFiles = Get-ChildItem $sourceRoot -Recurse -File -Filter '*.xaml' |
        Where-Object { $_.FullName -notmatch '[/\\]obj[/\\]' }

    foreach ($file in $xamlFiles) {
        $lines = Get-Content $file.FullName
        for ($i = 0; $i -lt $lines.Length; $i++) {
            $lineNumber = $i + 1
            $line = $lines[$i]

            foreach ($match in $regexGettext.Matches($line)) {
                $msgid = $match.Groups[1].Value.Trim()
                if (-not [string]::IsNullOrWhiteSpace($msgid)) {
                    $key = "gettext|$msgid"
                    if (-not $results.ContainsKey($key)) {
                        $results[$key] = [ordered]@{ MsgId = $msgid; References = @() }
                    }
                    $results[$key].References += "$($file.FullName):$lineNumber"
                }
            }

            foreach ($match in $regexPlural.Matches($line)) {
                $msgid = $match.Groups[1].Value.Trim()
                $msgidPlural = $match.Groups[2].Value.Trim()
                if (-not [string]::IsNullOrWhiteSpace($msgid) -and -not [string]::IsNullOrWhiteSpace($msgidPlural)) {
                    $key = "plural|$msgid|$msgidPlural"
                    if (-not $results.ContainsKey($key)) {
                        $results[$key] = [ordered]@{ MsgId = $msgid; MsgIdPlural = $msgidPlural; References = @() }
                    }
                    $results[$key].References += "$($file.FullName):$lineNumber"
                }
            }
        }
    }

    return $results
}

function Add-XamlGettextEntriesToPot {
    param([string]$potPath, [string]$sourceRoot)

    $entries = Get-XamlGettextStrings -sourceRoot $sourceRoot
    if ($entries.Count -eq 0) { return }

    $tempPot = "$potPath.xaml.tmp"
    Copy-Item $potPath $tempPot -Force

    foreach ($entry in $entries.GetEnumerator() | Sort-Object Name) {
        $item = $entry.Value
        $lines = @()
        foreach ($ref in $item.References | Sort-Object -Unique) {
            $lines += "#: $ref"
        }

        if ($item.Keys -contains 'MsgIdPlural') {
            $lines += 'msgid "' + (Escape-PotString $item.MsgId) + '"'
            $lines += 'msgid_plural "' + (Escape-PotString $item.MsgIdPlural) + '"'
            $lines += 'msgstr[0] ""'
            $lines += 'msgstr[1] ""'
        }
        else {
            $lines += 'msgid "' + (Escape-PotString $item.MsgId) + '"'
            $lines += 'msgstr ""'
        }
        $lines += ''
        Add-Content -Path $tempPot -Value $lines
    }

    $msguniq = Get-Command msguniq -ErrorAction SilentlyContinue
    if ($msguniq) {
        msguniq --sort-output --use-first -o $potPath $tempPot
        if ($LastExitCode) { throw $LastExitCode }
        Remove-Item $tempPot -Force
    } else {
        Copy-Item $tempPot $potPath -Force
        Remove-Item $tempPot -Force
        Write-Warning 'msguniq not found: source.pot may contain duplicate entries from XAML extraction.'
    }
}

Add-XamlGettextEntriesToPot -potPath $PSScriptRoot/source.pot -sourceRoot (Resolve-Path "$PSScriptRoot/.." | Select-Object -ExpandProperty Path)

# Backup .po files
$BackupTargetFolder = $env:TEMP + '/mpv.net po backup ' + (Get-Date -Format 'yyyy-MM-dd HH_mm_ss')
Copy-Item $PSScriptRoot/po $BackupTargetFolder -Force -Recurse
'PO file backup: ' + (Resolve-Path $BackupTargetFolder)

# Update .po files
(Get-ChildItem $PSScriptRoot/PO -Filter '*.po').FullName |
    ForEach-Object { msgmerge --sort-output --backup=none --update $_ $PSScriptRoot/source.pot }

if ($LastExitCode) { throw $LastExitCode }
