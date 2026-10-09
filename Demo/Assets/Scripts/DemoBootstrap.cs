using HeadTracked.Display;
using UnityEngine;

namespace HeadTracked.Demo
{
    /// <summary>Creates the scene at runtime so the FBX models can be replaced without touching tracking code.</summary>
    public sealed partial class DemoBootstrap : MonoBehaviour
    {
        private HeadTrackedDisplay display;
        private HeadObservationSource source;
        private Transform screenPlane;
        private string notice = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDemo()
        {
            if (FindFirstObjectByType<DemoBootstrap>() == null)
                new GameObject("Head Tracked Demo").AddComponent<DemoBootstrap>();
        }

        private void Start()
        {
            if (!Application.isEditor)
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Application.targetFrameRate = 60;
            screenPlane = new GameObject("Physical screen centre; +Z into scene").transform;
            MakeEnvironment();
            var cameraObject = new GameObject("Head-tracked render camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.11f, 0.14f, 0.18f);
            camera.nearClipPlane = 0.025f;
            camera.farClipPlane = 20f;
            camera.allowHDR = true;
            cameraObject.tag = "MainCamera";
            source = cameraObject.AddComponent<PythonBridgeSource>();
            display = cameraObject.AddComponent<HeadTrackedDisplay>();
            display.Configure(screenPlane, source);
            LoadSettings();
            if (!Application.isEditor) LoadHardwareScreenHint();
            new GameObject("HDR lighting and neutral tonemapping").AddComponent<RealismLighting>().Configure(camera);
            renderQuality = Resources.Load<RenderQualitySettings>("Realism/render_quality");
            if (renderQuality != null) renderQuality.Apply(enhancedRendering);
            CopyFieldsFromCalibration();
            LoadIntrinsics();
            LoadGazeCalibration();
            UseComparisonScene();
            LoadDemoPreferences();
            notice = "Move your head, then compare each switch on the same scene. F1 hides the interface.";
        }

    }
}
