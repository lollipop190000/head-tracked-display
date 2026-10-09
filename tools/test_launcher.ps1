$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'launcher_common.ps1')
$technologyRoot = Split-Path -Parent $PSScriptRoot
$script:assertions = 0
function Assert-LauncherTest {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
    $script:assertions++
}
foreach ($scriptName in @('run_demo.ps1', 'launcher_common.ps1')) {
    $parseErrors = $null; $tokens = $null
    [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $scriptName), [ref]$tokens, [ref]$parseErrors) | Out-Null
    Assert-LauncherTest ($parseErrors.Count -eq 0) "PowerShell syntax failed: $scriptName $parseErrors"
}
$target = Get-LauncherTarget -TechnologyRoot $technologyRoot
Assert-LauncherTest ($target.PlayerPath -eq (Join-Path $technologyRoot 'Builds\HeadTrackedDemo\HeadTrackedDemo.exe')) 'Default player changed.'
Assert-LauncherTest ($target.TrackerDirectory -eq (Join-Path $technologyRoot 'python_tracker')) 'Default tracker changed.'
$nativeArguments = Get-LauncherPlayerArguments -Target $target -PlayerArguments '--benchmark --benchmark-out "a folder"' -NoTracker
Assert-LauncherTest ($nativeArguments.EndsWith('--no-tracker')) 'NoTracker must also disable the consuming Unity bridge.'
Assert-LauncherTest ($nativeArguments.Contains('--benchmark-out "a folder"')) 'Quoted player arguments must be preserved without shell evaluation.'
$identity = Get-UnityPlayerIdentity (Join-Path $technologyRoot 'Demo')
Assert-LauncherTest ($target.CalibrationDirectory -eq (Join-Path $env:USERPROFILE ('AppData\LocalLow\' + $identity.Company + '\' + $identity.Product))) 'Default calibration must use Unity identity.'

$testRoot = Join-Path $technologyRoot ('Builds\LauncherTests-' + [guid]::NewGuid().ToString('N'))
try {
    $project = Join-Path $testRoot 'Project With Spaces'
    $settingsDirectory = Join-Path $project 'ProjectSettings'
    New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
    "PlayerSettings:`r`n  companyName: 'Team Manhattan'`r`n  productName: `"Manhattan Viewer`"`r`n" |
        Set-Content -LiteralPath (Join-Path $settingsDirectory 'ProjectSettings.asset') -Encoding UTF8
    $player = Join-Path $testRoot 'build folder\City.exe'
    $calibration = Join-Path $testRoot 'calibration folder'
    $custom = Get-LauncherTarget -TechnologyRoot $technologyRoot -UnityProject $project -PlayerPath $player `
        -TrackerRoot (Join-Path $technologyRoot 'python_tracker') -CalibrationDirectory $calibration
    Assert-LauncherTest ($custom.Company -eq 'Team Manhattan' -and $custom.Product -eq 'Manhattan Viewer') 'Quoted CRLF Unity identity failed.'
    Assert-LauncherTest ($custom.PlayerProcessName -eq 'City.exe') 'Process name must follow player.'
    Assert-LauncherTest ($custom.CalibrationFile -eq (Join-Path $calibration 'tracker_runtime_calibration.json')) 'Calibration override failed.'
    Assert-LauncherTest ($custom.PlayerLog -eq (Join-Path (Split-Path -Parent $player) 'Player.log')) 'Log must follow target player.'
    Assert-LauncherTest ($custom.MutexName -ne $target.MutexName) 'Players must have separate mutexes.'
    $same = Get-LauncherTarget -TechnologyRoot $technologyRoot -UnityProject $project -PlayerPath $player.ToUpperInvariant() -CalibrationDirectory $calibration
    Assert-LauncherTest ($same.MutexName -eq $custom.MutexName) 'Mutex must be case insensitive.'
    $automatic = Get-LauncherTarget -TechnologyRoot $technologyRoot -UnityProject $project
    Assert-LauncherTest ($automatic.PlayerPath -eq (Join-Path $testRoot 'Builds\Manhattan Viewer\Manhattan Viewer.exe')) 'Custom default build location failed.'

    $heartbeat = Join-Path $custom.LogDirectory ('tracker-session-' + ('a' * 32) + '.heartbeat')
    $command = '"' + $custom.PythonPath + '" -u "' + $custom.TrackerScript + '" --calibration-file "' + $custom.CalibrationFile + '" --lifetime-file "' + $heartbeat + '"'
    Assert-LauncherTest (Test-TargetTrackerCommand $command $custom) 'Managed target tracker was not recognized.'
    Assert-LauncherTest (-not (Test-TargetTrackerCommand $command $target)) 'Another project tracker must not be stopped.'
    Assert-LauncherTest (-not (Test-TargetTrackerCommand ($command.Replace($custom.TrackerScript, $custom.TrackerScript + '.other')) $custom)) 'Substring script matches must not be stopped.'
    Assert-LauncherTest (-not (Test-TargetTrackerCommand ('"' + $custom.PythonPath + '" "' + $custom.TrackerScript + '" --calibration-file "' + $custom.CalibrationFile + '"') $custom)) 'A manual tracker must not be stopped.'
    Assert-LauncherTest (-not (Test-TargetTrackerCommand ($command.Replace($heartbeat, (Join-Path $target.LogDirectory ([System.IO.Path]::GetFileName($heartbeat))))) $custom)) 'Another player heartbeat must not be stopped.'
    Assert-LauncherTest (-not (Test-TargetTrackerCommand ($command.Replace($heartbeat, (Join-Path $custom.LogDirectory 'notes.txt'))) $custom)) 'Non-session files must not grant tracker ownership.'
    Assert-LauncherTest (Test-SameLauncherPath $custom.PlayerPath $custom.PlayerPath.ToUpperInvariant()) 'Process paths must be case insensitive.'
}
finally {
    $resolvedRoot = [System.IO.Path]::GetFullPath($testRoot)
    $allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $technologyRoot 'Builds')) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedRoot.StartsWith($allowedRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing to clean a test directory outside Builds.' }
    if (Test-Path -LiteralPath $resolvedRoot) { Remove-Item -LiteralPath $resolvedRoot -Recurse -Force }
}
Write-Host "$script:assertions launcher assertions passed. No player or webcam was started."
