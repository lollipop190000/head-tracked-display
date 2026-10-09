param(
    [ValidateRange(0, 20)]
    [int]$Camera = 0,
    [switch]$NoTracker,
    [string]$UnityProject,
    [string]$PlayerPath,
    [string]$PlayerArguments,
    [string]$TrackerRoot,
    [string]$CalibrationDirectory,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'launcher_common.ps1')
$target = Get-LauncherTarget -TechnologyRoot $projectRoot -UnityProject $UnityProject -PlayerPath $PlayerPath `
    -TrackerRoot $TrackerRoot -CalibrationDirectory $CalibrationDirectory
$gameExe = $target.PlayerPath
$pythonExe = $target.PythonPath
$trackerScript = $target.TrackerScript
$modelFile = $target.ModelPath
$outputLog = $target.TrackerOutputLog
$errorLog = $target.TrackerErrorLog
$calibrationFile = $target.CalibrationFile
$CalibrationDirectory = $target.CalibrationDirectory

function Get-ProjectTrackers {
    Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('python.exe', 'pythonw.exe') -and
        (Test-TargetTrackerCommand $_.CommandLine $target)
    }
}

function Stop-ProjectTrackers {
    # A Windows venv executable can launch a second Python process. Stop both.
    $owned = @(Get-ProjectTrackers)
    foreach ($process in $owned) {
        Stop-Process -Id $process.ProcessId -ErrorAction SilentlyContinue
    }
}

function Update-PanelHint {
    # EDID dimensions are rounded centimetres: a hint for old demo defaults, not a measurement.
    $monitorHint = Join-Path (Split-Path -Parent $calibrationFile) 'monitor_hint.json'
    try {
        $monitors = @(Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorBasicDisplayParams |
            Where-Object { $_.Active -and $_.MaxHorizontalImageSize -gt 10 -and $_.MaxVerticalImageSize -gt 10 })
        if ($monitors.Count -eq 1) {
            New-Item -ItemType Directory -Path (Split-Path -Parent $monitorHint) -Force | Out-Null
            @{ widthCm = [int]$monitors[0].MaxHorizontalImageSize; heightCm = [int]$monitors[0].MaxVerticalImageSize } |
                ConvertTo-Json | Set-Content -LiteralPath $monitorHint -Encoding UTF8
        }
        else { Remove-Item -LiteralPath $monitorHint -ErrorAction SilentlyContinue }
    }
    catch { Write-Warning 'Panel dimensions could not be read; measure the screen width manually.' }
}

$players = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq $target.PlayerProcessName -and (Test-SameLauncherPath $_.ExecutablePath $gameExe)
})
if ($Stop) {
    foreach ($player in $players) {
        $running = Get-Process -Id $player.ProcessId -ErrorAction SilentlyContinue
        if ($running) {
            if (-not $running.CloseMainWindow() -or -not $running.WaitForExit(2000)) {
                Stop-Process -Id $player.ProcessId -ErrorAction SilentlyContinue
            }
        }
    }
    Stop-ProjectTrackers
    Write-Host "$($target.Product) and its webcam tracker stopped."
    exit 0
}

$mutex = New-Object System.Threading.Mutex($false, $target.MutexName)
$ownsMutex = $false
$tracker = $null
$game = $null
$lifetimeFile = $null
$calibrationEnvironmentChanged = $false
$previousCalibrationEnvironment = [Environment]::GetEnvironmentVariable('HEADTRACKED_CALIBRATION_DIRECTORY', 'Process')
try {
    try { $ownsMutex = $mutex.WaitOne(0) }
    catch [System.Threading.AbandonedMutexException] { $ownsMutex = $true }
    if (-not $ownsMutex -or $players.Count -gt 0) {
        throw "$($target.Product) is already running. Close it with Alt+F4, or use the same target arguments with -Stop."
    }
    $requiredFiles = @($gameExe)
    if (-not $NoTracker) { $requiredFiles += @($pythonExe, $trackerScript, $modelFile) }
    foreach ($required in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $required)) {
            throw "Missing $required. Follow the setup and build steps in README.md."
        }
    }
    Stop-ProjectTrackers
    New-Item -ItemType Directory -Path $target.LogDirectory -Force | Out-Null
    Update-PanelHint
    $lifetimeFile = Join-Path (Split-Path -Parent $gameExe) ('tracker-session-' + [guid]::NewGuid().ToString('N') + '.heartbeat')
    [System.IO.File]::WriteAllText($lifetimeFile, '')

    if ($NoTracker) { Write-Host "Starting $($target.Product) without a webcam tracker. Close the game with Alt+F4." }
    else { Write-Host "Starting $($target.Product) and webcam $Camera. Close the game with Alt+F4." }
    [Environment]::SetEnvironmentVariable('HEADTRACKED_CALIBRATION_DIRECTORY', $CalibrationDirectory, 'Process')
    $calibrationEnvironmentChanged = $true
    $startGame = @{
        FilePath = $gameExe; WorkingDirectory = (Split-Path -Parent $gameExe)
        WindowStyle = 'Normal'; PassThru = $true
        # Start-Process sends this to the executable directly; it is never evaluated as shell code.
        ArgumentList = Get-LauncherPlayerArguments -Target $target -PlayerArguments $PlayerArguments -NoTracker:$NoTracker
    }
    $game = Start-Process @startGame
    Start-Sleep -Milliseconds 750
    $game.Refresh()
    if ($game.HasExited) { throw "Unity exited at startup (code $($game.ExitCode)). Check $($target.PlayerLog)." }

    if (-not $NoTracker) { $tracker = Start-Process -FilePath $pythonExe `
        -ArgumentList @('-u', ('"' + $trackerScript + '"'), '--camera', [string]$Camera,
            '--model', ('"' + $modelFile + '"'),
            '--calibration-file', ('"' + $calibrationFile + '"'), '--lifetime-file', ('"' + $lifetimeFile + '"')) `
        -WorkingDirectory $target.TrackerDirectory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $outputLog -RedirectStandardError $errorLog }

    # Child processes inherit their own copy. Restore only this launcher's process environment.
    [Environment]::SetEnvironmentVariable('HEADTRACKED_CALIBRATION_DIRECTORY', $previousCalibrationEnvironment, 'Process')
    $calibrationEnvironmentChanged = $false

    while (-not $game.HasExited) {
        [System.IO.File]::WriteAllText($lifetimeFile, '')
        if ($tracker) { $tracker.Refresh() }
        if ($tracker -and $tracker.HasExited) {
            $details = if (Test-Path -LiteralPath $errorLog) { Get-Content -LiteralPath $errorLog -Raw } else { '' }
            throw "Webcam tracker stopped. $details"
        }
        Start-Sleep -Milliseconds 250
        $game.Refresh()
    }
    if ($game.ExitCode -ne 0) { throw "Unity exited with code $($game.ExitCode). Check $($target.PlayerLog)." }
}
finally {
    if ($calibrationEnvironmentChanged) {
        [Environment]::SetEnvironmentVariable('HEADTRACKED_CALIBRATION_DIRECTORY', $previousCalibrationEnvironment, 'Process')
    }
    # Only the launcher that acquired the mutex owns these resources.
    if ($ownsMutex -and $lifetimeFile) {
        Remove-Item -LiteralPath $lifetimeFile -ErrorAction SilentlyContinue
        Stop-ProjectTrackers
        if ($game) {
            $game.Refresh()
            if (-not $game.HasExited) { Stop-Process -Id $game.Id -ErrorAction SilentlyContinue }
        }
        if ($NoTracker) { Write-Host "$($target.Product) stopped." }
        else { Write-Host "Webcam tracker stopped. Logs: $outputLog and $errorLog" }
    }
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
