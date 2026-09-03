[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Join-Path $scriptDirectory '..'
}
$Root = [System.IO.Path]::GetFullPath($Root)
$expectedTfm = 'net10.0-windows10.0.19041.0'
$projectPaths = @(
    'MpvNet\MpvNet.csproj',
    'MpvNet.Windows\MpvNet.Windows.csproj',
    'NGettext.Wpf\NGettext.Wpf.csproj',
    'MpvNet.Tests\MpvNet.Tests.csproj',
    'MpvNet.Extension\ExampleExtension\ExampleExtension.csproj',
    'MpvNet.Pacote\MpvNet.Pacote.wapproj'
)

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($relativePath in $projectPaths) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $failures.Add("Projeto ausente: $relativePath")
        continue
    }

    [xml]$project = Get-Content -LiteralPath $path -Raw
    $tfm = ([string]$project.Project.PropertyGroup.TargetFramework).Trim()
    if ($tfm -ne $expectedTfm) {
        $failures.Add("$relativePath usa '$tfm'; esperado '$expectedTfm'")
    }
}

$manifestPath = Join-Path $Root 'MpvNet.Pacote\Package.appxmanifest'
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$families = $manifest.SelectNodes("//*[local-name()='TargetDeviceFamily']")
foreach ($family in $families) {
    if ([string]$family.MinVersion -ne '10.0.19041.0') {
        $failures.Add("Package.appxmanifest tem MinVersion inesperada: $($family.MinVersion)")
    }
}

$appManifestPath = Join-Path $Root 'MpvNet.Windows\app.manifest'
$appManifest = Get-Content -LiteralPath $appManifestPath -Raw
foreach ($legacyGuid in @(
    '{35138b9a-5d96-4fbd-8e2d-a2440225f93a}',
    '{4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38}',
    '{1f676c76-80e1-4239-95bb-83d0f6d0da78}'
)) {
    if ($appManifest.Contains($legacyGuid)) {
        $failures.Add("app.manifest ainda declara compatibilidade legada: $legacyGuid")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw 'A linha de base Windows 19041 não está coerente.'
}

Write-Output "Windows baseline validada: $expectedTfm; pacote mínimo 10.0.19041.0."
