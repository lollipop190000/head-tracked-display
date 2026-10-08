using UnityEngine;

namespace HeadTracked.Display
{
    /// <summary>One Euro filter, metres and seconds. Runs once per observation, not per render frame.</summary>
    public sealed class AdaptiveEyeFilter
    {
        public float MinimumCutoffHz = 2f;
        public float SpeedCoefficient = 15f;
        private bool initialized;
        private Vector3 raw, filtered, velocity;
        private double timestamp;

        public void Reset() => initialized = false;

        public Vector3 Filter(Vector3 value, double time)
        {
            if (!initialized || time - timestamp > .3)
            {
                raw = filtered = value;
                velocity = Vector3.zero;
                timestamp = time;
                initialized = true;
                return value;
            }
            float dt = (float)(time - timestamp);
            if (dt <= 0f) return filtered;
            velocity = Vector3.Lerp(velocity, (value - raw) / dt, Alpha(1f, dt));
            float cutoff = MinimumCutoffHz + SpeedCoefficient * velocity.magnitude;
            filtered = Vector3.Lerp(filtered, value, Alpha(cutoff, dt));
            raw = value;
            timestamp = time;
            return filtered;
        }

        private static float Alpha(float hz, float dt) => 1f / (1f + 1f / (2f * Mathf.PI * hz * dt));
    }
}
