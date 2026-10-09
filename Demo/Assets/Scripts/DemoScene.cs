using System;
using System.Collections.Generic;
using System.IO;
using HeadTracked.Display;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeadTracked.Demo
{
    public sealed partial class DemoBootstrap
    {
        private bool isolatePlant;
        private bool depthComparisonActive;

        private readonly List<SceneModel> sceneModels = new List<SceneModel>();
        private Transform roomFloor, backWall;
        private readonly ModelMaterials modelMaterials = new ModelMaterials();
        private bool materialStudyActive, enhancedRendering = true, cleanView;
        private GameObject surfaceComparisons;
        private RenderQualitySettings renderQuality;
        private float reportedScreenWidth, reportedScreenHeight;
        private BillboardIllusionController billboard;
        private bool billboardActive;
        private int billboardModel;
        private BillboardRenderProfile billboardRendering;
        private bool stableBillboardEdges = true, billboardContactAO;
        private float billboardRenderScale = .85f;
        private float fpsElapsed, renderFps;
        private int fpsFrames;
        private readonly List<GameObject> billboardHiddenObjects = new List<GameObject>();
        private readonly List<bool> billboardPreviousVisibility = new List<bool>();

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
            ExitBillboardIllusion();
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
        }

        private void OnDestroy()
        {
            billboardRendering?.Dispose();
            billboardRendering = null;
            if (billboard != null)
            {
                foreach (var material in new[] { billboard.frameMaterial, billboard.surroundMaterial,
                    billboard.chamberMaterial, billboard.defaultContentMaterial })
                    if (material != null) Destroy(material);
                Destroy(billboard.gameObject);
            }
            surfaceAppearance.Dispose();
            if (demoSkin != null) Destroy(demoSkin);
            modelMaterials.Dispose();
        }

        private void EnsureBillboard()
        {
            if (billboard != null) return;
            billboard = new GameObject("Reusable Billboard Illusion Mode").AddComponent<BillboardIllusionController>();
            billboard.enabled = false;
            billboard.frameMaterial = MakeMaterial(new Color(.10f, .18f, .25f));
            billboard.frameMaterial.SetFloat("_Metallic", .65f);
            billboard.surroundMaterial = MakeMaterial(new Color(.003f, .004f, .006f));
            billboard.chamberMaterial = MakeMaterial(new Color(.48f, .55f, .62f));
            billboard.defaultContentMaterial = MakeMaterial(new Color(.78f, .43f, .12f));
            billboard.defaultContentMaterial.SetFloat("_Metallic", .7f);
            billboard.defaultContentMaterial.SetFloat("_Smoothness", .65f);
            billboard.Configure(display);
        }

        public void UseBillboardIllusion()
        {
            if (billboardActive) return;
            surfaceAppearance.Apply(true);
            EnsureBillboard();
            billboardHiddenObjects.Clear(); billboardPreviousVisibility.Clear();
            foreach (var model in sceneModels)
            {
                RememberBillboardVisibility(model.root.gameObject);
                if (model.plinth != null) RememberBillboardVisibility(model.plinth.gameObject);
            }
            RememberBillboardVisibility(roomFloor.gameObject);
            RememberBillboardVisibility(backWall.gameObject);
            RememberBillboardVisibility(surfaceComparisons);
            billboardActive = true;
            billboard.enabled = true;
            if (renderQuality != null)
                billboardRendering = new BillboardRenderProfile(renderQuality, display.GetComponent<Camera>());
            ApplyBillboardRendering();
            SelectBillboardModel(billboardModel);
            RegisterComparisonSurfaces();
            notice = "Billboard Illusion Mode: move your head; the frame and content use the same physical projection.";
        }

        private void RememberBillboardVisibility(GameObject item)
        {
            billboardHiddenObjects.Add(item); billboardPreviousVisibility.Add(item.activeSelf);
            item.SetActive(false);
        }

        public void ExitBillboardIllusion()
        {
            if (!billboardActive) return;
            billboardRendering?.Dispose();
            billboardRendering = null;
            billboard.enabled = false;
            for (int i = 0; i < billboardHiddenObjects.Count; i++)
                if (billboardHiddenObjects[i] != null) billboardHiddenObjects[i].SetActive(billboardPreviousVisibility[i]);
            billboardHiddenObjects.Clear(); billboardPreviousVisibility.Clear();
            billboardActive = false;
            comparisonSceneActive = false;
            RegisterComparisonSurfaces();
        }

        private void SelectBillboardModel(int selection)
        {
            surfaceAppearance.Apply(true);
            billboardModel = Mathf.Clamp(selection, 0, 3);
            int index = billboardModel == 1 ? 0 : billboardModel == 2 ? 1 : 6;
            billboard.SetContent(billboardModel == 0 ? null : sceneModels[index].root.gameObject);
            RegisterComparisonSurfaces();
            billboardRendering?.ResetHistory();
        }

        public void SetBillboardRendering(bool stableEdges, bool contactAO, float renderScale)
        {
            stableBillboardEdges = stableEdges;
            billboardContactAO = contactAO;
            billboardRenderScale = float.IsNaN(renderScale) || float.IsInfinity(renderScale) ? .85f : Mathf.Clamp(renderScale, .65f, 1f);
            ApplyBillboardRendering();
        }

        private void ApplyBillboardRendering()
        {
            if (!billboardActive || billboardRendering == null) return;
            float viewingDistance = Mathf.Max(display.Calibration.maximumEyeDistanceFromScreen, -display.EyePositionMeters.z);
            float depth = Mathf.Max(billboard.Settings.boxDepth, billboard.Settings.staticDepth);
            billboardRendering.Apply(stableBillboardEdges, billboardContactAO, billboardRenderScale, viewingDistance + depth + .3f);
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
            ExitBillboardIllusion();
            if (sceneModels.Count < 4) return;
            float[] depths = deep ? new[] { -.04f, .29f, .42f, -.18f } : new[] { -.025f, .10f, .16f, -.06f };
            for (int i = 0; i < depths.Length; i++) sceneModels[i].depthCm = (depths[i] * 100f).ToString("F1");
            ApplyModelLayout();
        }

        // Compare a fixed target near eye level, independent of the floor and surrounding models.
        public void UseFixationTest(float depthMeters)
        {
            ExitBillboardIllusion();
            if (sceneModels.Count < 3) return;
            var model = sceneModels[2];
            model.xCm = "0";
            model.yCm = "0";
            model.heightCm = "15";
            model.depthCm = (depthMeters * 100f).ToString("F1");
            if (!ApplyModelLayout()) return;
            SetPlantIsolation(true);
        }

        public void UseDepthMotionTest()
        {
            ExitBillboardIllusion();
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
            ExitBillboardIllusion();
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
            ExitBillboardIllusion();
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
            if (billboard != null)
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new BillboardIllusionSettings()), billboard.Settings);
            billboardModel = 0;
            enhancedRendering = true;
            stableBillboardEdges = true; billboardContactAO = false; billboardRenderScale = .85f;
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
            showSettings = showGazePoint = false;
            cleanView = showDisplaySize = showScreenRuler = false;
            settingsScroll = Vector2.zero;
            CopyFieldsFromCalibration();
            display.TrackingEnabled = true;
            texturesEnabled = billboardEffectEnabled = true;
            surfaceAppearance.Apply(true);
            UseComparisonScene();
            SaveDemoPreferences();
            notice = "Comparison defaults restored; measured physical calibration kept.";
        }

        public void SetPlantIsolation(bool isolate)
        {
            ExitBillboardIllusion();
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
                if (saved.billboardSettings != null)
                {
                    EnsureBillboard();
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(saved.billboardSettings), billboard.Settings);
                    billboard.Settings.Validate();
                    billboardModel = Mathf.Clamp(saved.billboardModel, 0, 3);
                }
                if (saved.hasBillboardRendering)
                    SetBillboardRendering(saved.stableBillboardEdges, saved.billboardContactAO, saved.billboardRenderScale);
                if (saved.billboardMode) UseBillboardIllusion();
            }
            catch (Exception ex) { notice = "Could not load model layout: " + ex.Message; }
        }

    }
}
