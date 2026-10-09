using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeadTracked.Display
{
    /// <summary>
    /// Optional physical-window scene dressing. Never writes camera matrices, observations, or calibration.
    /// Owns a cloned content object and generated geometry; source models and materials are retained.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class BillboardIllusionController : MonoBehaviour
    {
        [SerializeField] private HeadTrackedDisplay display;
        [SerializeField] private GameObject contentPrefab;
        [SerializeField] private BillboardIllusionSettings settings = new BillboardIllusionSettings();
        public Material frameMaterial;
        public Material surroundMaterial;
        public Material chamberMaterial;
        public Material defaultContentMaterial;

        private Transform root, frameRoot, chamberRoot, contentRoot;
        private readonly Transform[] frame = new Transform[4], surround = new Transform[4], walls = new Transform[5];
        private readonly Transform[] references = new Transform[2];
        private readonly List<Material> ownedMaterials = new List<Material>();
        private GameObject content;
        private GameObject builtPrefab;
        private Vector3 contentCentre, originalContentScale;
        private float originalContentHeight, appliedHeight = -1f, animationStarted;
        private bool wasAnimating;
        private Material fallbackFrame, fallbackSurround, fallbackChamber, fallbackContent;
        private Vector4 lastOpening, lastConstruction;
        private bool geometryDirty = true, materialsDirty = true, lastShowFrame, lastShowReferences;
        [SerializeField] private bool effectEnabled = true;
        private Material lastFrameMaterial, lastSurroundMaterial, lastChamberMaterial, lastContentMaterial;

        public BillboardIllusionSettings Settings => settings;
        public Transform RigRoot => root;
        public GameObject ContentInstance => content;
        /// <summary>Toggle scene dressing without replacing, moving, or hiding the content.</summary>
        public bool EffectEnabled
        {
            get => effectEnabled;
            set { effectEnabled = value; Refresh(); }
        }

        /// <summary>Use an existing display and optional ordinary model. Null uses an 8 cm default sphere.</summary>
        public void Configure(HeadTrackedDisplay targetDisplay, GameObject prefab = null)
        {
            display = targetDisplay;
            contentPrefab = prefab;
            Refresh();
        }

        public void SetContent(GameObject prefab)
        {
            contentPrefab = prefab;
            Refresh();
        }

        /// <summary>Apply settings and physical dimensions without recreating geometry each frame.</summary>
        public void Refresh()
        {
            if (display == null) return;
            if (settings == null) settings = new BillboardIllusionSettings();
            settings.Validate();
            float width = display.Calibration.screenWidth, height = display.Calibration.screenHeight;
            if (!(width > 0f && height > 0f) || float.IsInfinity(width) || float.IsInfinity(height)) return;
            if (root == null) Build();
            if (root.parent != display.ScreenPlane)
            {
                root.SetParent(display.ScreenPlane, false);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
            }
            if (root.gameObject.activeSelf != isActiveAndEnabled) root.gameObject.SetActive(isActiveAndEnabled);

            float w = width * settings.openingWidthFraction, h = height * settings.openingHeightFraction;
            float rim = Mathf.Min(settings.frameWidth, Mathf.Min(width - w, height - h) * .45f);
            float t = settings.wallThickness, d = settings.boxDepth;
            var opening = new Vector4(width, height, w, h);
            var construction = new Vector4(rim, t, d, 0);
            if (geometryDirty || !opening.Equals(lastOpening) || !construction.Equals(lastConstruction) ||
                lastShowFrame != settings.showFrame || lastShowReferences != settings.showDepthReferences)
            {
                Place(frame[0], new Vector3(-(w + rim) * .5f, 0, 0), new Vector3(rim, h + 2 * rim, t));
                Place(frame[1], new Vector3((w + rim) * .5f, 0, 0), new Vector3(rim, h + 2 * rim, t));
                Place(frame[2], new Vector3(0, -(h + rim) * .5f, 0), new Vector3(w, rim, t));
                Place(frame[3], new Vector3(0, (h + rim) * .5f, 0), new Vector3(w, rim, t));
                // Front faces lie on Z=0; normal depth testing allows protruding content to cover them.
                float side = (width - w - 2 * rim) * .5f, band = (height - h - 2 * rim) * .5f;
                Place(surround[0], new Vector3(-(width - side) * .5f, 0, t * .5f), new Vector3(side, height, t));
                Place(surround[1], new Vector3((width - side) * .5f, 0, t * .5f), new Vector3(side, height, t));
                Place(surround[2], new Vector3(0, -(height - band) * .5f, t * .5f), new Vector3(w + 2 * rim, band, t));
                Place(surround[3], new Vector3(0, (height - band) * .5f, t * .5f), new Vector3(w + 2 * rim, band, t));
                Place(walls[0], new Vector3(0, 0, d + t * .5f), new Vector3(w + 2 * t, h + 2 * t, t));
                // The rim's inner faces and chamber faces share X/Y planes. Keep their Z ranges
                // disjoint: overlapping coplanar surfaces shimmer even with anti-aliasing.
                float start = t * .5f + .0005f;
                float wallCentre = (start + d) * .5f, wallLength = d - start;
                Place(walls[1], new Vector3(0, -(h + t) * .5f, wallCentre), new Vector3(w, t, wallLength));
                Place(walls[2], new Vector3(0, (h + t) * .5f, wallCentre), new Vector3(w, t, wallLength));
                Place(walls[3], new Vector3(-(w + t) * .5f, 0, wallCentre), new Vector3(t, h + 2 * t, wallLength));
                Place(walls[4], new Vector3((w + t) * .5f, 0, wallCentre), new Vector3(t, h + 2 * t, wallLength));
                for (int i = 0; i < 2; i++)
                {
                    float size = Mathf.Min(w, h) * (i == 0 ? .22f : .14f);
                    Place(references[i], new Vector3(w * (i == 0 ? -.30f : -.05f), -h * .5f + size * .5f,
                        d * (i == 0 ? .64f : .82f)), Vector3.one * size);
                    references[i].gameObject.SetActive(settings.showDepthReferences);
                }
                frameRoot.gameObject.SetActive(settings.showFrame);
                lastOpening = opening; lastConstruction = construction;
                lastShowFrame = settings.showFrame; lastShowReferences = settings.showDepthReferences;
                geometryDirty = false;
            }
            if (materialsDirty || lastFrameMaterial != frameMaterial || lastSurroundMaterial != surroundMaterial ||
                lastChamberMaterial != chamberMaterial || lastContentMaterial != defaultContentMaterial)
            {
                ApplyMaterials();
                lastFrameMaterial = frameMaterial; lastSurroundMaterial = surroundMaterial;
                lastChamberMaterial = chamberMaterial; lastContentMaterial = defaultContentMaterial;
                materialsDirty = false;
            }
            if (content == null || builtPrefab != contentPrefab) BuildContent();
            if (content != null) PositionContent(w, h);
            frameRoot.gameObject.SetActive(effectEnabled && settings.showFrame);
            chamberRoot.gameObject.SetActive(effectEnabled);
        }

        private void LateUpdate() => Refresh();
        private void OnValidate() { if (settings != null) settings.Validate(); }
        private void OnEnable() { if (root != null) root.gameObject.SetActive(true); }
        private void OnDisable() { if (root != null) root.gameObject.SetActive(false); }

        private void Build()
        {
            geometryDirty = materialsDirty = true;
            root = new GameObject("Billboard illusion / physical screen space").transform;
            frameRoot = Child("Screen-plane frame and matte surround", root);
            chamberRoot = Child("Recessed chamber", root);
            contentRoot = Child("Ordinary 3D content", root);
            string[] edgeNames = { "left", "right", "bottom", "top" };
            for (int i = 0; i < 4; i++)
            {
                frame[i] = Box("Frame " + edgeNames[i], frameRoot);
                surround[i] = Box("Surround " + edgeNames[i], frameRoot);
                surround[i].GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            string[] wallNames = { "back", "floor", "ceiling", "left", "right" };
            for (int i = 0; i < 5; i++)
            {
                walls[i] = Box("Chamber " + wallNames[i], chamberRoot);
                // Closed enclosure surfaces receive target/frame shadows; avoid self-shadow acne at seams.
                walls[i].GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            for (int i = 0; i < 2; i++) references[i] = Box("Depth reference " + (i + 1), chamberRoot);
            animationStarted = Time.unscaledTime;
        }

        private void ApplyMaterials()
        {
            if (frameMaterial == null && fallbackFrame == null) fallbackFrame = MakeMaterial(new Color(.12f, .18f, .24f), .65f, .5f);
            if (surroundMaterial == null && fallbackSurround == null) fallbackSurround = MakeMaterial(new Color(.003f, .004f, .006f), 0f, .1f);
            if (chamberMaterial == null && fallbackChamber == null) fallbackChamber = MakeMaterial(new Color(.48f, .55f, .62f), 0f, .3f);
            for (int i = 0; i < 4; i++)
            {
                frame[i].GetComponent<Renderer>().sharedMaterial = frameMaterial != null ? frameMaterial : fallbackFrame;
                surround[i].GetComponent<Renderer>().sharedMaterial = surroundMaterial != null ? surroundMaterial : fallbackSurround;
            }
            foreach (var wall in walls) wall.GetComponent<Renderer>().sharedMaterial = chamberMaterial != null ? chamberMaterial : fallbackChamber;
            foreach (var marker in references) marker.GetComponent<Renderer>().sharedMaterial = chamberMaterial != null ? chamberMaterial : fallbackChamber;
            if (content != null && builtPrefab == null)
                content.GetComponent<Renderer>().sharedMaterial = DefaultContentMaterial();
        }

        private Material DefaultContentMaterial()
        {
            if (defaultContentMaterial != null) return defaultContentMaterial;
            if (fallbackContent == null) fallbackContent = MakeMaterial(new Color(.75f, .38f, .12f), .65f, .65f);
            return fallbackContent;
        }

        private void BuildContent()
        {
            if (content != null) { content.SetActive(false); Destroy(content); }
            content = contentPrefab != null ? Instantiate(contentPrefab, contentRoot, false) :
                GameObject.CreatePrimitive(PrimitiveType.Sphere);
            content.transform.SetParent(contentRoot, false);
            content.name = "Billboard content / " + (contentPrefab != null ? contentPrefab.name : "default sphere");
            content.SetActive(true);
            if (contentPrefab == null) content.GetComponent<Renderer>().sharedMaterial = DefaultContentMaterial();
            foreach (var collider in content.GetComponentsInChildren<Collider>()) Destroy(collider);
            builtPrefab = contentPrefab;
            originalContentScale = content.transform.localScale;
            originalContentHeight = LocalBounds(content.transform).size.y;
            appliedHeight = -1f;
        }

        private void PositionContent(float openingWidth, float openingHeight)
        {
            if (appliedHeight != settings.contentHeight)
            {
                content.transform.localPosition = Vector3.zero;
                content.transform.localScale = originalContentScale * settings.contentHeight / Mathf.Max(.0001f, originalContentHeight);
                contentCentre = LocalBounds(content.transform).center;
                appliedHeight = settings.contentHeight;
            }
            if (settings.animate && !wasAnimating) animationStarted = Time.unscaledTime;
            wasAnimating = settings.animate;
            float z = settings.staticDepth;
            if (settings.animate)
            {
                float phase = (Time.unscaledTime - animationStarted) / settings.animationPeriodSeconds;
                float travel = .5f - .5f * Mathf.Cos(phase * Mathf.PI * 2f);
                z = Mathf.Lerp(settings.animationInsideDepth, -settings.animationProtrusion, travel);
            }
            Vector3 centre = new Vector3(openingWidth * settings.horizontalOffsetFraction,
                -openingHeight * .5f + settings.contentHeight * .5f, z);
            Vector3 position = centre - contentCentre;
            if (!content.transform.localPosition.Equals(position)) content.transform.localPosition = position;
        }

        // Mesh-local corners avoid world AABB inflation when the physical screen is rotated.
        private Bounds LocalBounds(Transform item)
        {
            Bounds result = default;
            bool first = true;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = renderer.localBounds;
                Matrix4x4 matrix = contentRoot.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1,
                        (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    p = matrix.MultiplyPoint3x4(p);
                    if (first) { result = new Bounds(p, Vector3.zero); first = false; } else result.Encapsulate(p);
                }
            }
            return result;
        }

        private Material MakeMaterial(Color color, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) { Debug.LogError("Assign billboard materials compatible with your render pipeline.", this); return null; }
            var material = new Material(shader) { name = "Billboard generated surface", color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            ownedMaterials.Add(material);
            return material;
        }

        private static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform Box(string name, Transform parent)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            Destroy(box.GetComponent<Collider>());
            return box.transform;
        }

        private static void Place(Transform target, Vector3 centre, Vector3 dimensions)
        { target.localPosition = centre; target.localScale = dimensions; }

        private void OnDestroy()
        {
            if (root != null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
        }
    }
}
