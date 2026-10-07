using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using UnityEngine;

namespace HeadTracked.Display
{
    /// <summary>Optional in-Unity provider; compiled when MediaPipe Unity Plugin 0.16.3+ is installed.</summary>
    public sealed class MediaPipeUnitySource : HeadObservationSource
    {
        [SerializeField] private string webcamName = "";
        [SerializeField] private string modelFileName = "face_landmarker.task";
        [SerializeField, Min(160)] private int captureWidth = 640;
        [SerializeField, Min(120)] private int captureHeight = 480;
        [SerializeField, Range(1, 60)] private int captureFps = 30;

        private WebCamTexture webcam;
        private HeadObservation latest;
        private bool received;
        private string status = "Stopped";
        public override string Status => status;
        public string[] AvailableWebcams
        {
            get
            {
                var devices = WebCamTexture.devices;
                var names = new string[devices.Length];
                for (int i = 0; i < devices.Length; i++) names[i] = devices[i].name;
                return names;
            }
        }

        public void SelectWebcam(string name)
        {
            webcamName = name ?? "";
            if (isActiveAndEnabled)
            {
                StopAllCoroutines();
                StopWebcam();
                StartCoroutine(Run());
            }
        }

        private void OnEnable() => StartCoroutine(Run());

        private void OnDisable()
        {
            StopAllCoroutines();
            StopWebcam();
            received = false;
            status = "Stopped";
        }

        private void StopWebcam()
        {
            if (webcam != null)
            {
                webcam.Stop();
                Destroy(webcam);
                webcam = null;
            }
        }

        private IEnumerator Run()
        {
            string modelPath = Path.Combine(Application.streamingAssetsPath, modelFileName);
            if (!File.Exists(modelPath))
            {
                status = "Model missing: " + modelPath;
                yield break;
            }
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                status = "No webcam found";
                yield break;
            }
            string selected = webcamName;
            bool foundDevice = false;
            foreach (var device in devices) if (device.name == selected) foundDevice = true;
            if (!foundDevice) selected = devices[0].name;

            webcam = new WebCamTexture(selected, captureWidth, captureHeight, captureFps);
            webcam.Play();
            status = "Waiting for webcam: " + selected;
            float start = Time.realtimeSinceStartup;
            while (webcam != null && webcam.width <= 16 && Time.realtimeSinceStartup - start < 5f)
                yield return null;
            if (webcam == null || webcam.width <= 16)
            {
                status = "Webcam did not start";
                StopWebcam();
                yield break;
            }

            FaceLandmarker landmarker = null;
            Mediapipe.Unity.Experimental.TextureFrame textureFrame = null;
            try
            {
                var options = new FaceLandmarkerOptions(
                    baseOptions: new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: File.ReadAllBytes(modelPath)),
                    runningMode: RunningMode.VIDEO,
                    numFaces: 1);
                landmarker = FaceLandmarker.CreateFromOptions(options);
                textureFrame = new Mediapipe.Unity.Experimental.TextureFrame(webcam.width, webcam.height, TextureFormat.RGBA32);
            }
            catch (Exception ex)
            {
                landmarker?.Dispose();
                status = "MediaPipe initialization failed: " + ex.Message;
                StopWebcam();
                yield break;
            }
            using (landmarker)
            using (textureFrame)
            {
                var stopwatch = Stopwatch.StartNew();
                var wait = new WaitForEndOfFrame();
                status = "Tracking with Unity MediaPipe: " + selected;
                while (webcam != null && webcam.isPlaying)
                {
                    if (webcam.didUpdateThisFrame)
                    {
                        textureFrame.ReadTextureOnCPU(webcam, flipHorizontally: false,
                            flipVertically: !webcam.videoVerticallyMirrored);
                        using (var image = textureFrame.BuildCPUImage())
                        {
                            FaceLandmarkerResult result = default;
                            Exception detectionError = null;
                            try { result = landmarker.DetectForVideo(image, stopwatch.ElapsedMilliseconds); }
                            catch (Exception ex) { detectionError = ex; }
                            if (detectionError != null)
                            {
                                status = "Face Landmarker failed: " + detectionError.Message;
                                StopWebcam();
                                yield break;
                            }
                            var observation = new HeadObservation
                            {
                                frameWidth = webcam.width,
                                frameHeight = webcam.height,
                                receivedAtSeconds = Time.realtimeSinceStartupAsDouble
                            };
                            if (result.faceLandmarks != null && result.faceLandmarks.Count > 0)
                            {
                                var points = result.faceLandmarks[0].landmarks;
                                if (points.Count > 362)
                                {
                                    observation.found = true;
                                    observation.confidence = 1f;
                                    observation.leftEye = new Vector2((points[33].x + points[133].x) * 0.5f,
                                        (points[33].y + points[133].y) * 0.5f);
                                    observation.rightEye = new Vector2((points[263].x + points[362].x) * 0.5f,
                                        (points[263].y + points[362].y) * 0.5f);
                                }
                            }
                            latest = observation;
                            received = true;
                        }
                    }
                    yield return wait;
                }
            }
            StopWebcam();
            status = "Webcam stopped";
        }

        public override bool TryGetLatest(out HeadObservation observation)
        {
            observation = latest;
            return received;
        }
    }
}
