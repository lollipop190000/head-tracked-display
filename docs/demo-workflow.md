# Demo workflow and extension rules

The reusable product is `Packages/com.headtracked.display`. `Demo` is a controlled environment for evaluating it.

## Normal flow

1. First run: enter measured visible screen width/height and viewing distance. Detailed physical setup lives in Settings > Calibration.
2. Observe one fixed comparison scene. Its three primary switches change head tracking, billboard dressing and surface detail independently.
3. Open Settings on the right only when needed. F1 hides the UI; F2 opens/closes Settings.
4. Select experimental layouts explicitly. Return to the fixed comparison scene for all three comparisons.

Head tracking OFF holds the rendered eye position while observation collection and filtering continue. Billboard OFF hides the frame/chamber/reference dressing, retaining the same content and rendering profile. Surface OFF uses owned material variants; ON restores original material arrays. Alpha coverage is retained where it defines a mesh silhouette.

## Code ownership

| File or folder | Responsibility |
| --- | --- |
| Package `Runtime/` | Projection, observations, filtering, calibration and reusable dressing. No demo UI, assets, launcher paths or URP in the core assembly. |
| `DemoBootstrap.cs` | Runtime composition and initial wiring. |
| `DemoComparison.cs` | Fixed scene, independent switch actions and setup/preferences. |
| `DemoInterface.cs` | Comparison strip and single right-hand settings drawer. |
| `DemoSettingsPanel.cs` | Display-size and experimental model input widgets. |
| `DemoCalibration.cs` | Backend selection, calibration and explicit experiment saves. |
| `DemoDiagnostics.cs` | Samples, gaze/distance calibration, recording and readings. |
| `DemoScene.cs` | Example geometry, explicit layouts and rendering lifecycle. |
| `DemoSettingsData.cs` | Demo persistence DTOs. |
| `SurfaceAppearanceComparison.cs` | Demo-only reversible material variants. |
| `tools/run_demo.ps1` | One Windows player/tracker lifecycle; no feature-specific launch modes. |

The partial files retain the existing scene coordinator's script identity and separate its responsibilities. They do not introduce demo dependencies into the package.

## Persistence

Files live under Unity's `Application.persistentDataPath`, derived from company/product identity:

| File | Saved by | Loaded by |
| --- | --- | --- |
| `display_calibration.json` | Quick setup, display-size apply, Save physical calibration. | Every launch. |
| `demo_preferences.json` | Quick setup completion, Save comparison switch preferences, Reset comparison defaults. | Every launch; setup completion and the three switch states only. |
| `demo_model_layout.json` | Save experimental layout. | Explicit Load saved experimental layout only. |
| `screen_gaze_calibration.json` | Successful nine-point gaze fit. | Launch, with context validation. |
| `tracker_runtime_calibration.json` | Python bridge synchronization. | Python tracker. |

Saving calibration never saves or selects a scene experiment. Older layouts remain available for explicit loading. Normal launches no longer auto-load them. Animation, render scale and model/chamber adjustments are session settings unless saved as an experimental layout. Reset preserves completed physical, distance and gaze calibration.

## Adding a feature

- Put reusable behavior in the package and test it without demo assets. Keep example content, UI and URP tuning in Demo.
- Give each A/B switch one responsibility. Preserve content, transforms, camera composition, lighting, calibration and quality unless that property is the comparison subject.
- Primary comparisons must not select presets, replace content, reset the scene or start animation.
- Put optional numeric input in the existing right-hand drawer. Keep initial setup minimal.
- Add explicit experiments under Layouts when placement/content must change.
- Extend the single launcher for lifecycle or backend needs; do not add another CMD per feature.
- Keep personal calibration and machine-specific paths out of Git.
- Verify combinations and round trips, material restoration and continued measurements during viewpoint hold. Update the affected guides with each UI change.
