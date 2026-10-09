using System;
using System.IO;
using HeadTracked.Display;
using UnityEngine;

namespace HeadTracked.Demo
{
    public sealed partial class DemoBootstrap
    {
        private readonly SurfaceAppearanceComparison surfaceAppearance = new SurfaceAppearanceComparison();
        private bool comparisonSceneActive, texturesEnabled = true, billboardEffectEnabled = true;
        private bool showInitialSetup, setupCompleted;

        public bool IsComparisonScene => comparisonSceneActive;
        public bool TexturesEnabled => texturesEnabled;
        public bool BillboardEffectEnabled => billboardEffectEnabled;

        /// <summary>Explicitly return to the single stationary A/B scene. Switches never call this.</summary>
        public void UseComparisonScene()
        {
            surfaceAppearance.Dispose();
            ResetModelLayout();
            EnsureBillboard();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new BillboardIllusionSettings
            {
                contentHeight = .16f, staticDepth = -.02f, boxDepth = .30f,
                horizontalOffsetFraction = .24f, animate = false
            }), billboard.Settings);
            billboardModel = 3;
            UseBillboardIllusion();
            comparisonSceneActive = true;
            // A textured floor and ceramic target demonstrate surfaces in the same fixed chamber.
            surfaceAppearance.Dispose();
            var floor = billboard.RigRoot.Find("Recessed chamber/Chamber floor");
            var wood = Resources.Load<Material>("Realism/wood");
            if (floor != null && wood != null) floor.GetComponent<Renderer>().sharedMaterial = wood;
            billboard.EffectEnabled = billboardEffectEnabled;
            RegisterComparisonSurfaces();
            notice = "Fixed comparison scene. Each ON/OFF changes only its named feature.";
        }

        public void SetHeadTrackingEnabled(bool value)
        {
            display.TrackingEnabled = value;
            notice = value ? "Head tracking ON: the camera follows the eye position."
                : "Head tracking OFF: the current viewpoint is held; webcam measurements continue.";
        }

        public void SetBillboardEffectEnabled(bool value)
        {
            billboardEffectEnabled = value;
            if (billboard != null) billboard.EffectEnabled = value;
            notice = value ? "Billboard effect ON: frame, chamber and depth references visible."
                : "Billboard effect OFF: the same model, camera, lighting and quality remain.";
        }

        public void SetTexturesEnabled(bool value)
        {
            texturesEnabled = value;
            surfaceAppearance.Apply(value);
            notice = value ? "Surface detail ON: original material slots and textures restored."
                : "Surface detail OFF: plain matte materials; model placement and lighting retained.";
        }

        private void RegisterComparisonSurfaces()
        {
            foreach (var model in sceneModels) surfaceAppearance.Register(model.root.gameObject);
            surfaceAppearance.Register(roomFloor != null ? roomFloor.gameObject : null);
            surfaceAppearance.Register(backWall != null ? backWall.gameObject : null);
            surfaceAppearance.Register(surfaceComparisons);
            if (billboard != null && billboard.RigRoot != null)
                surfaceAppearance.Register(billboard.RigRoot.gameObject);
            surfaceAppearance.Apply(texturesEnabled);
        }

        private string PreferencesPath => Path.Combine(Application.persistentDataPath, "demo_preferences.json");

        private void LoadDemoPreferences()
        {
            try
            {
                if (File.Exists(PreferencesPath))
                {
                    var saved = JsonUtility.FromJson<DemoPreferences>(File.ReadAllText(PreferencesPath));
                    if (saved != null && saved.version == 1)
                    {
                        setupCompleted = saved.setupCompleted;
                        SetHeadTrackingEnabled(saved.headTracking);
                        SetBillboardEffectEnabled(saved.billboardEffect);
                        SetTexturesEnabled(saved.textures);
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning("Demo preferences unavailable: " + ex.Message); }
            showInitialSetup = !setupCompleted;
            if (showInitialSetup)
            {
                OpenDisplaySize();
                showDisplaySize = false;
                // First-time setup asks for both measured dimensions, without silently using a hint.
                screenSizeDeriveHeight = false;
            }
        }

        private bool SaveDemoPreferences()
        {
            try
            {
                File.WriteAllText(PreferencesPath, JsonUtility.ToJson(new DemoPreferences
                {
                    setupCompleted = setupCompleted, headTracking = display.TrackingEnabled,
                    billboardEffect = billboardEffectEnabled, textures = texturesEnabled
                }, true));
                notice = "Comparison preferences saved; physical calibration and experimental layouts are separate.";
                return true;
            }
            catch (Exception ex) { notice = "Could not save comparison preferences: " + ex.Message; return false; }
        }

        private void CompleteInitialSetup()
        {
            if (!float.TryParse(eyeDistanceCm, out float distance) || !IsFinite(distance) || distance < 20f || distance > 200f)
            { screenSizeNotice = "Enter a measured viewing distance between 20 and 200 cm."; return; }
            float previousDistance = display.Calibration.referenceEyeDistanceFromScreen;
            display.Calibration.referenceEyeDistanceFromScreen = distance * .01f;
            // ApplyDisplaySize validates and saves before changing the projection.
            showDisplaySize = true;
            ApplyDisplaySize();
            if (showDisplaySize)
            {
                display.Calibration.referenceEyeDistanceFromScreen = previousDistance;
                showDisplaySize = false;
                return;
            }
            if (display.IsTracking && display.CaptureReference()) SaveSettings();
            setupCompleted = true;
            if (!SaveDemoPreferences())
            { setupCompleted = false; screenSizeNotice = notice; return; }
            showInitialSetup = false;
            notice = "Setup saved. Compare ON/OFF while moving your head. F2 opens detailed settings.";
        }

        [Serializable]
        private sealed class DemoPreferences
        {
            public int version = 1;
            public bool setupCompleted;
            public bool headTracking = true, billboardEffect = true, textures = true;
        }
    }
}
