using System;
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
            notice = "Python bridge selected. Start python_tracker/tracker.py or switch to Unity MediaPipe.";
        }

        private void MakeEnvironment()
        {
            MakeBox("Floor", new Vector3(0f, -0.19f, 0.43f), new Vector3(1.55f, 0.02f, 1.42f),
                new Color(0.28f, 0.30f, 0.32f));
            MakeBox("Back wall", new Vector3(0f, 0.25f, 1.12f), new Vector3(1.55f, 0.9f, 0.02f),
                new Color(0.18f, 0.22f, 0.27f));
            MakeBox("Rear plinth", new Vector3(0.20f, -0.13f, 0.42f), new Vector3(0.20f, 0.10f, 0.20f),
                new Color(0.55f, 0.48f, 0.38f));
            MakeBox("Front plinth", new Vector3(-0.16f, -0.13f, -0.035f), new Vector3(0.14f, 0.10f, 0.14f),
                new Color(0.49f, 0.44f, 0.37f));
            MakeBox("Protruding plinth", new Vector3(0.04f, -0.13f, -0.18f), new Vector3(0.10f, 0.10f, 0.10f),
                new Color(0.45f, 0.37f, 0.34f));

            AddModel("Models/bear", "Bear, in front of screen", new Vector3(-0.16f, -0.015f, -0.04f),
                0.15f, new Color(0.88f, 0.61f, 0.32f));
            AddModel("Models/chairDesk", "Chair, behind screen", new Vector3(-0.01f, -0.055f, 0.29f),
                0.25f, new Color(0.44f, 0.68f, 0.81f));
            AddModel("Models/pottedPlant", "Plant, behind chair", new Vector3(0.20f, 0.03f, 0.42f),
                0.22f, new Color(0.55f, 0.76f, 0.53f));
            AddModel("Models/pottedPlant", "Plant, 18 cm in front of screen", new Vector3(0.04f, -0.03f, -0.18f),
                0.10f, new Color(0.90f, 0.46f, 0.33f));

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

        private static void MakeBox(string name, Vector3 position, Vector3 scale, Color color)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().material = MakeMaterial(color);
        }

        private static void AddModel(string resource, string name, Vector3 centre, float height, Color color)
        {
            var asset = Resources.Load<GameObject>(resource);
            if (asset == null)
            {
                Debug.LogError("Demo model missing: " + resource);
                return;
            }
            var item = Instantiate(asset);
            item.name = name;
            var renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
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
            return float.TryParse(edited, out float value) ? value * scale : fallback;
        }

        private void OnGUI()
        {
            if (display == null) return;
            if (GUI.Button(new Rect(12, 12, 170, 28), showSettings ? "Hide settings" : "Show settings"))
                showSettings = !showSettings;
            if (!showSettings) return;
            GUILayout.BeginArea(new Rect(12, 48, 440, Mathf.Min(Screen.height - 60, 690)), GUI.skin.box);
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
            if (c.usePreciseIntrinsics)
                c.measuredEyeSeparationMeters = Mathf.Clamp(
                    Input("Measured eye distance (mm)", ipdMm, out ipdMm, 0.001f, c.measuredEyeSeparationMeters),
                    0.045f, 0.085f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture reference distance"))
                notice = display.CaptureReference() ? "Reference captured." : "Face must be tracked first.";
            if (GUILayout.Button("Reload camera intrinsics")) LoadIntrinsics();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Save calibration")) SaveSettings();
            if (source != null && source.GetType().Name == "MediaPipeUnitySource")
            {
                GUILayout.Label("Unity webcam (select; tracker restarts):");
                foreach (var device in WebCamTexture.devices)
                    if (GUILayout.Button(device.name))
                        source.GetType().GetMethod("SelectWebcam")?.Invoke(source, new object[] { device.name });
            }
            GUILayout.Label(notice);
            GUILayout.Label("Move left/right and forward/back. The red plant is 18 cm in front of the screen.");
            GUILayout.Label("Models need no tracking scripts; the monitor edge still clips them.");
            GUILayout.EndArea();
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
