using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HeadTracked.Display.Tests
{
    public sealed class NativeWebcamTests
    {
        [UnityTest]
        public IEnumerator NativeFaceLandmarkerProcessesWebcamFrames()
        {
            if (WebCamTexture.devices.Length == 0)
                Assert.Ignore("No webcam is connected to this machine.");
            if (!File.Exists(Path.Combine(Application.streamingAssetsPath, "face_landmarker.task")))
                Assert.Ignore("Download face_landmarker.task into StreamingAssets before the hardware test.");

            var owner = new GameObject("Native tracking hardware test");
            var source = owner.AddComponent<MediaPipeUnitySource>();
            bool received = false;
            HeadObservation observation = default;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline)
            {
                received = source.TryGetLatest(out observation);
                if (received) break;
                yield return null;
            }
            string status = source.Status;
            Object.Destroy(owner);
            Assert.That(received, Is.True, status);
            Assert.That(observation.frameWidth, Is.GreaterThan(0));
            Assert.That(observation.frameHeight, Is.GreaterThan(0));
        }
    }
}
