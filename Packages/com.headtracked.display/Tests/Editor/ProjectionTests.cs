using NUnit.Framework;
using UnityEngine;

namespace HeadTracked.Display.Tests
{
    public sealed class ProjectionTests
    {
        private static Vector2 ScreenPoint(Matrix4x4 projection, Vector3 eye, Vector3 world)
        {
            var view = new Vector4(world.x - eye.x, world.y - eye.y, -(world.z - eye.z), 1f);
            Vector4 clip = projection * view;
            return new Vector2(clip.x / clip.w, clip.y / clip.w);
        }

        [TestCase(-0.17f, 0.04f, -0.43f)]
        [TestCase(0f, 0f, -0.6f)]
        [TestCase(0.15f, -0.06f, -1.0f)]
        public void PhysicalScreenCornersAlwaysMapToViewportEdges(float x, float y, float z)
        {
            var eye = new Vector3(x, y, z);
            Matrix4x4 projection = OffAxisProjection.Calculate(eye, 0.53f, 0.30f, 0.025f, 10f);
            Assert.That(ScreenPoint(projection, eye, new Vector3(-0.265f, -0.15f, 0f)).x,
                Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(ScreenPoint(projection, eye, new Vector3(0.265f, 0.15f, 0f)).x,
                Is.EqualTo(1f).Within(1e-5f));
            Assert.That(ScreenPoint(projection, eye, new Vector3(-0.265f, -0.15f, 0f)).y,
                Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(ScreenPoint(projection, eye, new Vector3(0.265f, 0.15f, 0f)).y,
                Is.EqualTo(1f).Within(1e-5f));
        }

        [TestCase(0.12f, 0.03f, -0.6f, -0.08f, 0.04f, 0.4f)]
        [TestCase(-0.1f, -0.05f, -0.4f, 0.05f, -0.03f, -0.1f)]
        [TestCase(0.03f, 0.07f, -0.9f, 0.17f, 0.1f, 0.8f)]
        [TestCase(-0.1f, 0.04f, -0.6f, 0.04f, -0.03f, -0.18f)]
        [TestCase(0.1f, -0.04f, -0.6f, 0.04f, -0.03f, -0.18f)]
        public void ProjectionEqualsThePhysicalEyeScreenObjectSightline(
            float ex, float ey, float ez, float px, float py, float pz)
        {
            const float width = 0.53f, height = 0.30f;
            var eye = new Vector3(ex, ey, ez);
            var point = new Vector3(px, py, pz);
            float fractionToScreen = -eye.z / (point.z - eye.z);
            Vector3 physicalScreenHit = Vector3.LerpUnclamped(eye, point, fractionToScreen);
            var projected = ScreenPoint(OffAxisProjection.Calculate(eye, width, height, .025f, 10f),
                eye, point);
            Assert.That(projected.x, Is.EqualTo(physicalScreenHit.x * 2f / width).Within(1e-5f));
            Assert.That(projected.y, Is.EqualTo(physicalScreenHit.y * 2f / height).Within(1e-5f));
        }

        [TestCase(0.12f, 0.03f, -0.6f, -0.08f, 0.04f, 0.4f)]
        [TestCase(-0.1f, -0.05f, -0.4f, 0.05f, -0.03f, -0.1f)]
        public void UnityCameraViewportMatchesThePhysicalSightline(
            float ex, float ey, float ez, float px, float py, float pz)
        {
            const float width = .53f, height = .30f;
            var eye = new Vector3(ex, ey, ez);
            var point = new Vector3(px, py, pz);
            var cameraObject = new GameObject("Projection test camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(eye, Quaternion.identity);
                camera.nearClipPlane = .025f;
                camera.farClipPlane = 10f;
                camera.projectionMatrix = OffAxisProjection.Calculate(eye, width, height, .025f, 10f);
                float fractionToScreen = -eye.z / (point.z - eye.z);
                Vector3 screenHit = Vector3.LerpUnclamped(eye, point, fractionToScreen);
                Vector3 viewport = camera.WorldToViewportPoint(point);
                Assert.That(viewport.x, Is.EqualTo(.5f + screenHit.x / width).Within(1e-5f));
                Assert.That(viewport.y, Is.EqualTo(.5f + screenHit.y / height).Within(1e-5f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void MovingRightShowsOppositeParallaxForObjectsAtDifferentDepths()
        {
            var neutral = new Vector3(0f, 0f, -0.6f);
            var right = new Vector3(0.1f, 0f, -0.6f);
            Vector3 behind = new Vector3(0f, 0f, 0.3f);
            Vector3 inFront = new Vector3(0f, 0f, -0.1f);
            float nBehind = ScreenPoint(OffAxisProjection.Calculate(neutral, .53f, .3f, .025f, 10f), neutral, behind).x;
            float rBehind = ScreenPoint(OffAxisProjection.Calculate(right, .53f, .3f, .025f, 10f), right, behind).x;
            float nFront = ScreenPoint(OffAxisProjection.Calculate(neutral, .53f, .3f, .025f, 10f), neutral, inFront).x;
            float rFront = ScreenPoint(OffAxisProjection.Calculate(right, .53f, .3f, .025f, 10f), right, inFront).x;
            Assert.That(rBehind, Is.GreaterThan(nBehind));
            Assert.That(rFront, Is.LessThan(nFront));
        }

        [Test]
        public void MovingUpShowsOppositeVerticalParallax()
        {
            var neutral = new Vector3(0f, 0f, -0.6f);
            var up = new Vector3(0f, 0.08f, -0.6f);
            var behind = new Vector3(0f, 0f, 0.3f);
            var inFront = new Vector3(0f, 0f, -0.1f);
            float behindAtNeutral = ScreenPoint(OffAxisProjection.Calculate(neutral, .53f, .3f, .025f, 10f), neutral, behind).y;
            float behindAtUp = ScreenPoint(OffAxisProjection.Calculate(up, .53f, .3f, .025f, 10f), up, behind).y;
            float frontAtNeutral = ScreenPoint(OffAxisProjection.Calculate(neutral, .53f, .3f, .025f, 10f), neutral, inFront).y;
            float frontAtUp = ScreenPoint(OffAxisProjection.Calculate(up, .53f, .3f, .025f, 10f), up, inFront).y;
            Assert.That(behindAtUp, Is.GreaterThan(behindAtNeutral));
            Assert.That(frontAtUp, Is.LessThan(frontAtNeutral));
        }

        [Test]
        public void MovingCloserChangesApparentSizeAcrossScreenPlane()
        {
            var farEye = new Vector3(0f, 0f, -0.8f);
            var nearEye = new Vector3(0f, 0f, -0.4f);
            var behind = new Vector3(0.1f, 0f, 0.3f);
            var inFront = new Vector3(0.1f, 0f, -0.1f);
            float behindFar = ScreenPoint(OffAxisProjection.Calculate(farEye, .53f, .3f, .025f, 10f), farEye, behind).x;
            float behindNear = ScreenPoint(OffAxisProjection.Calculate(nearEye, .53f, .3f, .025f, 10f), nearEye, behind).x;
            float frontFar = ScreenPoint(OffAxisProjection.Calculate(farEye, .53f, .3f, .025f, 10f), farEye, inFront).x;
            float frontNear = ScreenPoint(OffAxisProjection.Calculate(nearEye, .53f, .3f, .025f, 10f), nearEye, inFront).x;
            Assert.That(behindNear, Is.LessThan(behindFar));
            Assert.That(frontNear, Is.GreaterThan(frontFar));
        }

        [Test]
        public void ReferenceEyeSpanControlsEstimatedDepth()
        {
            var calibration = new DisplayCalibration
            {
                webcamPosition = Vector3.zero,
                referenceEyeDistanceFromScreen = .6f,
                mirrorImageX = false
            };
            var observation = new HeadObservation
            {
                found = true, confidence = 1f, frameWidth = 640, frameHeight = 480,
                leftEye = new Vector2(.45f, .5f), rightEye = new Vector2(.55f, .5f)
            };
            Assert.That(calibration.CaptureReference(observation), Is.True);
            Assert.That(EyePoseEstimator.TryEstimate(observation, calibration, out Vector3 baseline), Is.True);
            Assert.That(baseline.z, Is.EqualTo(-.6f).Within(1e-4f));
            observation.leftEye.x = .4f;
            observation.rightEye.x = .6f;
            Assert.That(EyePoseEstimator.TryEstimate(observation, calibration, out Vector3 closer), Is.True);
            Assert.That(closer.z, Is.EqualTo(-.3f).Within(1e-4f));
        }

        [Test]
        public void TurningFaceWithoutMovingEyesDoesNotFakeDistanceInBasicMode()
        {
            var calibration = new DisplayCalibration
            {
                webcamPosition = Vector3.zero,
                referenceEyeDistanceFromScreen = .6f,
                mirrorImageX = false
            };
            var frontal = EyeObservation(.1f, 1f);
            Assert.That(calibration.CaptureReference(frontal), Is.True);
            Assert.That(EyePoseEstimator.TryEstimate(frontal, calibration, out Vector3 baseline), Is.True);
            var turned = EyeObservation(.05f, .5f);
            Assert.That(EyePoseEstimator.TryEstimate(turned, calibration, out Vector3 atYaw), Is.True);
            Assert.That(atYaw.z, Is.EqualTo(baseline.z).Within(1e-4f));
            Assert.That(atYaw.x, Is.EqualTo(baseline.x).Within(1e-4f));

            Assert.That(calibration.CaptureReference(turned), Is.True);
            Assert.That(EyePoseEstimator.TryEstimate(frontal, calibration, out Vector3 frontalAgain), Is.True);
            Assert.That(frontalAgain.z, Is.EqualTo(-.6f).Within(1e-4f));
        }

        [Test]
        public void TurningFaceWithoutMovingEyesDoesNotFakeDistanceInPreciseMode()
        {
            var calibration = new DisplayCalibration
            {
                webcamPosition = Vector3.zero,
                usePreciseIntrinsics = true,
                focalXPixels = 600f,
                focalYPixels = 600f,
                principalXPixels = 320f,
                principalYPixels = 240f,
                measuredEyeSeparationMeters = .063f,
                mirrorImageX = false
            };
            Assert.That(EyePoseEstimator.TryEstimate(EyeObservation(.1f, 1f), calibration,
                out Vector3 baseline), Is.True);
            Assert.That(EyePoseEstimator.TryEstimate(EyeObservation(.05f, .5f), calibration,
                out Vector3 atYaw), Is.True);
            Assert.That(atYaw.z, Is.EqualTo(baseline.z).Within(1e-4f));
        }

        private static HeadObservation EyeObservation(float horizontalSpan, float foreshortening)
        {
            return new HeadObservation
            {
                found = true,
                confidence = 1f,
                frameWidth = 640,
                frameHeight = 480,
                leftEye = new Vector2(.5f - horizontalSpan * .5f, .5f),
                rightEye = new Vector2(.5f + horizontalSpan * .5f, .5f),
                eyeSpanForeshortening = foreshortening
            };
        }
    }
}
