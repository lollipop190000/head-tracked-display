using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentQueue<string> pending = new ConcurrentQueue<string>();
        private TcpListener listener;
        private TcpClient client;
        private Thread worker;
        private volatile bool running;
        private volatile string status = "Stopped";
        private HeadObservation latest;
        private bool received;

        public override string Status => status;
        public int Port => port;

        public void SetPort(int value)
        {
            if (running) throw new InvalidOperationException("Stop the source before changing its port.");
            port = value;
        }

        private void OnEnable()
        {
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
                                pending.Enqueue(line);
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
            finally
            {
                try { listener?.Stop(); } catch { }
            }
        }

        private void Update()
        {
            string line = null;
            while (pending.TryDequeue(out string next)) line = next;
            if (line == null) return;
            try
            {
                var wire = JsonUtility.FromJson<WireObservation>(line);
                latest = new HeadObservation
                {
                    found = wire.found,
                    leftEye = new Vector2(wire.leftX, wire.leftY),
                    rightEye = new Vector2(wire.rightX, wire.rightY),
                    frameWidth = wire.width,
                    frameHeight = wire.height,
                    confidence = wire.confidence,
                    eyeSpanForeshortening = wire.eyeSpanForeshortening,
                    receivedAtSeconds = Time.realtimeSinceStartupAsDouble
                };
                received = true;
            }
            catch (Exception ex)
            {
                status = "Invalid tracker data: " + ex.Message;
            }
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
        }
    }
}
