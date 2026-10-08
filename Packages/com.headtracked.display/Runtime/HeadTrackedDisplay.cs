using System;
using UnityEngine;

namespace HeadTracked.Display
{
    [RequireComponent(typeof(Camera))]
    public sealed class HeadTrackedDisplay : MonoBehaviour
    {
        [SerializeField] private Transform screenPlane;
        [SerializeField] private HeadObservationSource observationSource;
        [SerializeField] private DisplayCalibration calibration = new DisplayCalibration();
        [SerializeField, Range(0.005f, 0.3f)] private float trackingSmoothingSeconds = 0.045f;
        [SerializeField, Range(0.1f, 2f)] private float returnToNeutralSeconds = 0.4f;
        [SerializeField, Range(0.05f, 1f)] private float observationTimeoutSeconds = 0.3f;

        private Camera targetCamera;
        private Vector3 currentEye;
        private HeadObservation latest;
        private bool hasLatest;
        private bool initialized;
        private readonly AdaptiveEyeFilter adaptiveFilter = new AdaptiveEyeFilter();
        private double lastFilteredObservation = double.NegativeInfinity;
        private Vector3 filteredEye;
        private double lastValidTime = double.NegativeInfinity;
        private double reacquireUntil;

        public DisplayCalibration Calibration => calibration;
        public Vector3 EyePositionMeters => currentEye;
        public Vector3 EstimatedEyePositionMeters { get; private set; }
        public Vector3 UncalibratedEyePositionMeters { get; private set; }
        public bool HasFreshEyeEstimate { get; private set; }
        private bool freezeViewingDistance;
        private float frozenEyeZ;
        public bool FreezeViewingDistance
        {
            get => freezeViewingDistance;
            set
            {
                if (value && !freezeViewingDistance) frozenEyeZ = currentEye.z;
                freezeViewingDistance = value;
            }
        }
        public bool UseAdaptiveFilter { get; set; } = true;
        public float FilterMinimumCutoffHz { get => adaptiveFilter.MinimumCutoffHz; set => adaptiveFilter.MinimumCutoffHz = Mathf.Clamp(value, .5f, 8f); }
        public float FilterSpeedCoefficient { get => adaptiveFilter.SpeedCoefficient; set => adaptiveFilter.SpeedCoefficient = Mathf.Clamp(value, 0f, 50f); }
        public HeadObservation LatestObservation => latest;
        public bool HasObservation => hasLatest;
        public float ResultAgeMilliseconds => hasLatest ? latest.frameAgeMs +
            (float)(Time.realtimeSinceStartupAsDouble - latest.receivedAtSeconds) * 1000f : 0f;
        public float TrackingSmoothingSeconds
        {
            get => trackingSmoothingSeconds;
            set => trackingSmoothingSeconds = Mathf.Clamp(value, .005f, .3f);
        }
        public bool IsTracking { get; private set; }
        public float Confidence { get; private set; }
        public string SourceStatus => observationSource != null ? observationSource.Status : "No tracking source";
        public event Action<Vector3, bool, float> PoseUpdated;

        public void Configure(Transform plane, HeadObservationSource source)
        {
            screenPlane = plane;
            observationSource = source;
            if (source is PythonBridgeSource python) python.ConfigureCalibration(calibration);
            hasLatest = false;
            adaptiveFilter.Reset();
            lastFilteredObservation = double.NegativeInfinity;
            lastValidTime = double.NegativeInfinity;
        }

        public bool CaptureReference()
        {
            return IsTracking && hasLatest && calibration.CaptureReference(latest);
        }

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            if (observationSource is PythonBridgeSource python) python.ConfigureCalibration(calibration);
            currentEye = NeutralEye;
            initialized = true;
        }

        private Vector3 NeutralEye => new Vector3(0f, 0f, -calibration.referenceEyeDistanceFromScreen);

        private void LateUpdate()
        {
            if (!initialized) Awake();
            if (calibration.deriveScreenHeightFromResolution && targetCamera.pixelWidth > 0 && targetCamera.pixelHeight > 0)
                calibration.screenHeight = calibration.screenWidth * targetCamera.pixelHeight / targetCamera.pixelWidth;
            Vector3 estimated = NeutralEye;
            bool available = observationSource != null && observationSource.isActiveAndEnabled &&
                             observationSource.TryGetLatest(out latest);
            if (available) hasLatest = true;
            Vector3 raw = NeutralEye;
            bool rawValid = available && latest.IsUsable &&
                Time.realtimeSinceStartupAsDouble - latest.receivedAtSeconds + latest.frameAgeMs * .001 < observationTimeoutSeconds &&
                EyePoseEstimator.TryEstimateUncalibrated(latest, calibration, out raw);
            HasFreshEyeEstimate = rawValid;
            UncalibratedEyePositionMeters = raw;
            bool valid = rawValid && EyePoseEstimator.TryApplyViewingDistance(raw, latest, calibration, out estimated);
            if (valid && !IsTracking) reacquireUntil = Time.realtimeSinceStartupAsDouble + .12;
            IsTracking = valid;
            Confidence = valid ? (calibration.useRigidFacePose && latest.poseSupported ? latest.poseConfidence : latest.confidence) : 0f;
            if (valid && FreezeViewingDistance) estimated.z = frozenEyeZ;
            EstimatedEyePositionMeters = estimated;
            if (valid) lastValidTime = Time.realtimeSinceStartupAsDouble;
            Vector3 target = valid ? estimated :
                (Time.realtimeSinceStartupAsDouble - lastValidTime < .12 ? currentEye : NeutralEye);
            float timeConstant = valid ? trackingSmoothingSeconds : returnToNeutralSeconds;
            float alpha = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.001f, timeConstant));
            if (valid && UseAdaptiveFilter)
            {
                if (latest.receivedAtSeconds != lastFilteredObservation)
                {
                    filteredEye = adaptiveFilter.Filter(estimated, latest.receivedAtSeconds - latest.frameAgeMs * .001);
                    lastFilteredObservation = latest.receivedAtSeconds;
                }
                currentEye = Time.realtimeSinceStartupAsDouble < reacquireUntil
                    ? Vector3.Lerp(currentEye, filteredEye, 1f - Mathf.Exp(-Time.unscaledDeltaTime / .045f))
                    : filteredEye;
            }
            else
            {
                currentEye = Vector3.Lerp(currentEye, target, alpha);
                adaptiveFilter.Reset();
                lastFilteredObservation = double.NegativeInfinity;
            }

            targetCamera.transform.SetPositionAndRotation(
                screenPlane != null ? screenPlane.TransformPoint(currentEye) : currentEye,
                screenPlane != null ? screenPlane.rotation : Quaternion.identity);
            targetCamera.projectionMatrix = OffAxisProjection.Calculate(currentEye,
                calibration.screenWidth, calibration.screenHeight,
                targetCamera.nearClipPlane, targetCamera.farClipPlane);
            PoseUpdated?.Invoke(currentEye, IsTracking, Confidence);
        }

        private void OnDisable()
        {
            if (targetCamera != null) targetCamera.ResetProjectionMatrix();
        }
    }
}
