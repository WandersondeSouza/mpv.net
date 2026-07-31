[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [string]$MediaPath,

    [ValidateRange(5, 300)]
    [int]$StartupTimeoutSeconds = 30,

    [ValidateRange(1, 300)]
    [int]$ObserveSeconds = 5,

    [switch]$KeepOpen
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Executable not found: $ExecutablePath"
}

$resolvedExecutable = (Resolve-Path -LiteralPath $ExecutablePath).Path
$arguments = @()
if (-not [string]::IsNullOrWhiteSpace($MediaPath)) {
    if (-not (Test-Path -LiteralPath $MediaPath)) {
        throw "Media path not found: $MediaPath"
    }

    $arguments += (Resolve-Path -LiteralPath $MediaPath).Path
}

if ($arguments.Count -gt 0) {
    $process = Start-Process -FilePath $resolvedExecutable -ArgumentList $arguments -PassThru
}
else {
    $process = Start-Process -FilePath $resolvedExecutable -PassThru
}
$deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)

do {
    Start-Sleep -Milliseconds 250
    $process.Refresh()
    if ($process.HasExited) {
        throw "mpv.net exited during startup with code $($process.ExitCode)."
    }
} while ((Get-Date) -lt $deadline)

Write-Output "Startup smoke passed: PID $($process.Id) remained alive for $StartupTimeoutSeconds seconds."

if (-not $KeepOpen) {
    Start-Sleep -Seconds $ObserveSeconds
    $process.Refresh()
    if ($process.HasExited) {
        throw "mpv.net exited during the observation window with code $($process.ExitCode)."
    }

    Stop-Process -Id $process.Id -Force
    Write-Output "Close smoke passed: process stopped after the observation window."
}
