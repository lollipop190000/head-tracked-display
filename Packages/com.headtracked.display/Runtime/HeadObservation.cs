using System;
using UnityEngine;

namespace HeadTracked.Display
{
    /// <summary>Eye centers in the original camera image: x right, y down, both in [0,1].</summary>
    [Serializable]
    public struct HeadObservation
    {
        public bool found;
        public Vector2 leftEye;
        public Vector2 rightEye;
        public int frameWidth;
        public int frameHeight;
        public float confidence;
        // Projected length of the face's local horizontal axis divided by its 3D length.
        // Zero means that the source did not provide a face orientation (legacy protocol).
        public float eyeSpanForeshortening;
        public double receivedAtSeconds;
        public bool poseSupported, poseValid, gazeValid;
        public Vector3 poseEyeCamera;
        public Vector3 headEulerDegrees;
        public Vector2 irisOffset;
        public float reprojectionErrorPixels, poseConfidence;
        public float inferenceMs, poseMs, frameAgeMs, trackerFps;
        public int sequence;

        public float EyeSpanForeshortening => eyeSpanForeshortening > 0f &&
            !float.IsNaN(eyeSpanForeshortening) && !float.IsInfinity(eyeSpanForeshortening)
                ? Mathf.Clamp(eyeSpanForeshortening, 0.25f, 1f) : 1f;

        public bool IsUsable => found && frameWidth > 0 && frameHeight > 0 &&
            confidence > 0f && Vector2.Distance(leftEye, rightEye) > 0.005f &&
            (eyeSpanForeshortening <= 0f || eyeSpanForeshortening >= 0.25f);
    }
}
