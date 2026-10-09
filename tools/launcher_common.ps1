# Pure target resolution and process matching shared by the launcher and its tests.
function Get-LauncherPlayerArguments {
    param($Target, [string]$PlayerArguments, [switch]$NoTracker)
    $arguments = ('-logFile "' + $Target.PlayerLog + '" ' + $PlayerArguments).TrimEnd()
    if ($NoTracker) { $arguments += ' --no-tracker' }
    return $arguments
}

function Resolve-LauncherPath {
    param([string]$Path, [string]$BaseDirectory)
    if ([System.IO.Path]::IsPathRooted($Path)) { return [System.IO.Path]::GetFullPath($Path) }
    return [System.IO.Path]::GetFullPath((Join-Path $BaseDirectory $Path))
}

function Get-UnityPlayerIdentity {
    param([string]$UnityProject)
    $settingsFile = Join-Path $UnityProject 'ProjectSettings\ProjectSettings.asset'
    if (-not (Test-Path -LiteralPath $settingsFile)) { throw "Missing Unity project settings: $settingsFile" }
    $settings = Get-Content -LiteralPath $settingsFile -Raw
    $company = [regex]::Match($settings, '(?m)^[ \t]*companyName:[ \t]*([^\r\n]+)').Groups[1].Value.Trim()
    $product = [regex]::Match($settings, '(?m)^[ \t]*productName:[ \t]*([^\r\n]+)').Groups[1].Value.Trim()
    foreach ($field in @('company', 'product')) {
        $value = Get-Variable -Name $field -ValueOnly
        if ($value.StartsWith('"') -and $value.EndsWith('"')) { $value = ConvertFrom-Json $value }
        elseif ($value.StartsWith("'") -and $value.EndsWith("'")) { $value = $value.Substring(1, $value.Length - 2).Replace("''", "'") }
        Set-Variable -Name $field -Value $value
    }
    if (-not $company -or -not $product) { throw 'Unity companyName/productName are missing.' }
    [pscustomobject]@{ Company = $company; Product = $product }
}

function Get-LauncherTarget {
    param([string]$TechnologyRoot, [string]$UnityProject, [string]$PlayerPath,
        [string]$TrackerRoot, [string]$CalibrationDirectory)
    $technology = Resolve-LauncherPath $TechnologyRoot (Get-Location).Path
    $project = if ($UnityProject) { Resolve-LauncherPath $UnityProject (Get-Location).Path }
        else { Join-Path $technology 'Demo' }
    $identity = Get-UnityPlayerIdentity $project
    if ($PlayerPath) { $player = Resolve-LauncherPath $PlayerPath (Get-Location).Path }
    elseif ($project -eq (Join-Path $technology 'Demo')) { $player = Join-Path $technology 'Builds\HeadTrackedDemo\HeadTrackedDemo.exe' }
    else { $player = Join-Path (Split-Path -Parent $project) ('Builds\' + $identity.Product + '\' + $identity.Product + '.exe') }
    $tracker = if ($TrackerRoot) { Resolve-LauncherPath $TrackerRoot (Get-Location).Path } else { $technology }
    if (Test-Path -LiteralPath (Join-Path $tracker 'python_tracker') -PathType Container) { $tracker = Join-Path $tracker 'python_tracker' }
    $calibration = if ($CalibrationDirectory) { Resolve-LauncherPath $CalibrationDirectory (Get-Location).Path }
        else { Join-Path $env:USERPROFILE ('AppData\LocalLow\' + $identity.Company + '\' + $identity.Product) }
    $logs = Split-Path -Parent $player
    $hash = [System.Security.Cryptography.SHA256]::Create()
    try {
        $targetId = [System.BitConverter]::ToString($hash.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($player.ToUpperInvariant()))).Replace('-', '').Substring(0, 24)
    }
    finally { $hash.Dispose() }
    [pscustomobject]@{
        UnityProject = $project; PlayerPath = $player; TrackerDirectory = $tracker
        Company = $identity.Company; Product = $identity.Product
        CalibrationDirectory = $calibration
        CalibrationFile = Join-Path $calibration 'tracker_runtime_calibration.json'
        TrackerScript = Join-Path $tracker 'tracker.py'
        PythonPath = Join-Path $tracker '.venv\Scripts\python.exe'
        ModelPath = Join-Path $tracker 'face_landmarker.task'
        LogDirectory = $logs; PlayerLog = Join-Path $logs 'Player.log'
        TrackerOutputLog = Join-Path $logs 'tracker-output.log'
        TrackerErrorLog = Join-Path $logs 'tracker-error.log'
        PlayerProcessName = [System.IO.Path]::GetFileName($player)
        MutexName = 'Local\HeadTrackedDisplayLauncher_' + $targetId
    }
}

function Test-SameLauncherPath {
    param([string]$Left, [string]$Right)
    if (-not $Left -or -not $Right) { return $false }
    try { return [System.IO.Path]::GetFullPath($Left) -eq [System.IO.Path]::GetFullPath($Right) }
    catch { return $false }
}

function Test-TargetTrackerCommand {
    param([string]$CommandLine, $Target)
    if (-not $CommandLine) { return $false }
    # Paths cannot contain double quotes on Windows. All launcher paths are quoted.
    $arguments = @([regex]::Matches($CommandLine, '"([^\"]*)"|(\S+)') | ForEach-Object {
        if ($_.Groups[1].Success) { $_.Groups[1].Value } else { $_.Groups[2].Value }
    })
    $hasScript = $false; $calibration = $null; $lifetime = $null
    for ($index = 0; $index -lt $arguments.Count; $index++) {
        if (Test-SameLauncherPath $arguments[$index] $Target.TrackerScript) { $hasScript = $true }
        if ($arguments[$index] -eq '--calibration-file' -and $index + 1 -lt $arguments.Count) { $calibration = $arguments[$index + 1] }
        if ($arguments[$index] -eq '--lifetime-file' -and $index + 1 -lt $arguments.Count) { $lifetime = $arguments[$index + 1] }
    }
    # A manually started tracker has no owned heartbeat and is never terminated by this launcher.
    return $hasScript -and (Test-SameLauncherPath $calibration $Target.CalibrationFile) -and
        $lifetime -and (Test-SameLauncherPath (Split-Path -Parent $lifetime) $Target.LogDirectory) -and
        ([System.IO.Path]::GetFileName($lifetime) -match '^tracker-session-[a-fA-F0-9]{32}\.heartbeat$')
}
