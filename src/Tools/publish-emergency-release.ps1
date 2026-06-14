<#

Increments the patch version, commits it, pushes the current branch, and starts
the manual GitHub Actions release workflow.

This script is intended for emergency releases from the maintained fork.
Run it from a clean working tree after reviewing the changes that should be
published.

#>

param(
    [string] $Repo = 'WandersondeSouza/mpv.net',

    [string] $Branch,

    [switch] $CreateInstaller,

    [switch] $EnableFileLogging
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked($command, $arguments) {
    & $command @arguments
    if ($LastExitCode) {
        throw "$command failed with exit code $LastExitCode"
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Push-Location $repoRoot
try {
    Invoke-Checked git @('diff', '--check')

    $status = @(git status --porcelain)
    if ($status.Count) {
        throw "Working tree is not clean. Commit or discard pending changes before running this emergency release script."
    }

    if (-not $Branch) {
        $Branch = (git branch --show-current).Trim()
    }

    if (-not $Branch) {
        throw 'Could not determine the current Git branch. Pass -Branch explicitly.'
    }

    $setVersionScript = Join-Path $repoRoot 'src\Tools\set-release-version.ps1'
    $nextVersion = (& $setVersionScript -IncrementRevision | Select-Object -Last 1).Trim()

    $enableFileLoggingValue = if ($EnableFileLogging) { 'true' } else { 'false' }
    Invoke-Checked dotnet @('build', 'src\MpvNet.Windows\MpvNet.Windows.csproj', '--no-restore', '/p:EnsureBuildAssets=false', "/p:EnableFileLogging=$enableFileLoggingValue")
    Invoke-Checked git @('add', 'src\BuildVersion.props', 'src\MpvNet.Pacote\Package.appxmanifest')
    Invoke-Checked git @('commit', '-m', "Bump version to v$nextVersion")
    Invoke-Checked git @('push', 'origin', $Branch)

    $createInstallerValue = if ($CreateInstaller) { 'true' } else { 'false' }
    Invoke-Checked gh @(
        'workflow',
        'run',
        'release-packages.yml',
        '--repo',
        $Repo,
        '--ref',
        $Branch,
        '-f',
        'create_release=true',
        '-f',
        "create_installer=$createInstallerValue",
        '-f',
        "enable_file_logging=$enableFileLoggingValue"
    )

    Write-Host "Emergency release workflow started for v$nextVersion on $Repo ($Branch)."
}
finally {
    Pop-Location
}
