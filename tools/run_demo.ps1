param(
    [ValidateRange(0, 20)]
    [int]$Camera = 0
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$gameExe = Join-Path $projectRoot 'Builds\HeadTrackedDemo\HeadTrackedDemo.exe'
$pythonExe = Join-Path $projectRoot 'python_tracker\.venv\Scripts\python.exe'
$trackerScript = Join-Path $projectRoot 'python_tracker\tracker.py'
$modelFile = Join-Path $projectRoot 'python_tracker\face_landmarker.task'
$outputLog = Join-Path $projectRoot 'Builds\HeadTrackedDemo\tracker-output.log'
$errorLog = Join-Path $projectRoot 'Builds\HeadTrackedDemo\tracker-error.log'

foreach ($required in @($gameExe, $pythonExe, $trackerScript, $modelFile)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Missing $required. Follow the setup and build steps in README.md."
    }
}

Write-Host "Starting webcam $Camera tracker and the Unity demo. Close the game with Alt+F4."
$tracker = Start-Process -FilePath $pythonExe `
    -ArgumentList @('-u', ('"' + $trackerScript + '"'), '--camera', [string]$Camera) `
    -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput $outputLog -RedirectStandardError $errorLog

try {
    Start-Sleep -Seconds 2
    $tracker.Refresh()
    if ($tracker.HasExited) {
        $details = if (Test-Path -LiteralPath $errorLog) { Get-Content -LiteralPath $errorLog -Raw } else { '' }
        throw "Webcam tracker stopped before the demo opened. $details"
    }
    Start-Process -FilePath $gameExe -WorkingDirectory (Split-Path -Parent $gameExe) `
        -WindowStyle Normal -Wait
}
finally {
    $tracker.Refresh()
    if (-not $tracker.HasExited) { Stop-Process -Id $tracker.Id -ErrorAction SilentlyContinue }
    Write-Host "Tracker logs: $outputLog and $errorLog"
}
