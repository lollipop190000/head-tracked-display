using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HeadTracked.Display.Tests
{
    public sealed class ScreenWindowFrameTests
    {
        private GameObject plane, cameraObject, owner, content;
        private HeadTrackedDisplay display;
        private ScreenWindowFrame frame;
        private Material material;

        [SetUp]
        public void SetUp()
        {
            plane = new GameObject("Physical screen");
            plane.transform.SetPositionAndRotation(new Vector3(20f, 4f, -8f), Quaternion.Euler(10f, 45f, 3f));
            cameraObject = new GameObject("Tracked camera");
            cameraObject.AddComponent<Camera>();
            display = cameraObject.AddComponent<HeadTrackedDisplay>();
            display.enabled = false;
            display.Configure(plane.transform, null);
            display.Calibration.screenWidth = .53f;
            display.Calibration.screenHeight = .30f;
            content = new GameObject("Unowned city");
            content.transform.SetPositionAndRotation(new Vector3(15f, 5f, 12f), Quaternion.Euler(0f, 30f, 0f));
            content.transform.localScale = Vector3.one * 12f;
            material = new Material(Shader.Find("Standard") ?? Shader.Find("Hidden/Internal-Colored"));
            owner = new GameObject("Frame owner");
            frame = owner.AddComponent<ScreenWindowFrame>();
            frame.FrameMaterial = material;
            frame.Configure(display);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(plane);
            Object.DestroyImmediate(content);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void PhysicalFrameFollowsScreenAndResizesWithoutReplacingGeometry()
        {
            Assert.That(frame.RigRoot.parent, Is.SameAs(plane.transform));
            Assert.That(frame.RigRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(frame.RigRoot.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(frame.RigRoot.childCount, Is.EqualTo(4));
            Assert.That(frame.RigRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
            Transform left = frame.RigRoot.GetChild(0), bottom = frame.RigRoot.GetChild(2);
            Mesh mesh = left.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(bottom.localScale.x, Is.EqualTo(.53f).Within(1e-6f));
            Assert.That(left.localScale.y, Is.EqualTo(.284f).Within(1e-6f));
            display.Calibration.screenWidth = .72f;
            display.Calibration.screenHeight = .45f;
            frame.Refresh();
            Assert.That(frame.RigRoot.GetChild(0), Is.SameAs(left));
            Assert.That(left.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            Assert.That(bottom.localScale.x, Is.EqualTo(.72f).Within(1e-6f));
            Assert.That(left.localScale.y, Is.EqualTo(.434f).Within(1e-6f));
            // Strip ranges meet at the corners without overlapping coplanar surfaces.
            Assert.That(left.localPosition.y - left.localScale.y * .5f,
                Is.EqualTo(bottom.localPosition.y + bottom.localScale.y * .5f).Within(1e-6f));
            plane.transform.position += new Vector3(100f, 0f, 0f);
            frame.Refresh();
            Assert.That(frame.RigRoot.position, Is.EqualTo(plane.transform.position));
            left.hasChanged = false;
            frame.Refresh();
            Assert.That(left.hasChanged, Is.False);
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void ComparisonRoundTripChangesOnlyFrameVisibility(bool tracking, bool visible)
        {
            Transform root = frame.RigRoot;
            Vector3 position = content.transform.position, scale = content.transform.localScale;
            Quaternion rotation = content.transform.rotation;
            string calibration = JsonUtility.ToJson(display.Calibration);
            Matrix4x4 projection = cameraObject.GetComponent<Camera>().projectionMatrix;
            display.TrackingEnabled = tracking;
            frame.EffectEnabled = visible;
            Assert.That(root.gameObject.activeSelf, Is.EqualTo(visible));
            frame.EffectEnabled = !visible;
            frame.EffectEnabled = visible;
            Assert.That(frame.RigRoot, Is.SameAs(root));
            Assert.That(content.activeSelf, Is.True);
            Assert.That(content.transform.position, Is.EqualTo(position));
            Assert.That(content.transform.rotation, Is.EqualTo(rotation));
            Assert.That(content.transform.localScale, Is.EqualTo(scale));
            Assert.That(JsonUtility.ToJson(display.Calibration), Is.EqualTo(calibration));
            Assert.That(cameraObject.GetComponent<Camera>().projectionMatrix, Is.EqualTo(projection));
            Assert.That(display.TrackingEnabled, Is.EqualTo(tracking));
        }

        [Test]
        public void InvalidCalibrationAndComponentDisableHideOnlyOwnedFrame()
        {
            Transform root = frame.RigRoot;
            display.Calibration.screenWidth = float.NaN;
            frame.Refresh();
            Assert.That(root.gameObject.activeSelf, Is.False);
            display.Calibration.screenWidth = .53f;
            frame.Refresh();
            Assert.That(root.gameObject.activeSelf, Is.True);
            frame.enabled = false;
            Assert.That(root.gameObject.activeSelf, Is.False);
            Assert.That(content.activeSelf, Is.True);
            frame.enabled = true;
            Assert.That(root.gameObject.activeSelf, Is.True);
            frame.FrameMaterial = null;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) Assert.That(renderer.enabled, Is.False);
            frame.FrameMaterial = material;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                Assert.That(renderer.enabled, Is.True);
                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            }
        }

        [UnityTest]
        public IEnumerator DestroyedExternalScreenCanBeReplacedWithoutLeakingOwnedMesh()
        {
            Mesh previousMesh = frame.RigRoot.GetChild(0).GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(plane);
            frame.Refresh();
            plane = new GameObject("Replacement physical screen");
            display.Configure(plane.transform, null);
            frame.Refresh();
            yield return null;
            Assert.That(frame.RigRoot.parent, Is.SameAs(plane.transform));
            Assert.That(previousMesh == null, Is.True);
            Assert.That(frame.RigRoot.GetChild(0).GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ReconfigurationAndDestroyPreserveBorrowedMaterialAndExternalContent()
        {
            var replacement = new GameObject("Another screen");
            try
            {
                Transform root = frame.RigRoot;
                Mesh mesh = root.GetChild(0).GetComponent<MeshFilter>().sharedMesh;
                display.Configure(replacement.transform, null);
                frame.Refresh();
                Assert.That(frame.RigRoot, Is.SameAs(root));
                Assert.That(root.parent, Is.SameAs(replacement.transform));
                Object.DestroyImmediate(owner);
                yield return null;
                Assert.That(root == null, Is.True);
                Assert.That(mesh == null, Is.True);
                Assert.That(material == null, Is.False);
                Assert.That(content == null, Is.False);
                Assert.That(content.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(replacement); }
        }
    }
}
