using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeadTracked.Demo
{
    /// <summary>Demo-only material A/B. Retains submesh slots and never modifies imported materials.</summary>
    public sealed class SurfaceAppearanceComparison : IDisposable
    {
        private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<Material, Material> plain = new Dictionary<Material, Material>();
        private static readonly string[] TextureProperties =
        {
            "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap", "_SpecGlossMap", "_OcclusionMap",
            "_ParallaxMap", "_DetailMask", "_DetailAlbedoMap", "_DetailNormalMap", "_EmissionMap"
        };
        private static readonly string[] TextureKeywords =
        {
            "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_PARALLAXMAP",
            "_DETAIL_MULX2", "_DETAIL_SCALED", "_EMISSION"
        };

        public void Register(GameObject root)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (!originals.ContainsKey(renderer)) originals.Add(renderer, renderer.sharedMaterials);
        }

        public void Apply(bool detailed)
        {
            foreach (var pair in originals)
            {
                if (pair.Key == null) continue;
                if (detailed) { pair.Key.sharedMaterials = pair.Value; continue; }
                var materials = new Material[pair.Value.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = Plain(pair.Value[i]);
                pair.Key.sharedMaterials = materials;
            }
        }

        private Material Plain(Material original)
        {
            if (original == null) return null;
            if (plain.TryGetValue(original, out var cached)) return cached;
            var material = new Material(original) { name = original.name + " (plain comparison)" };
            foreach (string property in TextureProperties)
            {
                // Alpha coverage can define foliage silhouettes; retain it in the plain variant.
                bool coverage = (property == "_BaseMap" || property == "_MainTex") &&
                    ((material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > .5f) ||
                     (material.HasProperty("_Surface") && material.GetFloat("_Surface") > .5f));
                if (!coverage && material.HasProperty(property)) material.SetTexture(property, null);
            }
            foreach (string keyword in TextureKeywords) material.DisableKeyword(keyword);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .25f);
            if (material.HasProperty("_SpecColor")) material.SetColor("_SpecColor", new Color(.04f, .04f, .04f));
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            plain.Add(original, material);
            return material;
        }

        public void Dispose()
        {
            Apply(true);
            foreach (var material in plain.Values) UnityEngine.Object.Destroy(material);
            originals.Clear(); plain.Clear();
        }
    }
}
