
$ErrorActionPreference = 'Stop'

# Write the xgettext input list to a temporary file. Keeping this generated
# list in the repository leaked machine-specific paths and included build
# artifacts after local builds.
$sourceRoot = (Resolve-Path "$PSScriptRoot/..").Path
$csFiles = Get-ChildItem $sourceRoot -Recurse -File -Filter '*.cs' |
    Where-Object {
        $_.FullName -notmatch '[/\\](bin|obj|artifacts|AppPackages|MpvNet\.Tests|\.venv)[/\\]' -and
        $_.Name -ne 'Resources.Designer.cs'
    } |
    ForEach-Object { $_.FullName.Substring($sourceRoot.Length).TrimStart('\', '/') }

# Create .pot file
$xgettext = Get-Command xgettext -ErrorAction SilentlyContinue
if (-not $xgettext) {
    Write-Warning 'xgettext not found. Using Python fallback for source.pot extraction.'
    $python = Get-Command python -ErrorAction SilentlyContinue
    if (-not $python) { throw 'xgettext not found and Python is not available to generate source.pot.' }

    & python "$PSScriptRoot/update-source-pot-fallback.py"
    if ($LastExitCode) { throw $LastExitCode }
    Write-Host 'Python fallback source.pot extraction completed.'
} else {
    $utf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false
    $csFileList = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllLines($csFileList, $csFiles, $utf8)

    try {
        xgettext --directory=$sourceRoot -k_ -k_n:1,2 -k_p:1c,2 -k_pn:1c,2,3 -kAddMenuItem:1 --force-po --from-code=UTF-8 '--language=c#' -o $PSScriptRoot/source.pot --files-from=$csFileList --keyword=_
        if ($LastExitCode) { throw $LastExitCode }
    }
    finally {
        Remove-Item $csFileList -Force -ErrorAction SilentlyContinue
    }
}

function Escape-PotString {
    param([string]$value)
    return $value.Replace('\', '\\').Replace('"', '\"')
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
                    $relativePath = $file.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
                    $results[$key].References += "$($relativePath):$lineNumber"
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
                    $relativePath = $file.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
                    $results[$key].References += "$($relativePath):$lineNumber"
                }
            }
        }
    }

    return $results
}

function Add-EditorConfString {
    param([hashtable]$results, [string]$msgid)

    if ([string]::IsNullOrWhiteSpace($msgid)) { return }

    $trimmed = $msgid.Trim()
    $key = "gettext|$trimmed"

    if (-not $results.ContainsKey($key)) {
        $results[$key] = [ordered]@{ MsgId = $trimmed; References = @() }
    }
}

function Get-EditorConfStrings {
    param([string]$editorConfPath)

    $results = @{}
    if (-not (Test-Path $editorConfPath)) { return $results }

    $lines = Get-Content $editorConfPath -Encoding UTF8
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if ($trimmed -eq "" -or $trimmed.StartsWith("#")) { continue }

        if ($trimmed -match '^name\s*=\s*(.*)$') {
            Add-EditorConfString $results $matches[1]
        }
        elseif ($trimmed -match '^directory\s*=\s*(.*)$') {
            $directory = $matches[1]
            foreach ($part in $directory.Split('/')) {
                Add-EditorConfString $results $part.Trim()
            }
        }
        elseif ($trimmed -match '^help\s*=\s*(.*)$') {
            Add-EditorConfString $results $matches[1]
        }
        elseif ($trimmed -match '^option\s*=\s*(.*)$') {
            $value = $matches[1].Trim()
            if ($value -match '^(?<name>\S+)\s+(?<help>.*)$') {
                Add-EditorConfString $results $matches['name']
                Add-EditorConfString $results $matches['help']
            }
            else {
                Add-EditorConfString $results $value
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

function Add-EditorConfEntriesToPot {
    param([string]$potPath, [string]$editorConfPath)

    $entries = Get-EditorConfStrings -editorConfPath $editorConfPath
    if ($entries.Count -eq 0) { return }

    $tempPot = "$potPath.editor_conf.tmp"
    Copy-Item $potPath $tempPot -Force

    foreach ($entry in $entries.GetEnumerator() | Sort-Object Name) {
        $item = $entry.Value
        $lines = @()
        foreach ($ref in $item.References | Sort-Object -Unique) {
            $lines += "#: $ref"
        }

        $lines += 'msgid "' + (Escape-PotString $item.MsgId) + '"'
        $lines += 'msgstr ""'
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
        Write-Warning 'msguniq not found: source.pot may contain duplicate entries from editor_conf extraction.'
    }
}

Add-XamlGettextEntriesToPot -potPath $PSScriptRoot/source.pot -sourceRoot (Resolve-Path "$PSScriptRoot/.." | Select-Object -ExpandProperty Path)
Add-EditorConfEntriesToPot -potPath $PSScriptRoot/source.pot -editorConfPath (Resolve-Path "$PSScriptRoot/../src/MpvNet.Windows/Resources/editor_conf.txt" | Select-Object -ExpandProperty Path)

# Backup .po files
$BackupTargetFolder = $env:TEMP + '/mpv.net po backup ' + (Get-Date -Format 'yyyy-MM-dd HH_mm_ss')
Copy-Item $PSScriptRoot/po $BackupTargetFolder -Force -Recurse
'PO file backup: ' + (Resolve-Path $BackupTargetFolder)

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) { throw 'Python is required to merge PO files with source.pot.' }

# Merge by exact gettext key. msgmerge may apply fuzzy matches and silently move
# a translation between semantically different entries during catalog cleanup.
& python "$PSScriptRoot/merge-po-with-pot-fallback.py" --po-directory (Join-Path $PSScriptRoot 'po') --pot-path (Join-Path $PSScriptRoot 'source.pot')
if ($LastExitCode) { throw $LastExitCode }

$cleanScript = Join-Path $PSScriptRoot 'validate-po-files.ps1'
if (Test-Path $cleanScript) {
    & $cleanScript -PoDirectory (Join-Path $PSScriptRoot 'po') -PotPath (Join-Path $PSScriptRoot 'source.pot')
    if ($LastExitCode) { throw $LastExitCode }
}
