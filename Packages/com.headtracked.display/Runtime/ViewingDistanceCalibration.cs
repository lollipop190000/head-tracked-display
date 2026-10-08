using System;
using UnityEngine;

namespace HeadTracked.Display
{
    /// <summary>Two measured screen-relative distances correct longitudinal scale/bias only.</summary>
    [Serializable]
    public sealed class ViewingDistanceCalibration
    {
        public bool enabled;
        public float scale = 1f;
        public float offsetMeters;
        public string context;

        public bool Fit(float rawNear, float measuredNear, float rawFar, float measuredFar, string setup)
        {
            if (!Finite(rawNear) || !Finite(rawFar) || !Finite(measuredNear) || !Finite(measuredFar) ||
                measuredNear < .25f || measuredFar > 1.5f || measuredFar - measuredNear < .1f ||
                rawFar - rawNear < .06f || string.IsNullOrEmpty(setup)) return false;
            float slope = (measuredFar - measuredNear) / (rawFar - rawNear);
            float bias = measuredNear - slope * rawNear;
            if (!Finite(slope) || !Finite(bias) || slope < .25f || slope > 3f || Mathf.Abs(bias) > .4f) return false;
            scale = slope;
            offsetMeters = bias;
            context = setup;
            enabled = true;
            return true;
        }

        public float Correct(float distance, string setup) =>
            enabled && context == setup && Finite(scale) && scale > 0f && Finite(offsetMeters)
                ? scale * distance + offsetMeters : distance;

        public bool Matches(DisplayCalibration c, HeadObservation o) => enabled && context == Setup(c, o);

        public static string Setup(DisplayCalibration c, HeadObservation o)
        {
            EyePoseEstimator.CameraIntrinsics(c, o.frameWidth, o.frameHeight, out float fx, out float fy, out float cx, out float cy);
            return JsonUtility.ToJson(new EstimationSetup
            {
                webcamPosition = c.webcamPosition, webcamRotation = c.webcamEulerDegrees,
                mirror = c.mirrorImageX, rigid = c.useRigidFacePose && o.poseSupported,
                precise = c.usePreciseIntrinsics, basicEyeScale = c.useEyeSeparationForBasicScale,
                referenceMidpoint = c.referenceEyeMidpoint, hasReferenceMidpoint = c.hasReferenceEyeMidpoint,
                width = o.frameWidth, height = o.frameHeight, fx = fx, fy = fy, cx = cx, cy = cy,
                ipd = c.measuredEyeSeparationMeters, referenceDistance = c.referenceEyeDistanceFromScreen,
                referenceSpan = c.referenceEyeSpanPixels, referenceWidth = c.referenceFrameWidth,
                k1 = c.usePreciseIntrinsics ? c.k1 : 0f, k2 = c.usePreciseIntrinsics ? c.k2 : 0f,
                k3 = c.usePreciseIntrinsics ? c.k3 : 0f, p1 = c.usePreciseIntrinsics ? c.p1 : 0f,
                p2 = c.usePreciseIntrinsics ? c.p2 : 0f
            });
        }

        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        [Serializable]
        private sealed class EstimationSetup
        {
            public Vector3 webcamPosition, webcamRotation;
            public Vector2 referenceMidpoint;
            public bool mirror, rigid, precise, basicEyeScale, hasReferenceMidpoint;
            public int width, height, referenceWidth;
            public float fx, fy, cx, cy, ipd, referenceDistance, referenceSpan, k1, k2, k3, p1, p2;
        }
    }
}
