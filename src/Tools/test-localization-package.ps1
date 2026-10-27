param(
    [string] $LocaleDirectory = "$PSScriptRoot\..\MpvNet.Windows\bin\Debug\win-x64\Locale"
)

$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'validate-package-contents.ps1'
$testRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\artifacts\localization-package-tests'))
$fixture = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
$payload = Join-Path $fixture 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null

function Assert-Rejected([scriptblock] $Action, [string] $Expected) {
    $rejected = $false
    try { & $Action }
    catch {
        if ($_.Exception.Message -notlike "*$Expected*") { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw "Expected package rejection: $Expected" }
}

try {
    # Only content fixtures: these bytes are never executed as native binaries.
    foreach ($name in @('mpvnet.exe', 'libmpv-2.dll', 'libmpv-2-v3.dll', 'Scripts\osc.lua')) {
        $file = Join-Path $payload $name
        New-Item -ItemType Directory -Path (Split-Path $file) -Force | Out-Null
        [IO.File]::WriteAllText($file, 'content-fixture')
    }
    Copy-Item -LiteralPath $LocaleDirectory -Destination (Join-Path $payload 'Locale') -Recurse
    & $validator -Path $payload

    $missing = Join-Path $payload 'Locale\id_ID\LC_MESSAGES\mpvnet.mo'
    $bytes = [IO.File]::ReadAllBytes($missing)
    Remove-Item -LiteralPath $missing
    Assert-Rejected { & $validator -Path $payload } 'id_ID'
    [IO.File]::WriteAllBytes($missing, [byte[]]@())
    Assert-Rejected { & $validator -Path $payload } 'empty'
    [IO.File]::WriteAllBytes($missing, $bytes)

    $zip = Join-Path $fixture 'portable.zip'
    Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $zip
    & $validator -ArchiveFile $zip

    # The Store validator must see catalogs inside nested upload containers.
    $nested = Join-Path $fixture 'nested'
    New-Item -ItemType Directory -Path $nested | Out-Null
    Copy-Item -LiteralPath $zip -Destination (Join-Path $nested 'app.msix')
    $upload = Join-Path $fixture 'store-upload.zip'
    Compress-Archive -Path (Join-Path $nested '*') -DestinationPath $upload
    & $validator -ArchiveFile $upload

    Remove-Item -LiteralPath $missing
    $broken = Join-Path $fixture 'incomplete.zip'
    Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $broken
    Assert-Rejected { & $validator -ArchiveFile $broken } 'id_ID'
    Write-Host 'Localization package contracts passed: directory, ZIP, nested Store and missing/empty catalog rejection.'
}
finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    if (-not $resolvedFixture.StartsWith($testRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove fixture outside test root: $resolvedFixture"
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
