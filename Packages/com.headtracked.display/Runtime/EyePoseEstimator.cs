using UnityEngine;

namespace HeadTracked.Display
{
    public static class EyePoseEstimator
    {
        /// <summary>Returns the cyclopean eye in screen-local metres; the display lies at Z=0 and the viewer at Z&lt;0.</summary>
        public static bool TryEstimate(HeadObservation observation, DisplayCalibration calibration, out Vector3 eye)
        {
            eye = default;
            if (!observation.IsUsable || calibration == null) return false;

            float width = observation.frameWidth;
            float height = observation.frameHeight;
            float intrinsicsScaleX = calibration.intrinsicsImageWidth > 0
                ? width / calibration.intrinsicsImageWidth : 1f;
            float intrinsicsScaleY = calibration.intrinsicsImageHeight > 0
                ? height / calibration.intrinsicsImageHeight : 1f;
            float fx = calibration.usePreciseIntrinsics && calibration.focalXPixels > 1f
                ? calibration.focalXPixels * intrinsicsScaleX
                : width / (2f * Mathf.Tan(calibration.horizontalFovDegrees * Mathf.Deg2Rad * 0.5f));
            float fy = calibration.usePreciseIntrinsics && calibration.focalYPixels > 1f
                ? calibration.focalYPixels * intrinsicsScaleY : fx;
            float cx = calibration.usePreciseIntrinsics && calibration.focalXPixels > 1f
                ? calibration.principalXPixels * intrinsicsScaleX : width * 0.5f;
            float cy = calibration.usePreciseIntrinsics && calibration.focalYPixels > 1f
                ? calibration.principalYPixels * intrinsicsScaleY : height * 0.5f;

            Vector2 eyeDelta = observation.leftEye - observation.rightEye;
            float spanPixels = new Vector2(eyeDelta.x * width, eyeDelta.y * height).magnitude;
            if (spanPixels < 1f) return false;

            Vector2 leftPixels = new Vector2(observation.leftEye.x * width, observation.leftEye.y * height);
            Vector2 rightPixels = new Vector2(observation.rightEye.x * width, observation.rightEye.y * height);
            Vector2 leftRay = Undistort((leftPixels.x - cx) / fx, (leftPixels.y - cy) / fy, calibration);
            Vector2 rightRay = Undistort((rightPixels.x - cx) / fx, (rightPixels.y - cy) / fy, calibration);
            if (calibration.mirrorImageX)
            {
                leftRay.x = -leftRay.x;
                rightRay.x = -rightRay.x;
            }

            float cameraToEye;
            if (calibration.usePreciseIntrinsics && calibration.focalXPixels > 1f)
            {
                float raySpan = Vector2.Distance(leftRay, rightRay);
                if (raySpan < 0.001f) return false;
                cameraToEye = calibration.measuredEyeSeparationMeters / raySpan;
            }
            else if (calibration.referenceEyeSpanPixels > 1f && calibration.referenceFrameWidth > 0)
            {
                float referenceSpan = calibration.referenceEyeSpanPixels * width / calibration.referenceFrameWidth;
                cameraToEye = (calibration.referenceEyeDistanceFromScreen + calibration.webcamPosition.z) *
                              referenceSpan / spanPixels;
            }
            else
            {
                cameraToEye = calibration.referenceEyeDistanceFromScreen + calibration.webcamPosition.z;
            }

            if (float.IsNaN(cameraToEye) || float.IsInfinity(cameraToEye) || cameraToEye <= 0f) return false;
            Vector2 midpointRay = (leftRay + rightRay) * 0.5f;
            Vector3 cameraRay = new Vector3(midpointRay.x, -midpointRay.y, -1f);
            Vector3 local = calibration.webcamPosition +
                            Quaternion.Euler(calibration.webcamEulerDegrees) * (cameraRay * cameraToEye);
            float distance = Mathf.Clamp(-local.z, calibration.minimumEyeDistanceFromScreen,
                Mathf.Max(calibration.minimumEyeDistanceFromScreen, calibration.maximumEyeDistanceFromScreen));
            eye = new Vector3(local.x, local.y, -distance);
            return true;
        }

        private static Vector2 Undistort(float x, float y, DisplayCalibration c)
        {
            if (!c.usePreciseIntrinsics) return new Vector2(x, y);
            float estimateX = x, estimateY = y;
            for (int i = 0; i < 5; i++)
            {
                float r2 = estimateX * estimateX + estimateY * estimateY;
                float radial = 1f + c.k1 * r2 + c.k2 * r2 * r2 + c.k3 * r2 * r2 * r2;
                if (Mathf.Abs(radial) < 0.01f) break;
                float dx = 2f * c.p1 * estimateX * estimateY + c.p2 * (r2 + 2f * estimateX * estimateX);
                float dy = c.p1 * (r2 + 2f * estimateY * estimateY) + 2f * c.p2 * estimateX * estimateY;
                estimateX = (x - dx) / radial;
                estimateY = (y - dy) / radial;
            }
            return new Vector2(estimateX, estimateY);
        }
    }
}
