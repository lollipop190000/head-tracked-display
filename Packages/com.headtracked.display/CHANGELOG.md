# Changelog

## Unreleased

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
