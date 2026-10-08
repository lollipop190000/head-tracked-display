using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace HeadTracked.Demo
{
    public sealed class RenderQualitySettings : ScriptableObject
    {
        public UniversalRenderPipelineAsset pipeline;
        public UniversalRendererData renderer;
        public float[] ambientCoefficients = new float[27];

        public void ApplyAmbient()
        {
            var probe = new SphericalHarmonicsL2();
            for (int channel = 0; channel < 3; channel++)
                for (int term = 0; term < 9; term++) probe[channel, term] = ambientCoefficients[channel * 9 + term];
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = probe;
        }

        public void Apply(bool enhanced)
        {
            pipeline.msaaSampleCount = enhanced ? 4 : 1;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.shadows != LightShadows.None) light.shadows = enhanced ? LightShadows.Soft : LightShadows.Hard;
            foreach (var feature in renderer.rendererFeatures)
                if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion") feature.SetActive(enhanced);
        }
    }
}
