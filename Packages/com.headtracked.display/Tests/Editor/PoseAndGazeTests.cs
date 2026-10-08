using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HeadTracked.Display.Tests
{
    public sealed class PoseAndGazeTests
    {
        private static HeadObservation Observation() => new HeadObservation
        {
            found = true, confidence = 1, frameWidth = 640, frameHeight = 480,
            leftEye = new Vector2(.45f, .5f), rightEye = new Vector2(.55f, .5f),
            poseSupported = true, poseValid = true, gazeValid = true,
            poseEyeCamera = new Vector3(.04f, -.06f, .6f)
        };

        [Test]
        public void RigidEyeUsesCameraExtrinsicsAndMirrorConsistently()
        {
            var c = new DisplayCalibration { webcamPosition = new Vector3(.02f, .17f, -.025f),
                webcamEulerDegrees = new Vector3(-12f, 0, 0), mirrorImageX = true };
            Assert.That(EyePoseEstimator.TryEstimate(Observation(), c, out var eye), Is.True);
            var expected = c.webcamPosition + Quaternion.Euler(c.webcamEulerDegrees) * new Vector3(-.04f, .06f, -.6f);
            Assert.That(Vector3.Distance(eye, expected), Is.LessThan(1e-5f));
        }

        [Test]
        public void IrisAndHeadAnglesDoNotDirectlyMoveTheRenderEye()
        {
            var c = new DisplayCalibration();
            var o = Observation();
            EyePoseEstimator.TryEstimate(o, c, out var first);
            o.irisOffset = new Vector2(.25f, -.2f);
            o.headEulerDegrees = new Vector3(15, 40, -10);
            EyePoseEstimator.TryEstimate(o, c, out var second);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void InvalidRigidPoseDoesNotSilentlySwitchToEyeSpan()
        {
            var o = Observation();
            o.poseValid = false;
            Assert.That(EyePoseEstimator.TryEstimate(o, new DisplayCalibration(), out _), Is.False);
            Assert.That(EyePoseEstimator.TryEstimate(o, new DisplayCalibration { useRigidFacePose = false }, out _), Is.True);
            o.poseValid = true;
            o.poseEyeCamera.z = float.NaN;
            Assert.That(EyePoseEstimator.TryEstimate(o, new DisplayCalibration(), out _), Is.False);
        }

        [Test]
        public void AdaptiveFilterReducesStaticNoiseAndReactsFasterWhenMoving()
        {
            var quiet = new AdaptiveEyeFilter();
            float squaredError = 0;
            for (int i = 0; i < 120; i++)
            {
                float noise = i % 2 == 0 ? .004f : -.004f;
                float filtered = quiet.Filter(new Vector3(noise, 0, -.6f), i / 60.0).x;
                if (i > 60) squaredError += filtered * filtered;
            }
            Assert.That(Mathf.Sqrt(squaredError / 59f), Is.LessThan(.002f));
            var fixedFilter = new AdaptiveEyeFilter { SpeedCoefficient = 0 };
            var fastFilter = new AdaptiveEyeFilter { SpeedCoefficient = 15 };
            fixedFilter.Filter(Vector3.zero, 0); fastFilter.Filter(Vector3.zero, 0);
            Vector3 target = new Vector3(.1f, 0, 0);
            var fixedValue = fixedFilter.Filter(target, 1.0 / 60);
            var fastValue = fastFilter.Filter(target, 1.0 / 60);
            Assert.That(Vector3.Distance(fastValue, target), Is.LessThan(Vector3.Distance(fixedValue, target)));
        }

        [Test]
        public void PersonalGazeFitGeneralizesToAnUnseenSyntheticDirection()
        {
            var samples = new List<HeadObservation>();
            var targets = new List<Vector2>();
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) for (int k = 0; k < 10; k++)
            {
                var target = new Vector2(.2f + x * .3f, .2f + y * .3f);
                var o = Observation();
                float yaw = (k - 5) * .02f, pitch = ((k * 3) % 7 - 3) * .02f;
                o.headEulerDegrees = new Vector3(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0);
                o.irisOffset = new Vector2(.2f * (target.x - .5f) - .1f * yaw,
                    .15f * (target.y - .5f) - .08f * pitch);
                samples.Add(o); targets.Add(target);
            }
            var mapping = new ScreenGazeCalibration();
            Assert.That(mapping.Fit(samples, targets), Is.True);
            var unseen = Observation();
            unseen.headEulerDegrees = new Vector3(-4, 3, 0);
            var expected = new Vector2(.37f, .66f);
            unseen.irisOffset = new Vector2(.2f * (expected.x - .5f) - .1f * 3f * Mathf.Deg2Rad,
                .15f * (expected.y - .5f) - .08f * -4f * Mathf.Deg2Rad);
            Assert.That(mapping.TryEstimate(unseen, out var predicted), Is.True);
            Assert.That(Vector2.Distance(predicted, expected), Is.LessThan(.005f));
            unseen.gazeValid = false;
            Assert.That(mapping.TryEstimate(unseen, out _), Is.False);
        }
    }
}
