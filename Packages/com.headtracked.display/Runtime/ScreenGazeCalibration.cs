using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeadTracked.Display
{
    /// <summary>Personal linear screen-gaze mapping. This is not a measured 3D gaze ray or fixation depth.</summary>
    [Serializable]
    public sealed class ScreenGazeCalibration
    {
        public float[] coefficients;
        public float trainingRms;
        public string context;
        public bool IsCalibrated => coefficients != null && coefficients.Length == 10;

        private static double[] Features(HeadObservation o) => new[]
            { (double)o.irisOffset.x, o.irisOffset.y, o.headEulerDegrees.y * Mathf.Deg2Rad,
              o.headEulerDegrees.x * Mathf.Deg2Rad, 1.0 };

        public bool TryEstimate(HeadObservation observation, out Vector2 viewport)
        {
            viewport = default;
            if (!IsCalibrated || !observation.gazeValid || !observation.poseValid) return false;
            var f = Features(observation);
            for (int i = 0; i < 5; i++)
            {
                viewport.x += coefficients[i] * (float)f[i];
                viewport.y += coefficients[i + 5] * (float)f[i];
            }
            return !float.IsNaN(viewport.x) && !float.IsNaN(viewport.y) &&
                   !float.IsInfinity(viewport.x) && !float.IsInfinity(viewport.y);
        }

        public bool Fit(IReadOnlyList<HeadObservation> samples, IReadOnlyList<Vector2> targets)
        {
            coefficients = null;
            if (samples.Count < 45 || samples.Count != targets.Count) return false;
            var normal = new double[5, 7];
            for (int k = 0; k < samples.Count; k++)
            {
                var f = Features(samples[k]);
                for (int i = 0; i < 5; i++)
                {
                    for (int j = 0; j < 5; j++) normal[i, j] += f[i] * f[j];
                    normal[i, 5] += f[i] * targets[k].x;
                    normal[i, 6] += f[i] * targets[k].y;
                }
            }
            for (int i = 0; i < 4; i++) normal[i, i] += .0001; // small ridge; bias remains unpenalized
            for (int pivot = 0; pivot < 5; pivot++)
            {
                int row = pivot;
                for (int i = pivot + 1; i < 5; i++)
                    if (Math.Abs(normal[i, pivot]) > Math.Abs(normal[row, pivot])) row = i;
                for (int j = 0; j < 7; j++)
                {
                    double swap = normal[pivot, j]; normal[pivot, j] = normal[row, j]; normal[row, j] = swap;
                }
                double divisor = normal[pivot, pivot];
                if (Math.Abs(divisor) < 1e-10) return false;
                for (int j = pivot; j < 7; j++) normal[pivot, j] /= divisor;
                for (int i = 0; i < 5; i++)
                {
                    if (i == pivot) continue;
                    double factor = normal[i, pivot];
                    for (int j = pivot; j < 7; j++) normal[i, j] -= factor * normal[pivot, j];
                }
            }
            coefficients = new float[10];
            for (int i = 0; i < 5; i++)
            {
                coefficients[i] = (float)normal[i, 5];
                coefficients[i + 5] = (float)normal[i, 6];
            }
            double error = 0;
            for (int k = 0; k < samples.Count; k++)
            {
                if (!TryEstimate(samples[k], out Vector2 predicted)) { coefficients = null; return false; }
                error += (predicted - targets[k]).sqrMagnitude;
            }
            trainingRms = (float)Math.Sqrt(error / samples.Count);
            if (trainingRms > .12f) { coefficients = null; return false; }
            return true;
        }
    }
}
