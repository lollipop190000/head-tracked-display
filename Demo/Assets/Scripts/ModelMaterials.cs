using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeadTracked.Demo
{
    /// <summary>Preserve URP/custom materials; translate known legacy shader properties without flattening submeshes.</summary>
    public sealed class ModelMaterials
    {
        private readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();

        public Material Prepare(Material original, Material fallback)
        {
            if (original == null) return fallback;
            string name = original.shader.name;
            if (name != "Standard" && name != "Standard (Specular setup)" && name != "Legacy Shaders/Diffuse")
                return original;
            if (converted.TryGetValue(original, out var existing)) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = original.name + " (URP)" };
            CopyTexture(original, material, "_MainTex", "_BaseMap");
            if (original.HasProperty("_Color")) material.SetColor("_BaseColor", original.GetColor("_Color"));
            CopyTexture(original, material, "_BumpMap", "_BumpMap", "_NORMALMAP");
            CopyTexture(original, material, "_OcclusionMap", "_OcclusionMap");
            CopyTexture(original, material, "_EmissionMap", "_EmissionMap");
            CopyTexture(original, material, "_MetallicGlossMap", "_MetallicGlossMap", "_METALLICSPECGLOSSMAP");
            CopyFloat(original, material, "_Metallic", "_Metallic");
            CopyFloat(original, material, "_Glossiness", "_Smoothness");
            CopyFloat(original, material, "_BumpScale", "_BumpScale");
            CopyFloat(original, material, "_OcclusionStrength", "_OcclusionStrength");
            CopyFloat(original, material, "_Cutoff", "_Cutoff");
            if (name == "Standard (Specular setup)")
            {
                material.SetFloat("_WorkflowMode", 0f);
                material.EnableKeyword("_SPECULAR_SETUP");
                material.SetColor("_SpecColor", original.GetColor("_SpecColor"));
                CopyTexture(original, material, "_SpecGlossMap", "_SpecGlossMap", "_METALLICSPECGLOSSMAP");
            }
            if (original.HasProperty("_EmissionColor"))
            {
                Color emission = original.GetColor("_EmissionColor");
                material.SetColor("_EmissionColor", emission);
                if (emission.maxColorComponent > 0f) material.EnableKeyword("_EMISSION");
            }
            if (original.HasProperty("_Mode"))
            {
                int mode = (int)original.GetFloat("_Mode");
                if (mode == 1)
                {
                    material.SetFloat("_AlphaClip", 1);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                }
                else if (mode >= 2)
                {
                    material.SetFloat("_Surface", 1);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)RenderQueue.Transparent;
                }
            }
            converted.Add(original, material);
            return material;
        }

        private static void CopyFloat(Material from, Material to, string source, string target)
        { if (from.HasProperty(source)) to.SetFloat(target, from.GetFloat(source)); }

        private static void CopyTexture(Material from, Material to, string source, string target, string keyword = null)
        {
            if (!from.HasProperty(source) || from.GetTexture(source) == null) return;
            to.SetTexture(target, from.GetTexture(source));
            to.SetTextureScale(target, from.GetTextureScale(source));
            to.SetTextureOffset(target, from.GetTextureOffset(source));
            if (keyword != null) to.EnableKeyword(keyword);
        }

        public void Dispose()
        { foreach (var material in converted.Values) Object.Destroy(material); converted.Clear(); }
    }
}
