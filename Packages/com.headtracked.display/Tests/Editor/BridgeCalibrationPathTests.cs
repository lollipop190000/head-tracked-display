using System.IO;
using NUnit.Framework;

namespace HeadTracked.Display.Tests
{
    public sealed class BridgeCalibrationPathTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void MissingOverridePreservesUnityPersistentDirectory(string value)
        {
            const string persistent = "consumer-persistent-directory";
            Assert.That(PythonBridgeSource.ResolveCalibrationDirectory(value, persistent), Is.EqualTo(persistent));
        }

        [Test]
        public void CustomTrackerAndUnityCalibrationPathsResolveToSameDirectory()
        {
            string custom = Path.Combine(Path.GetTempPath(), "HeadTracked Calibration", "temporary", "..", "team");
            string directory = PythonBridgeSource.ResolveCalibrationDirectory(custom, "default");
            Assert.That(directory, Is.EqualTo(Path.GetFullPath(custom)));
            Assert.That(Path.Combine(directory, "tracker_runtime_calibration.json"),
                Is.EqualTo(Path.Combine(Path.GetFullPath(custom), "tracker_runtime_calibration.json")));
        }
    }
}
