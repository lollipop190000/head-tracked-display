using NUnit.Framework;
using UnityEngine;
using System.Reflection;

namespace HeadTracked.Display.Tests
{
    public sealed class ScreenAspectTests
    {
        [TestCase(2880, 1800)]
        [TestCase(1920, 1080)]
        public void MeasuredWidthAndRenderAspectProduceEqualPhysicalScaleOnBothAxes(int width, int height)
        {
            var item = new GameObject("Physical aspect test");
            var target = new RenderTexture(width, height, 0);
            try
            {
                var camera = item.AddComponent<Camera>();
                camera.targetTexture = target;
                var display = item.AddComponent<HeadTrackedDisplay>();
                display.Calibration.screenWidth = .53f;
                display.Calibration.screenHeight = .30f;
                display.Calibration.deriveScreenHeightFromResolution = true;
                var update = typeof(HeadTrackedDisplay).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                update.Invoke(display, null);
                float physicalHeight = .53f * height / width;
                Assert.That(display.Calibration.screenHeight, Is.EqualTo(physicalHeight).Within(1e-6));
                var left = camera.WorldToViewportPoint(new Vector3(-.05f, 0, .1f));
                var right = camera.WorldToViewportPoint(new Vector3(.05f, 0, .1f));
                var down = camera.WorldToViewportPoint(new Vector3(0, -.05f, .1f));
                var up = camera.WorldToViewportPoint(new Vector3(0, .05f, .1f));
                Assert.That((right.x - left.x) * .53f, Is.EqualTo((up.y - down.y) * physicalHeight).Within(1e-5));
                display.Calibration.deriveScreenHeightFromResolution = false;
                display.Calibration.screenHeight = .30f;
                update.Invoke(display, null);
                Assert.That(display.Calibration.screenHeight, Is.EqualTo(.30f));
            }
            finally { Object.DestroyImmediate(item); Object.DestroyImmediate(target); }
        }
    }
}
