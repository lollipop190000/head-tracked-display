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
        public double receivedAtSeconds;

        public bool IsUsable => found && frameWidth > 0 && frameHeight > 0 &&
            confidence > 0f && Vector2.Distance(leftEye, rightEye) > 0.005f;
    }
}
