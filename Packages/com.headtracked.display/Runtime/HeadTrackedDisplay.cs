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

        public DisplayCalibration Calibration => calibration;
        public Vector3 EyePositionMeters => currentEye;
        public Vector3 EstimatedEyePositionMeters { get; private set; }
        public bool FreezeViewingDistance { get; set; }
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
        }

        public bool CaptureReference()
        {
            return IsTracking && hasLatest && calibration.CaptureReference(latest);
        }

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            currentEye = NeutralEye;
            initialized = true;
        }

        private Vector3 NeutralEye => new Vector3(0f, 0f, -calibration.referenceEyeDistanceFromScreen);

        private void LateUpdate()
        {
            if (!initialized) Awake();
            Vector3 estimated = NeutralEye;
            bool valid = observationSource != null && observationSource.isActiveAndEnabled &&
                         observationSource.TryGetLatest(out latest) && latest.IsUsable &&
                         Time.realtimeSinceStartupAsDouble - latest.receivedAtSeconds < observationTimeoutSeconds &&
                         EyePoseEstimator.TryEstimate(latest, calibration, out estimated);
            if (valid) hasLatest = true;
            IsTracking = valid;
            Confidence = valid ? latest.confidence : 0f;
            if (valid && FreezeViewingDistance) estimated.z = NeutralEye.z;
            EstimatedEyePositionMeters = estimated;
            Vector3 target = valid ? estimated : NeutralEye;
            float timeConstant = valid ? trackingSmoothingSeconds : returnToNeutralSeconds;
            float alpha = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.001f, timeConstant));
            currentEye = Vector3.Lerp(currentEye, target, alpha);

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
