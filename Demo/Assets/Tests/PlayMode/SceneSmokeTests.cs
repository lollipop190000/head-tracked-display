using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using HeadTracked.Display;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HeadTracked.Demo.Tests
{
    public sealed class SceneSmokeTests
    {
        [UnityTest]
        public IEnumerator RealismStudyUsesPbrMapsAndPreservesSubmeshMaterials()
        {
            yield return SceneManager.LoadSceneAsync("HeadTrackedDemo", LoadSceneMode.Single);
            yield return null;
            var demo = Object.FindFirstObjectByType<DemoBootstrap>();
            var original = Resources.Load<GameObject>("Models/pottedPlant").GetComponentsInChildren<Renderer>();
            var placed = GameObject.Find("Plant, behind chair").GetComponentsInChildren<Renderer>();
            for (int r = 0; r < original.Length; r++)
            {
                Assert.That(placed[r].sharedMaterials.Length, Is.EqualTo(original[r].sharedMaterials.Length));
                for (int m = 0; m < original[r].sharedMaterials.Length; m++)
                {
                    var source = original[r].sharedMaterials[m];
                    var result = placed[r].sharedMaterials[m];
                    if (source.HasProperty("_Color")) Assert.That(result.color, Is.EqualTo(source.color));
                    if (source.shader.name.StartsWith("Universal")) Assert.That(result, Is.SameAs(source));
                }
            }
            demo.UseMaterialStudy();
            var vase = GameObject.Find("Ceramic vase, measured surface comparison");
            Assert.That(ModelBounds(vase).size.y, Is.EqualTo(.40f).Within(.001f));
            Assert.That(ModelBounds(vase).min.y, Is.EqualTo(-.18f).Within(.001f));
            var material = vase.GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            foreach (string map in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                Assert.That(material.GetTexture(map), Is.Not.Null, map);
            Assert.That(RenderSettings.customReflectionTexture, Is.Not.Null);
            Assert.That(GameObject.Find("Brushed brass sphere"), Is.Not.Null);
            var brass = GameObject.Find("Brushed brass sphere").GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
            Assert.That(Vector4.Distance(brass, new Color(.64f, .44f, .18f)), Is.LessThan(1e-5f));
            yield return null;
            string path = System.Environment.GetEnvironmentVariable("HEADTRACK_CAPTURE_REALISM");
            if (!string.IsNullOrEmpty(path))
            {
                var camera = Camera.main;
                var target = new RenderTexture(1280, 800, 24) { antiAliasing = 4 };
                var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
                var active = RenderTexture.active;
                try
                {
                    camera.targetTexture = target;
                    camera.GetComponent<HeadTrackedDisplay>().Calibration.screenWidth = .34f;
                    camera.gameObject.SendMessage("LateUpdate");
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(path, image.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = null;
                    RenderTexture.active = active;
                    Object.Destroy(image); Object.Destroy(target);
                }
            }
        }

        [UnityTest]
        public IEnumerator DepthPresetsChangePlacementWithoutResizingOrRotatingModels()
        {
            yield return SceneManager.LoadSceneAsync("HeadTrackedDemo", LoadSceneMode.Single);
            yield return null;
            var model = GameObject.Find("Plant, in front of screen").transform;
            var renderer = model.GetComponentInChildren<Renderer>();
            Vector3 scale = model.localScale;
            Quaternion rotation = model.rotation;
            float height = renderer.bounds.size.y;
            var demo = Object.FindFirstObjectByType<DemoBootstrap>();
            demo.UseDepthPreset(true);
            Assert.That(renderer.bounds.center.z, Is.EqualTo(-.18f).Within(.001f));
            Assert.That(renderer.bounds.size.y, Is.EqualTo(height).Within(.001f));
            Assert.That(Vector3.Distance(model.localScale, scale), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(model.rotation, rotation), Is.LessThan(.001f));
            demo.UseDepthPreset(false);
            Assert.That(renderer.bounds.center.z, Is.EqualTo(-.06f).Within(.001f));
            Assert.That(ModelBounds(GameObject.Find("Chair, distant")).center.z,
                Is.EqualTo(3f).Within(.001f));
            Assert.That(ModelBounds(GameObject.Find("Plant, distant")).center.z,
                Is.EqualTo(6f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator ModelsRenderOnBothSidesOfThePhysicalScreen()
        {
            yield return SceneManager.LoadSceneAsync("HeadTrackedDemo", LoadSceneMode.Single);
            yield return null;

            var front = GameObject.Find("Bear, in front of screen");
            var protruding = GameObject.Find("Plant, in front of screen");
            var chair = GameObject.Find("Chair, behind screen");
            var plant = GameObject.Find("Plant, behind chair");
            Assert.That(front, Is.Not.Null);
            Assert.That(protruding, Is.Not.Null);
            Assert.That(chair, Is.Not.Null);
            Assert.That(plant, Is.Not.Null);
            Assert.That(front.GetComponentInChildren<MeshFilter>(), Is.Not.Null);
            Assert.That(protruding.GetComponentInChildren<MeshFilter>(), Is.Not.Null);
            Assert.That(chair.GetComponentInChildren<MeshFilter>(), Is.Not.Null);
            Assert.That(plant.GetComponentInChildren<MeshFilter>(), Is.Not.Null);
            Assert.That(front.GetComponentInChildren<Renderer>().bounds.center.z, Is.LessThan(0f));
            Assert.That(protruding.GetComponentInChildren<Renderer>().bounds.center.z, Is.EqualTo(-.06f).Within(.001f));
            Assert.That(chair.GetComponentInChildren<Renderer>().bounds.center.z, Is.GreaterThan(0f));
            Assert.That(plant.GetComponentInChildren<Renderer>().bounds.center.z, Is.GreaterThan(0f));
            Assert.That(front.GetComponentInChildren<HeadTrackedDisplay>(), Is.Null);
            Assert.That(protruding.GetComponentInChildren<HeadTrackedDisplay>(), Is.Null);
            Assert.That(chair.GetComponentInChildren<HeadTrackedDisplay>(), Is.Null);
            Assert.That(plant.GetComponentInChildren<HeadTrackedDisplay>(), Is.Null);
            Assert.That(Camera.main.GetComponent<HeadTrackedDisplay>(), Is.Not.Null);
            Vector3 protrudingViewport = Camera.main.WorldToViewportPoint(
                protruding.GetComponentInChildren<Renderer>().bounds.center);
            Assert.That(protrudingViewport.z, Is.GreaterThan(0f));
            Assert.That(protrudingViewport.x, Is.InRange(0f, 1f));
            Assert.That(protrudingViewport.y, Is.InRange(0f, 1f));
            foreach (string name in new[] { "Chair, distant", "Plant, distant" })
            {
                var distant = GameObject.Find(name);
                Assert.That(distant, Is.Not.Null);
                Assert.That(distant.GetComponentInChildren<MeshFilter>(), Is.Not.Null);
                Vector3 viewport = Camera.main.WorldToViewportPoint(ModelBounds(distant).center);
                Assert.That(viewport.x, Is.InRange(0f, 1f));
                Assert.That(viewport.y, Is.InRange(0f, 1f));
                Assert.That(viewport.z, Is.InRange(Camera.main.nearClipPlane, Camera.main.farClipPlane));
            }

            string screenshotPath = System.Environment.GetEnvironmentVariable("HEADTRACK_CAPTURE");
            if (!string.IsNullOrEmpty(screenshotPath))
            {
                const int width = 1280, height = 720;
                var camera = Camera.main;
                var target = new RenderTexture(width, height, 24);
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(screenshotPath, image.EncodeToPNG());
                camera.targetTexture = null;
                RenderTexture.active = null;
                Object.Destroy(image);
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator FixationLayoutsKeepTheTargetFixedAndReduceScreenDisplacement()
        {
            yield return SceneManager.LoadSceneAsync("HeadTrackedDemo", LoadSceneMode.Single);
            yield return null;
            var demo = Object.FindFirstObjectByType<DemoBootstrap>();
            var plant = GameObject.Find("Plant, behind chair");
            var chair = GameObject.Find("Chair, distant");
            var floor = GameObject.Find("Floor");
            var camera = Camera.main;
            foreach (float depth in new[] { 0f, .05f, .15f, .30f })
            {
                demo.UseFixationTest(depth);
                var bounds = ModelBounds(plant);
                Assert.That(Vector3.Distance(bounds.center, new Vector3(0f, 0f, depth)), Is.LessThan(.001f));
                Assert.That(bounds.size.y, Is.EqualTo(.15f).Within(.001f));
                Assert.That(plant.activeSelf, Is.True);
                Assert.That(chair.activeSelf, Is.False);
                Assert.That(floor.activeSelf, Is.False);
                Vector3 position = plant.transform.position;
                Quaternion rotation = plant.transform.rotation;
                camera.transform.SetPositionAndRotation(new Vector3(-.1f, 0f, -.6f), Quaternion.identity);
                camera.projectionMatrix = OffAxisProjection.Calculate(camera.transform.position, .53f, .30f, .025f, 20f);
                float leftX = camera.WorldToViewportPoint(bounds.center).x;
                camera.transform.position = new Vector3(.1f, 0f, -.6f);
                camera.projectionMatrix = OffAxisProjection.Calculate(camera.transform.position, .53f, .30f, .025f, 20f);
                float rightX = camera.WorldToViewportPoint(bounds.center).x;
                Assert.That((rightX - leftX) * .53f,
                    Is.EqualTo(.2f * depth / (.6f + depth)).Within(.0001f));
                Assert.That(plant.transform.position, Is.EqualTo(position));
                Assert.That(plant.transform.rotation, Is.EqualTo(rotation));
            }
            demo.ResetModelLayout();
            Assert.That(chair.activeSelf, Is.True);
            Assert.That(floor.activeSelf, Is.True);
            Assert.That(ModelBounds(chair).center.z, Is.EqualTo(3f).Within(.001f));
            Assert.That(ModelBounds(plant).center.x, Is.EqualTo(.20f).Within(.001f));
            Assert.That(ModelBounds(plant).center.z, Is.EqualTo(.16f).Within(.001f));
            Assert.That(ModelBounds(plant).size.y, Is.EqualTo(.22f).Within(.001f));
        }

        private static Bounds ModelBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        [UnityTest]
        public IEnumerator DepthComparisonUsesEqualSizePlantsAndRestoresOriginalLayout()
        {
            yield return SceneManager.LoadSceneAsync("HeadTrackedDemo", LoadSceneMode.Single);
            yield return null;
            var demo = Object.FindFirstObjectByType<DemoBootstrap>();
            var plants = new[] { GameObject.Find("Plant, behind chair"), GameObject.Find("Plant, in front of screen"),
                GameObject.Find("Plant, distant") };
            var floor = GameObject.Find("Floor");
            demo.UseDepthMotionTest();
            float[] depths = { .05f, .3f, 1f };
            for (int i = 0; i < plants.Length; i++)
            {
                var bounds = ModelBounds(plants[i]);
                Assert.That(bounds.size.y, Is.EqualTo(.15f).Within(.001f));
                Assert.That(bounds.center.y, Is.EqualTo(0f).Within(.001f));
                Assert.That(bounds.center.z, Is.EqualTo(depths[i]).Within(.001f));
                Assert.That(plants[i].activeSelf, Is.True);
                Vector3 viewport = Camera.main.WorldToViewportPoint(bounds.center);
                Assert.That(viewport.x, Is.InRange(0f, 1f));
            }
            Assert.That(floor.activeSelf, Is.False);
            demo.ResetModelLayout();
            Assert.That(ModelBounds(plants[2]).center.z, Is.EqualTo(6f).Within(.001f));
            Assert.That(ModelBounds(plants[2]).size.y, Is.EqualTo(1.4f).Within(.001f));
            Assert.That(floor.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator PythonWirePreservesPoseAndGazeAndRejectsStaleOrMismatchedFits()
        {
            yield return SceneManager.LoadSceneAsync("HeadTrackedDemo", LoadSceneMode.Single);
            yield return null;
            var source = Object.FindFirstObjectByType<PythonBridgeSource>();
            var display = Camera.main.GetComponent<HeadTrackedDisplay>();
            display.Calibration.webcamPosition = Vector3.zero;
            display.Calibration.webcamEulerDegrees = Vector3.zero;
            display.Calibration.mirrorImageX = false;
            display.Calibration.useRigidFacePose = true;
            display.Calibration.minimumEyeDistanceFromScreen = .25f;
            display.Calibration.maximumEyeDistanceFromScreen = 1.5f;
            int revision = (int)typeof(PythonBridgeSource).GetField("calibrationRevision", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(source);
            using (var client = new TcpClient())
            {
                var connecting = client.ConnectAsync("127.0.0.1", source.Port);
                float timeout = Time.realtimeSinceStartup + 5f;
                while (!connecting.IsCompleted && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(connecting.IsCompleted && !connecting.IsFaulted, Is.True);
                var stream = client.GetStream();
                foreach (int variant in new[] { 0, 1, 2 })
                {
                    int sentRevision = variant == 1 ? revision + 1 : revision;
                    int age = variant == 2 ? 500 : 5;
                    string message = "{\"found\":true,\"leftX\":0.45,\"rightX\":0.55,\"leftY\":0.5,\"rightY\":0.5," +
                        "\"width\":640,\"height\":480,\"confidence\":1,\"poseSupported\":true,\"poseValid\":true," +
                        "\"poseEyeX\":0.1,\"poseEyeY\":-0.02,\"poseEyeZ\":0.6,\"headYawDegrees\":30," +
                        "\"gazeValid\":true,\"irisHorizontal\":0.1,\"irisVertical\":-0.05,\"poseConfidence\":0.9," +
                        "\"inferenceMs\":3,\"poseMs\":0.3,\"trackerFps\":30,\"sequence\":" + (variant + 101) +
                        ",\"frameAgeMs\":" + age + ",\"calibrationRevision\":" + sentRevision + "}\n";
                    byte[] bytes = Encoding.UTF8.GetBytes(message);
                    stream.Write(bytes, 0, bytes.Length);
                    timeout = Time.realtimeSinceStartup + 5f;
                    while (display.LatestObservation.sequence != variant + 101 && Time.realtimeSinceStartup < timeout)
                        yield return null;
                    Assert.That(display.LatestObservation.sequence, Is.EqualTo(variant + 101));
                    Assert.That(display.IsTracking, Is.EqualTo(variant == 0));
                    if (variant == 0)
                    {
                        Assert.That(Vector3.Distance(display.EstimatedEyePositionMeters, new Vector3(.1f, .02f, -.6f)), Is.LessThan(.001f));
                        Assert.That(display.LatestObservation.headEulerDegrees.y, Is.EqualTo(30f));
                        Assert.That(display.LatestObservation.irisOffset.x, Is.EqualTo(.1f));
                        Assert.That(display.ResultAgeMilliseconds, Is.GreaterThanOrEqualTo(5f));
                    }
                }
            }
        }
    }
}
