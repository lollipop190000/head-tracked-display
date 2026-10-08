using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HeadTracked.Demo
{
    public sealed class RealismLighting : MonoBehaviour
    {
        private VolumeProfile profile;
        private Material sky;

        public void Configure(Camera camera)
        {
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.allowMSAA = true;
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 5;
            var authored = Resources.Load<VolumeProfile>("Realism/neutral_tone");
            profile = authored != null ? Instantiate(authored) : ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            if (!profile.Has<Tonemapping>()) profile.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;
            var cube = Resources.Load<Cubemap>("Realism/studio");
            if (cube != null)
            {
                sky = new Material(Resources.Load<Material>("Realism/studio_sky"));
                sky.SetTexture("_Tex", cube);
                sky.SetFloat("_Exposure", .65f);
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = .7f;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = cube;
                RenderSettings.reflectionIntensity = .75f;
                var quality = Resources.Load<RenderQualitySettings>("Realism/render_quality");
                if (quality != null) quality.ApplyAmbient();
            }
        }

        private void OnDestroy()
        { if (profile != null) Destroy(profile); if (sky != null) Destroy(sky); }
    }
}
