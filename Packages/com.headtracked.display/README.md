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

`EyePositionMeters`, `IsTracking`, `Confidence`, `SourceStatus`, and `PoseUpdated` expose the tracking result for game interaction. `Confidence` is currently a binary face found value, not a graded landmark quality score. When tracking times out, the view eases back to the neutral position.

`EstimatedEyePositionMeters` exposes the estimate before temporal smoothing. `TrackingSmoothingSeconds` controls the filter time constant. `FreezeViewingDistance` is a temporary diagnostic that fixes estimated Z at the reference viewing distance; it disables forward/back tracking and defaults to false. See [physical scale and apparent motion](https://github.com/lollipop190000/head-tracked-display/blob/main/docs/physical-scale.md) for calibration and known-size object placement.

The render camera keeps the screen plane's orientation: turning the viewer's face does not rotate the scene. Both MediaPipe sources use face orientation only to compensate for the apparent narrowing of eye spacing during yaw, which would otherwise be mistaken for a change in viewing distance. The projection maps every fixed 3D point to the ray from the measured eye through the physical screen.

See the [repository README](https://github.com/lollipop190000/head-tracked-display) for the Python tracker, native plugin, checkerboard calibration, and demo instructions. MIT license; see `LICENSE.md`.
