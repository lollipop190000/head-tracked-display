# Repository guidance

This repository's product is an open-source, reusable Unity package. The demo is a controlled test environment for that package.

- Keep reusable runtime behavior in `Packages/com.headtracked.display`, independent of demo assets, UI, URP and local launcher paths. Backend-specific optional integrations belong in their separate assemblies.
- Keep normal demo startup minimal: measured display width/height and viewing distance. Put detailed numeric inputs, IPD, camera calibration and diagnostic controls in the existing right-hand Settings drawer.
- Preserve one fixed scene for the primary head tracking / billboard effect / surface detail comparisons. Each switch changes only its named feature. Do not replace content, change its transform, reset the camera, alter quality/lighting, select a layout or start animation as a side effect of a comparison switch.
- Tracking OFF holds the current rendered viewpoint while observations/filtering continue. Billboard OFF hides dressing while retaining content. Surface OFF uses reversible material variants without mutating imported assets.
- Scene/content experiments require an explicit action under Layouts. Normal startup must not auto-load a saved experiment.
- Save physical calibration, demo comparison preferences and experimental layouts separately. A calibration save must not change subsequent scene selection.
- Maintain one public Windows CMD entry point, `Run-HeadTrackedDemo.cmd`, backed by `tools/run_demo.ps1`. Use parameters for camera, no-tracker and stop actions; do not add feature-specific CMD files.
- Organize demo additions by the responsibilities described in `docs/demo-workflow.md`. Keep bootstrap limited to composition.
- Test meaningful independent combinations and round trips for comparison changes. Preserve existing projection, lifecycle and calibration tests. Update README and affected detailed guides when controls change.
- Validate runtime package changes in a minimal consumer project without URP or the demo. Declare every used built-in Unity module in the package manifest; do not rely on rendering packages or the test framework to pull it in indirectly.
- Do not check in local builds, tracker environments, downloaded face models, logs or personal hardware calibration. Preserve MIT licensing and third-party asset attribution.
