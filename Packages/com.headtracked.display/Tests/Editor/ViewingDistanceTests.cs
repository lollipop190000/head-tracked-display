using NUnit.Framework;
using UnityEngine;

namespace HeadTracked.Display.Tests
{
    public sealed class ViewingDistanceTests
    {
        private static HeadObservation Observation() => new HeadObservation
        {
            found = true, confidence = 1, frameWidth = 640, frameHeight = 480,
            leftEye = new Vector2(.45f, .5f), rightEye = new Vector2(.55f, .5f),
            poseSupported = true, poseValid = true, poseEyeCamera = new Vector3(.1f, -.03f, .7f)
        };

        [Test]
        public void TwoMeasuredDistancesCorrectZWithoutChangingXY()
        {
            var c = new DisplayCalibration { webcamPosition = Vector3.zero, mirrorImageX = false };
            var o = Observation();
            string setup = ViewingDistanceCalibration.Setup(c, o);
            Assert.That(c.viewingDistance.Fit(.45f, .4f, .95f, .8f, setup), Is.True);
            Assert.That(EyePoseEstimator.TryEstimateUncalibrated(o, c, out var raw), Is.True);
            Assert.That(EyePoseEstimator.TryEstimate(o, c, out var corrected), Is.True);
            Assert.That(corrected.z, Is.EqualTo(-.6f).Within(1e-5f));
            Assert.That(corrected.x, Is.EqualTo(raw.x));
            Assert.That(corrected.y, Is.EqualTo(raw.y));
            o.poseEyeCamera.z = .45f;
            EyePoseEstimator.TryEstimate(o, c, out var near);
            o.poseEyeCamera.z = .95f;
            EyePoseEstimator.TryEstimate(o, c, out var far);
            Assert.That(near.z, Is.EqualTo(-.4f).Within(1e-5f));
            Assert.That(far.z, Is.EqualTo(-.8f).Within(1e-5f));
        }

        [Test]
        public void ReversedCloseOrInvalidAnchorsCannotInvertPerspective()
        {
            var c = new ViewingDistanceCalibration();
            Assert.That(c.Fit(.8f, .4f, .4f, .8f, "setup"), Is.False);
            Assert.That(c.Fit(.5f, .4f, .51f, .8f, "setup"), Is.False);
            Assert.That(c.Fit(.5f, .4f, .9f, .45f, "setup"), Is.False);
            Assert.That(c.Fit(float.NaN, .4f, .9f, .8f, "setup"), Is.False);
            Assert.That(c.enabled, Is.False);
        }

        [Test]
        public void DistanceCorrectionPersistsAndIsIgnoredAfterCameraSetupChanges()
        {
            var c = new DisplayCalibration { webcamPosition = Vector3.zero, mirrorImageX = false };
            var o = Observation();
            c.viewingDistance.Fit(.45f, .4f, .95f, .8f, ViewingDistanceCalibration.Setup(c, o));
            var loaded = JsonUtility.FromJson<DisplayCalibration>(JsonUtility.ToJson(c));
            Assert.That(loaded.viewingDistance.Matches(loaded, o), Is.True);
            EyePoseEstimator.TryEstimate(o, loaded, out var calibrated);
            Assert.That(calibrated.z, Is.EqualTo(-.6f).Within(1e-5f));
            loaded.measuredEyeSeparationMeters = .07f;
            Assert.That(loaded.viewingDistance.Matches(loaded, o), Is.False);
            EyePoseEstimator.TryEstimate(o, loaded, out var unchanged);
            Assert.That(unchanged.z, Is.EqualTo(-.7f).Within(1e-5f));
        }

        [TestCase(0f)]
        [TestCase(.3f)]
        [TestCase(1f)]
        public void ApproachingScreenChangesPixelSizeButBehindObjectSubtendsALargerAngle(float depth)
        {
            const float height = .15f;
            var item = new GameObject("Depth projection test");
            try
            {
                var camera = item.AddComponent<Camera>();
                var lower = new Vector3(0, -height * .5f, depth);
                var upper = new Vector3(0, height * .5f, depth);
                float[] screenHeights = new float[2], visualAngles = new float[2];
                float[] distances = { .4f, .8f };
                for (int i = 0; i < 2; i++)
                {
                    float d = distances[i];
                    camera.transform.position = new Vector3(0, 0, -d);
                    camera.projectionMatrix = OffAxisProjection.Calculate(camera.transform.position, .53f, .3f, .025f, 20f);
                    screenHeights[i] = (camera.WorldToViewportPoint(upper).y - camera.WorldToViewportPoint(lower).y) * .3f;
                    visualAngles[i] = Vector3.Angle(upper - camera.transform.position, lower - camera.transform.position);
                    Assert.That(screenHeights[i], Is.EqualTo(d * height / (d + depth)).Within(1e-5f));
                }
                Assert.That(visualAngles[0], Is.GreaterThan(visualAngles[1]));
                if (depth > 0) Assert.That(screenHeights[0], Is.LessThan(screenHeights[1]));
                else Assert.That(screenHeights[0], Is.EqualTo(screenHeights[1]).Within(1e-5f));
            }
            finally { Object.DestroyImmediate(item); }
        }
    }
}
