using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace HeadTracked.Display
{
    /// <summary>Receives newline-delimited JSON from the companion Python tracker over loopback TCP.</summary>
    public sealed class PythonBridgeSource : HeadObservationSource
    {
        [SerializeField, Range(1024, 65535)] private int port = 8765;
        private PendingFrame pending;
        private TcpListener listener;
        private TcpClient client;
        private Thread worker;
        private volatile bool running;
        private volatile string status = "Stopped";
        private HeadObservation latest;
        private bool received;
        private DisplayCalibration calibration;
        private float nextCalibrationWrite;
        private string lastCameraConfiguration;
        private int calibrationRevision;

        public void ConfigureCalibration(DisplayCalibration value) => calibration = value;

        public override string Status => status;
        public int Port => port;

        public void SetPort(int value)
        {
            if (running) throw new InvalidOperationException("Stop the source before changing its port.");
            port = value;
        }

        private void OnEnable()
        {
            calibrationRevision = Environment.TickCount & int.MaxValue;
            lastCameraConfiguration = null;
            running = true;
            worker = new Thread(ReceiveLoop) { IsBackground = true, Name = "HeadTracked Python bridge" };
            worker.Start();
        }

        private void OnDisable()
        {
            running = false;
            try { client?.Close(); } catch { /* Closing an already closed socket is harmless. */ }
            try { listener?.Stop(); } catch { /* Accept exits when the listener stops. */ }
            if (worker != null && worker.IsAlive) worker.Join(500);
            status = "Stopped";
            received = false;
            Interlocked.Exchange(ref pending, null);
        }

        private void ReceiveLoop()
        {
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                status = $"Listening on 127.0.0.1:{port}";
                while (running)
                {
                    using (TcpClient incoming = listener.AcceptTcpClient())
                    {
                        client = incoming;
                        status = "Python tracker connected";
                        using (var reader = new StreamReader(incoming.GetStream()))
                        {
                            string line;
                            while (running && (line = reader.ReadLine()) != null)
                                Interlocked.Exchange(ref pending, new PendingFrame(line, Stopwatch.GetTimestamp()));
                        }
                    }
                    client = null;
                    if (running) status = "Python tracker disconnected; waiting";
                }
            }
            catch (SocketException ex)
            {
                if (running) status = "Bridge socket error: " + ex.Message;
            }
            catch (IOException ex)
            {
                if (running) status = "Bridge I/O error: " + ex.Message;
            }
            catch (ObjectDisposedException) { if (running) status = "Bridge connection closed"; }
            finally
            {
                try { listener?.Stop(); } catch { }
            }
        }

        private void Update()
        {
            PublishCameraConfiguration();
            var frame = Interlocked.Exchange(ref pending, null);
            if (frame == null) return;
            try
            {
                var wire = JsonUtility.FromJson<WireObservation>(frame.line);
                double queuedSeconds = (double)(Stopwatch.GetTimestamp() - frame.receivedTicks) / Stopwatch.Frequency;
                latest = new HeadObservation
                {
                    found = wire.found,
                    leftEye = new Vector2(wire.leftX, wire.leftY),
                    rightEye = new Vector2(wire.rightX, wire.rightY),
                    frameWidth = wire.width,
                    frameHeight = wire.height,
                    confidence = wire.confidence,
                    eyeSpanForeshortening = wire.eyeSpanForeshortening,
                    receivedAtSeconds = Time.realtimeSinceStartupAsDouble - queuedSeconds,
                    poseSupported = wire.poseSupported,
                    poseValid = wire.poseValid && wire.calibrationRevision == calibrationRevision,
                    poseEyeCamera = new Vector3(wire.poseEyeX, wire.poseEyeY, wire.poseEyeZ),
                    headEulerDegrees = new Vector3(wire.headPitchDegrees, wire.headYawDegrees, wire.headRollDegrees),
                    gazeValid = wire.gazeValid && wire.calibrationRevision == calibrationRevision,
                    irisOffset = new Vector2(wire.irisHorizontal, wire.irisVertical),
                    reprojectionErrorPixels = wire.reprojectionErrorPixels,
                    poseConfidence = wire.poseConfidence,
                    inferenceMs = wire.inferenceMs, poseMs = wire.poseMs,
                    frameAgeMs = wire.frameAgeMs, trackerFps = wire.trackerFps, sequence = wire.sequence
                };
                received = true;
            }
            catch (Exception ex)
            {
                status = "Invalid tracker data: " + ex.Message;
            }
        }

        private void PublishCameraConfiguration()
        {
            if (calibration == null || Application.isBatchMode || Time.unscaledTime < nextCalibrationWrite) return;
            nextCalibrationWrite = Time.unscaledTime + .25f;
            int width = latest.frameWidth > 0 ? latest.frameWidth : 640;
            int height = latest.frameHeight > 0 ? latest.frameHeight : 480;
            EyePoseEstimator.CameraIntrinsics(calibration, width, height, out float fx, out float fy, out float cx, out float cy);
            var c = calibration;
            var camera = new CameraConfiguration
            {
                imageWidth = width, imageHeight = height, focalXPixels = fx, focalYPixels = fy,
                principalXPixels = cx, principalYPixels = cy, eyeSeparationMeters = c.measuredEyeSeparationMeters,
                k1 = c.usePreciseIntrinsics ? c.k1 : 0f, k2 = c.usePreciseIntrinsics ? c.k2 : 0f,
                k3 = c.usePreciseIntrinsics ? c.k3 : 0f, p1 = c.usePreciseIntrinsics ? c.p1 : 0f,
                p2 = c.usePreciseIntrinsics ? c.p2 : 0f
            };
            string fingerprint = JsonUtility.ToJson(camera);
            if (fingerprint == lastCameraConfiguration) return;
            camera.revision = calibrationRevision + 1;
            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "tracker_runtime_calibration.json"),
                    JsonUtility.ToJson(camera));
                calibrationRevision = camera.revision;
                latest.poseValid = latest.gazeValid = false;
                lastCameraConfiguration = fingerprint;
            }
            catch (Exception ex) { status = "Could not publish tracker calibration: " + ex.Message; }
        }

        private sealed class PendingFrame
        {
            public readonly string line;
            public readonly long receivedTicks;
            public PendingFrame(string line, long ticks) { this.line = line; receivedTicks = ticks; }
        }

        [Serializable]
        private sealed class CameraConfiguration
        {
            public int revision, imageWidth, imageHeight;
            public float focalXPixels, focalYPixels, principalXPixels, principalYPixels, eyeSeparationMeters;
            public float k1, k2, k3, p1, p2;
        }

        public override bool TryGetLatest(out HeadObservation observation)
        {
            observation = latest;
            return received;
        }

        [Serializable]
        private sealed class WireObservation
        {
            public bool found;
            public float leftX;
            public float leftY;
            public float rightX;
            public float rightY;
            public int width;
            public int height;
            public float confidence;
            public float eyeSpanForeshortening;
            public bool poseSupported, poseValid, gazeValid;
            public int calibrationRevision, sequence;
            public float poseEyeX, poseEyeY, poseEyeZ;
            public float headPitchDegrees, headYawDegrees, headRollDegrees;
            public float irisHorizontal, irisVertical, reprojectionErrorPixels, poseConfidence;
            public float inferenceMs, poseMs, frameAgeMs, trackerFps;
        }
    }
}
