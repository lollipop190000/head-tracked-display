# Physical scale and apparent motion

The goal is a fixed object at a measured position relative to the monitor. Unity units represent metres. A model's real dimensions, the eye position, and the screen dimensions must agree in that coordinate system.

## Why a small object can appear to move a lot

For a point at screen-relative depth `z`, an eye distance `d`, and lateral eye movement `deltaEye`, the displacement on the physical screen is:

```text
deltaScreen = [ z / (d + z) ] deltaEye
```

For a vertical segment of height `H` whose endpoints have the same depth, its projected height is `d H / (d + z)`. Its lateral displacement relative to that image height is therefore:

```text
abs(deltaScreen) / imageHeight = abs(z deltaEye) / (d H)
```

This is an exact relation for that segment, and an approximate size comparison for a volumetric model. At `d = 60 cm`, `z = 42 cm`, `H = 22 cm`, a `10 cm` head move shifts the image about `4.1 cm`, or `32%` of its projected height. A `10 cm` plant at `z = -18 cm` shifts about `4.3 cm`, or `30%` of its projected height. These large changes are geometrically possible even with perfect tracking.

The correct angle for revealing another side comes from the eye-to-object geometry. Model dimensions already affect the projected size and visible silhouette. An extra size-dependent rotation would move the object away from the fixed-world target.

## Basic tracking scale

Previously, the basic estimator used an assumed `60 degree` webcam horizontal field of view for lateral and vertical movement. Capturing a reference calibrated depth, but did not calibrate that movement scale. A mismatched webcam FOV could therefore make a physical head move appear too large or too small.

After reference capture, **Use eye spacing for basic movement scale** now estimates the basic focal length from:

```text
focalPixels = yawCorrectedReferenceEyeSpanPixels
              * referenceCameraDepthMetres / eyeSeparationMetres
```

Both lateral and vertical coordinates then use that scale. Reference capture also records the eye midpoint, allowing the estimator to solve for optical-axis depth using the configured webcam rotation and offset. Under the pinhole approximation with a level webcam, the reference-distance factor cancels from lateral translation. An incorrect reference distance still produces an incorrect Z coordinate and therefore incorrect parallax; this is not a way to recover unknown absolute distance from one camera. Old saved reference captures without a midpoint use the previous optical-depth approximation until recaptured.

Measure interpupillary distance (IPD) where possible. The default `63 mm` is an example. MediaPipe eye-corner midpoints approximate eye centres; they are not a precise measurement of pupil separation. Yaw correction, an incorrectly entered webcam tilt, principal-point error, and lens distortion remain possible error sources. The checkerboard-calibrated intrinsics path remains available. Disabling the eye-spacing option restores the assumed-FOV path for comparison.

## A measurement sequence

1. Measure the visible screen, enter its width and height, and run full screen. Enable **Show 10 cm screen ruler** and compare it with a physical ruler. Resolve an aspect warning before judging model motion.
2. Enter measured IPD, webcam offset, and a physically measured eye-to-screen reference distance. Face forward, enable the basic eye-spacing option, and press **Capture reference distance**.
3. Press **Set movement measurement origin**. Translate your head horizontally by a measured `10 cm`, keeping its orientation and distance approximately constant. After stopping, the X readout should change by about `10 cm`. Check the mirror setting if its sign is reversed. Judge the settled endpoint so smoothing lag does not masquerade as a scale error.
4. Open **Model size and depth**. Start with **Shallow desktop**, then compare **Depth stress test**. Heights stay constant between presets. Enter real object heights and centre depths for the objects you want to represent; the `25 cm` chair is a scale model, not a life-sized office chair. Apply the layout and save calibration to persist it.
5. Temporarily enable **Freeze viewing distance** while moving only laterally. If unintended zoom or floating is reduced, investigate the distance estimate. This diagnostic disables forward/back tracking; turn it off for normal use.
6. Compare smoothing values while moving and stopping. The displayed value is the filter's time constant, not measured camera-to-display latency. Low smoothing can expose jitter, while high smoothing can make a fixed scene appear to lag.

The default shallow preset uses centre depths `-2.5, +10, +16, -6 cm` for the bear, chair, green plant, and red plant. The stress preset uses `-4, +29, +42, -18 cm`. A separate purple chair is 1 m tall at depth `+3 m`, and a blue plant is 1.4 m tall at depth `+6 m`. The presets affect the first four near models; distant models retain their settings. Their dimensions are preserved, and supports and room dimensions follow the placement. Model depth accepts up to `+15 m`. This changes actual virtual positions, without changing the projection rule or multiplying head movement by an arbitrary visual gain.

The model layout is saved separately as `demo_model_layout.json` in Unity's persistent data directory. The default and saved layouts may be clipped at the screen edge; foreground models must also remain beyond the camera near plane.

## What the perception papers support

[Kubota & Fukiage (2025)](https://journals.plos.org/ploscompbiol/article?id=10.1371/journal.pcbi.1013020) measures depth biases in still indoor photographs. Its approach motivates measuring perceived distance separately from geometric distance; its fitted compression parameters are not validated corrections for this interactive display.

[Scarfe & Hibbard (2013)](https://pubmed.ncbi.nlm.nih.gov/23665429/) studies the contribution of edges and contours to perceived shape, and [Aubuchon et al. (2024)](https://research-repository.st-andrews.ac.uk/handle/10023/30901) demonstrates interactions between shading and disparity. Their application here is an engineering hypothesis: provide coherent silhouettes, scale, occlusion, support contact, and illumination, then test whether observers perceive a stable object. Their stereoscopic results do not specify an optimal motion gain for a monoscopic monitor.

These adjustments can improve measurement and cue consistency on a normal monitor. They cannot supply separate left/right images or focus at the virtual object's depth. Human testing is still needed to establish whether a correctly calibrated scene feels stable and physically present.
