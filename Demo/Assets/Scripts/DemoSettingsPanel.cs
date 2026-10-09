using System;
using System.IO;
using HeadTracked.Display;
using UnityEngine;

namespace HeadTracked.Demo
{
    public sealed partial class DemoBootstrap
    {
        private bool showDisplaySize, screenSizeDeriveHeight;
        private string screenSizeNotice = "";
        private Vector2 screenSizeScroll;
        private bool showScreenRuler;

        private void DrawBillboardSettings()
        {
            if (!billboardActive) return;
            var s = billboard.Settings;
            GUILayout.Label("Fixed 3D chamber + screen-plane frame; tracking is unchanged.");
            GUILayout.Label($"Rendering: {renderFps:F0} FPS / {(renderFps > 0f ? 1000f / renderFps : 0f):F1} ms per frame (includes frame pacing)");
            bool stable = GUILayout.Toggle(stableBillboardEdges, "Stable edges (TAA; off = 4x MSAA comparison)");
            bool contactAO = GUILayout.Toggle(billboardContactAO, "Contact AO (extra cost; off recommended)");
            GUILayout.Label($"Render scale: {billboardRenderScale * 100f:F0}% / lower for speed, higher for detail");
            float scale = GUILayout.HorizontalSlider(billboardRenderScale, .65f, 1f);
            if (stable != stableBillboardEdges || contactAO != billboardContactAO || scale != billboardRenderScale)
                SetBillboardRendering(stable, contactAO, scale);
            s.showFrame = GUILayout.Toggle(s.showFrame, "Frame / dark surround (off for A/B comparison)");
            s.showDepthReferences = GUILayout.Toggle(s.showDepthReferences, "Depth reference blocks");
            s.animate = GUILayout.Toggle(s.animate, "Animate in/out (off for tracking stability test)");
            GUILayout.Label($"Chamber depth: {s.boxDepth * 100f:F1} cm");
            s.boxDepth = GUILayout.HorizontalSlider(s.boxDepth, .10f, .50f);
            GUILayout.Label($"Model height: {s.contentHeight * 100f:F1} cm (miniature)");
            s.contentHeight = GUILayout.HorizontalSlider(s.contentHeight, .03f, .12f);
            GUILayout.Label($"Horizontal position: {s.horizontalOffsetFraction:F2} of opening width");
            s.horizontalOffsetFraction = GUILayout.HorizontalSlider(s.horizontalOffsetFraction, -.4f, .4f);
            if (s.animate)
            {
                GUILayout.Label($"Maximum protrusion: {s.animationProtrusion * 100f:F1} cm");
                s.animationProtrusion = GUILayout.HorizontalSlider(s.animationProtrusion, .01f, .06f);
            }
            else
            {
                GUILayout.Label($"Model centre depth: {s.staticDepth * 100f:F1} cm (+ behind / - in front)");
                s.staticDepth = GUILayout.HorizontalSlider(s.staticDepth, -.06f, .18f);
            }
            GUILayout.Label("F1: clean view. Apply/save screen size separately; physical bezel still clips.");
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

    }
}
