# A physical screen frame in a moving world

`ScreenWindowFrame` provides a near-field depth cue for an independently managed world. It generates four narrow border strips, with an open centre and no chamber, background or content clone. Ordinary depth testing lets world geometry pass behind or in front of it.

## Moving observer rig

Keep the rig and its ancestors at scale `(1,1,1)`. One Unity unit represents one metre. The physical plane's local +Z points into the city. With the rig origin representing the neutral observer, place the physical screen ahead by the measured viewing distance:

```csharp
var rig = new GameObject("Observer rig").transform;
var plane = new GameObject("Physical screen").transform;
plane.SetParent(rig, false);
var cameraObject = new GameObject("Render camera");
cameraObject.transform.SetParent(rig, false);
cameraObject.AddComponent<Camera>();
var display = cameraObject.AddComponent<HeadTrackedDisplay>();
display.Calibration.screenWidth = measuredWidthMeters;
display.Calibration.screenHeight = measuredHeightMeters;
display.Calibration.referenceEyeDistanceFromScreen = measuredDistanceMeters;
plane.localPosition = Vector3.forward * measuredDistanceMeters;
display.Configure(plane, observationSource);
var frame = rig.gameObject.AddComponent<ScreenWindowFrame>();
frame.FrameMaterial = pipelineCompatibleFrameMaterial;
frame.Configure(display);
```

Move and rotate `rig` in `Update` or before `HeadTrackedDisplay.LateUpdate`. Head tracking computes the final camera from the moving plane and the eye offset. Input code should steer the rig; `HeadTrackedDisplay` owns the final camera transform and off-axis projection. Update the plane's offset when the reference viewing distance changes.

Turning tracking off holds its current screen-local eye offset. A moving rig can still move through the city. Observation collection and filtering continue. Frame visibility is a separate comparison and does not call tracking, movement, content or quality controls.

## API and ownership

| Member | Behavior |
| --- | --- |
| `Configure(HeadTrackedDisplay)` | Assigns the display and follows its `ScreenPlane`. |
| `FrameMaterial` | Borrowed shared material. Use an asset compatible with the consumer's render pipeline. Null hides the strips; the package never discovers shaders or creates material variants. |
| `EffectEnabled` | Shows or hides only the generated frame. |
| `BorderWidthMeters` | 0.008m by default; capped at one quarter of the smaller panel dimension to preserve an opening. |
| `DepthMeters` | 0.015m by default, centred on the screen's Z=0 plane. |
| `RigRoot` | Generated frame Transform; no content is parented into it. |
| `Refresh()` | Applies calibration, material, dimensions and visibility without rebuilding geometry. |

The strips use one owned 24-vertex mesh, with no colliders or runtime asset dependency. Opposite edges fit inside the measured panel rectangle. Vertical strips meet the horizontal ones without coplanar corner overlap. Invalid/missing screen measurements hide the existing frame until valid measurements return. Disabling the component hides it; destroying the component removes its objects and mesh, preserving the borrowed material and all external content.

This component belongs to the pipeline-independent core assembly and requires no URP, demo or Unity Test Framework in a consumer. The package still declares Physics, Audio and JSON built-in modules for other core functionality.

## Validation

`ScreenWindowFrameTests` run in PlayMode and cover tracking/frame combinations and visibility round trips, retained camera projection/calibration/content, resized and rotated screens, reparenting, invalid dimensions, missing materials, and deferred disposal of owned geometry. Run package Editor and PlayMode tests in the demo and a minimal consumer without URP to catch accidental assembly coupling.
