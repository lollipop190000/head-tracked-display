# Head Tracked Display

A reusable Unity package that makes a normal monitor behave like a window into a fixed 3D scene. A webcam estimates one viewer's eye position; an off-axis projection uses the measured screen rectangle. Ordinary models keep their transforms and materials. This is a **single-viewer, monoscopic** display.

## Repository

| Path | Responsibility |
| --- | --- |
| [Packages/com.headtracked.display](Packages/com.headtracked.display) | Reusable projection, calibration, observations, interchangeable tracking sources and optional scene dressing. The core assembly has no demo assets or URP dependency. |
| [Demo](Demo) | Unity 6.3 LTS URP comparison environment, UI, example assets and diagnostic layouts. |
| [python_tracker](python_tracker) | Optional MediaPipe/OpenCV webcam tracker and camera calibration. |
| [tools](tools) | One Windows launcher, optional plugin setup and asset preparation. |

For your own project, start with the [package integration guide](Packages/com.headtracked.display/README.md). Its use does not require the demo UI, assets or launch scripts.

## Run the demo

Double-click [Run-HeadTrackedDemo.cmd](Run-HeadTrackedDemo.cmd) on a prepared Windows checkout.

```powershell
./Run-HeadTrackedDemo.cmd                 # webcam 0
./Run-HeadTrackedDemo.cmd -Camera 1       # another webcam
./Run-HeadTrackedDemo.cmd -NoTracker      # visual comparisons without a webcam
./Run-HeadTrackedDemo.cmd -Stop           # stop this checkout's player and tracker
```

**Alt+F4** closes the player and stops its tracker. A heartbeat releases the webcam if the launcher disappears. Builds, Python environments and the downloaded face model are excluded from Git; a new clone needs the setup below.

First launch asks only for **visible screen width, height and eye-to-screen distance**. Measure these values; the examples are not hardware measurements. Sit at the entered distance. Starting captures a reference if a face is available; otherwise capture it later under Settings.

The fixed comparison scene contains a stationary 16 cm ceramic vase, wood floor and billboard chamber. All three switches operate on that same scene:

| Switch | ON | OFF | Retained |
| --- | --- | --- | --- |
| Head tracking | Camera follows eye position. | Holds the viewpoint when turned off; measurements continue. | Models and physical projection. |
| Billboard effect | Frame, surround, chamber and depth references visible. | Hides only the dressing. | Content, camera, lighting and render quality. |
| Surface detail | Original textures, normal maps and material finish. | Plain matte material variants. | Geometry, submesh slots, placement, camera and lighting. |

Compare one switch at a time or any combination. **F1** hides the UI. **Settings / F2** opens one drawer on the right:

- **Calibration:** dimensions, webcam placement, IPD, reference capture, intrinsics and backend.
- **Appearance:** edge stability, render scale, chamber dimensions and optional animation. Leave animation off for stationary comparisons.
- **Layouts:** explicit content changes and original/fixation/depth/material experiments. Return to the fixed comparison scene for all three primary switches.
- **Diagnostics:** filtering, distance hold/correction, pose/gaze readings, gaze calibration and CSV recording.

**Save physical calibration**, **Save comparison switch preferences** and **Save experimental layout** are separate actions. Experiments load only when explicitly requested. Normal launches always start in the comparison scene. **Reset comparison defaults** keeps measured calibration.

See [demo structure and extension rules](docs/demo-workflow.md), [tracking tests](docs/tracking-test.md), [physical scale](docs/physical-scale.md), [rendering](docs/rendering.md), and [billboard API](docs/billboard-illusion.md).

## Build from source

The checked-in demo targets **Unity 6000.3.22f1 (6.3 LTS)**, URP 17.3.0 and Windows x64. Python tracking needs Python 3 and a webcam. Other Unity projects can consume the core package independently; other platforms need a compatible provider and build validation.

```powershell
git clone https://github.com/lollipop190000/head-tracked-display.git
cd head-tracked-display
py -3 -m venv python_tracker/.venv
./python_tracker/.venv/Scripts/python.exe -m pip install -r python_tracker/requirements.txt
py -3 python_tracker/download_model.py
```

Open `Demo` in Unity, open `Assets/Scenes/HeadTrackedDemo.unity`, then Play. Scene and rendering assets are checked in. **Head Tracked > Create or refresh demo scene** regenerates the scene if needed.

Start Python in another terminal:

```powershell
./python_tracker/.venv/Scripts/python.exe python_tracker/tracker.py --list-cameras
./python_tracker/.venv/Scripts/python.exe python_tracker/tracker.py --camera 0 --calibration-file "$env:USERPROFILE/AppData/LocalLow/DefaultCompany/Demo/tracker_runtime_calibration.json"
```

That path matches this demo's company/product names. Other projects use their own `Application.persistentDataPath`. The Windows launcher reads those names from the project settings; `-CalibrationDirectory <path>` overrides the directory.

Build the enabled scene for **Windows x64** to `Builds/HeadTrackedDemo/HeadTrackedDemo.exe`, then use the launcher. With the optional Unity CLI:

```powershell
unity build Demo --target StandaloneWindows64 --output-path Builds/HeadTrackedDemo/HeadTrackedDemo.exe
```

## Providers and calibration

Both providers implement `HeadObservationSource` and use the same projection.

| Provider | Setup | Operation |
| --- | --- | --- |
| Python bridge | Install requirements and download the task model above. | Sends landmarks/pose over 127.0.0.1:8765; video frames are not sent to Unity. |
| Unity MediaPipe, optional | Run `py -3 tools/setup_native_plugin.py` and reimport in Unity. | Select it in Calibration, then choose a webcam. Windows CPU support needs target-PC testing. |

Python supports calibrated rigid face pose, angles, iris features and timing. Unity-native tracking uses the yaw-compensated eye-span estimator. Head orientation and gaze do not rotate the camera or models.

Measure screen size, webcam lens position, viewing distance and eye separation. Capture a reference at the measured distance, then **Save physical calibration**. Check both 10 cm rulers. Optional camera intrinsics and two-distance Z calibration are covered in the [tracking guide](docs/tracking-test.md).

## Use in another Unity project

Install with **Add package from disk** using [package.json](Packages/com.headtracked.display/package.json), or add this Git URL:

```text
https://github.com/lollipop190000/head-tracked-display.git?path=/Packages/com.headtracked.display
```

Add `HeadTrackedDisplay` to a camera, assign a screen-center Transform and a `HeadObservationSource`, and enter `DisplayCalibration` measurements. Screen-local +Z points behind the monitor; units are metres. Meshes need no tracking script. `TrackingEnabled` can hold the current viewpoint while measurements remain available.

Optional `BillboardIllusionController.EffectEnabled` hides dressing while retaining content. Disabling the component hides its entire rig. Supply materials compatible with your render pipeline. See the [package README](Packages/com.headtracked.display/README.md).

## Geometry and validation

For eye `E = (ex, ey, -d)` and model point `P = (px, py, z)`, the ray crosses the physical screen at:

```text
Sxy = Exy + [d / (d + z)] (Pxy - Exy)
```

The camera translates but keeps the screen plane's orientation. Models stay fixed; a stationary eye does not produce head-driven model rotation.

Use Unity Test Runner, or:

```powershell
unity test Demo --mode EditMode --output Builds/editmode.xml
unity test Demo --mode PlayMode --output Builds/playmode.xml
./python_tracker/.venv/Scripts/python.exe -m unittest discover -s python_tracker -p 'test_*.py' -v
```

Tests cover projection, calibration, filtering, protocol/session lifetime, scene lifecycle and all eight primary switch combinations. They check retained content/transforms, calibration, render quality and continued measurements while the viewpoint is held.

Synthetic tests do not establish webcam accuracy, perceived realism or full motion latency. Real movement and face loss/re-entry need a live test. The monitor edge clips foreground content; a conventional display still provides one focal plane and one image to both eyes.

## License

Code is [MIT licensed](LICENSE). Example assets and optional dependencies have separate terms in [THIRD_PARTY.md](THIRD_PARTY.md).
