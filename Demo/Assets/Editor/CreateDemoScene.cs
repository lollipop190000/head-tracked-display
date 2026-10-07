using HeadTracked.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HeadTracked.Demo.Editor
{
    public static class CreateDemoScene
    {
        [MenuItem("Head Tracked/Create or refresh demo scene")]
        public static void Create()
        {
            const string sceneFolder = "Assets/Scenes";
            const string scenePath = sceneFolder + "/HeadTrackedDemo.unity";
            const string settingsFolder = "Assets/Settings";
            if (!AssetDatabase.IsValidFolder(sceneFolder)) AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!AssetDatabase.IsValidFolder(settingsFolder)) AssetDatabase.CreateFolder("Assets", "Settings");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(settingsFolder + "/DemoRenderer.asset");
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, settingsFolder + "/DemoRenderer.asset");
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(settingsFolder + "/DemoURP.asset");
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, settingsFolder + "/DemoURP.asset");
            }
            pipeline.supportsHDR = true;
            EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Head Tracked Demo").AddComponent<DemoBootstrap>();
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Demo scene and URP asset created: " + scenePath);
        }
    }
}
