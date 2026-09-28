#nullable enable

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>Flipbook strip metadata from particles.json (TDD §14.2); defaults describe an 8-frame strip at 12 fps.</summary>
    public sealed class VfxSheet
    {
        public string name = "";
        public Texture2D? texture;
        public int columns = 8;
        public int rows = 1;
        public float fps = 12f;
        public bool loop;
    }

    /// <summary>
    /// Loads the particle flipbooks and owns the VFX materials in Assets/Materials/BilliardRogue/Vfx (one per shading ×
    /// sheet, shared by every prefab so particle systems batch). Uses the Rendering module's LitParticle/GlowParticle
    /// shaders (TDD §16); until they exist, URP particle shaders stand in so the prefabs still render (re-run to upgrade).
    /// </summary>
    public sealed class VfxMaterialLibrary
    {
        public const string SheetRoot = "Assets/Sprites/BilliardRogue/Particles";
        public const string MaterialRoot = "Assets/Materials/BilliardRogue/Vfx";
        const string SheetManifest = SheetRoot + "/particles.json";
        const string LitShaderName = "BilliardRogue/LitParticle";
        const string GlowShaderName = "BilliardRogue/GlowParticle";
        const string LitFallbackName = "Universal Render Pipeline/Particles/Simple Lit";
        const string GlowFallbackName = "Universal Render Pipeline/Particles/Unlit";
        const float DefaultGlowIntensity = 1.5f;
        const float DefaultLightInfluence = 0.9f;
        const float LitSelfEmission = 0.12f;
        const float AlphaCutoff = 0.5f;

        // HDR multiplier per glow sheet: the world Volume blooms above 1.0 (research/urp-hd2d-rendering.md §5); kept
        // below ~2 so saturated vertex colours keep their hue through the neutral tonemapper instead of clipping to white.
        static readonly Dictionary<string, float> glowIntensity = new()
        {
            { "Flash", 1.8f }, { "Spark", 1.6f }, { "Bolt", 1.8f }, { "Star", 1.6f }, { "Ring", 1.5f }, { "Ember", 1.6f },
            { "Mote", 1.4f }, { "Snow", 1.25f }, { "Smoke", 1.5f }, { "Bubble", 1.25f }, { "Heart", 1.35f },
        };

        // Smoke and dust read softer when the key light dominates less.
        static readonly Dictionary<string, float> lightInfluence = new() { { "Smoke", 0.75f }, { "Dust", 0.8f } };

        readonly Dictionary<string, VfxSheet> sheets = new();
        readonly Dictionary<string, Material> materials = new();
        readonly List<string> warnings;
        readonly Shader litShader;
        readonly Shader glowShader;
        readonly bool litFallback;
        readonly bool glowFallback;

        public VfxMaterialLibrary(List<string> warnings)
        {
            this.warnings = warnings;
            litShader = ResolveShader(LitShaderName, LitFallbackName, out litFallback);
            glowShader = ResolveShader(GlowShaderName, GlowFallbackName, out glowFallback);
            LoadManifest();
        }

        #region Public Methods

        public VfxSheet Sheet(string sheetName)
        {
            if (sheets.TryGetValue(sheetName, out var sheet)) return sheet;
            sheet = new VfxSheet { name = sheetName, texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SheetRoot}/{sheetName}.png") };
            if (sheet.texture == null) Warn($"{SheetRoot}/{sheetName}.png missing: its layers render as untextured squares until the sheet exists.");
            sheets[sheetName] = sheet;
            return sheet;
        }

        public Material Material(VfxShading shading, string sheetName)
        {
            var key = $"M_Vfx_{shading}_{sheetName}";
            if (materials.TryGetValue(key, out var material)) return material;

            BuilderAssets.EnsureFolder(MaterialRoot);
            var path = $"{MaterialRoot}/{key}.mat";
            var shader = shading == VfxShading.Lit ? litShader : glowShader;
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = key };
                AssetDatabase.CreateAsset(material, path);
            }

            Configure(material, shader, shading, Sheet(sheetName));
            EditorUtility.SetDirty(material);
            materials[key] = material;
            return material;
        }

        #endregion

        #region Materials

        void Configure(Material material, Shader shader, VfxShading shading, VfxSheet sheet)
        {
            material.shader = shader;
            material.shaderKeywords = System.Array.Empty<string>();
            material.renderQueue = -1;
            material.SetTexture("_BaseMap", sheet.texture);
            material.SetFloat("_Cutoff", AlphaCutoff);
            if (shading == VfxShading.Lit)
            {
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_LightInfluence", lightInfluence.TryGetValue(sheet.name, out var influence) ? influence : DefaultLightInfluence);
                material.SetFloat("_EmissionStrength", LitSelfEmission);
                if (litFallback) SetupFallbackLit(material);
                return;
            }

            var intensity = glowIntensity.TryGetValue(sheet.name, out var value) ? value : DefaultGlowIntensity;
            material.SetFloat("_Intensity", intensity);
            if (glowFallback)
            {
                SetupFallbackGlow(material, intensity);
                return;
            }

            material.SetColor("_BaseColor", Color.white);
        }

        static void SetupFallbackLit(Material material)
        {
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 1f);
            BaseShaderGUI.SetMaterialKeywords(material, SimpleLitGUI.SetMaterialKeywords, ParticleGUI.SetMaterialKeywords);
        }

        static void SetupFallbackGlow(Material material, float intensity)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetColor("_BaseColor", new Color(intensity, intensity, intensity, 1f));
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
        }

        Shader ResolveShader(string wanted, string fallback, out bool usedFallback)
        {
            var shader = Shader.Find(wanted);
            usedFallback = shader == null;
            if (!usedFallback) return shader!;
            Warn($"Shader {wanted} missing (Rendering module): VFX materials use {fallback} until the builder is re-run.");
            return Shader.Find(fallback);
        }

        #endregion

        #region Manifest

        void LoadManifest()
        {
            if (!File.Exists(SheetManifest))
            {
                Warn($"{SheetManifest} missing: every sheet is assumed to be an 8×1 strip at 12 fps.");
                return;
            }

            var root = JObject.Parse(File.ReadAllText(SheetManifest));
            if (root["sheets"] is not JObject entries) return;
            foreach (var entry in entries.Properties())
            {
                var data = (JObject)entry.Value;
                var sheet = new VfxSheet
                {
                    name = entry.Name,
                    columns = data.Value<int?>("columns") ?? 8,
                    rows = data.Value<int?>("rows") ?? 1,
                    fps = data.Value<float?>("fps") ?? 12f,
                    loop = data.Value<bool?>("loop") ?? false,
                };
                var file = data.Value<string>("file") ?? entry.Name + ".png";
                sheet.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SheetRoot}/{file}");
                if (sheet.texture == null) Warn($"{SheetRoot}/{file} missing: its layers render as untextured squares until the sheet exists.");
                sheets[entry.Name] = sheet;
            }
        }

        void Warn(string message)
        {
            warnings.Add(message);
            Debug.LogWarning("[VfxPrefabsBuilder] " + message);
        }

        #endregion
    }
}
