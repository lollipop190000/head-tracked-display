using UnityEngine;

namespace HeadTracked.Display
{
    public static class EyePoseEstimator
    {
        /// <summary>Returns the cyclopean eye in screen-local metres; the display lies at Z=0 and the viewer at Z&lt;0.</summary>
        public static bool TryEstimate(HeadObservation observation, DisplayCalibration calibration, out Vector3 eye)
        {
            eye = default;
            return TryEstimateUncalibrated(observation, calibration, out Vector3 raw) &&
                TryApplyViewingDistance(raw, observation, calibration, out eye);
        }

        public static bool TryApplyViewingDistance(Vector3 raw, HeadObservation observation,
            DisplayCalibration calibration, out Vector3 eye)
        {
            eye = raw;
            if (calibration == null || !IsFinite(raw)) return false;
            float distance = -raw.z;
            if (calibration.viewingDistance != null && calibration.viewingDistance.enabled)
                distance = calibration.viewingDistance.Correct(distance, ViewingDistanceCalibration.Setup(calibration, observation));
            if (distance < calibration.minimumEyeDistanceFromScreen || distance > calibration.maximumEyeDistanceFromScreen)
                return false;
            eye.z = -distance;
            return true;
        }

        public static bool TryEstimateUncalibrated(HeadObservation observation, DisplayCalibration calibration, out Vector3 eye)
        {
            eye = default;
            if (!observation.IsUsable || calibration == null) return false;
            if (calibration.useRigidFacePose && observation.poseSupported)
            {
                if (!observation.poseValid || !IsFinite(observation.poseEyeCamera) || observation.poseEyeCamera.z <= 0f)
                    return false;
                var cameraEye = observation.poseEyeCamera;
                if (calibration.mirrorImageX) cameraEye.x = -cameraEye.x;
                cameraEye.y = -cameraEye.y;
                cameraEye.z = -cameraEye.z;
                eye = calibration.webcamPosition + Quaternion.Euler(calibration.webcamEulerDegrees) * cameraEye;
                return IsFinite(eye) && -eye.z > .1f && -eye.z < 3f;
            }

            float width = observation.frameWidth;
            float height = observation.frameHeight;
            float intrinsicsScaleX = calibration.intrinsicsImageWidth > 0
                ? width / calibration.intrinsicsImageWidth : 1f;
            float intrinsicsScaleY = calibration.intrinsicsImageHeight > 0
                ? height / calibration.intrinsicsImageHeight : 1f;
            float referenceSpan = calibration.referenceFrameWidth > 0
                ? calibration.referenceEyeSpanPixels * width / calibration.referenceFrameWidth : 0f;
            float basicFocal = width / (2f * Mathf.Tan(calibration.horizontalFovDegrees * Mathf.Deg2Rad * 0.5f));
            float referenceCameraDepth = ReferenceCameraDepth(calibration, referenceSpan, width, height, basicFocal);
            if (calibration.useEyeSeparationForBasicScale && referenceSpan > 1f &&
                calibration.measuredEyeSeparationMeters > 0f)
            {
                basicFocal = referenceSpan * referenceCameraDepth / calibration.measuredEyeSeparationMeters;
            }
            float fx = calibration.usePreciseIntrinsics && calibration.focalXPixels > 1f
                ? calibration.focalXPixels * intrinsicsScaleX
                : basicFocal;
            float fy = calibration.usePreciseIntrinsics && calibration.focalYPixels > 1f
                ? calibration.focalYPixels * intrinsicsScaleY : fx;
            float cx = calibration.usePreciseIntrinsics && calibration.focalXPixels > 1f
                ? calibration.principalXPixels * intrinsicsScaleX : width * 0.5f;
            float cy = calibration.usePreciseIntrinsics && calibration.focalYPixels > 1f
                ? calibration.principalYPixels * intrinsicsScaleY : height * 0.5f;

            Vector2 eyeDelta = observation.leftEye - observation.rightEye;
            float spanPixels = new Vector2(eyeDelta.x * width, eyeDelta.y * height).magnitude;
            if (spanPixels < 1f) return false;
            float foreshortening = observation.EyeSpanForeshortening;

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
                cameraToEye = calibration.measuredEyeSeparationMeters * foreshortening / raySpan;
            }
            else if (calibration.referenceEyeSpanPixels > 1f && calibration.referenceFrameWidth > 0)
            {
                cameraToEye = referenceCameraDepth * referenceSpan * foreshortening / spanPixels;
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
            eye = local;
            return IsFinite(eye) && -eye.z > .1f && -eye.z < 3f;
        }

        private static bool IsFinite(Vector3 p) =>
            !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) &&
            !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);

        // Share exactly the same camera parameters with the optional Python PnP fit.
        public static void CameraIntrinsics(DisplayCalibration c, int width, int height,
            out float fx, out float fy, out float cx, out float cy)
        {
            float focal = width / (2f * Mathf.Tan(c.horizontalFovDegrees * Mathf.Deg2Rad * .5f));
            float span = c.referenceFrameWidth > 0 ? c.referenceEyeSpanPixels * width / c.referenceFrameWidth : 0f;
            float depth = ReferenceCameraDepth(c, span, width, height, focal);
            if (c.useEyeSeparationForBasicScale && span > 1f && c.measuredEyeSeparationMeters > 0f)
                focal = span * depth / c.measuredEyeSeparationMeters;
            float sx = c.intrinsicsImageWidth > 0 ? (float)width / c.intrinsicsImageWidth : 1f;
            float sy = c.intrinsicsImageHeight > 0 ? (float)height / c.intrinsicsImageHeight : 1f;
            bool precise = c.usePreciseIntrinsics && c.focalXPixels > 1f;
            fx = precise ? c.focalXPixels * sx : focal;
            fy = precise && c.focalYPixels > 1f ? c.focalYPixels * sy : fx;
            cx = precise ? c.principalXPixels * sx : width * .5f;
            cy = precise ? c.principalYPixels * sy : height * .5f;
        }

        private static float ReferenceCameraDepth(DisplayCalibration c, float referenceSpan,
            float width, float height, float assumedFocal)
        {
            float fallback = c.referenceEyeDistanceFromScreen + c.webcamPosition.z;
            if (!c.hasReferenceEyeMidpoint || referenceSpan <= 1f) return fallback;
            float xPixels = (c.referenceEyeMidpoint.x - .5f) * width;
            if (c.mirrorImageX) xPixels = -xPixels;
            float yPixels = - (c.referenceEyeMidpoint.y - .5f) * height;
            Quaternion rotation = Quaternion.Euler(c.webcamEulerDegrees);
            if (c.useEyeSeparationForBasicScale && c.measuredEyeSeparationMeters > 0f)
            {
                Vector3 transverse = new Vector3(xPixels, yPixels, 0f) *
                    (c.measuredEyeSeparationMeters / referenceSpan);
                float opticalZ = (rotation * Vector3.forward).z;
                if (opticalZ > .1f)
                    return (fallback + (rotation * transverse).z) / opticalZ;
            }
            else
            {
                Vector3 ray = rotation * new Vector3(xPixels / assumedFocal, yPixels / assumedFocal, -1f);
                if (ray.z < -.1f) return -fallback / ray.z;
            }
            return fallback;
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
