using UnityEngine;

namespace HeadTracked.Demo
{
    /// <summary>Minimal setup, three independent comparisons, and one optional right-hand drawer.</summary>
    public sealed partial class DemoBootstrap
    {
        private bool showSettings;
        private Vector2 settingsScroll;

        private GUISkin demoSkin;
        private int settingsTab;
        private static readonly string[] SettingsTabs = { "Calibration", "Appearance", "Layouts", "Diagnostics" };

        private void OnGUI()
        {
            if (display == null || cleanView) return;
            if (demoSkin == null)
            {
                demoSkin = Instantiate(GUI.skin);
                foreach (var style in new[] { demoSkin.label, demoSkin.button, demoSkin.toggle,
                    demoSkin.textField, demoSkin.box }) style.fontSize = 14;
                demoSkin.label.wordWrap = true;
                demoSkin.button.wordWrap = true;
                demoSkin.button.padding = new RectOffset(8, 8, 6, 6);
                demoSkin.textField.padding = new RectOffset(4, 4, 4, 4);
                demoSkin.toggle.padding.top = 3;
                demoSkin.toggle.padding.bottom = 3;
            }
            GUI.skin = demoSkin;
            if (calibratingGaze) { DrawGazeCalibration(); return; }
            if (showInitialSetup) { DrawInitialSetup(); return; }
            if (showDisplaySize) { DrawDisplaySize(); return; }
            DrawComparisonBar();
            DrawScreenRuler();
            if (showGazePoint && gazeAvailable && display.IsTracking && display.LatestObservation.gazeValid)
                GUI.Box(new Rect(Mathf.Clamp01(gazePoint.x) * Screen.width - 8,
                    (1f - Mathf.Clamp01(gazePoint.y)) * Screen.height - 8, 16, 16), "+");
            if (showSettings) DrawSettingsDrawer();
            string state = display.TrackingEnabled ? "Head tracking follows viewer" : "Viewpoint held for comparison";
            GUI.Box(new Rect(12, Screen.height - 64, Screen.width - 24, 52),
                $"{(display.IsTracking ? "FACE FOUND" : "NO FACE")} | {state} | {renderFps:F0} FPS\n{notice}");
        }

        private void DrawComparisonBar()
        {
            float drawerButtonWidth = Mathf.Min(164, Screen.width * .23f);
            float switchWidth = Mathf.Max(70, (Screen.width - drawerButtonWidth - 48) / 3f);
            bool enabled = GUI.enabled;
            // Special layouts are explicitly selected experiments; return to the baseline for dressing A/B.
            if (GUI.Button(new Rect(12, 12, switchWidth, 36),
                "Head tracking: " + (display.TrackingEnabled ? "ON" : "OFF")))
                SetHeadTrackingEnabled(!display.TrackingEnabled);
            GUI.enabled = enabled && comparisonSceneActive;
            if (GUI.Button(new Rect(20 + switchWidth, 12, switchWidth, 36),
                "Billboard effect: " + (billboardEffectEnabled ? "ON" : "OFF")))
                SetBillboardEffectEnabled(!billboardEffectEnabled);
            GUI.enabled = enabled;
            if (GUI.Button(new Rect(28 + switchWidth * 2, 12, switchWidth, 36),
                "Surface detail: " + (texturesEnabled ? "ON" : "OFF")))
                SetTexturesEnabled(!texturesEnabled);
            if (GUI.Button(new Rect(Screen.width - drawerButtonWidth - 12, 12, drawerButtonWidth, 36),
                showSettings ? "Close settings [F2]" : "Settings [F2]")) showSettings = !showSettings;
            GUI.Box(new Rect(12, 54, Screen.width - 24, 28), comparisonSceneActive
                ? "SAME SCENE / Switch one feature at a time. OFF retains the model and viewpoint. F1: clean view."
                : "EXPERIMENTAL LAYOUT / Return to the comparison scene in Settings > Layouts for all three switches.");
        }

        private void DrawSettingsDrawer()
        {
            float width = Mathf.Min(460, Screen.width - 24);
            GUILayout.BeginArea(new Rect(Screen.width - width - 12, 90, width,
                Mathf.Max(80, Screen.height - 164)), GUI.skin.box);
            settingsTab = GUILayout.Toolbar(settingsTab, SettingsTabs, GUILayout.Height(32));
            settingsScroll = GUILayout.BeginScrollView(settingsScroll);
            GUILayout.Space(8);
            switch (settingsTab)
            {
                case 0: DrawPhysicalCalibration(); break;
                case 1: DrawAppearanceSettings(); break;
                case 2: DrawExperimentalLayouts(); break;
                case 3: DrawTrackingTest(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.Space(6);
            if (GUILayout.Button("Reset comparison defaults (keep measured calibration)")) ResetDemo();
            GUILayout.EndArea();
        }

        private void DrawInitialSetup()
        {
            float width = Mathf.Min(480, Screen.width - 24);
            float height = Mathf.Min(480, Screen.height - 24);
            GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f,
                width, height), GUI.skin.box);
            screenSizeScroll = GUILayout.BeginScrollView(screenSizeScroll);
            GUILayout.Label("HEAD TRACKED DISPLAY / QUICK SETUP");
            GUILayout.Label("Measure the visible screen and your eye-to-screen distance. These example values must match your hardware.");
            Input("Screen width (cm)", widthCm, out widthCm, 1f, 53f);
            Input("Screen height (cm)", heightCm, out heightCm, 1f, 30f);
            Input("Viewing distance (cm)", eyeDistanceCm, out eyeDistanceCm, 1f, 60f);
            GUILayout.Space(8);
            GUILayout.Label($"Tracking: {(display.IsTracking ? "FACE FOUND" : "NO FACE")} | {display.SourceStatus}");
            GUILayout.Label("Sit at the entered distance. Start captures a reference if a face is available. You can also test a fixed view without a webcam.");
            GUILayout.Label("Webcam placement, eye separation, precise calibration and diagnostics are in the right-hand Settings drawer.");
            if (!string.IsNullOrEmpty(screenSizeNotice)) GUILayout.Label(screenSizeNotice);
            if (GUILayout.Button("Save setup and start comparison", GUILayout.Height(38))) CompleteInitialSetup();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPhysicalCalibration()
        {
            var c = display.Calibration;
            GUILayout.Label("PHYSICAL SCREEN AND CAMERA");
            GUILayout.Label($"Applied screen: {c.screenWidth * 100f:0.##} x {c.screenHeight * 100f:0.##} cm");
            if (GUILayout.Button("Change measured display size")) OpenDisplaySize();
            showScreenRuler = GUILayout.Toggle(showScreenRuler, "Show horizontal / vertical 10 cm rulers");
            c.referenceEyeDistanceFromScreen = Mathf.Clamp(Input("Eye distance (cm)", eyeDistanceCm,
                out eyeDistanceCm, .01f, c.referenceEyeDistanceFromScreen), .2f, 2f);
            var webcam = c.webcamPosition;
            webcam.x = Input("Webcam X (cm)", webcamXCm, out webcamXCm, .01f, webcam.x);
            webcam.y = Input("Webcam Y (cm)", webcamYCm, out webcamYCm, .01f, webcam.y);
            webcam.z = Input("Webcam Z (cm)", webcamZCm, out webcamZCm, .01f, webcam.z);
            c.webcamPosition = webcam;
            var euler = c.webcamEulerDegrees;
            euler.x = Input("Webcam pitch (degrees)", webcamPitch, out webcamPitch, 1f, euler.x);
            c.webcamEulerDegrees = euler;
            c.measuredEyeSeparationMeters = Mathf.Clamp(Input("Eye separation / IPD (mm)", ipdMm,
                out ipdMm, .001f, c.measuredEyeSeparationMeters), .045f, .085f);
            c.mirrorImageX = GUILayout.Toggle(c.mirrorImageX, "Mirror webcam X");
            c.useRigidFacePose = GUILayout.Toggle(c.useRigidFacePose, "Rigid face pose (Python)");
            c.usePreciseIntrinsics = GUILayout.Toggle(c.usePreciseIntrinsics, "Use precise camera intrinsics");
            if (!c.usePreciseIntrinsics)
            {
                c.useEyeSeparationForBasicScale = GUILayout.Toggle(c.useEyeSeparationForBasicScale,
                    "Use captured eye spacing for basic movement scale");
                if (!c.useEyeSeparationForBasicScale)
                {
                    GUILayout.Label($"Assumed horizontal webcam FOV: {c.horizontalFovDegrees:F0} degrees");
                    c.horizontalFovDegrees = GUILayout.HorizontalSlider(c.horizontalFovDegrees, 30f, 110f);
                }
            }
            if (GUILayout.Button("Capture reference at entered distance"))
                notice = display.CaptureReference() ? "Reference captured. Save physical calibration to persist." : "A tracked face is required to capture a reference.";
            if (GUILayout.Button("Reload camera intrinsics")) LoadIntrinsics();
            if (GUILayout.Button("Save physical calibration")) SaveSettings();
            GUILayout.Space(10);
            GUILayout.Label("TRACKING SOURCE");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Python bridge")) SelectProvider(false);
            if (GUILayout.Button("Unity MediaPipe")) SelectProvider(true);
            GUILayout.EndHorizontal();
            if (source != null && source.GetType().Name == "MediaPipeUnitySource")
                foreach (var device in WebCamTexture.devices)
                    if (GUILayout.Button(device.name))
                        source.GetType().GetMethod("SelectWebcam")?.Invoke(source, new object[] { device.name });
            GUILayout.Label($"Eye (m): {display.EstimatedEyePositionMeters:F3} | confidence {display.Confidence:F2}");
        }

        private void DrawAppearanceSettings()
        {
            GUILayout.Label("COMPARISON APPEARANCE");
            GUILayout.Label("The top switches retain the content transform, lights and render quality. Detailed changes here are explicit adjustments.");
            if (billboardActive) DrawBillboardSettings();
            else
            {
                bool quality = GUILayout.Toggle(enhancedRendering, "4x MSAA / soft shadows / SSAO");
                if (quality != enhancedRendering)
                { enhancedRendering = quality; if (renderQuality != null) renderQuality.Apply(quality); }
            }
            GUILayout.Label("Surface OFF removes texture maps, normal detail and metallic finish. Surface ON restores the original material slots.");
            GUILayout.Label("Detailed appearance changes last for this session. Save an experiment in Layouts to keep them.");
            if (GUILayout.Button("Save comparison switch preferences")) SaveDemoPreferences();
        }

        private void DrawTrackingControls()
        {
            display.FreezeViewingDistance = GUILayout.Toggle(display.FreezeViewingDistance,
                "Hold distance: XY only (disables forward/back parallax)");
            display.UseAdaptiveFilter = GUILayout.Toggle(display.UseAdaptiveFilter, "Adaptive eye filter (One Euro)");
            if (display.UseAdaptiveFilter)
            {
                GUILayout.Label($"Minimum cutoff: {display.FilterMinimumCutoffHz:F1} Hz / speed: {display.FilterSpeedCoefficient:F1}");
                display.FilterMinimumCutoffHz = GUILayout.HorizontalSlider(display.FilterMinimumCutoffHz, .5f, 8f);
                display.FilterSpeedCoefficient = GUILayout.HorizontalSlider(display.FilterSpeedCoefficient, 0f, 50f);
            }
            else
            {
                GUILayout.Label($"Smoothing: {display.TrackingSmoothingSeconds * 1000f:F0} ms");
                display.TrackingSmoothingSeconds = GUILayout.HorizontalSlider(display.TrackingSmoothingSeconds, .005f, .10f);
            }
            if (GUILayout.Button("Set movement measurement origin") && display.IsTracking)
            { measurementOrigin = display.EstimatedEyePositionMeters; hasMeasurementOrigin = true; }
            if (hasMeasurementOrigin && display.IsTracking)
            {
                Vector3 delta = (display.EstimatedEyePositionMeters - measurementOrigin) * 100f;
                GUILayout.Label($"Head travel (cm): X {delta.x:F1}, Y {delta.y:F1}, Z {delta.z:F1}");
            }
        }

        private void DrawExperimentalLayouts()
        {
            GUILayout.Label("EXPLICIT SCENE EXPERIMENTS");
            GUILayout.Label("These actions change placement or content. The primary ON/OFF comparison never selects a different layout.");
            if (GUILayout.Button("Return to fixed comparison scene")) UseComparisonScene();
            if (comparisonSceneActive)
            {
                GUILayout.Label("Choose comparison content (explicit model change):");
                int selected = GUILayout.Toolbar(billboardModel, new[] { "Sphere", "Bear", "Chair", "Vase" });
                if (selected != billboardModel) SelectBillboardModel(selected);
            }
            GUILayout.Space(8);
            if (GUILayout.Button("Full original model layout")) ResetModelLayout();
            if (GUILayout.Button("Centred fixation target (+5 cm)")) UseFixationTest(.05f);
            if (GUILayout.Button("Equal-size plants at three depths")) UseDepthMotionTest();
            if (GUILayout.Button("Vase / wood / metal surface study")) UseMaterialStudy();
            if (!billboardActive) DrawModelSettings();
            GUILayout.Space(8);
            if (GUILayout.Button("Save experimental layout")) SaveModelLayout();
            if (GUILayout.Button("Load saved experimental layout"))
            { ResetModelLayout(); LoadModelLayout(); RegisterComparisonSurfaces(); }
        }
    }
}
