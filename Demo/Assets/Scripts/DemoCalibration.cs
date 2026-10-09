using System;
using System.IO;
using HeadTracked.Display;
using UnityEngine;

namespace HeadTracked.Demo
{
    public sealed partial class DemoBootstrap
    {
        private string widthCm = "53";
        private string heightCm = "30";
        private string eyeDistanceCm = "60";
        private string webcamXCm = "0";
        private string webcamYCm = "17";
        private string webcamZCm = "-2.5";
        private string webcamPitch = "0";
        private string ipdMm = "63";

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
                Debug.Log($"Panel hint: {reportedScreenWidth:F0} x {reportedScreenHeight:F0} cm (rounded; applied only when selected by the viewer).");
            }
            catch (Exception ex) { Debug.LogWarning("Panel hint unavailable: " + ex.Message); }
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

        private void SaveModelLayout()
        {
            try
            {
                var layout = new ModelLayoutFile { models = new ModelSizeAndDepth[sceneModels.Count], isolatePlant = isolatePlant,
                    depthComparison = depthComparisonActive, materialStudy = materialStudyActive,
                    billboardMode = billboardActive, billboardModel = billboardModel,
                    hasBillboardRendering = true, stableBillboardEdges = stableBillboardEdges,
                    billboardContactAO = billboardContactAO, billboardRenderScale = billboardRenderScale,
                    billboardSettings = billboard != null ? billboard.Settings : null };
                for (int i = 0; i < sceneModels.Count; i++)
                    layout.models[i] = new ModelSizeAndDepth { height = sceneModels[i].height, depth = sceneModels[i].depth,
                        x = sceneModels[i].x, y = sceneModels[i].supportY + sceneModels[i].height * .5f, hasPosition = true };
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "demo_model_layout.json"),
                    JsonUtility.ToJson(layout, true));
                notice = "Experimental layout saved. Load it explicitly from the Layouts tab.";
            }
            catch (Exception ex) { notice = "Could not save layout: " + ex.Message; }
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

    }
}
