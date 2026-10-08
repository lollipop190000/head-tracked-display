# Head Tracked Display for Unity

Single viewer, physical monitor off-axis projection for Unity 6.3 or newer. The package moves one Unity camera and updates its asymmetric projection from an interchangeable eye observation source. Models and their materials require no tracking code.

## Install

In Unity Package Manager, select **Add package from disk** and choose this folder's `package.json`. For a Git installation, use the repository URL with `?path=/Packages/com.headtracked.display`. The package provides the Python TCP bridge without other third-party Unity packages. The optional Unity-native provider activates after installing MediaPipe Unity Plugin v0.16.3 or a compatible later version; use `tools/setup_native_plugin.py` from the repository root to set up the demo.

## Scene setup

1. Add `HeadTrackedDisplay` to the render camera.
2. Create a Transform at the physical screen centre. Its local +Z points into the virtual scene; use scale `(1,1,1)`.
3. Add one `HeadObservationSource`, such as `PythonBridgeSource`, to an active GameObject. Assign the screen Transform and source to the display component, or call `display.Configure(plane, source)`.
4. Set the physical screen width and height in metres. Measure the webcam position relative to the screen centre, with negative Z toward the viewer. Set the reference eye distance and measured eye separation (IPD), and capture the reference while the viewer is at that distance. In basic mode, `useEyeSeparationForBasicScale` defaults to true and uses the reference eye spacing to estimate camera scale. Disabling it restores the assumed `horizontalFovDegrees` path. Before capture, basic mode also falls back to that assumed FOV.
5. Run full screen and keep the rendered aspect ratio close to the physical screen aspect ratio. Place normal 3D meshes at positive or negative Z relative to the screen plane.

`EyePositionMeters`, `IsTracking`, `Confidence`, `SourceStatus`, and `PoseUpdated` expose the tracking result. `LatestObservation` also provides optional camera-space rigid eye position, camera-relative head angles, iris offsets, pose quality, and timing data. Rigid-pose confidence is a reprojection-derived heuristic, not a measured position error or calibrated probability; legacy sources retain binary face-found confidence. Short losses hold briefly, then the view eases back to neutral.

`EstimatedEyePositionMeters` exposes the estimate before temporal smoothing. `UseAdaptiveFilter` defaults to true and runs One Euro filtering once per new observation. `FilterMinimumCutoffHz` and `FilterSpeedCoefficient` tune it. With adaptive filtering disabled, `TrackingSmoothingSeconds` controls the original fixed filter. `ResultAgeMilliseconds` includes tracker-reported software age plus time since packet reception; it excludes exposure, prior driver buffering, and monitor latency. `FreezeViewingDistance` disables forward/back tracking for diagnosis.

The render camera keeps the screen plane's orientation. The Python tracker fits 20 canonical face landmarks with calibrated PnP and reconstructs the eye-corner midpoint proxy in 3D. Set `useRigidFacePose=false` to compare with the legacy eye-span estimator. The optional Unity-native source continues to use yaw-compensated eye spacing. A rigid fit that is invalid or stale is rejected; it does not silently switch estimation methods.

`HeadTrackedDisplay.Configure()` wires calibration into the Python source, as does a serialized source assigned before `Awake`. Unity automatically publishes `tracker_runtime_calibration.json` under `Application.persistentDataPath`; launch Python with `--calibration-file` pointing to that exact path. Other projects have their own company/product persistent-data directory. Fits from a different camera configuration revision are rejected until Python reloads it. Camera coordinates in `HeadObservation.poseEyeCamera` follow OpenCV: +X right, +Y down, +Z away from the lens, in metres. Conversion to screen coordinates applies the configured mirror and webcam extrinsics.

`ScreenGazeCalibration` fits a personal approximate screen mapping from iris offsets and head pitch/yaw. `TryEstimate` returns an unclamped viewport point. The demo implements nine-target sample collection and saves the fit. This is not a validated 3D gaze ray or fixation depth; gaze does not steer the projection. See the [tracking test guide](https://github.com/lollipop190000/head-tracked-display/blob/main/docs/tracking-test.md) and [physical scale guide](https://github.com/lollipop190000/head-tracked-display/blob/main/docs/physical-scale.md).

See the [repository README](https://github.com/lollipop190000/head-tracked-display) for the Python tracker, native plugin, checkerboard calibration, and demo instructions. MIT license; see `LICENSE.md`.
