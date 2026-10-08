using System;
using UnityEngine;

namespace HeadTracked.Display
{
    [Serializable]
    public sealed class DisplayCalibration
    {
        [Header("Physical display, metres")]
        [Min(0.1f)] public float screenWidth = 0.53f;
        [Min(0.1f)] public float screenHeight = 0.30f;
        [Tooltip("Webcam lens relative to the screen centre. Negative Z is in front of the screen.")]
        public Vector3 webcamPosition = new Vector3(0f, 0.17f, -0.025f);
        [Tooltip("Rotation of the webcam's outward optical axis relative to the screen. Usually X is negative if it tilts down.")]
        public Vector3 webcamEulerDegrees = Vector3.zero;
        [Tooltip("Flip the horizontal image coordinate if moving right moves the scene the wrong way.")]
        public bool mirrorImageX = true;
        [Tooltip("Use calibrated multi-landmark rigid face pose when supplied. Legacy/native sources retain the eye-span estimator.")]
        public bool useRigidFacePose = true;
        public ViewingDistanceCalibration viewingDistance = new ViewingDistanceCalibration();

        [Header("Basic distance calibration")]
        [Range(30f, 110f)] public float horizontalFovDegrees = 60f;
        [Tooltip("After reference capture, estimate basic camera scale from the measured eye separation instead of an assumed webcam FOV.")]
        public bool useEyeSeparationForBasicScale = true;
        [Min(0.2f)] public float referenceEyeDistanceFromScreen = 0.60f;
        [Tooltip("Captured eye separation in pixels at the reference position. Zero means fixed distance until capture.")]
        public float referenceEyeSpanPixels;
        public int referenceFrameWidth;
        public Vector2 referenceEyeMidpoint;
        public bool hasReferenceEyeMidpoint;

        [Header("Precision calibration")]
        public bool usePreciseIntrinsics;
        [Min(1f)] public float focalXPixels;
        [Min(1f)] public float focalYPixels;
        public float principalXPixels;
        public float principalYPixels;
        public int intrinsicsImageWidth;
        public int intrinsicsImageHeight;
        public float k1;
        public float k2;
        public float k3;
        public float p1;
        public float p2;
        [Range(0.045f, 0.085f)] public float measuredEyeSeparationMeters = 0.063f;

        [Header("Allowed viewing range")]
        [Min(0.15f)] public float minimumEyeDistanceFromScreen = 0.25f;
        [Min(0.25f)] public float maximumEyeDistanceFromScreen = 1.5f;

        public bool CaptureReference(HeadObservation observation)
        {
            if (!observation.IsUsable) return false;
            Vector2 delta = observation.leftEye - observation.rightEye;
            referenceEyeSpanPixels = new Vector2(delta.x * observation.frameWidth,
                delta.y * observation.frameHeight).magnitude / observation.EyeSpanForeshortening;
            referenceFrameWidth = observation.frameWidth;
            referenceEyeMidpoint = (observation.leftEye + observation.rightEye) * .5f;
            hasReferenceEyeMidpoint = true;
            return referenceEyeSpanPixels > 1f;
        }
    }
}
