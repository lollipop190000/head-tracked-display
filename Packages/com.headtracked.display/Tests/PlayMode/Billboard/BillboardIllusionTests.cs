using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HeadTracked.Display.Tests
{
    public sealed class BillboardIllusionTests
    {
        [UnityTest]
        public IEnumerator RigFollowsPhysicalScreenAndRetainsSourceModelAndCalibration()
        {
            var plane = new GameObject("Rotated physical screen");
            plane.transform.SetPositionAndRotation(new Vector3(2, 1, 3), Quaternion.Euler(10, 20, 5));
            var cameraObject = new GameObject("Independent package camera");
            cameraObject.AddComponent<Camera>();
            var display = cameraObject.AddComponent<HeadTrackedDisplay>();
            display.enabled = false;
            display.Configure(plane.transform, null);
            display.Calibration.screenWidth = .345f; display.Calibration.screenHeight = .215f;
            string before = JsonUtility.ToJson(display.Calibration);
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.position = new Vector3(8, 9, 10);
            model.transform.localScale = new Vector3(2, 3, 1);
            var originalPosition = model.transform.position;
            var originalScale = model.transform.localScale;
            var material = model.GetComponent<Renderer>().sharedMaterial;
            var controllerObject = new GameObject("Independent package billboard");
            var controller = controllerObject.AddComponent<BillboardIllusionController>();
            try
            {
                controller.Configure(display, model);
                yield return null;
                Assert.That(controller.RigRoot.parent, Is.SameAs(plane.transform));
                Assert.That(controller.RigRoot.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(controller.RigRoot.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(controller.ContentInstance.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(controller.ContentInstance.transform.localScale.y, Is.EqualTo(.08f).Within(1e-5));
                controller.ContentInstance.transform.hasChanged = false;
                controller.Refresh();
                Assert.That(controller.ContentInstance.transform.hasChanged, Is.False, "Static content must not invalidate transforms every frame.");
                Assert.That(model.transform.position, Is.EqualTo(originalPosition));
                Assert.That(model.transform.localScale, Is.EqualTo(originalScale));
                Assert.That(JsonUtility.ToJson(display.Calibration), Is.EqualTo(before));
                Vector3 fixedContentPosition = controller.ContentInstance.transform.position;
                cameraObject.transform.position += new Vector3(.1f, .03f, -.05f);
                yield return null;
                Assert.That(Vector3.Distance(controller.ContentInstance.transform.position, fixedContentPosition), Is.LessThan(1e-6));
                display.Calibration.screenWidth = .50f;
                controller.Refresh();
                float openingWidth = .50f * controller.Settings.openingWidthFraction;
                Assert.That(controller.RigRoot.Find("Recessed chamber/Chamber back").localScale.x,
                    Is.EqualTo(openingWidth + 2 * controller.Settings.wallThickness).Within(1e-6));
                controller.enabled = false;
                Assert.That(controller.RigRoot.gameObject.activeSelf, Is.False);
                controller.enabled = true;
                Assert.That(controller.RigRoot.gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.Destroy(controllerObject); Object.Destroy(model);
                Object.Destroy(cameraObject); Object.Destroy(plane);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChamberFacesNeverOverlapTheCoplanarInnerRim()
        {
            var cameraObject = new GameObject("Seam regression display");
            cameraObject.AddComponent<Camera>();
            var display = cameraObject.AddComponent<HeadTrackedDisplay>();
            display.enabled = false;
            var rigObject = new GameObject("Seam regression billboard");
            var rig = rigObject.AddComponent<BillboardIllusionController>();
            try
            {
                rig.Configure(display);
                foreach (float thickness in new[] { .002f, .006f, .03f })
                {
                    rig.Settings.wallThickness = thickness;
                    rig.Settings.boxDepth = .08f;
                    rig.Refresh();
                    string[] rimNames = { "bottom", "top", "left", "right" };
                    string[] wallNames = { "floor", "ceiling", "left", "right" };
                    for (int i = 0; i < 4; i++)
                    {
                        var rim = rig.RigRoot.Find("Screen-plane frame and matte surround/Frame " + rimNames[i]);
                        var wall = rig.RigRoot.Find("Recessed chamber/Chamber " + wallNames[i]);
                        float rimBack = rim.localPosition.z + rim.localScale.z * .5f;
                        float wallFront = wall.localPosition.z - wall.localScale.z * .5f;
                        Assert.That(wallFront - rimBack, Is.GreaterThan(.0004f), "Coplanar faces need disjoint depth intervals.");
                        Assert.That(wall.localPosition.z + wall.localScale.z * .5f,
                            Is.EqualTo(rig.Settings.boxDepth).Within(1e-6), "Chamber must still meet its back wall.");
                    }
                }
            }
            finally { Object.Destroy(rigObject); Object.Destroy(cameraObject); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DepthTestingLetsContentPassInFrontOfTheScreenFrame()
        {
            var cameraObject = new GameObject("Billboard occlusion camera");
            var camera = cameraObject.AddComponent<Camera>();
            var display = cameraObject.AddComponent<HeadTrackedDisplay>();
            display.enabled = false;
            display.Calibration.screenWidth = .345f; display.Calibration.screenHeight = .215f;
            var controllerObject = new GameObject("Occlusion test rig");
            var controller = controllerObject.AddComponent<BillboardIllusionController>();
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.SetActive(false);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var red = new Material(shader) { color = Color.red };
            var blue = new Material(shader) { color = Color.blue };
            if (red.HasProperty("_BaseColor")) red.SetColor("_BaseColor", Color.red);
            if (blue.HasProperty("_BaseColor")) blue.SetColor("_BaseColor", Color.blue);
            prefab.GetComponent<Renderer>().sharedMaterial = red;
            controller.frameMaterial = blue;
            var target = new RenderTexture(800, 500, 24);
            var image = new Texture2D(800, 500, TextureFormat.RGB24, false);
            var oldActive = RenderTexture.active;
            try
            {
                controller.Settings.horizontalOffsetFraction = .40f;
                controller.Settings.staticDepth = .10f;
                controller.Configure(display, prefab);
                yield return null;
                camera.transform.SetPositionAndRotation(new Vector3(0, 0, -.6f), Quaternion.identity);
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.projectionMatrix = OffAxisProjection.Calculate(camera.transform.position, .345f, .215f, camera.nearClipPlane, camera.farClipPlane);
                var rim = controller.RigRoot.Find("Screen-plane frame and matte surround/Frame right");
                Vector3 sample = camera.WorldToViewportPoint(new Vector3(rim.position.x, -.03f, rim.position.z));
                int x = Mathf.RoundToInt(sample.x * 800), y = Mathf.RoundToInt(sample.y * 500);
                Color behind = Read(camera, target, image, x, y);
                Assert.That(behind.b, Is.GreaterThan(behind.r + .2f), "Frame must cover the back pose.");
                controller.Settings.staticDepth = -.035f;
                controller.Refresh();
                Color front = Read(camera, target, image, x, y);
                Assert.That(front.r, Is.GreaterThan(front.b + .2f), "Front content must cover the frame.");
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = oldActive;
                Object.Destroy(image); Object.Destroy(target); Object.Destroy(controllerObject);
                Object.Destroy(cameraObject); Object.Destroy(prefab); Object.Destroy(red); Object.Destroy(blue);
            }
            yield return null;
        }

        private static Color Read(Camera camera, RenderTexture target, Texture2D image, int x, int y)
        {
            camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 800, 500), 0, 0); image.Apply();
            return image.GetPixel(x, y);
        }
    }
}
