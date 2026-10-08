using System;
using System.Collections.Generic;
using System.IO;
using HeadTracked.Display;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeadTracked.Demo
{
    /// <summary>Creates the scene at runtime so the FBX models can be replaced without touching tracking code.</summary>
    public sealed class DemoBootstrap : MonoBehaviour
    {
        private HeadTrackedDisplay display;
        private HeadObservationSource source;
        private Transform screenPlane;
        private string notice = "";
        private string widthCm = "53";
        private string heightCm = "30";
        private string eyeDistanceCm = "60";
        private string webcamXCm = "0";
        private string webcamYCm = "17";
        private string webcamZCm = "-2.5";
        private string webcamPitch = "0";
        private string ipdMm = "63";
        private bool showSettings = true;
        private bool showModelSettings;
        private bool showScreenRuler;
        private Vector2 settingsScroll;
        private Vector3 measurementOrigin;
        private bool hasMeasurementOrigin;
        private readonly List<SceneModel> sceneModels = new List<SceneModel>();
        private Transform roomFloor, backWall;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDemo()
        {
            if (FindFirstObjectByType<DemoBootstrap>() == null)
                new GameObject("Head Tracked Demo").AddComponent<DemoBootstrap>();
        }

        private void Start()
        {
            if (!Application.isEditor)
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Application.targetFrameRate = 60;
            screenPlane = new GameObject("Physical screen centre; +Z into scene").transform;
            MakeEnvironment();
            var cameraObject = new GameObject("Head-tracked render camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.11f, 0.14f, 0.18f);
            camera.nearClipPlane = 0.025f;
            camera.farClipPlane = 20f;
            camera.allowHDR = true;
            cameraObject.tag = "MainCamera";
            source = cameraObject.AddComponent<PythonBridgeSource>();
            display = cameraObject.AddComponent<HeadTrackedDisplay>();
            display.Configure(screenPlane, source);
            LoadSettings();
            CopyFieldsFromCalibration();
            LoadIntrinsics();
            if (!Application.isEditor) LoadModelLayout();
            notice = "Python bridge selected. Start python_tracker/tracker.py or switch to Unity MediaPipe.";
        }

        private void MakeEnvironment()
        {
            roomFloor = MakeBox("Floor", new Vector3(0f, -0.19f, 0.43f), new Vector3(1.55f, 0.02f, 1.42f),
                new Color(0.28f, 0.30f, 0.32f));
            backWall = MakeBox("Back wall", new Vector3(0f, 0.25f, 1.12f), new Vector3(1.55f, 0.9f, 0.02f),
                new Color(0.18f, 0.22f, 0.27f));
            var rearPlinth = MakeBox("Rear plinth", new Vector3(0.20f, -0.13f, 0.16f), new Vector3(0.20f, 0.10f, 0.20f),
                new Color(0.55f, 0.48f, 0.38f));
            var frontPlinth = MakeBox("Front plinth", new Vector3(-0.16f, -0.13f, -0.025f), new Vector3(0.14f, 0.10f, 0.14f),
                new Color(0.49f, 0.44f, 0.37f));
            var protrudingPlinth = MakeBox("Protruding plinth", new Vector3(0.04f, -0.13f, -0.06f), new Vector3(0.10f, 0.10f, 0.10f),
                new Color(0.45f, 0.37f, 0.34f));

            AddSceneModel("Bear", "Models/bear", "Bear, in front of screen", -0.16f, .15f, -.025f,
                frontPlinth, -.08f, new Color(0.88f, 0.61f, 0.32f));
            AddSceneModel("Chair (scale model)", "Models/chairDesk", "Chair, behind screen", -.01f, .25f, .10f,
                null, -.18f, new Color(0.44f, 0.68f, 0.81f));
            AddSceneModel("Green plant", "Models/pottedPlant", "Plant, behind chair", .20f, .22f, .16f,
                rearPlinth, -.08f, new Color(0.55f, 0.76f, 0.53f));
            AddSceneModel("Red plant", "Models/pottedPlant", "Plant, in front of screen", .04f, .10f, -.06f,
                protrudingPlinth, -.08f, new Color(0.90f, 0.46f, 0.33f));
            AddSceneModel("Distant chair", "Models/chairDesk", "Chair, distant", -.50f, 1.0f, 3.0f,
                null, -.18f, new Color(0.68f, 0.50f, 0.91f));
            AddSceneModel("Distant plant", "Models/pottedPlant", "Plant, distant", .90f, 1.4f, 6.0f,
                null, -.18f, new Color(0.40f, 0.72f, 0.90f));
            UpdateRoomGeometry();

            var sun = new GameObject("Soft key light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.85f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.40f, 0.43f, 0.48f);
        }

        private static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.08f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.45f);
            return material;
        }

        private static Transform MakeBox(string name, Vector3 position, Vector3 scale, Color color)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().material = MakeMaterial(color);
            return item.transform;
        }

        private static Transform AddModel(string resource, string name, Vector3 centre, float height, Color color)
        {
            var asset = Resources.Load<GameObject>(resource);
            if (asset == null)
            {
                Debug.LogError("Demo model missing: " + resource);
                return null;
            }
            var item = Instantiate(asset);
            item.name = name;
            var renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float factor = height / Mathf.Max(0.001f, bounds.size.y);
            item.transform.localScale *= factor;
            bounds = renderers[0].bounds;
            var material = MakeMaterial(color);
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            item.transform.position += centre - bounds.center;
            return item.transform;
        }

        private void AddSceneModel(string label, string resource, string name, float x, float height,
            float depth, Transform plinth, float supportY, Color color)
        {
            var model = AddModel(resource, name, new Vector3(x, supportY + height * .5f, depth), height, color);
            if (model == null) return;
            sceneModels.Add(new SceneModel
            {
                label = label, root = model, plinth = plinth, x = x, supportY = supportY,
                height = height, depth = depth, heightCm = (height * 100f).ToString("F1"),
                depthCm = (depth * 100f).ToString("F1")
            });
        }

        private static Bounds ModelBounds(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        public void UseDepthPreset(bool deep)
        {
            if (sceneModels.Count < 4) return;
            float[] depths = deep ? new[] { -.04f, .29f, .42f, -.18f } : new[] { -.025f, .10f, .16f, -.06f };
            for (int i = 0; i < depths.Length; i++) sceneModels[i].depthCm = (depths[i] * 100f).ToString("F1");
            ApplyModelLayout();
        }

        private bool ApplyModelLayout()
        {
            var values = new ModelSizeAndDepth[sceneModels.Count];
            for (int i = 0; i < sceneModels.Count; i++)
            {
                var model = sceneModels[i];
                if (!float.TryParse(model.heightCm, out float height) || !float.TryParse(model.depthCm, out float depth) ||
                    !IsFinite(height) || !IsFinite(depth) || height < 2f || height > 200f || depth < -20f || depth > 1500f)
                {
                    notice = "Use model height 2-200 cm and centre depth -20 to +1500 cm.";
                    return false;
                }
                values[i] = new ModelSizeAndDepth { height = height * .01f, depth = depth * .01f };
            }
            for (int i = 0; i < sceneModels.Count; i++)
            {
                var model = sceneModels[i];
                model.height = values[i].height;
                model.depth = values[i].depth;
                model.root.localScale *= model.height / Mathf.Max(.001f, ModelBounds(model.root).size.y);
                Vector3 centre = new Vector3(model.x, model.supportY + model.height * .5f, model.depth);
                model.root.position += centre - ModelBounds(model.root).center;
                if (model.plinth != null)
                {
                    var position = model.plinth.position;
                    position.z = model.depth;
                    model.plinth.position = position;
                }
            }
            UpdateRoomGeometry();
            notice = "Physical model size and centre depth applied. Models stay fixed in the scene.";
            return true;
        }

        private void UpdateRoomGeometry()
        {
            float nearest = 0f, farthest = 0f;
            foreach (var model in sceneModels)
            {
                Bounds bounds = ModelBounds(model.root);
                nearest = Mathf.Min(nearest, bounds.min.z);
                farthest = Mathf.Max(farthest, bounds.max.z);
                if (model.plinth != null)
                {
                    nearest = Mathf.Min(nearest, model.depth - model.plinth.localScale.z * .5f);
                    farthest = Mathf.Max(farthest, model.depth + model.plinth.localScale.z * .5f);
                }
            }
            float front = nearest - .10f, back = farthest + .12f;
            float roomWidth = Mathf.Max(1.55f, back * 2f);
            float wallHeight = Mathf.Max(.9f, back * .7f);
            var wallPosition = backWall.position;
            wallPosition.z = back + .01f;
            wallPosition.y = -.18f + wallHeight * .5f;
            backWall.position = wallPosition;
            backWall.localScale = new Vector3(roomWidth, wallHeight, .02f);
            var floorPosition = roomFloor.position;
            floorPosition.z = (front + back) * .5f;
            roomFloor.position = floorPosition;
            var floorScale = roomFloor.localScale;
            floorScale.x = roomWidth;
            floorScale.z = back - front;
            roomFloor.localScale = floorScale;
        }

        private void LoadModelLayout()
        {
            string path = Path.Combine(Application.persistentDataPath, "demo_model_layout.json");
            if (!File.Exists(path)) return;
            try
            {
                var saved = JsonUtility.FromJson<ModelLayoutFile>(File.ReadAllText(path));
                if (saved?.models == null || saved.models.Length > sceneModels.Count) return;
                // Keep an older four-model layout while adding the new distant models at their defaults.
                for (int i = 0; i < saved.models.Length; i++)
                {
                    sceneModels[i].heightCm = (saved.models[i].height * 100f).ToString("F1");
                    sceneModels[i].depthCm = (saved.models[i].depth * 100f).ToString("F1");
                }
                ApplyModelLayout();
            }
            catch (Exception ex) { notice = "Could not load model layout: " + ex.Message; }
        }

        private void SelectProvider(bool native)
        {
            if (source != null)
            {
                source.enabled = false;
                Destroy(source);
            }
            source = native ? AddNativeSource() : display.gameObject.AddComponent<PythonBridgeSource>();
            if (source == null)
            {
                source = display.gameObject.AddComponent<PythonBridgeSource>();
                notice = "Install MediaPipe Unity Plugin v0.16.3+ to enable Unity-native tracking.";
            }
            display.Configure(screenPlane, source);
        }

        private HeadObservationSource AddNativeSource()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("HeadTracked.Display.MediaPipeUnitySource");
                if (type != null) return (HeadObservationSource)display.gameObject.AddComponent(type);
            }
            return null;
        }

        private void LoadIntrinsics()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "camera_intrinsics.json");
            if (!File.Exists(path)) return;
            try
            {
                var data = JsonUtility.FromJson<IntrinsicsFile>(File.ReadAllText(path));
                var c = display.Calibration;
                c.focalXPixels = data.focalXPixels;
                c.focalYPixels = data.focalYPixels;
                c.principalXPixels = data.principalXPixels;
                c.principalYPixels = data.principalYPixels;
                c.intrinsicsImageWidth = data.imageWidth;
                c.intrinsicsImageHeight = data.imageHeight;
                c.k1 = data.k1; c.k2 = data.k2; c.k3 = data.k3;
                c.p1 = data.p1; c.p2 = data.p2;
                notice = $"Camera intrinsics loaded (RMS {data.rmsReprojectionError:F2} px).";
            }
            catch (Exception ex) { notice = "Could not load camera intrinsics: " + ex.Message; }
        }

        private void LoadSettings()
        {
            var path = Path.Combine(Application.persistentDataPath, "display_calibration.json");
            if (File.Exists(path))
            {
                try { JsonUtility.FromJsonOverwrite(File.ReadAllText(path), display.Calibration); }
                catch (Exception ex) { notice = "Could not read saved settings: " + ex.Message; }
            }
        }

        private void SaveSettings()
        {
            try
            {
                var path = Path.Combine(Application.persistentDataPath, "display_calibration.json");
                File.WriteAllText(path, JsonUtility.ToJson(display.Calibration, true));
                var layout = new ModelLayoutFile { models = new ModelSizeAndDepth[sceneModels.Count] };
                for (int i = 0; i < sceneModels.Count; i++)
                    layout.models[i] = new ModelSizeAndDepth { height = sceneModels[i].height, depth = sceneModels[i].depth };
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "demo_model_layout.json"),
                    JsonUtility.ToJson(layout, true));
                notice = "Saved settings to " + path;
            }
            catch (Exception ex) { notice = "Could not save settings: " + ex.Message; }
        }

        private void CopyFieldsFromCalibration()
        {
            var c = display.Calibration;
            widthCm = (c.screenWidth * 100f).ToString("F1");
            heightCm = (c.screenHeight * 100f).ToString("F1");
            eyeDistanceCm = (c.referenceEyeDistanceFromScreen * 100f).ToString("F1");
            webcamXCm = (c.webcamPosition.x * 100f).ToString("F1");
            webcamYCm = (c.webcamPosition.y * 100f).ToString("F1");
            webcamZCm = (c.webcamPosition.z * 100f).ToString("F1");
            webcamPitch = c.webcamEulerDegrees.x.ToString("F1");
            ipdMm = (c.measuredEyeSeparationMeters * 1000f).ToString("F1");
        }

        private static float Input(string label, string current, out string edited, float scale, float fallback)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(170));
            edited = GUILayout.TextField(current, GUILayout.Width(75));
            GUILayout.EndHorizontal();
            return float.TryParse(edited, out float value) && IsFinite(value) ? value * scale : fallback;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void DrawScreenRuler()
        {
            if (!showScreenRuler) return;
            float pixels = Screen.width * .10f / display.Calibration.screenWidth;
            float left = (Screen.width - pixels) * .5f;
            float y = Screen.height - 42f;
            GUI.DrawTexture(new Rect(left, y, pixels, 2f), Texture2D.whiteTexture);
            for (int i = 0; i <= 10; i++)
                GUI.DrawTexture(new Rect(left + pixels * i / 10f, y - 5f, 2f, 12f), Texture2D.whiteTexture);
            GUI.Box(new Rect(left, y - 30f, pixels, 24f), "10 cm on the physical screen");
        }

        private void DrawModelSettings()
        {
            showModelSettings = GUILayout.Toggle(showModelSettings, "Model size and depth / compare physical layouts");
            if (!showModelSettings) return;
            GUILayout.Label("Height is the real object height. Depth: + behind, - in front.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Shallow desktop")) UseDepthPreset(false);
            if (GUILayout.Button("Depth stress test")) UseDepthPreset(true);
            GUILayout.EndHorizontal();
            foreach (var model in sceneModels)
            {
                GUILayout.Label(model.label);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Height cm", GUILayout.Width(75));
                model.heightCm = GUILayout.TextField(model.heightCm, GUILayout.Width(65));
                GUILayout.Label("Depth cm", GUILayout.Width(75));
                model.depthCm = GUILayout.TextField(model.depthCm, GUILayout.Width(65));
                GUILayout.EndHorizontal();
                float d = -display.EyePositionMeters.z;
                if (d + model.depth > .025f)
                {
                    float shift = .1f * model.depth / (d + model.depth);
                    float imageHeight = d * model.height / (d + model.depth);
                    GUILayout.Label($"10 cm head move: {shift * 100f:+0.0;-0.0;0.0} cm on screen (~{Mathf.Abs(shift) / imageHeight * 100f:F0}% of height)");
                }
                if (ModelBounds(model.root).min.z <= display.EyePositionMeters.z + .025f)
                    GUILayout.Label("This model reaches the camera near plane; move farther from the monitor.");
            }
            if (GUILayout.Button("Apply model size and depth")) ApplyModelLayout();
        }

        private void OnGUI()
        {
            if (display == null) return;
            if (GUI.Button(new Rect(12, 12, 170, 28), showSettings ? "Hide settings" : "Show settings"))
                showSettings = !showSettings;
            DrawScreenRuler();
            if (!showSettings) return;
            GUILayout.BeginArea(new Rect(12, 48, 440, Mathf.Min(Screen.height - 60, 690)), GUI.skin.box);
            settingsScroll = GUILayout.BeginScrollView(settingsScroll);
            GUILayout.Label("HEAD-TRACKED DISPLAY / physical monitor setup");
            GUILayout.Label($"Tracking: {(display.IsTracking ? "FACE FOUND" : "NO FACE")} | {display.SourceStatus}");
            GUILayout.Label($"Eye (m): {display.EyePositionMeters.ToString("F3")}  Confidence: {display.Confidence:F2}");
            GUILayout.Label($"Display: {Screen.width} x {Screen.height} px");
            var c = display.Calibration;
            float physicalAspect = c.screenWidth / c.screenHeight;
            float imageAspect = (float)Screen.width / Screen.height;
            if (Mathf.Abs(physicalAspect - imageAspect) > 0.03f)
                GUILayout.Label("WARNING: physical screen aspect differs from render aspect.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Python bridge")) SelectProvider(false);
            if (GUILayout.Button("Unity MediaPipe")) SelectProvider(true);
            GUILayout.EndHorizontal();
            c.screenWidth = Mathf.Max(0.1f, Input("Screen width (cm)", widthCm, out widthCm, 0.01f, c.screenWidth));
            c.screenHeight = Mathf.Max(0.1f, Input("Screen height (cm)", heightCm, out heightCm, 0.01f, c.screenHeight));
            c.referenceEyeDistanceFromScreen = Mathf.Max(0.2f,
                Input("Reference eye distance (cm)", eyeDistanceCm, out eyeDistanceCm, 0.01f, c.referenceEyeDistanceFromScreen));
            var webcam = c.webcamPosition;
            webcam.x = Input("Webcam X from centre (cm)", webcamXCm, out webcamXCm, 0.01f, webcam.x);
            webcam.y = Input("Webcam Y from centre (cm)", webcamYCm, out webcamYCm, 0.01f, webcam.y);
            webcam.z = Input("Webcam Z from screen (cm)", webcamZCm, out webcamZCm, 0.01f, webcam.z);
            c.webcamPosition = webcam;
            var webcamEuler = c.webcamEulerDegrees;
            webcamEuler.x = Input("Webcam pitch (degrees)", webcamPitch, out webcamPitch, 1f, webcamEuler.x);
            c.webcamEulerDegrees = webcamEuler;
            c.mirrorImageX = GUILayout.Toggle(c.mirrorImageX, "Mirror webcam X");
            c.usePreciseIntrinsics = GUILayout.Toggle(c.usePreciseIntrinsics, "Use precise camera calibration");
            c.measuredEyeSeparationMeters = Mathf.Clamp(
                Input("Eye separation / IPD (mm)", ipdMm, out ipdMm, 0.001f, c.measuredEyeSeparationMeters),
                0.045f, 0.085f);
            if (!c.usePreciseIntrinsics)
            {
                c.useEyeSeparationForBasicScale = GUILayout.Toggle(c.useEyeSeparationForBasicScale,
                    "Use eye spacing for basic movement scale");
                if (c.referenceEyeSpanPixels <= 1f)
                    GUILayout.Label("Capture a reference to replace the assumed webcam FOV.");
                if (!c.useEyeSeparationForBasicScale)
                {
                    GUILayout.Label($"Assumed webcam horizontal FOV: {c.horizontalFovDegrees:F0} degrees");
                    c.horizontalFovDegrees = GUILayout.HorizontalSlider(c.horizontalFovDegrees, 30f, 110f);
                }
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture reference distance"))
                notice = display.CaptureReference() ? "Reference captured." : "Face must be tracked first.";
            if (GUILayout.Button("Reload camera intrinsics")) LoadIntrinsics();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Save calibration")) SaveSettings();
            showScreenRuler = GUILayout.Toggle(showScreenRuler, "Show 10 cm screen ruler (check with a physical ruler)");
            display.FreezeViewingDistance = GUILayout.Toggle(display.FreezeViewingDistance,
                "Freeze viewing distance (diagnostic; no forward/back tracking)");
            GUILayout.Label($"Tracking smoothing time constant: {display.TrackingSmoothingSeconds * 1000f:F0} ms");
            display.TrackingSmoothingSeconds = GUILayout.HorizontalSlider(display.TrackingSmoothingSeconds, .005f, .10f);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = display.IsTracking;
            if (GUILayout.Button("Set movement measurement origin"))
            {
                measurementOrigin = display.EstimatedEyePositionMeters;
                hasMeasurementOrigin = true;
            }
            GUI.enabled = previousEnabled;
            if (hasMeasurementOrigin && display.IsTracking)
            {
                Vector3 delta = (display.EstimatedEyePositionMeters - measurementOrigin) * 100f;
                GUILayout.Label($"Measured head travel (cm): X {delta.x:F1}, Y {delta.y:F1}, Z {delta.z:F1}");
            }
            DrawModelSettings();
            if (source != null && source.GetType().Name == "MediaPipeUnitySource")
            {
                GUILayout.Label("Unity webcam (select; tracker restarts):");
                foreach (var device in WebCamTexture.devices)
                    if (GUILayout.Button(device.name))
                        source.GetType().GetMethod("SelectWebcam")?.Invoke(source, new object[] { device.name });
            }
            GUILayout.Label(notice);
            if (sceneModels.Count >= 4)
                GUILayout.Label($"The red plant centre is {Mathf.Abs(sceneModels[3].depth) * 100f:F1} cm {(sceneModels[3].depth < 0f ? "in front of" : "behind")} the screen.");
            GUILayout.Label("Models need no tracking scripts; the monitor edge still clips them.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private sealed class SceneModel
        {
            public string label, heightCm, depthCm;
            public Transform root, plinth;
            public float x, supportY, height, depth;
        }

        [Serializable]
        private sealed class ModelSizeAndDepth
        {
            public float height, depth;
        }

        [Serializable]
        private sealed class ModelLayoutFile
        {
            public ModelSizeAndDepth[] models;
        }

        [Serializable]
        private sealed class IntrinsicsFile
        {
            public int imageWidth, imageHeight;
            public float focalXPixels, focalYPixels, principalXPixels, principalYPixels;
            public float k1, k2, k3, p1, p2, rmsReprojectionError;
        }
    }
}
