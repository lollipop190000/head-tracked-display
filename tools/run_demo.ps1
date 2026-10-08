param(
    [ValidateRange(0, 20)]
    [int]$Camera = 0,
    [switch]$TrackingTest,
    [switch]$DepthTest,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$gameExe = Join-Path $projectRoot 'Builds\HeadTrackedDemo\HeadTrackedDemo.exe'
$pythonExe = Join-Path $projectRoot 'python_tracker\.venv\Scripts\python.exe'
$trackerScript = Join-Path $projectRoot 'python_tracker\tracker.py'
$modelFile = Join-Path $projectRoot 'python_tracker\face_landmarker.task'
$outputLog = Join-Path $projectRoot 'Builds\HeadTrackedDemo\tracker-output.log'
$errorLog = Join-Path $projectRoot 'Builds\HeadTrackedDemo\tracker-error.log'
$calibrationFile = Join-Path $env:USERPROFILE 'AppData\LocalLow\DefaultCompany\Demo\tracker_runtime_calibration.json'

function Get-ProjectTrackers {
    Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('python.exe', 'pythonw.exe') -and
        $_.CommandLine -and $_.CommandLine.Contains($trackerScript)
    }
}

function Stop-ProjectTrackers {
    # A Windows venv executable can launch a second Python process. Stop both.
    $owned = @(Get-ProjectTrackers)
    foreach ($process in $owned) {
        Stop-Process -Id $process.ProcessId -ErrorAction SilentlyContinue
    }
}

$players = @(Get-CimInstance Win32_Process -Filter "Name = 'HeadTrackedDemo.exe'" |
    Where-Object { $_.ExecutablePath -eq $gameExe })
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
    Write-Host 'Demo and its webcam tracker stopped.'
    exit 0
}

$mutex = New-Object System.Threading.Mutex($false, 'Local\HeadTrackedDisplayDemoLauncher')
$ownsMutex = $false
$tracker = $null
$game = $null
$lifetimeFile = $null
try {
    try { $ownsMutex = $mutex.WaitOne(0) }
    catch [System.Threading.AbandonedMutexException] { $ownsMutex = $true }
    if (-not $ownsMutex -or $players.Count -gt 0) {
        throw 'The demo is already running. Close it with Alt+F4, or run Stop-HeadTrackedDemo.cmd, before launching again.'
    }
    foreach ($required in @($gameExe, $pythonExe, $trackerScript, $modelFile)) {
        if (-not (Test-Path -LiteralPath $required)) {
            throw "Missing $required. Follow the setup and build steps in README.md."
        }
    }
    Stop-ProjectTrackers
    $lifetimeFile = Join-Path (Split-Path -Parent $gameExe) ('tracker-session-' + [guid]::NewGuid().ToString('N') + '.heartbeat')
    [System.IO.File]::WriteAllText($lifetimeFile, '')

    Write-Host "Starting Unity demo and webcam $Camera. Close the game with Alt+F4."
    $gameArguments = if ($DepthTest) { @('--depth-test') } elseif ($TrackingTest) { @('--tracking-test') } else { @() }
    $startGame = @{
        FilePath = $gameExe; WorkingDirectory = (Split-Path -Parent $gameExe)
        WindowStyle = 'Normal'; PassThru = $true
    }
    if ($gameArguments.Count -gt 0) { $startGame.ArgumentList = $gameArguments }
    $game = Start-Process @startGame
    Start-Sleep -Milliseconds 750
    $game.Refresh()
    if ($game.HasExited) { throw "Unity exited at startup (code $($game.ExitCode)). Check Player.log in AppData\LocalLow\DefaultCompany\Demo." }

    $tracker = Start-Process -FilePath $pythonExe `
        -ArgumentList @('-u', ('"' + $trackerScript + '"'), '--camera', [string]$Camera,
            '--calibration-file', ('"' + $calibrationFile + '"'), '--lifetime-file', ('"' + $lifetimeFile + '"')) `
        -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $outputLog -RedirectStandardError $errorLog

    while (-not $game.HasExited) {
        [System.IO.File]::WriteAllText($lifetimeFile, '')
        $tracker.Refresh()
        if ($tracker.HasExited) {
            $details = if (Test-Path -LiteralPath $errorLog) { Get-Content -LiteralPath $errorLog -Raw } else { '' }
            throw "Webcam tracker stopped. $details"
        }
        Start-Sleep -Milliseconds 250
        $game.Refresh()
    }
    if ($game.ExitCode -ne 0) { throw "Unity exited with code $($game.ExitCode). Check Player.log in AppData\LocalLow\DefaultCompany\Demo." }
}
finally {
    # Only the launcher that acquired the mutex owns these resources.
    if ($ownsMutex -and $lifetimeFile) {
        Remove-Item -LiteralPath $lifetimeFile -ErrorAction SilentlyContinue
        Stop-ProjectTrackers
        if ($game) {
            $game.Refresh()
            if (-not $game.HasExited) { Stop-Process -Id $game.Id -ErrorAction SilentlyContinue }
        }
        Write-Host "Webcam tracker stopped. Logs: $outputLog and $errorLog"
    }
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
