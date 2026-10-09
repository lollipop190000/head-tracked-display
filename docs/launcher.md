# One Windows entry point for Unity players

`Run-HeadTrackedDemo.cmd` delegates to `tools/run_demo.ps1`. The default target remains the fixed technical demo; optional paths let a separate game consume the same tracker and lifecycle handling.

| Parameter | Default and meaning |
| --- | --- |
| `-UnityProject` | Technology checkout's `Demo` folder. Reads company/product from `ProjectSettings/ProjectSettings.asset`. |
| `-PlayerPath` | Default demo: `Builds/HeadTrackedDemo/HeadTrackedDemo.exe`. A custom project defaults to `Builds/<product>/<product>.exe` beside the project folder; use an explicit path when your build differs. |
| `-TrackerRoot` | Technology checkout. Accepts a checkout containing `python_tracker`, or that directory directly. Requires `.venv/Scripts/python.exe`, `tracker.py`, and `face_landmarker.task`. |
| `-PlayerArguments` | Optional command-line string appended to the player arguments, for example benchmark controls. Passed directly to the executable without shell evaluation. Quote argument values containing spaces within this string. |
| `-CalibrationDirectory` | `%USERPROFILE%/AppData/LocalLow/<company>/<product>`. An override is supplied to both children through `HEADTRACKED_CALIBRATION_DIRECTORY`; `PythonBridgeSource` publishes tracker configuration there. Consumers can use the same directory for their own calibration/preferences. |
| `-Camera` | Webcam index 0, configurable from 0 to 20. |
| `-NoTracker` | Opens the player without Python and forwards `--no-tracker` so consumers can disable their Unity bridge. |
| `-Stop` | Stops the target executable and that target's managed Python processes. Pass the same path/calibration arguments used to start it. |

All optional relative paths use the invoking shell's working directory. Paths with spaces must be quoted. Builds and Python setup remain explicit steps in the consuming project's documentation.

The calibration environment override exists only in the launcher process while it starts Unity and Python; each child inherits its own copy. The launcher restores its previous process environment on success and failure. It never changes the user's saved Windows environment. Existing consumers without this variable retain `Application.persistentDataPath`; the technology demo's UI saves still use Unity's persistent directory.

The launcher sends Unity `-logFile` pointing to `Player.log` beside the executable. Tracker stdout/stderr and session heartbeats use the same directory. Its mutex derives from the normalized executable path. The Unity player is matched by both its executable name and full path, so identical names from other build directories are not affected.

A tracker is owned only when its exact script path, calibration argument and managed heartbeat directory match the target. Both Python processes of a Windows virtual environment can be terminated, but another project's tracker and manually started trackers are preserved. Alt+F4 stops the managed tracker; deleting or abandoning the heartbeat also releases its webcam after the tracker's eight-second deadline. `-Stop` first attempts a graceful player close.

The current Python bridge uses loopback TCP port 8765. Run one webcam-tracked target at a time; separate target ownership does not allocate extra tracker ports or webcams.

Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/test_launcher.ps1` for target resolution, path quoting, defaults, mutex identity and process ownership checks. The test starts no player and opens no webcam. It also checks PowerShell syntax and cleans only its explicitly verified temporary directory under `Builds`.
