using UnityEngine;
using UnityEngine.Rendering;

namespace HeadTracked.Display
{
    /// <summary>
    /// Four physical screen-edge strips. Owns only its generated mesh and frame;
    /// never clones content, changes calibration or writes camera matrices.
    /// Supply a material compatible with the consuming project's render pipeline.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class ScreenWindowFrame : MonoBehaviour
    {
        [SerializeField] private HeadTrackedDisplay display;
        [SerializeField] private Material frameMaterial;
        [SerializeField, Min(.0001f)] private float borderWidthMeters = .008f;
        [SerializeField, Min(.0001f)] private float depthMeters = .015f;
        [SerializeField] private bool effectEnabled = true;

        private Transform root;
        private readonly Transform[] strips = new Transform[4];
        private readonly MeshRenderer[] renderers = new MeshRenderer[4];
        private Mesh boxMesh;
        private Vector4 lastDimensions;
        private bool dimensionsDirty = true;
        private Material appliedMaterial;

        public Transform RigRoot => root;
        public Material FrameMaterial
        {
            get => frameMaterial;
            set { frameMaterial = value; Refresh(); }
        }
        public float BorderWidthMeters
        {
            get => borderWidthMeters;
            set { borderWidthMeters = PositiveOrDefault(value, .008f); Refresh(); }
        }
        public float DepthMeters
        {
            get => depthMeters;
            set { depthMeters = PositiveOrDefault(value, .015f); Refresh(); }
        }
        public bool EffectEnabled
        {
            get => effectEnabled;
            set { effectEnabled = value; Refresh(); }
        }

        public void Configure(HeadTrackedDisplay targetDisplay)
        {
            display = targetDisplay;
            Refresh();
        }

        /// <summary>Apply current screen dimensions without rebuilding content or geometry.</summary>
        public void Refresh()
        {
            bool valid = display != null && display.ScreenPlane != null &&
                IsPositiveFinite(display.Calibration.screenWidth) && IsPositiveFinite(display.Calibration.screenHeight);
            if (!valid)
            {
                if (root != null) root.gameObject.SetActive(false);
                return;
            }
            if (root == null) Build();
            if (root.parent != display.ScreenPlane)
            {
                root.SetParent(display.ScreenPlane, false);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
            }
            float width = display.Calibration.screenWidth, height = display.Calibration.screenHeight;
            // Leave a positive opening even when a consumer supplies an oversized border.
            float border = Mathf.Min(PositiveOrDefault(borderWidthMeters, .008f), Mathf.Min(width, height) * .25f);
            float depth = PositiveOrDefault(depthMeters, .015f);
            var dimensions = new Vector4(width, height, border, depth);
            if (dimensionsDirty || !dimensions.Equals(lastDimensions))
            {
                Place(strips[0], new Vector3(-(width - border) * .5f, 0f, 0f),
                    new Vector3(border, height - 2f * border, depth));
                Place(strips[1], new Vector3((width - border) * .5f, 0f, 0f),
                    new Vector3(border, height - 2f * border, depth));
                Place(strips[2], new Vector3(0f, -(height - border) * .5f, 0f),
                    new Vector3(width, border, depth));
                Place(strips[3], new Vector3(0f, (height - border) * .5f, 0f),
                    new Vector3(width, border, depth));
                lastDimensions = dimensions;
                dimensionsDirty = false;
            }
            if (appliedMaterial != frameMaterial)
            {
                foreach (var renderer in renderers) renderer.sharedMaterial = frameMaterial;
                appliedMaterial = frameMaterial;
            }
            // A caller-supplied material avoids runtime shader discovery/stripping and pipeline coupling.
            foreach (var renderer in renderers) renderer.enabled = frameMaterial != null;
            bool visible = isActiveAndEnabled && effectEnabled;
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        }

        private void Build()
        {
            // The external screen may have been destroyed while this component survived.
            if (boxMesh != null) Release(boxMesh);
            root = new GameObject("Physical screen window frame").transform;
            boxMesh = MakeBoxMesh();
            string[] names = { "Left", "Right", "Bottom", "Top" };
            for (int i = 0; i < strips.Length; i++)
            {
                strips[i] = new GameObject(names[i]).transform;
                strips[i].SetParent(root, false);
                strips[i].gameObject.AddComponent<MeshFilter>().sharedMesh = boxMesh;
                var renderer = strips[i].gameObject.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.allowOcclusionWhenDynamic = false;
                renderer.sharedMaterial = frameMaterial;
                renderers[i] = renderer;
            }
            appliedMaterial = frameMaterial;
            dimensionsDirty = true;
        }

        private static Mesh MakeBoxMesh()
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var uv = new Vector2[24];
            var indices = new int[36];
            Vector3[] faceNormals = { Vector3.forward, Vector3.back, Vector3.right,
                Vector3.left, Vector3.up, Vector3.down };
            Vector3[] faceRights = { Vector3.right, Vector3.left, Vector3.back,
                Vector3.forward, Vector3.right, Vector3.right };
            Vector3[] faceUps = { Vector3.up, Vector3.up, Vector3.up,
                Vector3.up, Vector3.back, Vector3.forward };
            for (int face = 0; face < 6; face++)
            {
                int v = face * 4, t = face * 6;
                Vector3 centre = faceNormals[face] * .5f;
                Vector3 right = faceRights[face] * .5f, up = faceUps[face] * .5f;
                vertices[v] = centre - right - up; vertices[v + 1] = centre + right - up;
                vertices[v + 2] = centre + right + up; vertices[v + 3] = centre - right + up;
                for (int corner = 0; corner < 4; corner++) normals[v + corner] = faceNormals[face];
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right;
                uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "ScreenWindowFrame unit box", vertices = vertices,
                normals = normals, uv = uv, triangles = indices };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static void Place(Transform strip, Vector3 centre, Vector3 size)
        { strip.localPosition = centre; strip.localScale = size; }
        private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);
        private static float PositiveOrDefault(float value, float fallback) => IsPositiveFinite(value) ? value : fallback;
        private void LateUpdate() => Refresh();
        private void OnEnable() => Refresh();
        private void OnDisable() { if (root != null) root.gameObject.SetActive(false); }
        private void OnValidate()
        {
            borderWidthMeters = PositiveOrDefault(borderWidthMeters, .008f);
            depthMeters = PositiveOrDefault(depthMeters, .015f);
            dimensionsDirty = true;
        }
        private void OnDestroy()
        {
            if (root != null) { root.gameObject.SetActive(false); Release(root.gameObject); }
            if (boxMesh != null) Release(boxMesh);
        }
        private static void Release(Object item)
        {
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
    }
}
