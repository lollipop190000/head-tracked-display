# Changelog

## 0.4.0

- Add optional `BillboardIllusionController` and serializable `BillboardIllusionSettings`: physical-screen frame, recessed chamber, owned visual model clone, and optional in/out animation.
- Expose the existing `HeadTrackedDisplay.ScreenPlane` reference for scene integration. Tracking and projection calculations are unchanged.
- Add package PlayMode coverage for standalone rig integration, source preservation, resizing/lifecycle, and rendered frame occlusion.

## 0.3.0

- Add optional physical screen height derivation from camera resolution and measured width.
- Demo preserves source submesh materials and provides a PBR rendering comparison.
