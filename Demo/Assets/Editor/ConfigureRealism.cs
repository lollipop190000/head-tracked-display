using System;
using HeadTracked.Demo;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HeadTracked.Demo.Editor
{
    public static class ConfigureRealism
    {
        private const string Folder = "Assets/Resources/Realism/";

        [MenuItem("Head Tracked/Configure realistic surface comparison")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            foreach (string prefix in new[] { "vase", "wood" })
                foreach (string suffix in new[] { "albedo", "normal", "rough", "ao", "metallic_smoothness" })
                {
                    string path = Folder + prefix + "_" + suffix + ".png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new Exception("Texture missing: " + path);
                    importer.textureType = suffix == "normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = suffix == "albedo";
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.alphaIsTransparency = false;
                    importer.maxTextureSize = prefix == "vase" ? 2048 : 1024;
                    importer.mipmapEnabled = true;
                    importer.anisoLevel = 4;
                    importer.SaveAndReimport();
                }
            var hdr = (TextureImporter)AssetImporter.GetAtPath(Folder + "studio.hdr");
            hdr.textureShape = TextureImporterShape.TextureCube;
            hdr.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            var cubeSettings = new TextureImporterSettings();
            hdr.ReadTextureSettings(cubeSettings);
            cubeSettings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
            hdr.SetTextureSettings(cubeSettings);
            hdr.sRGBTexture = false;
            hdr.maxTextureSize = 512;
            hdr.mipmapEnabled = true;
            hdr.isReadable = true;
            hdr.textureCompression = TextureImporterCompression.Uncompressed;
            hdr.SaveAndReimport();
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(Folder + "studio.hdr");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(Folder + "studio_sky.mat");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Cubemap"));
                AssetDatabase.CreateAsset(sky, Folder + "studio_sky.mat");
            }
            sky.SetTexture("_Tex", cube);
            sky.SetFloat("_Exposure", .65f);
            EditorUtility.SetDirty(sky);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder + "neutral_tone.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, Folder + "neutral_tone.asset");
                var tone = profile.Add<Tonemapping>(true);
                tone.mode.value = TonemappingMode.Neutral;
                AssetDatabase.AddObjectToAsset(tone, profile);
                EditorUtility.SetDirty(profile);
            }

            var vase = MakePbr("vase");
            MakePbr("wood");
            // Keep the untextured Lit variant available to runtime legacy-material conversion.
            if (AssetDatabase.LoadAssetAtPath<Material>(Folder + "fallback.mat") == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")), Folder + "fallback.mat");
            var model = (ModelImporter)AssetImporter.GetAtPath(Folder + "ceramic_vase.fbx");
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.importCameras = false;
            model.importLights = false;
            model.SaveAndReimport();
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(model.assetPath);
            foreach (var mesh in imported.GetComponentsInChildren<Renderer>())
                foreach (var material in mesh.sharedMaterials)
                    if (material != null)
                        model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), vase);
            model.SaveAndReimport();

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/DemoURP.asset");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/DemoRenderer.asset");
            if (pipeline == null || renderer == null) throw new Exception("Demo URP assets missing.");
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            pipelineSettings.FindProperty("m_MainLightShadowmapResolution").intValue = 2048;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            pipeline.shadowDistance = 8f;
            pipeline.shadowCascadeCount = 2;
            pipeline.cascade2Split = .25f;
            var data = new SerializedObject(renderer);
            data.FindProperty("postProcessData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            data.ApplyModifiedPropertiesWithoutUndo();
            ScriptableRendererFeature ao = renderer.rendererFeatures.Find(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion");
            if (ao == null)
            {
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                ao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                ao.name = "Contact ambient occlusion";
                AssetDatabase.AddObjectToAsset(ao, renderer);
                renderer.rendererFeatures.Add(ao);
            }
            var settings = new SerializedObject(ao);
            settings.FindProperty("m_Settings.Downsample").boolValue = true;
            settings.FindProperty("m_Settings.Intensity").floatValue = .8f;
            settings.FindProperty("m_Settings.Radius").floatValue = .025f;
            settings.FindProperty("m_Settings.DirectLightingStrength").floatValue = .15f;
            settings.FindProperty("m_Settings.Samples").enumValueIndex = 1;
            settings.FindProperty("m_Settings.BlurQuality").enumValueIndex = 0;
            settings.FindProperty("m_Settings.AOMethod").enumValueIndex = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            ao.SetActive(true);
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(ao);
            var quality = AssetDatabase.LoadAssetAtPath<RenderQualitySettings>(Folder + "render_quality.asset");
            if (quality == null)
            {
                quality = ScriptableObject.CreateInstance<RenderQualitySettings>();
                AssetDatabase.CreateAsset(quality, Folder + "render_quality.asset");
            }
            quality.pipeline = pipeline;
            quality.renderer = renderer;
            BakeAmbient(cube, quality);
            EditorUtility.SetDirty(quality);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
            Debug.Log("Realism configured: preserved materials, PBR maps, HDR reflections, 4x MSAA, soft shadows, downsampled SSAO.");
        }

        private static Material MakePbr(string prefix)
        {
            string path = Folder + prefix + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + prefix + "_albedo.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + prefix + "_normal.png"));
            material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + prefix + "_ao.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + prefix + "_metallic_smoothness.png"));
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_OcclusionStrength", .65f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BakeAmbient(Cubemap cube, RenderQualitySettings quality)
        {
            var probe = new SphericalHarmonicsL2();
            int mip = Mathf.Min(4, cube.mipmapCount - 1);
            int size = cube.width >> mip;
            for (int face = 0; face < 6; face++)
            {
                var pixels = cube.GetPixels((CubemapFace)face, mip);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float u = 2f * (x + .5f) / size - 1f, v = 2f * (y + .5f) / size - 1f;
                    Vector3 direction;
                    switch ((CubemapFace)face)
                    {
                        case CubemapFace.PositiveX: direction = new Vector3(1, -v, -u); break;
                        case CubemapFace.NegativeX: direction = new Vector3(-1, -v, u); break;
                        case CubemapFace.PositiveY: direction = new Vector3(u, 1, v); break;
                        case CubemapFace.NegativeY: direction = new Vector3(u, -1, -v); break;
                        case CubemapFace.PositiveZ: direction = new Vector3(u, -v, 1); break;
                        default: direction = new Vector3(-u, -v, -1); break;
                    }
                    float solidAngle = 4f / (size * size * Mathf.Pow(1 + u * u + v * v, 1.5f));
                    probe.AddDirectionalLight(direction.normalized, pixels[y * size + x], solidAngle * .085f);
                }
            }
            quality.ambientCoefficients = new float[27];
            for (int channel = 0; channel < 3; channel++)
                for (int term = 0; term < 9; term++) quality.ambientCoefficients[channel * 9 + term] = probe[channel, term];
        }
    }
}
