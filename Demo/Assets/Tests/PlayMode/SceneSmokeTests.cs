using System.Collections;
using System.IO;
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
    }
}
