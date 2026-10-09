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
    public sealed partial class DemoBootstrap
    {
        private Vector3 measurementOrigin;
        private bool hasMeasurementOrigin;

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
        private bool showGazePoint;
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
            if (!showInitialSetup && !calibratingGaze && UnityEngine.Input.GetKeyDown(KeyCode.F1)) cleanView = !cleanView;
            fpsElapsed += Time.unscaledDeltaTime;
            fpsFrames++;
            if (fpsElapsed >= .5f)
            { renderFps = fpsFrames / fpsElapsed; fpsFrames = 0; fpsElapsed = 0f; }
            if (!showInitialSetup && !calibratingGaze && UnityEngine.Input.GetKeyDown(KeyCode.F2)) showSettings = !showSettings;
            ApplyBillboardRendering();
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
            DrawTrackingControls();
            GUILayout.Space(10);
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
                    distanceNotice = "Z scale/bias corrected. X/Y unchanged. Save physical calibration to persist.";
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

    }
}
