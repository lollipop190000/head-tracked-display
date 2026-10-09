using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
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
        private bool showDisplaySize, screenSizeDeriveHeight;
        private string screenSizeNotice = "";
        private Vector2 screenSizeScroll;
        private bool showModelSettings;
        private bool showScreenRuler;
        private bool isolatePlant;
        private bool showTrackingTest;
        private bool depthComparisonActive;
        private Vector2 trackingScroll;
        private string distanceNearCm = "40", distanceFarCm = "80";
        private bool capturingDistance, distanceCaptureNear, hasNearDistance, hasFarDistance;
        private float distanceCaptureStarted, measuredCaptureDistance;
        private float rawNearDistance, rawFarDistance, measuredNearDistance, measuredFarDistance;
        private string captureDistanceContext, nearDistanceContext, farDistanceContext;
        private readonly List<float> distanceSamples = new List<float>();
        private string distanceNotice = "Enter physically measured eye-to-screen distances.";
        private int motionTest;
        private double lastDiagnosticFrame = double.NegativeInfinity;
        private Vector3 testEyeOrigin;
        private Vector3 testHeadOrigin;
        private bool hasTestOrigin;
        private ScreenGazeCalibration gazeCalibration = new ScreenGazeCalibration();
        private Vector2 gazePoint;
        private bool gazeAvailable;
        private bool showGazePoint = true;
        private bool calibratingGaze, capturingGaze;
        private int gazeTargetIndex, gazeTargetSamples;
        private float gazeCaptureStarted;
        private readonly List<HeadObservation> gazeSamples = new List<HeadObservation>();
        private readonly List<Vector2> gazeTargets = new List<Vector2>();
        private static readonly Vector2[] GazePoints = { new Vector2(.5f, .5f), new Vector2(.2f, .8f),
            new Vector2(.5f, .8f), new Vector2(.8f, .8f), new Vector2(.8f, .5f), new Vector2(.8f, .2f),
            new Vector2(.5f, .2f), new Vector2(.2f, .2f), new Vector2(.2f, .5f) };
        private StringBuilder recording;
        private float recordingEnds;
        private int recordedMotionTest;
        private string diagnosticNotice = "";
        private string currentGazeContext;
        private float nextGazeContextCheck;
        private Vector2 settingsScroll;
        private Vector3 measurementOrigin;
        private bool hasMeasurementOrigin;
        private readonly List<SceneModel> sceneModels = new List<SceneModel>();
        private Transform roomFloor, backWall;
        private readonly ModelMaterials modelMaterials = new ModelMaterials();
        private bool materialStudyActive, enhancedRendering = true, cleanView;
        private GameObject surfaceComparisons;
        private RenderQualitySettings renderQuality;
        private float reportedScreenWidth, reportedScreenHeight;

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
            display.Calibration.deriveScreenHeightFromResolution = true;
            LoadSettings();
            if (!Application.isEditor) LoadHardwareScreenHint();
            new GameObject("HDR lighting and neutral tonemapping").AddComponent<RealismLighting>().Configure(camera);
            renderQuality = Resources.Load<RenderQualitySettings>("Realism/render_quality");
            if (renderQuality != null) renderQuality.Apply(enhancedRendering);
            CopyFieldsFromCalibration();
            LoadIntrinsics();
            if (!Application.isEditor) LoadModelLayout();
            LoadGazeCalibration();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tracking-test") >= 0)
            {
                UseFixationTest(.05f);
                showModelSettings = false;
                showTrackingTest = true;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--depth-test") >= 0)
            {
                UseDepthMotionTest();
                showTrackingTest = true;
                showModelSettings = false;
                motionTest = 3;
            }
            notice = "Python bridge selected. Start python_tracker/tracker.py or switch to Unity MediaPipe.";
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--realism-test") >= 0) UseMaterialStudy();
        }

        private string GazeContext() => Screen.width + "x" + Screen.height + ":" + JsonUtility.ToJson(display.Calibration);

        private void LoadGazeCalibration()
        {
            string path = Path.Combine(Application.persistentDataPath, "screen_gaze_calibration.json");
            if (!File.Exists(path)) return;
            try { gazeCalibration = JsonUtility.FromJson<ScreenGazeCalibration>(File.ReadAllText(path)) ?? new ScreenGazeCalibration(); }
            catch (Exception ex) { diagnosticNotice = "Could not load gaze calibration: " + ex.Message; }
        }

        private void Update()
        {
            if (display == null) return;
            if (UnityEngine.Input.GetKeyDown(KeyCode.F1)) cleanView = !cleanView;
            if (Time.unscaledTime >= nextGazeContextCheck)
            {
                currentGazeContext = GazeContext();
                nextGazeContextCheck = Time.unscaledTime + .5f;
            }
            if (recording != null && Time.unscaledTime >= recordingEnds) FinishRecording();
            if (capturingDistance && Time.unscaledTime - distanceCaptureStarted > 4f)
            { capturingDistance = false; distanceNotice = "Distance capture timed out; face camera and try again."; }
            if (capturingGaze && Time.unscaledTime - gazeCaptureStarted > 4f)
            {
                gazeSamples.RemoveRange(gazeSamples.Count - gazeTargetSamples, gazeTargetSamples);
                gazeTargets.RemoveRange(gazeTargets.Count - gazeTargetSamples, gazeTargetSamples);
                gazeTargetSamples = 0;
                capturingGaze = false;
                diagnosticNotice = "Not enough valid eye samples. Face forward, open both eyes, then retry Space.";
            }
            if (!display.HasObservation) return;
            var o = display.LatestObservation;
            if (o.receivedAtSeconds == lastDiagnosticFrame) return;
            lastDiagnosticFrame = o.receivedAtSeconds;
            CollectDistanceSample(o);
            gazeAvailable = display.IsTracking && gazeCalibration.context == currentGazeContext &&
                gazeCalibration.TryEstimate(o, out gazePoint);
            if (recording != null)
            {
                var eye = display.EstimatedEyePositionMeters;
                recording.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:F3},{1},{2},{3:F5},{4:F5},{5:F5},{6:F2},{7:F2},{8:F2},{9:F4},{10:F4},{11},{12:F2},{13:F2},{14:F2},{15:F2},{16:F2},{17:F5},{18:F5},{19}",
                    Time.realtimeSinceStartupAsDouble, recordedMotionTest, display.IsTracking ? 1 : 0,
                    eye.x, eye.y, eye.z, o.headEulerDegrees.x, o.headEulerDegrees.y, o.headEulerDegrees.z,
                    o.irisOffset.x, o.irisOffset.y, o.gazeValid ? 1 : 0, o.reprojectionErrorPixels,
                    o.inferenceMs, o.poseMs, display.ResultAgeMilliseconds, o.trackerFps,
                    -display.UncalibratedEyePositionMeters.z, -display.EyePositionMeters.z,
                    display.FreezeViewingDistance ? 1 : 0));
            }
            if (!capturingGaze || !display.IsTracking || !o.poseValid || !o.gazeValid) return;
            gazeSamples.Add(o);
            gazeTargets.Add(GazePoints[gazeTargetIndex]);
            gazeTargetSamples++;
            if (gazeTargetSamples < 20) return;
            capturingGaze = false;
            if (++gazeTargetIndex < GazePoints.Length) return;
            calibratingGaze = false;
            if (gazeCalibration.Fit(gazeSamples, gazeTargets))
            {
                gazeCalibration.context = GazeContext();
                try
                {
                    File.WriteAllText(Path.Combine(Application.persistentDataPath, "screen_gaze_calibration.json"),
                        JsonUtility.ToJson(gazeCalibration, true));
                    diagnosticNotice = "Gaze calibration saved. Check NEW positions; training fit is not accuracy.";
                }
                catch (Exception ex) { diagnosticNotice = "Gaze fitted; could not save: " + ex.Message; }
            }
            else diagnosticNotice = "Gaze fit failed. Improve lighting and repeat all nine targets.";
        }

        private void FinishRecording()
        {
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "TrackingTests");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "motion-" + recordedMotionTest + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
                File.WriteAllText(path, recording.ToString());
                diagnosticNotice = "CSV saved: " + path;
            }
            catch (Exception ex) { diagnosticNotice = "Could not save CSV: " + ex.Message; }
            recording = null;
        }

        private void DrawGazeCalibration()
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.grayTexture);
            GUI.Box(new Rect((Screen.width - 740f) * .5f, 12, 740, 72),
                $"GAZE CALIBRATION {gazeTargetIndex + 1}/9: look at the +, then press Space.\n" +
                "Keep your head comfortable and roughly still. Both eyes must be visible.\n" +
                (capturingGaze ? $"Collecting {gazeTargetSamples}/20 new frames..." : "Ready for Space; Esc cancels."));
            Vector2 p = GazePoints[gazeTargetIndex];
            GUI.Box(new Rect(p.x * Screen.width - 12, (1f - p.y) * Screen.height - 12, 24, 24), "+");
            GUI.Box(new Rect(12, Screen.height - 65, Screen.width - 24, 52), diagnosticNotice);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { calibratingGaze = capturingGaze = false; Event.current.Use(); }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space && !capturingGaze)
            {
                capturingGaze = true;
                gazeTargetSamples = 0;
                gazeCaptureStarted = Time.unscaledTime;
                Event.current.Use();
            }
        }

        private void DrawTrackingTest()
        {
            if (!showTrackingTest) return;
            float panelWidth = Mathf.Min(420, Screen.width * .44f);
            GUILayout.BeginArea(new Rect(Screen.width - panelWidth - 12, 12, panelWidth, Mathf.Min(570, Screen.height - 24)), GUI.skin.box);
            trackingScroll = GUILayout.BeginScrollView(trackingScroll);
            var o = display.LatestObservation;
            GUILayout.Label("EYE / HEAD / GAZE TEST");
            GUILayout.Label($"Pose: {(o.poseSupported ? (o.poseValid ? "RIGID FIT" : "FIT INVALID / synchronizing") : "LEGACY SOURCE")}");
            GUILayout.Label($"Head degrees: pitch {o.headEulerDegrees.x:F1}, yaw {o.headEulerDegrees.y:F1}, roll {o.headEulerDegrees.z:F1}");
            GUILayout.Label($"Fit error: {o.reprojectionErrorPixels:F1} px (geometric fit, not true position error)");
            GUILayout.Label($"Tracker {o.trackerFps:F1} Hz | Face {o.inferenceMs:F1} ms | Pose {o.poseMs:F1} ms");
            GUILayout.Label($"Result age {display.ResultAgeMilliseconds:F1} ms (excludes exposure/display latency)");
            GUILayout.Label($"Distance: raw {-display.UncalibratedEyePositionMeters.z * 100f:F1} cm / rendered {-display.EyePositionMeters.z * 100f:F1} cm");
            if (GUILayout.Button("Forward/back depth comparison")) { UseDepthMotionTest(); motionTest = 3; }
            motionTest = GUILayout.Toolbar(motionTest, new[] { "Eyes", "Head", "Sideways", "Depth" });
            GUILayout.Label(motionTest == 0 ? "Keep head still; look left/right with eyes only." :
                motionTest == 1 ? "Keep body still; turn head while watching the plant. Eyes can physically move with rotation." :
                motionTest == 2 ? "Translate sideways by a measured 10 cm; keep head roughly forward." :
                "Move towards/away from the screen. Raw distance must decrease/increase respectively.");
            if (motionTest == 3) DrawDistanceSettings();
            if (depthComparisonActive) DrawDepthPredictions();
            if (GUILayout.Button("Set test baseline") && display.IsTracking)
            { testEyeOrigin = display.EstimatedEyePositionMeters; testHeadOrigin = o.headEulerDegrees; hasTestOrigin = true; }
            if (hasTestOrigin && display.IsTracking)
            {
                Vector3 delta = (display.EstimatedEyePositionMeters - testEyeOrigin) * 100f;
                GUILayout.Label($"Eye travel cm: {delta.x:F1}, {delta.y:F1}, {delta.z:F1}");
                GUILayout.Label($"Head change: pitch {Mathf.DeltaAngle(testHeadOrigin.x, o.headEulerDegrees.x):F1}, yaw {Mathf.DeltaAngle(testHeadOrigin.y, o.headEulerDegrees.y):F1}");
            }
            if (recording == null && GUILayout.Button("Record selected test for 10 seconds"))
            {
                recording = new StringBuilder("time_seconds,test,tracking,eye_x_m,eye_y_m,eye_z_m,pitch_deg,yaw_deg,roll_deg,iris_x,iris_y,gaze_valid,fit_error_px,inference_ms,pose_ms,result_age_ms,tracker_hz,raw_distance_m,render_distance_m,z_held\n");
                recordingEnds = Time.unscaledTime + 10f;
                recordedMotionTest = motionTest;
            }
            if (recording != null) GUILayout.Label($"Recording: {Mathf.Max(0, recordingEnds - Time.unscaledTime):F1} s");
            bool enabled = GUI.enabled;
            GUI.enabled = display.IsTracking && o.gazeValid;
            if (GUILayout.Button("Calibrate approximate screen gaze (9 points)"))
            {
                gazeSamples.Clear(); gazeTargets.Clear(); gazeTargetIndex = 0;
                calibratingGaze = true; capturingGaze = false; diagnosticNotice = "";
            }
            GUI.enabled = enabled;
            showGazePoint = GUILayout.Toggle(showGazePoint, "Show calibrated gaze marker");
            GUILayout.Label(gazeAvailable ? $"Approximate gaze: {gazePoint.x:F2}, {gazePoint.y:F2}" :
                "Gaze unavailable: calibrate after camera setup; eyes must be visible.");
            GUILayout.Label("Gaze does not rotate the render camera or set object depth.");
            GUILayout.Label(diagnosticNotice);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void StartDistanceCapture(bool near)
        {
            string text = near ? distanceNearCm : distanceFarCm;
            if (!float.TryParse(text, out float measured) || !IsFinite(measured) || measured < 25f || measured > 150f ||
                !display.HasFreshEyeEstimate)
            { distanceNotice = "Use a measured 25-150 cm distance and a valid fresh face fit."; return; }
            distanceCaptureNear = near;
            if (near) hasNearDistance = false; else hasFarDistance = false;
            measuredCaptureDistance = measured * .01f;
            captureDistanceContext = ViewingDistanceCalibration.Setup(display.Calibration, display.LatestObservation);
            distanceSamples.Clear(); capturingDistance = true;
            distanceCaptureStarted = Time.unscaledTime;
            distanceNotice = "Hold still, face forward: collecting 20 new frames.";
        }

        private void CollectDistanceSample(HeadObservation observation)
        {
            if (!capturingDistance || !display.HasFreshEyeEstimate) return;
            if (captureDistanceContext != ViewingDistanceCalibration.Setup(display.Calibration, observation))
            { capturingDistance = false; distanceNotice = "Camera setup changed; repeat this capture."; return; }
            distanceSamples.Add(-display.UncalibratedEyePositionMeters.z);
            if (distanceSamples.Count < 20) return;
            capturingDistance = false;
            float mean = 0f, variance = 0f;
            foreach (float d in distanceSamples) mean += d / distanceSamples.Count;
            foreach (float d in distanceSamples) variance += (d - mean) * (d - mean) / distanceSamples.Count;
            if (Mathf.Sqrt(variance) > .015f)
            { distanceNotice = "Distance varied too much; keep still and retry."; return; }
            if (distanceCaptureNear)
            {
                rawNearDistance = mean; measuredNearDistance = measuredCaptureDistance;
                nearDistanceContext = captureDistanceContext; hasNearDistance = true;
            }
            else
            {
                rawFarDistance = mean; measuredFarDistance = measuredCaptureDistance;
                farDistanceContext = captureDistanceContext; hasFarDistance = true;
            }
            distanceNotice = $"Captured {(distanceCaptureNear ? "near" : "far")}: measured {measuredCaptureDistance * 100f:F1} cm, raw {mean * 100f:F1} cm.";
        }

        private void DrawDistanceSettings()
        {
            GUILayout.Label("Physical Z / XY-only comparison");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Physical XYZ")) display.FreezeViewingDistance = false;
            if (GUILayout.Button("Hold size: XY only")) display.FreezeViewingDistance = true;
            GUILayout.EndHorizontal();
            GUILayout.Label(display.FreezeViewingDistance ? "Z held at activation distance; forward/back parallax disabled." : "Z tracks measured eye position; physical screen-window projection.");
            GUILayout.Label("Two-distance Z calibration: measure eye to screen, not webcam.");
            Input("Near measured cm", distanceNearCm, out distanceNearCm, .01f, .4f);
            Input("Far measured cm", distanceFarCm, out distanceFarCm, .01f, .8f);
            bool enabled = GUI.enabled;
            GUI.enabled = !capturingDistance && display.HasFreshEyeEstimate;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture near")) StartDistanceCapture(true);
            if (GUILayout.Button("Capture far")) StartDistanceCapture(false);
            GUILayout.EndHorizontal();
            GUI.enabled = !capturingDistance && hasNearDistance && hasFarDistance;
            if (GUILayout.Button("Apply measured Z calibration"))
            {
                string setup = ViewingDistanceCalibration.Setup(display.Calibration, display.LatestObservation);
                var fit = display.Calibration.viewingDistance ?? new ViewingDistanceCalibration();
                if (nearDistanceContext == farDistanceContext && nearDistanceContext == setup &&
                    fit.Fit(rawNearDistance, measuredNearDistance, rawFarDistance, measuredFarDistance, setup))
                {
                    display.Calibration.viewingDistance = fit;
                    display.FreezeViewingDistance = false;
                    distanceNotice = "Z scale/bias corrected. X/Y unchanged. Save calibration to persist.";
                }
                else distanceNotice = "Fit rejected: separate measured positions by >=10 cm; check direction and camera setup.";
            }
            GUI.enabled = enabled;
            var correction = display.Calibration.viewingDistance;
            if (correction != null && correction.enabled)
                GUILayout.Label(correction.Matches(display.Calibration, display.LatestObservation)
                    ? $"Z correction: scale {correction.scale:F3}, offset {correction.offsetMeters * 100f:F1} cm"
                    : "Z correction inactive: camera/source setup changed; recapture.");
            if (GUILayout.Button("Reset Z correction")) display.Calibration.viewingDistance = new ViewingDistanceCalibration();
            GUILayout.Label(capturingDistance ? $"Capturing distance: {distanceSamples.Count}/20" : distanceNotice);
        }

        private void DrawDepthPredictions()
        {
            float d = -display.EyePositionMeters.z;
            GUILayout.Label("Equal 15 cm plants: left +5, middle +30, right +100 cm.");
            GUILayout.Label("Centre-plane prediction: physical screen height / visual angle.");
            foreach (int index in new[] { 2, 3, 5 })
            {
                var model = sceneModels[index];
                float range = d + model.depth;
                if (range <= .025f) continue;
                float height = d * model.height / range;
                float horizontalRange = Mathf.Sqrt(range * range + Mathf.Pow(model.x - display.EyePositionMeters.x, 2));
                float centreY = model.supportY + model.height * .5f - display.EyePositionMeters.y;
                float angle = (Mathf.Atan2(centreY + model.height * .5f, horizontalRange) -
                    Mathf.Atan2(centreY - model.height * .5f, horizontalRange)) * Mathf.Rad2Deg;
                GUILayout.Label($"+{model.depth * 100f:F0} cm: {height * 100f:F1} cm on screen / {angle:F1} deg");
            }
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
            AddSceneModel("Ceramic vase (PBR)", "Realism/ceramic_vase", "Ceramic vase, measured surface comparison", -.10f, .40f, .85f,
                null, -.18f, Color.white);
            var wood = Resources.Load<Material>("Realism/wood");
            if (wood != null)
            {
                var floorMaterial = new Material(wood) { name = "Wood floor, world scale" };
                foreach (string map in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                    floorMaterial.SetTextureScale(map, new Vector2(2, 4));
                roomFloor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            }
            MakeSurfaceComparisons();
            UpdateRoomGeometry();

            var sun = new GameObject("Soft key light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.85f);
            sun.intensity = .85f;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = .015f;
            sun.shadowNormalBias = .02f;
            sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.26f, .29f, .32f);
            RenderSettings.ambientEquatorColor = new Color(.13f, .14f, .15f);
            RenderSettings.ambientGroundColor = new Color(.08f, .07f, .06f);
        }

        private static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var fallback = Resources.Load<Material>("Realism/fallback");
            var material = fallback != null ? new Material(fallback) : new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.08f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.45f);
            return material;
        }

        private void MakeSurfaceComparisons()
        {
            surfaceComparisons = new GameObject("Metal and roughness comparison");
            for (int i = 0; i < 2; i++)
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = i == 0 ? "Brushed brass sphere" : "Polished steel sphere";
                ball.transform.SetParent(surfaceComparisons.transform);
                ball.transform.localScale = Vector3.one * .08f;
                ball.transform.position = new Vector3(.14f + i * .18f, -.14f, .80f + i * .10f);
                var material = MakeMaterial(i == 0 ? new Color(.64f, .44f, .18f) : new Color(.72f, .75f, .78f));
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", i == 0 ? .58f : .90f);
                ball.GetComponent<Renderer>().sharedMaterial = material;
            }
            surfaceComparisons.SetActive(false);
        }

        public void UseMaterialStudy()
        {
            ResetModelLayout();
            isolatePlant = false; depthComparisonActive = false; materialStudyActive = true;
            for (int i = 0; i < sceneModels.Count; i++)
            {
                sceneModels[i].root.gameObject.SetActive(i == 6);
                if (sceneModels[i].plinth != null) sceneModels[i].plinth.gameObject.SetActive(false);
            }
            surfaceComparisons.SetActive(true);
            roomFloor.gameObject.SetActive(true); backWall.gameObject.SetActive(true);
            UpdateRoomGeometry();
            showTrackingTest = false; showModelSettings = false;
        }

        private void OnDestroy() => modelMaterials.Dispose();

        private static Transform MakeBox(string name, Vector3 position, Vector3 scale, Color color)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().material = MakeMaterial(color);
            return item.transform;
        }

        private Transform AddModel(string resource, string name, Vector3 centre, float height, Color color)
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
                for (int i = 0; i < materials.Length; i++) materials[i] = modelMaterials.Prepare(materials[i], material);
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
                depthCm = (depth * 100f).ToString("F1"), xCm = (x * 100f).ToString("F1"),
                yCm = ((supportY + height * .5f) * 100f).ToString("F1"),
                defaultHeight = height, defaultCentre = new Vector3(x, supportY + height * .5f, depth)
            });
        }

        private static Bounds ModelBounds(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
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

        // Compare a fixed target near eye level, independent of the floor and surrounding models.
        public void UseFixationTest(float depthMeters)
        {
            if (sceneModels.Count < 3) return;
            var model = sceneModels[2];
            model.xCm = "0";
            model.yCm = "0";
            model.heightCm = "15";
            model.depthCm = (depthMeters * 100f).ToString("F1");
            if (!ApplyModelLayout()) return;
            SetPlantIsolation(true);
            showModelSettings = true;
        }

        public void UseDepthMotionTest()
        {
            if (sceneModels.Count < 6) return;
            float d = Mathf.Max(.25f, display.Calibration.referenceEyeDistanceFromScreen);
            int[] indices = { 2, 3, 5 };
            float[] depths = { .05f, .30f, 1f };
            for (int i = 0; i < indices.Length; i++)
            {
                var model = sceneModels[indices[i]];
                float screenX = (i - 1) * display.Calibration.screenWidth * .24f;
                model.xCm = (screenX * (d + depths[i]) / d * 100f).ToString("F1");
                model.yCm = "0"; model.heightCm = "15";
                model.depthCm = (depths[i] * 100f).ToString("F1");
            }
            if (ApplyModelLayout()) SetDepthComparisonVisibility();
        }

        private void SetDepthComparisonVisibility()
        {
            materialStudyActive = false;
            if (surfaceComparisons != null) surfaceComparisons.SetActive(false);
            isolatePlant = false; depthComparisonActive = true;
            for (int i = 0; i < sceneModels.Count; i++)
            {
                sceneModels[i].root.gameObject.SetActive(i == 2 || i == 3 || i == 5);
                if (sceneModels[i].plinth != null) sceneModels[i].plinth.gameObject.SetActive(false);
            }
            roomFloor.gameObject.SetActive(false); backWall.gameObject.SetActive(false);
        }

        public void ResetModelLayout()
        {
            foreach (var model in sceneModels)
            {
                model.heightCm = (model.defaultHeight * 100f).ToString("F1");
                model.xCm = (model.defaultCentre.x * 100f).ToString("F1");
                model.yCm = (model.defaultCentre.y * 100f).ToString("F1");
                model.depthCm = (model.defaultCentre.z * 100f).ToString("F1");
            }
            ApplyModelLayout();
            SetPlantIsolation(false);
        }

        /// <summary>Restore demo presentation and controls, keeping the viewer's physical calibration.</summary>
        public void ResetDemo()
        {
            if (display == null) return;
            if (recording != null) FinishRecording();
            ResetModelLayout();
            enhancedRendering = true;
            if (renderQuality != null) renderQuality.Apply(true);
            display.FreezeViewingDistance = false;
            display.UseAdaptiveFilter = true;
            var defaults = new AdaptiveEyeFilter();
            display.FilterMinimumCutoffHz = defaults.MinimumCutoffHz;
            display.FilterSpeedCoefficient = defaults.SpeedCoefficient;
            display.TrackingSmoothingSeconds = .045f;
            display.Configure(screenPlane, source);

            // Cancel unfinished measurements; keep completed screen/camera/Z/gaze calibration.
            capturingDistance = hasNearDistance = hasFarDistance = false;
            distanceSamples.Clear();
            distanceNearCm = "40"; distanceFarCm = "80";
            distanceNotice = "Enter physically measured eye-to-screen distances.";
            calibratingGaze = capturingGaze = false;
            gazeSamples.Clear(); gazeTargets.Clear();
            gazeTargetIndex = gazeTargetSamples = 0;
            gazeAvailable = hasTestOrigin = hasMeasurementOrigin = false;
            diagnosticNotice = "";
            motionTest = 0;
            showSettings = showGazePoint = true;
            cleanView = showDisplaySize = showModelSettings = showTrackingTest = showScreenRuler = false;
            settingsScroll = trackingScroll = Vector2.zero;
            CopyFieldsFromCalibration();
            SaveSettings();
            notice = "Demo defaults restored; physical calibration kept. " + notice;
        }

        public void SetPlantIsolation(bool isolate)
        {
            materialStudyActive = false;
            if (surfaceComparisons != null) surfaceComparisons.SetActive(false);
            depthComparisonActive = false;
            isolatePlant = isolate;
            for (int i = 0; i < sceneModels.Count; i++)
            {
                var model = sceneModels[i];
                model.root.gameObject.SetActive(!isolate || i == 2);
                if (model.plinth != null) model.plinth.gameObject.SetActive(!isolate);
            }
            roomFloor.gameObject.SetActive(!isolate);
            backWall.gameObject.SetActive(!isolate);
            if (!isolate) UpdateRoomGeometry();
        }

        private bool ApplyModelLayout()
        {
            var values = new ModelSizeAndDepth[sceneModels.Count];
            for (int i = 0; i < sceneModels.Count; i++)
            {
                var model = sceneModels[i];
                if (!float.TryParse(model.heightCm, out float height) || !float.TryParse(model.depthCm, out float depth) ||
                    !float.TryParse(model.xCm, out float x) || !float.TryParse(model.yCm, out float y) ||
                    !IsFinite(height) || !IsFinite(depth) || !IsFinite(x) || !IsFinite(y) ||
                    height < 2f || height > 200f || depth < -20f || depth > 1500f ||
                    Mathf.Abs(x) > 1500f || Mathf.Abs(y) > 1500f)
                {
                    notice = "Use height 2-200 cm, X/Y within +/-1500 cm, depth -20 to +1500 cm.";
                    return false;
                }
                values[i] = new ModelSizeAndDepth
                    { height = height * .01f, depth = depth * .01f, x = x * .01f, y = y * .01f, hasPosition = true };
            }
            for (int i = 0; i < sceneModels.Count; i++)
            {
                var model = sceneModels[i];
                model.height = values[i].height;
                model.depth = values[i].depth;
                model.x = values[i].x;
                model.supportY = values[i].y - model.height * .5f;
                model.root.localScale *= model.height / Mathf.Max(.001f, ModelBounds(model.root).size.y);
                Vector3 centre = new Vector3(model.x, model.supportY + model.height * .5f, model.depth);
                model.root.position += centre - ModelBounds(model.root).center;
                if (model.plinth != null)
                {
                    var position = model.plinth.position;
                    position.x = model.x;
                    position.y = model.supportY - model.plinth.localScale.y * .5f;
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
                if (!model.root.gameObject.activeSelf) continue;
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
            var floorMaterial = roomFloor.GetComponent<Renderer>().sharedMaterial;
            foreach (string map in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                if (floorMaterial.HasProperty(map)) floorMaterial.SetTextureScale(map, new Vector2(floorScale.x / .8f, floorScale.z / .8f));
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
                    if (saved.models[i].hasPosition)
                    {
                        sceneModels[i].xCm = (saved.models[i].x * 100f).ToString("F1");
                        sceneModels[i].yCm = (saved.models[i].y * 100f).ToString("F1");
                    }
                    else
                        sceneModels[i].yCm = ((sceneModels[i].supportY + saved.models[i].height * .5f) * 100f).ToString("F1");
                }
                ApplyModelLayout();
                SetPlantIsolation(saved.isolatePlant);
                if (saved.depthComparison) SetDepthComparisonVisibility();
                if (saved.materialStudy) UseMaterialStudy();
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

        private void LoadHardwareScreenHint()
        {
            string path = Path.Combine(Application.persistentDataPath, "monitor_hint.json");
            if (!File.Exists(path)) return;
            try
            {
                var hint = JsonUtility.FromJson<MonitorHint>(File.ReadAllText(path));
                if (hint.widthCm < 10 || hint.widthCm > 200 || hint.heightCm < 10 || hint.heightCm > 150) return;
                reportedScreenWidth = hint.widthCm; reportedScreenHeight = hint.heightCm;
                var c = display.Calibration;
                if (Mathf.Abs(c.screenWidth - .53f) < .0001f && Mathf.Abs(c.screenHeight - .30f) < .0001f)
                {
                    c.screenWidth = reportedScreenWidth * .01f;
                    c.deriveScreenHeightFromResolution = true;
                }
                Debug.Log($"Panel hint: {reportedScreenWidth:F0} x {reportedScreenHeight:F0} cm (rounded). Using width {c.screenWidth * 100f:F1} cm; derive height {c.deriveScreenHeightFromResolution}.");
            }
            catch (Exception ex) { Debug.LogWarning("Panel hint unavailable: " + ex.Message); }
        }

        private void SaveSettings()
        {
            try
            {
                var path = Path.Combine(Application.persistentDataPath, "display_calibration.json");
                File.WriteAllText(path, JsonUtility.ToJson(display.Calibration, true));
                var layout = new ModelLayoutFile { models = new ModelSizeAndDepth[sceneModels.Count], isolatePlant = isolatePlant,
                    depthComparison = depthComparisonActive, materialStudy = materialStudyActive };
                for (int i = 0; i < sceneModels.Count; i++)
                    layout.models[i] = new ModelSizeAndDepth { height = sceneModels[i].height, depth = sceneModels[i].depth,
                        x = sceneModels[i].x, y = sceneModels[i].supportY + sceneModels[i].height * .5f, hasPosition = true };
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

        private void OpenDisplaySize()
        {
            var c = display.Calibration;
            widthCm = (c.screenWidth * 100f).ToString("0.##");
            heightCm = (c.screenHeight * 100f).ToString("0.##");
            screenSizeDeriveHeight = c.deriveScreenHeightFromResolution;
            screenSizeNotice = "";
            screenSizeScroll = Vector2.zero;
            showDisplaySize = true;
        }

        private void ApplyDisplaySize()
        {
            if (!float.TryParse(widthCm, out float width) || !IsFinite(width) || width < 10f || width > 300f)
            { screenSizeNotice = "Enter a width between 10 and 300 cm."; return; }
            float height;
            if (screenSizeDeriveHeight)
                height = width * Screen.height / Mathf.Max(1, Screen.width);
            else if (!float.TryParse(heightCm, out height))
            { screenSizeNotice = "Enter a valid height in cm."; return; }
            if (!IsFinite(height) || height < 10f || height > 200f)
            { screenSizeNotice = "Enter a height between 10 and 200 cm."; return; }
            var c = display.Calibration;
            // Save before applying so a disk failure leaves the current projection unchanged.
            var saved = JsonUtility.FromJson<DisplayCalibration>(JsonUtility.ToJson(c));
            saved.screenWidth = width * .01f;
            saved.screenHeight = height * .01f;
            saved.deriveScreenHeightFromResolution = screenSizeDeriveHeight;
            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "display_calibration.json"),
                    JsonUtility.ToJson(saved, true));
            }
            catch (Exception ex) { screenSizeNotice = "Could not save display size: " + ex.Message; return; }
            c.screenWidth = saved.screenWidth;
            c.screenHeight = saved.screenHeight;
            c.deriveScreenHeightFromResolution = saved.deriveScreenHeightFromResolution;
            CopyFieldsFromCalibration();
            showDisplaySize = false;
            notice = $"Display size applied and saved: {width:0.##} x {height:0.##} cm.";
        }

        private void DrawDisplaySize()
        {
            DrawScreenRuler();
            float panelWidth = Mathf.Min(440, Screen.width - 24);
            float panelHeight = Mathf.Min(420, Screen.height - 24);
            GUILayout.BeginArea(new Rect((Screen.width - panelWidth) * .5f,
                (Screen.height - panelHeight) * .5f, panelWidth, panelHeight), GUI.skin.box);
            screenSizeScroll = GUILayout.BeginScrollView(screenSizeScroll);
            GUILayout.Label("DISPLAY SIZE / centimetres");
            GUILayout.Label("Measure the visible display area, excluding its bezel.");
            GUILayout.Label($"Currently applied: {display.Calibration.screenWidth * 100f:0.##} x {display.Calibration.screenHeight * 100f:0.##} cm");
            GUILayout.Label($"Rendering: {Screen.width} x {Screen.height} px | aspect {(float)Screen.width / Mathf.Max(1, Screen.height):F3}");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Width (cm)", GUILayout.Width(170));
            widthCm = GUILayout.TextField(widthCm, GUILayout.Width(100));
            GUILayout.EndHorizontal();
            screenSizeDeriveHeight = GUILayout.Toggle(screenSizeDeriveHeight,
                "Calculate height from width and render resolution");
            bool previousEnabled = GUI.enabled;
            if (screenSizeDeriveHeight)
            {
                if (float.TryParse(widthCm, out float width) && IsFinite(width))
                    heightCm = (width * Screen.height / Mathf.Max(1, Screen.width)).ToString("0.##");
                GUI.enabled = false;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Height (cm)", GUILayout.Width(170));
            heightCm = GUILayout.TextField(heightCm, GUILayout.Width(100));
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;
            GUILayout.Label("Leave automatic height OFF to use both measured dimensions.");
            if (reportedScreenWidth > 0)
            {
                GUILayout.Label($"Monitor hint: about {reportedScreenWidth:F0} x {reportedScreenHeight:F0} cm (rounded).");
                if (GUILayout.Button("Use monitor width estimate"))
                { widthCm = reportedScreenWidth.ToString("0.##"); screenSizeDeriveHeight = true; }
            }
            showScreenRuler = GUILayout.Toggle(showScreenRuler, "Show horizontal / vertical 10 cm rulers");
            GUILayout.Label("Rulers use the applied size; apply first, then check them.");
            if (!string.IsNullOrEmpty(screenSizeNotice)) GUILayout.Label(screenSizeNotice);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply and save")) ApplyDisplaySize();
            if (GUILayout.Button("Cancel")) showDisplaySize = false;
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

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
            float verticalPixels = Screen.height * .10f / display.Calibration.screenHeight;
            GUI.Box(new Rect(Screen.width - 36, (Screen.height - verticalPixels) * .5f, 24, verticalPixels), "10\ncm");
        }

        private void DrawModelSettings()
        {
            showModelSettings = GUILayout.Toggle(showModelSettings, "Model size and depth / compare physical layouts");
            if (!showModelSettings) return;
            GUILayout.Label("Height is the real object height. Depth: + behind, - in front.");
            GUILayout.Label("X/Y are the model centre relative to screen centre; +Y is up.");
            GUILayout.Label("Centred fixation test: a 15 cm plant at eye level.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0 cm")) UseFixationTest(0f);
            if (GUILayout.Button("+5 cm")) UseFixationTest(.05f);
            if (GUILayout.Button("+15 cm")) UseFixationTest(.15f);
            if (GUILayout.Button("+30 cm")) UseFixationTest(.30f);
            GUILayout.EndHorizontal();
            bool isolate = GUILayout.Toggle(isolatePlant, "Only green plant / hide room and supports");
            if (isolate != isolatePlant) SetPlantIsolation(isolate);
            if (GUILayout.Button("Restore all models and original positions")) ResetModelLayout();
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
                GUILayout.EndHorizontal();
                DrawPositionInput("X cm", ref model.xCm);
                DrawPositionInput("Y cm", ref model.yCm);
                DrawPositionInput("Depth cm", ref model.depthCm);
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
            if (GUILayout.Button("Apply size and X/Y/depth")) ApplyModelLayout();
        }

        private void DrawPositionInput(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(75));
            value = GUILayout.TextField(value, GUILayout.Width(65));
            bool decrease = GUILayout.Button("-1", GUILayout.Width(42));
            bool increase = GUILayout.Button("+1", GUILayout.Width(42));
            if ((decrease || increase) && float.TryParse(value, out float cm) && IsFinite(cm))
            {
                value = (cm + (increase ? 1f : -1f)).ToString("F1");
                ApplyModelLayout();
            }
            GUILayout.EndHorizontal();
        }

        private void OnGUI()
        {
            if (display == null) return;
            if (cleanView) return;
            if (calibratingGaze) { DrawGazeCalibration(); return; }
            if (showDisplaySize) { DrawDisplaySize(); return; }
            if (showGazePoint && gazeAvailable && display.IsTracking && display.LatestObservation.gazeValid)
                GUI.Box(new Rect(Mathf.Clamp01(gazePoint.x) * Screen.width - 8,
                    (1f - Mathf.Clamp01(gazePoint.y)) * Screen.height - 8, 16, 16), "+");
            DrawTrackingTest();
            if (GUI.Button(new Rect(12, 12, 170, 28), showSettings ? "Hide settings" : "Show settings"))
                showSettings = !showSettings;
            if (GUI.Button(new Rect(190, 12, 262, 28), "Reset demo to defaults")) ResetDemo();
            if (GUI.Button(new Rect(12, 46, 440, 28),
                $"Display size: {display.Calibration.screenWidth * 100f:0.##} x {display.Calibration.screenHeight * 100f:0.##} cm / change"))
                OpenDisplaySize();
            DrawScreenRuler();
            if (!showSettings) return;
            GUILayout.BeginArea(new Rect(12, 82, 440, Mathf.Min(Screen.height - 94, 690)), GUI.skin.box);
            settingsScroll = GUILayout.BeginScrollView(settingsScroll);
            GUILayout.Label("HEAD-TRACKED DISPLAY / physical monitor setup");
            GUILayout.Label("Reset restores models and controls, keeping screen / camera calibration.");
            GUILayout.Label($"Tracking: {(display.IsTracking ? "FACE FOUND" : "NO FACE")} | {display.SourceStatus}");
            GUILayout.Label($"Eye (m): {display.EyePositionMeters.ToString("F3")}  Confidence: {display.Confidence:F2}");
            GUILayout.Label($"Display: {Screen.width} x {Screen.height} px");
            if (GUILayout.Button("Realistic material comparison")) UseMaterialStudy();
            bool quality = GUILayout.Toggle(enhancedRendering, "Enhanced rendering: 4x MSAA / soft shadows / SSAO");
            if (quality != enhancedRendering)
            {
                enhancedRendering = quality;
                if (renderQuality != null) renderQuality.Apply(quality);
            }
            GUILayout.Label("F1 hides all panels for judging the surfaces.");
            if (materialStudyActive) GUILayout.Label("40 cm ceramic vase, wood surface, brass and steel spheres.");
            var c = display.Calibration;
            float physicalAspect = c.screenWidth / c.screenHeight;
            float imageAspect = (float)Screen.width / Screen.height;
            if (Mathf.Abs(physicalAspect - imageAspect) > 0.03f)
                GUILayout.Label("WARNING: physical screen aspect differs from render aspect.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Python bridge")) SelectProvider(false);
            if (GUILayout.Button("Unity MediaPipe")) SelectProvider(true);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Start centred +5 cm fixation comparison")) UseFixationTest(.05f);
            showTrackingTest = GUILayout.Toggle(showTrackingTest, "Show eye / head / gaze test panel");
            c.useRigidFacePose = GUILayout.Toggle(c.useRigidFacePose, "Use rigid face pose (Python; disable for legacy A/B)");
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
                "Hold current viewing distance (XY only; no forward/back parallax)");
            display.UseAdaptiveFilter = GUILayout.Toggle(display.UseAdaptiveFilter, "Adaptive eye filter (One Euro)");
            if (display.UseAdaptiveFilter)
            {
                GUILayout.Label($"Minimum cutoff: {display.FilterMinimumCutoffHz:F1} Hz | speed coefficient: {display.FilterSpeedCoefficient:F1}");
                display.FilterMinimumCutoffHz = GUILayout.HorizontalSlider(display.FilterMinimumCutoffHz, .5f, 8f);
                display.FilterSpeedCoefficient = GUILayout.HorizontalSlider(display.FilterSpeedCoefficient, 0f, 50f);
            }
            else
            {
                GUILayout.Label($"Tracking smoothing time constant: {display.TrackingSmoothingSeconds * 1000f:F0} ms");
                display.TrackingSmoothingSeconds = GUILayout.HorizontalSlider(display.TrackingSmoothingSeconds, .005f, .10f);
            }
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
            public string label, heightCm, depthCm, xCm, yCm;
            public Transform root, plinth;
            public float x, supportY, height, depth, defaultHeight;
            public Vector3 defaultCentre;
        }

        [Serializable]
        private sealed class ModelSizeAndDepth
        {
            public float height, depth, x, y;
            public bool hasPosition;
        }

        [Serializable]
        private sealed class MonitorHint { public float widthCm, heightCm; }

        [Serializable]
        private sealed class ModelLayoutFile
        {
            public ModelSizeAndDepth[] models;
            public bool isolatePlant;
            public bool depthComparison;
            public bool materialStudy;
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
