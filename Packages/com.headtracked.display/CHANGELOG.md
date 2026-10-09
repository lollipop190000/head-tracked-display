# Changelog

## 0.5.0

- Add `ScreenWindowFrame`: four collider-free screen-edge strips that follow physical calibration, without creating a chamber, cloning content or changing projection. Consumers supply their own pipeline-compatible material.
- Add PlayMode frame visibility round-trip, screen resize/reparenting, invalid calibration, material ownership and cleanup regression tests.
- Generalize the one Windows launcher with project/player/tracker/calibration paths and target-scoped process, heartbeat, mutex and log handling.
- Honor a process-scoped `HEADTRACKED_CALIBRATION_DIRECTORY` override in the Python bridge and launcher. Forward `--no-tracker` to consuming players and preserve quoted native player arguments.

## 0.4.2

- Declare the built-in Physics and JSON serialization modules directly. Standalone package installs no longer depend on URP or the test framework to make `Collider` and `JsonUtility` available.
- Add `HeadTrackedDisplay.TrackingEnabled` to hold the current rendered viewpoint while fresh observation collection and filtering continue.
- Add `BillboardIllusionController.EffectEnabled` to compare dressing on/off without replacing or repositioning content.
- Reorganize the demo around one stationary comparison, independent switches, minimal setup, a right-hand settings drawer and separate calibration/preferences/experiment saves. Consolidate Windows launchers.

## 0.4.1

- Remove overlapping coplanar surfaces between billboard rims and chamber walls, which could shimmer at their shared boundary.
- Avoid redundant transform writes and animation calculations for static billboard content.
- Add seam regression coverage across wall thicknesses. The demo adds a temporary URP profile with TAA, compact single-cascade shadows, optional SSAO, adjustable render scale, and frame-rate feedback; the package retains its pipeline independence.

## 0.4.0

- Add optional `BillboardIllusionController` and serializable `BillboardIllusionSettings`: physical-screen frame, recessed chamber, owned visual model clone, and optional in/out animation.
- Expose the existing `HeadTrackedDisplay.ScreenPlane` reference for scene integration. Tracking and projection calculations are unchanged.
- Add package PlayMode coverage for standalone rig integration, source preservation, resizing/lifecycle, and rendered frame occlusion.

## 0.3.0

- Add optional physical screen height derivation from camera resolution and measured width.
- Demo preserves source submesh materials and provides a PBR rendering comparison.
