using System;
using System.Collections.Generic;
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
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.shadows != LightShadows.None) light.shadows = enhanced ? LightShadows.Soft : LightShadows.Hard;
            foreach (var feature in renderer.rendererFeatures)
                if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion") feature.SetActive(enhanced);
        }
    }

    /// <summary>Temporary URP settings for the compact billboard scene. The reusable package has no URP dependency.</summary>
    public sealed class BillboardRenderProfile : IDisposable
    {
        private readonly UniversalRenderPipelineAsset pipeline;
        private readonly Camera camera;
        private readonly UniversalAdditionalCameraData cameraData;
        private readonly List<ScriptableRendererFeature> ao = new List<ScriptableRendererFeature>();
        private readonly List<bool> previousAO = new List<bool>();
        private readonly int previousMSAA, previousCascades;
        private readonly float previousScale, previousShadowDistance;
        private readonly bool previousAllowMSAA;
        private readonly AntialiasingMode previousAA;
        private readonly TemporalAA.Settings previousTAA;
        private bool applied, disposed, lastStable, lastAO;
        private float lastScale, lastShadowDistance;

        public BillboardRenderProfile(RenderQualitySettings quality, Camera target)
        {
            pipeline = quality.pipeline;
            camera = target;
            cameraData = camera.GetUniversalAdditionalCameraData();
            previousMSAA = pipeline.msaaSampleCount;
            previousCascades = pipeline.shadowCascadeCount;
            previousScale = pipeline.renderScale;
            previousShadowDistance = pipeline.shadowDistance;
            previousAllowMSAA = camera.allowMSAA;
            previousAA = cameraData.antialiasing;
            previousTAA = cameraData.taaSettings;
            foreach (var feature in quality.renderer.rendererFeatures)
                if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                { ao.Add(feature); previousAO.Add(feature.isActive); }
        }

        public void Apply(bool stable, bool contactAO, float renderScale, float shadowDistance)
        {
            if (disposed) return;
            renderScale = float.IsNaN(renderScale) || float.IsInfinity(renderScale) ? .85f : Mathf.Clamp(renderScale, .65f, 1f);
            shadowDistance = Mathf.Clamp(shadowDistance, .8f, 5f);
            if (applied && lastStable == stable && lastAO == contactAO &&
                lastScale == renderScale && lastShadowDistance == shadowDistance) return;
            bool reset = !applied || lastStable != stable || lastAO != contactAO || lastScale != renderScale;
            // TAA requires a single-sampled target. MSAA remains available for an immediate A/B comparison.
            pipeline.msaaSampleCount = stable ? 1 : 4;
            camera.allowMSAA = !stable;
            cameraData.antialiasing = stable ? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.None;
            if (stable)
            {
                ref var taa = ref cameraData.taaSettings;
                taa.quality = TemporalAAQuality.Medium;
                taa.baseBlendFactor = .75f; // 25% current frame: less persistence than the URP default.
                taa.jitterScale = .75f;
                taa.mipBias = 0f;
                taa.varianceClampScale = .9f;
                taa.contrastAdaptiveSharpening = 0f;
            }
            pipeline.renderScale = renderScale;
            // A compact scene does not need the normal demo's 8 m, two-cascade shadow coverage.
            pipeline.shadowCascadeCount = 1;
            pipeline.shadowDistance = shadowDistance;
            foreach (var feature in ao) feature.SetActive(contactAO);
            if (reset) ResetHistory();
            applied = true; lastStable = stable; lastAO = contactAO;
            lastScale = renderScale; lastShadowDistance = shadowDistance;
        }

        public void ResetHistory() { if (cameraData != null) cameraData.resetHistory = true; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            pipeline.msaaSampleCount = previousMSAA;
            pipeline.shadowCascadeCount = previousCascades;
            pipeline.renderScale = previousScale;
            pipeline.shadowDistance = previousShadowDistance;
            for (int i = 0; i < ao.Count; i++) if (ao[i] != null) ao[i].SetActive(previousAO[i]);
            if (camera != null) camera.allowMSAA = previousAllowMSAA;
            if (cameraData != null)
            {
                cameraData.antialiasing = previousAA;
                cameraData.taaSettings = previousTAA;
                ResetHistory();
            }
        }
    }
}
