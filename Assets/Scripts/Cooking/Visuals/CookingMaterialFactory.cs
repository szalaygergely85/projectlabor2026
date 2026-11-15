using System.Collections.Generic;
using UnityEngine;

namespace CookingRehab
{
    public static class CookingMaterialFactory
    {
        private static readonly Dictionary<string, Material> Cache =
            new Dictionary<string, Material>();

        public static Material Lit(Color color, float metallic = 0f, float smoothness = 0.4f)
        {
            return GetMaterial(color, metallic, smoothness, 0f, false);
        }

        public static Material Emissive(Color color, float intensity = 2f, float smoothness = 0.3f)
        {
            return GetMaterial(color, 0f, smoothness, intensity, true);
        }

        public static Material GetMaterial(Color color, float metallic, float smoothness,
            float emissionIntensity, bool emissive)
        {
            string key = string.Format(
                "{0:F3}_{1:F3}_{2:F3}_{3:F2}_{4:F2}_{5:F2}_{6}",
                color.r, color.g, color.b, metallic, smoothness, emissionIntensity, emissive);

            Material cached;
            if (Cache.TryGetValue(key, out cached) && cached != null)
                return cached;

            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("HDRP/Unlit");

            Material material = new Material(shader);
            material.name = "Cooking_" + key;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);

            if (emissive)
            {
                Color emission = color * Mathf.Max(0f, emissionIntensity);
                if (material.HasProperty("_EmissiveColor"))
                    material.SetColor("_EmissiveColor", emission);
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission);
                }
                material.EnableKeyword("_EMISSIVE_COLOR");
            }

            Cache[key] = material;
            return material;
        }

        public static void Apply(GameObject target, Material material)
        {
            if (target == null || material == null)
                return;

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }
    }
}
