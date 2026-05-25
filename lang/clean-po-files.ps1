param(
    [string] $PoDirectory = "$PSScriptRoot/po",
    [string] $PotPath = "$PSScriptRoot/source.pot"
)

$ErrorActionPreference = 'Stop'

function Get-CommandPath {
    param([string]$name)
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Unescape-PoString {
    param([string]$value)
    if ($null -eq $value) { return '' }

    $result = $value
    $result = $result -replace '\\n', "`n"
    $result = $result -replace '\\r', "`r"
    $result = $result -replace '\\t', "`t"
    $result = $result -replace '\\"', '"'
    $result = $result -replace '\\\\', '\\'
    return $result
}

function Parse-PoEntries {
    param([string]$path)

    $lines = Get-Content -Encoding UTF8 -LiteralPath $path
    $entries = @()
    $current = [ordered]@{ MsgId = $null; MsgIdPlural = $null; MsgStrs = @(); State = '' }

    foreach ($line in $lines) {
        if ($line -match '^msgid\s+"(.*)"$') {
            if ($current.MsgId -ne $null) {
                $entries += [pscustomobject]@{
                    MsgId = $current.MsgId
                    MsgIdPlural = $current.MsgIdPlural
                    MsgStr = ($current.MsgStrs -join '|')
                }
            }
            $current.MsgId = Unescape-PoString $matches[1]
            $current.MsgIdPlural = $null
            $current.MsgStrs = @()
            $current.State = 'msgid'
            continue
        }

        if ($line -match '^msgid_plural\s+"(.*)"$') {
            $current.MsgIdPlural = Unescape-PoString $matches[1]
            $current.State = 'msgid_plural'
            continue
        }

        if ($line -match '^msgstr(?:\[[0-9]+\])?\s+"(.*)"$') {
            $current.MsgStrs += Unescape-PoString $matches[1]
            $current.State = 'msgstr'
            continue
        }

        if ($line -match '^"(.*)"$') {
            $text = Unescape-PoString $matches[1]
            switch ($current.State) {
                'msgid' { $current.MsgId += $text }
                'msgid_plural' { $current.MsgIdPlural += $text }
                'msgstr' {
                    if ($current.MsgStrs.Count -gt 0) {
                        $current.MsgStrs[$current.MsgStrs.Count - 1] += $text
                    }
                }
            }
            continue
        }

        if ($line.Trim() -eq '') {
            if ($current.MsgId -ne $null) {
                $entries += [pscustomobject]@{
                    MsgId = $current.MsgId
                    MsgIdPlural = $current.MsgIdPlural
                    MsgStr = ($current.MsgStrs -join '|')
                }
            }
            $current = [ordered]@{ MsgId = $null; MsgIdPlural = $null; MsgStrs = @(); State = '' }
        }
    }

    if ($current.MsgId -ne $null) {
        $entries += [pscustomobject]@{
            MsgId = $current.MsgId
            MsgIdPlural = $current.MsgIdPlural
            MsgStr = ($current.MsgStrs -join '|')
        }
    }

    return $entries | Where-Object { $_.MsgId -ne '' }
}

function Get-MessageIdsFromPot {
    param([string]$path)
    return (Parse-PoEntries -path $path).MsgId
}

function Get-MessageIdsFromPo {
    param([string]$path)
    return (Parse-PoEntries -path $path).MsgId
}

function Get-PoTranslationEntries {
    param([string]$path)
    return Parse-PoEntries -path $path
}

$MsgAttrib = Get-CommandPath 'msgattrib'
$MsgUniq = Get-CommandPath 'msguniq'
$MsgMerge = Get-CommandPath 'msgmerge'

if (-not $MsgAttrib -or -not $MsgUniq -or -not $MsgMerge) {
    throw 'Required gettext tools not found: msgattrib, msguniq, msgmerge.'
}

if (-not (Test-Path $PoDirectory)) {
    throw "PO directory not found: $PoDirectory"
}

if (-not (Test-Path $PotPath)) {
    throw "POT file not found: $PotPath"
}

$sourceMsgIds = Get-MessageIdsFromPot -path $PotPath | Sort-Object -Unique
$missingMessageIdsByLocale = @{}
$duplicateMsgStrGroupsByLocale = @{}
$duplicateMsgIdByLocale = @{}
$processedLocales = @()

$poFiles = Get-ChildItem -Path $PoDirectory -Filter '*.po' | Sort-Object Name
if ($poFiles.Count -eq 0) {
    throw "Nenhum arquivo .po encontrado em $PoDirectory"
}

foreach ($poFile in $poFiles) {
    Write-Host "Cleaning $($poFile.Name)"
    $poPath = $poFile.FullName
    $poBackup = "$poPath.bak"
    Copy-Item -LiteralPath $poPath -Destination $poBackup -Force

    & $MsgMerge --sort-output --backup=none --update $poPath $PotPath
    if ($LastExitCode) {
        throw "msgmerge failed for $poPath"
    }

    $filteredPo = "$($poFile.DirectoryName)\$($poFile.BaseName).filtered.po"
    & $MsgAttrib --no-obsolete --output-file="$filteredPo" "$poPath"
    if ($LastExitCode) {
        throw "msgattrib failed for $poPath"
    }

    $dedupedPo = "$($poFile.DirectoryName)\$($poFile.BaseName).deduped.po"
    & $MsgUniq --sort-output --use-first --output-file="$dedupedPo" "$filteredPo"
    if ($LastExitCode) {
        throw "msguniq failed for $poPath"
    }

    Copy-Item -LiteralPath $dedupedPo -Destination $poPath -Force
    Remove-Item -LiteralPath $filteredPo -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $dedupedPo -Force -ErrorAction SilentlyContinue

    $poEntries = Get-PoTranslationEntries -path $poPath
    $poMsgIds = $poEntries.MsgId | Sort-Object -Unique
    $missing = $sourceMsgIds | Where-Object { $_ -and (-not ($poMsgIds -contains $_)) }
    if ($missing.Count -gt 0) {
        $missingMessageIdsByLocale[$poFile.Name] = $missing
    }

    $duplicateMsgIds = $poEntries | Group-Object -Property MsgId | Where-Object { $_.Count -gt 1 }
    if ($duplicateMsgIds.Count -gt 0) {
        $duplicateMsgIdByLocale[$poFile.Name] = $duplicateMsgIds | ForEach-Object { $_.Name }
    }

    $translationGroups = $poEntries | Where-Object { $_.MsgStr -and $_.MsgStr.Trim() -ne '' } | Group-Object -Property MsgStr | Where-Object { $_.Count -gt 1 }
    if ($translationGroups.Count -gt 0) {
        $duplicateMsgStrGroupsByLocale[$poFile.Name] = $translationGroups | ForEach-Object {
            [pscustomobject]@{
                Translation = $_.Name
                MsgIds = ($_.Group | Select-Object -ExpandProperty MsgId | Sort-Object -Unique)
            }
        }
    }

    $processedLocales += $poFile.Name
}

$hadErrors = $false
if ($duplicateMsgIdByLocale.Count -gt 0) {
    $hadErrors = $true
    Write-Host "\nErro: msgid duplicados encontrados" -ForegroundColor Yellow
    foreach ($locale in $duplicateMsgIdByLocale.Keys) {
        Write-Host "  $locale"
        foreach ($msgid in $duplicateMsgIdByLocale[$locale]) {
            Write-Host "    - $msgid"
        }
    }
}

if ($duplicateMsgStrGroupsByLocale.Count -gt 0) {
    $hadErrors = $true
    Write-Host "\nAviso: msgstr duplicados em chaves diferentes" -ForegroundColor Yellow
    foreach ($locale in $duplicateMsgStrGroupsByLocale.Keys) {
        Write-Host "  $locale"
        foreach ($group in $duplicateMsgStrGroupsByLocale[$locale]) {
            Write-Host "    Tradução: $($group.Translation)"
            foreach ($msgid in $group.MsgIds) {
                Write-Host "      - $msgid"
            }
        }
    }
}

if ($missingMessageIdsByLocale.Count -gt 0) {
    $hadErrors = $true
    Write-Host "\nAviso: chaves faltando no arquivo PO em relação a source.pot" -ForegroundColor Yellow
    foreach ($locale in $missingMessageIdsByLocale.Keys) {
        Write-Host "  ${locale}: $($missingMessageIdsByLocale[$locale].Count) chaves faltando"
        foreach ($msgid in $missingMessageIdsByLocale[$locale] | Select-Object -First 20) {
            Write-Host "    - $msgid"
        }
        if ($missingMessageIdsByLocale[$locale].Count -gt 20) {
            Write-Host "    ... ($($missingMessageIdsByLocale[$locale].Count - 20) restantes)"
        }
    }
}

Write-Host "\nProcessados: $($processedLocales.Count) arquivos PO."

if ($hadErrors) {
    throw 'Limpeza de PO concluída com avisos/erros. Consulte a saída acima para corrigi-los.'
}

Write-Host 'Limpeza e validação de PO concluídas com sucesso.'
