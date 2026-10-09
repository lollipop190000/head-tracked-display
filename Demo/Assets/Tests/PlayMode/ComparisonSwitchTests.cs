using System.Collections;
using System.Collections.Generic;
using HeadTracked.Display;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;

namespace HeadTracked.Demo.Tests
{
    public sealed class ComparisonSwitchTests
    {
        [UnityTest]
        public IEnumerator AllEightSwitchCombinationsRetainContentCalibrationAndRenderQuality()
        {
            yield return SceneManager.LoadSceneAsync("HeadTrackedDemo", LoadSceneMode.Single);
            yield return null;
            var demo = Object.FindFirstObjectByType<DemoBootstrap>();
            demo.SetHeadTrackingEnabled(true);
            demo.SetBillboardEffectEnabled(true);
            demo.SetTexturesEnabled(true);
            var rig = Object.FindFirstObjectByType<BillboardIllusionController>();
            var content = rig.ContentInstance;
            var camera = Camera.main;
            var display = camera.GetComponent<HeadTrackedDisplay>();
            var quality = Resources.Load<RenderQualitySettings>("Realism/render_quality");
            var transforms = new Dictionary<Transform, Matrix4x4>();
            foreach (var item in content.GetComponentsInChildren<Transform>(true)) transforms.Add(item, item.localToWorldMatrix);
            var materials = new Dictionary<Renderer, Material[]>();
            foreach (var renderer in content.GetComponentsInChildren<Renderer>()) materials.Add(renderer, renderer.sharedMaterials);
            string calibration = JsonUtility.ToJson(display.Calibration);
            float scale = quality.pipeline.renderScale;
            int msaa = quality.pipeline.msaaSampleCount;
            var aa = camera.GetUniversalAdditionalCameraData().antialiasing;
            Vector3 eye = display.EyePositionMeters;
            int lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            for (int mask = 0; mask < 8; mask++)
            {
                demo.SetHeadTrackingEnabled((mask & 1) != 0);
                demo.SetBillboardEffectEnabled((mask & 2) != 0);
                demo.SetTexturesEnabled((mask & 4) != 0);
                yield return null;
                Assert.That(rig.ContentInstance, Is.SameAs(content), "Switches must not clone or replace the target.");
                Assert.That(content.activeInHierarchy, Is.True);
                foreach (var pair in transforms) Assert.That(pair.Key.localToWorldMatrix, Is.EqualTo(pair.Value));
                Assert.That(Camera.main, Is.SameAs(camera));
                Assert.That(Vector3.Distance(display.EyePositionMeters, eye), Is.LessThan(1e-6f));
                Assert.That(JsonUtility.ToJson(display.Calibration), Is.EqualTo(calibration));
                Assert.That(quality.pipeline.renderScale, Is.EqualTo(scale));
                Assert.That(quality.pipeline.msaaSampleCount, Is.EqualTo(msaa));
                Assert.That(camera.GetUniversalAdditionalCameraData().antialiasing, Is.EqualTo(aa));
                Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length, Is.EqualTo(lights));
                Assert.That(rig.RigRoot.Find("Recessed chamber").gameObject.activeSelf, Is.EqualTo((mask & 2) != 0));
                Assert.That(rig.RigRoot.Find("Screen-plane frame and matte surround").gameObject.activeSelf, Is.EqualTo((mask & 2) != 0));
                foreach (var pair in materials)
                {
                    Material[] current = pair.Key.sharedMaterials;
                    Assert.That(current.Length, Is.EqualTo(pair.Value.Length));
                    for (int slot = 0; slot < current.Length; slot++)
                    {
                        if ((mask & 4) != 0) Assert.That(current[slot], Is.SameAs(pair.Value[slot]));
                        else if (current[slot].HasProperty("_BaseMap")) Assert.That(current[slot].GetTexture("_BaseMap"), Is.Null);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator TrackingOffHoldsViewpointWhileFreshMeasurementsContinueAndResume()
        {
            yield return SceneManager.LoadSceneAsync("HeadTrackedDemo", LoadSceneMode.Single);
            yield return null;
            var display = Camera.main.GetComponent<HeadTrackedDisplay>();
            var source = display.gameObject.AddComponent<ComparisonTestSource>();
            display.Calibration.webcamPosition = Vector3.zero;
            display.Calibration.webcamEulerDegrees = Vector3.zero;
            display.Calibration.mirrorImageX = false;
            display.Calibration.useRigidFacePose = true;
            display.Calibration.viewingDistance = new ViewingDistanceCalibration();
            display.Configure(display.ScreenPlane, source);
            display.TrackingEnabled = true;
            source.eye = new Vector3(.02f, 0f, .6f);
            yield return new WaitForSecondsRealtime(.2f);
            display.TrackingEnabled = false;
            Vector3 held = display.EyePositionMeters;
            Matrix4x4 projection = Camera.main.projectionMatrix;
            source.eye = new Vector3(.12f, -.03f, .8f);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(display.IsTracking, Is.True);
            Assert.That(display.HasFreshEyeEstimate, Is.True);
            Assert.That(display.EstimatedEyePositionMeters.x, Is.EqualTo(.12f).Within(.001f));
            Assert.That(display.EyePositionMeters, Is.EqualTo(held));
            Assert.That(Camera.main.transform.position, Is.EqualTo(held));
            Assert.That(Camera.main.projectionMatrix, Is.EqualTo(projection));
            display.TrackingEnabled = true;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(display.EyePositionMeters.x, Is.EqualTo(.12f).Within(.005f));
            Assert.That(display.EyePositionMeters.z, Is.EqualTo(-.8f).Within(.005f));
        }

        [UnityTest]
        public IEnumerator ReturningFromExperimentsRestoresOneStationaryComparisonAndOriginalMaterials()
        {
            yield return SceneManager.LoadSceneAsync("HeadTrackedDemo", LoadSceneMode.Single);
            yield return null;
            var demo = Object.FindFirstObjectByType<DemoBootstrap>();
            var rig = Object.FindFirstObjectByType<BillboardIllusionController>();
            demo.SetTexturesEnabled(false);
            demo.UseDepthMotionTest();
            Assert.That(demo.IsComparisonScene, Is.False);
            demo.UseComparisonScene();
            demo.SetTexturesEnabled(true);
            yield return null;
            Assert.That(demo.IsComparisonScene, Is.True);
            Assert.That(rig.Settings.animate, Is.False);
            bool hasTexture = false;
            foreach (var renderer in rig.ContentInstance.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                    hasTexture |= material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null;
            Assert.That(hasTexture, Is.True, "Rebuilding after plain-material experiments must recover the imported maps.");
            Vector3 position = rig.ContentInstance.transform.position;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(rig.ContentInstance.transform.position, Is.EqualTo(position));
        }
    }

    public sealed class ComparisonTestSource : HeadObservationSource
    {
        public Vector3 eye;
        public override string Status => "Synthetic comparison test";
        public override bool TryGetLatest(out HeadObservation observation)
        {
            observation = new HeadObservation
            {
                found = true, confidence = 1f, frameWidth = 640, frameHeight = 480,
                leftEye = new Vector2(.45f, .5f), rightEye = new Vector2(.55f, .5f),
                poseSupported = true, poseValid = true, poseConfidence = 1f, poseEyeCamera = eye,
                receivedAtSeconds = Time.realtimeSinceStartupAsDouble
            };
            return true;
        }
    }
}
