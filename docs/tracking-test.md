# Eye, head, and gaze test

## Run on the prepared PC

Close any previous demo, then double-click **Run-TrackingTest.cmd**. It starts the Python tracker and Windows player, selects the centred 15 cm plant 5 cm behind the screen, and opens the diagnostic panel. Pass `-Camera 1` to the launcher if needed. Alt+F4 closes the player and stops the tracker.

1. Enter measured screen dimensions, webcam offset/tilt, IPD, and eye-to-screen reference distance in the left panel. Face forward at that measured distance and press **Capture reference distance**. Camera parameters automatically synchronize with Python within about half a second. **Save calibration** persists the physical setup.
2. Wait for **FACE FOUND** and **RIGID FIT**. The new pose/gaze path is in the **Python bridge**. The optional Unity-native provider retains the existing eye-span estimator and shows **LEGACY SOURCE**; it does not supply the new iris features.
3. Select each test in the right panel, press **Set test baseline**, and compare the readings and visible plant. Use **Record selected test for 10 seconds** to save numeric samples. No camera images are written by this test.

| Test | Motion | What to check |
| --- | --- | --- |
| Eyes only | Keep your head still; look left/right by moving only your eyes. | Iris features should change, while estimated eye position should remain approximately stable. |
| Head turn | Keep the body still; turn the head while watching the plant. | Head angles should change. Real eyes move around the neck pivot; expect that physical movement, rather than exactly zero eye travel. Look for large spurious Z changes or abrupt view jumps. |
| Body move | Face roughly forward and translate sideways by a measured 10 cm. | Estimated lateral eye travel should be close to the measured displacement, with the correct sign. |
| Depth | Face roughly forward and move towards/away from the monitor. | Raw distance should decrease/increase. Compare measured distances and the equal-size plants at different depths. |

Disable **Use rigid face pose** to compare with the old eye-span estimator. Keep screen measurements, model layout, and filtering unchanged for that comparison. The **Adaptive eye filter** switch compares One Euro filtering with the original fixed time constant. Cutoff and speed coefficient can be tuned in the left panel; those filter controls are session settings.

## Forward/back distance calibration

Close the demo and double-click **Run-DepthTest.cmd** (or pass `-Camera 1`). It opens three 15 cm plants: left at +5 cm, middle at +30 cm, right at +100 cm. The room and supports are hidden. **Restore all models and original positions** recovers the original layout.

1. Finish screen/webcam, IPD, reference capture, and any checkerboard setup first. Wait for a valid face fit. Changing this setup afterwards invalidates the distance correction.
2. Measure the perpendicular distance from your eye midpoint to the **screen plane**, not the webcam. Enter it in **Near measured cm**, sit at that position facing forward, and press **Capture near**. Hold still while 20 new observations are collected.
3. Repeat at a farther measured distance using **Far measured cm** and **Capture far**. The initial 40/80 cm entries are examples, not automatic measurements. Use distances 25–150 cm apart from the screen and at least 10 cm apart from each other.
4. Press **Apply measured Z calibration**, then **Save calibration** in the left panel. This fits `correctedDistance = scale * rawDistance + offset` to the two anchors. It changes Z only; X/Y, model size, model transforms, and projection geometry remain unchanged. Captures with excessive variation, reversed distance direction, insufficient separation, or implausible scale/bias are rejected.
5. Test an intermediate measured distance and gentle continuous motion. The raw and rendered readouts distinguish tracking error from projection behaviour. A two-point correction does not remove nonlinear error, person-specific pose error, or latency. **Reset Z correction** removes it. Recapture after changing camera parameters, reference capture, IPD, source estimation mode, or image size.

**Physical XYZ** uses tracked distance. **Hold size: XY only** captures the current rendered distance when activated and keeps it fixed while X/Y continue tracking. This is a diagnostic comparison with forward/back parallax disabled. Return to Physical XYZ for the physical-window view.

Approaching the screen normally makes behind-screen objects occupy fewer screen pixels, particularly distant objects. The screen occupies more of your visual field as you approach; the object's visual angle still increases. The panel shows both quantities separately as a centre-plane approximation for a 3D mesh. See [the geometry and numerical example](physical-scale.md#forwardback-size-and-visual-angle). Reversing Z or rescaling objects would change the fixed scene rather than correct distance estimation.

## Approximate gaze calibration

Finish physical camera setup first. With both eyes visible and the rigid pose valid, press **Calibrate approximate screen gaze (9 points)**. Look at each cross, wait for fixation, and press **Space**. The program collects 20 new valid frames per target. **Esc** cancels. A timeout allows retrying the same target. Avoid blinking during collection and keep the head roughly still.

The fitted screen mapping uses iris offsets plus head yaw and pitch. A visible marker shows its approximate screen intersection. Test additional positions between calibration targets and repeat with gentle head motion: calibration fit error is **not** independently measured gaze accuracy. Large head motion, glasses, reflections, low resolution, occlusion, and person-specific eye geometry can degrade the mapping. It does not measure fixation depth or provide a validated optical 3D gaze ray. Gaze never rotates the render camera or moves models.

The calibration is saved automatically as `screen_gaze_calibration.json`. Physical setup or display-resolution changes invalidate its context; recalibrate afterwards. The current linear calibration is intended for one person and a limited viewing range. A large neural gaze model or dedicated eye tracker is not required for this initial diagnostic.

## Data and timing

For this demo, files live in `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Demo`:

- `tracker_runtime_calibration.json`: derived focal length, principal point, lens distortion, IPD, image size, and revision. Unity publishes it and Python reloads it. Fits from older revisions are rejected.
- `TrackingTests/motion-<test>-<timestamp>.csv`: time, test ID, tracking validity, estimated screen-relative eye XYZ before smoothing (including Z correction/hold), head angles, iris offsets, gaze validity, reprojection error, inference/PnP time, result age, tracker update rate, uncorrected distance, rendered distance, and Z-hold flag. IDs 0/1/2/3 correspond to eyes/head/body/depth. Distances are in metres.
- `display_calibration.json`: physical setup and the optional two-distance Z correction, written by **Save calibration**.
- `screen_gaze_calibration.json`: personal screen-gaze mapping. To reset, use the calibration button and repeat the nine targets.

**Result age** starts after OpenCV delivers a frame and includes software processing and time waiting for Unity to consume it. It excludes sensor exposure, hidden camera-driver buffering before delivery, and monitor presentation latency. A high Unity render FPS alone does not establish low tracking latency. The capture thread continuously drains the webcam into one latest-frame slot; Unity also consumes a single latest packet. The tracker may update less often than rendering, depending on the webcam and CPU.

On this prepared PC, a synthetic 20-landmark PnP benchmark (100 fits) had median **0.306 ms**, p95 **0.937 ms**. A separate eight-second webcam capture produced 121 frames but **no detected faces**. Its no-face timings do not establish face-tracking speed, stability, or gaze accuracy. Actual human motion and perceived realism remain to be checked with the tests above.

## Implementation and limits

The Python path fits 20 facial landmarks to MediaPipe's canonical face with calibrated OpenCV `solvePnP` and LM refinement. Canonical eye-corner centres are scaled by entered IPD and transformed by the fitted head pose. These are anatomical proxies, not individually measured optical eye centres. Monocular scale, canonical-to-person shape mismatch, inaccurate camera intrinsics, and partial occlusion remain sources of error. Reprojection-derived quality is a geometric heuristic, not a probabilistic confidence or true position error.

Invalid fits do not silently change to the old estimator. The view holds briefly and returns smoothly to neutral if quality does not recover. Eye closure invalidates gaze while head/eye pose can remain available. The new fields are optional; legacy providers remain compatible.

Sources: [MediaPipe Face Landmarker](https://developers.google.com/edge/mediapipe/solutions/vision/face_landmarker), [OpenCV PnP](https://docs.opencv.org/4.13.0/d5/d1f/calib3d_solvePnP.html), [MediaPipe Iris limitations](https://research.google/blog/mediapipe-iris-real-time-iris-tracking-depth-estimation/), [One Euro filter](https://gery.casiez.net/1euro/).
