#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Nex.BilliardRogue.Simulation;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds the TDD §16 materials into Assets/Materials/BilliardRogue: palette, surface sets, balls, light shaft,
    /// aim guide, particle defaults and the danger tile. Materials are regenerated from code each run (same asset,
    /// same GUID); textures and ball colours are loaded by path and degrade to defaults with a warning.
    /// </summary>
    public static class MaterialsBuilder
    {
        public const string MaterialRoot = "Assets/Materials/BilliardRogue";
        public const string GeneratedRoot = MaterialRoot + "/Generated";
        public const string NoiseTexturePath = GeneratedRoot + "/T_Noise64.asset";
        const string PaletteRoot = "Assets/Textures/BilliardRogue/Palette";
        const string SurfaceRoot = "Assets/Textures/BilliardRogue/Surfaces";
        const string BallConfigRoot = BuilderAssets.ConfigRoot + "/Balls";
        const string ToonLit = "BilliardRogue/ToonLit";
        const string ToonLitTransparent = "BilliardRogue/ToonLitTransparent";
        const string FallbackShader = "Universal Render Pipeline/Lit";
        // Emissive palette cells (torches, crystals, eyes) at material strength 1: above the 1.2 bloom threshold, but
        // no longer the 2.2 that turned every emissive part into a bloom blob (views pulse _EmissionStrength on top).
        const float PaletteEmission = 1.6f;
        // Resting glow of the material alone (menus, icons; RenderingContractTests keeps it under 1.5 so the ball colour
        // still reads); BallView raises _EmissionStrength to JuiceConfig.Balls.flightGlow in play.
        const float BallEmission = 0.7f;

        /// <summary>Always-built surface set; surfaces.json (the 2D pipeline manifest) may add more, see SurfaceNames().</summary>
        public static readonly string[] Surfaces = { "StoneFloor", "MossyBrick", "CryptBrick", "CryptFloor", "WoodPlank", "CrystalRock", "Dirt", "Grass" };
        /// <summary>Surfaces used as the arena floor (layouts.json Env_FloorTile / Env_LaunchPad Top_Surface): calmer, darker material settings.</summary>
        public static readonly string[] FloorSurfaces = { "StoneFloor", "CryptFloor", "RuinFloor", "HollowFloor" };
        static readonly Color FloorTint = new(0.82f, 0.82f, 0.86f);
        // Shadow tint semantics: light * lerp(tint, 1, ramp). Darker tints = harder, more visible sun shadows.
        static readonly Color ActorShadowTint = new(0.34f, 0.32f, 0.5f);
        static readonly Color FloorShadowTint = new(0.3f, 0.29f, 0.44f);
        static readonly List<string> warnings = new();

        [MenuItem("Nex/Billiard Rogue/Materials", priority = 31)]
        static void Menu() => Debug.Log(Run());

        #region Public Methods

        public static string Run()
        {
            warnings.Clear();
            BuilderAssets.EnsureFolder(MaterialRoot);
            BuildPalette("M_Palette", "Palette_Main.png", ToonLit);
            BuildPalette("M_Palette_CatP2", "Palette_CatP2.png", ToonLit);
            var ghost = BuildPalette("M_Palette_Ghost", "Palette_Main.png", ToonLitTransparent);
            ghost.SetFloat("_Alpha", 0.45f);
            var surfaces = SurfaceNames();
            foreach (var surface in surfaces)
            {
                BuildSurface(surface);
            }

            foreach (BallType type in Enum.GetValues(typeof(BallType)))
            {
                BuildBall(type);
            }

            BuildLightShaft();
            BuildAimGuide();
            BuildGlowParticle();
            BuildBallTrail();
            BuildLitParticle();
            BuildDangerTile();
            BuildFloorBase();
            AssetDatabase.SaveAssets();
            return $"[MaterialsBuilder] built {3 + surfaces.Count + Enum.GetValues(typeof(BallType)).Length + 7} materials ({surfaces.Count} surfaces), {warnings.Count} warning(s)";
        }

        #endregion

        #region Materials

        static Material BuildPalette(string name, string textureName, string shaderName)
        {
            var material = LoadOrCreate(name, shaderName);
            SetTexture(material, "_BaseMap", $"{PaletteRoot}/{textureName}", true);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Bands", 4f);
            material.SetFloat("_BandSoftness", 0.02f);
            material.SetColor("_ShadowTint", ActorShadowTint);
            material.SetFloat("_AmbientStrength", 0.35f);
            // Actors (enemies, cat, props): a strong rim so silhouettes separate from the darker floor.
            material.SetColor("_RimColor", new Color(1f, 0.95f, 0.85f, 0.55f));
            material.SetFloat("_RimPower", 3f);
            SetKeyword(material, "_EMISSION", "_UseEmission", true);
            SetTexture(material, "_EmissionMap", $"{PaletteRoot}/Palette_Emission.png", false);
            material.SetColor("_EmissionColor", Color.white * PaletteEmission);
            material.SetFloat("_EmissionStrength", 1f);
            SetKeyword(material, "_NORMALMAP", "_UseNormalMap", false);
            SetKeyword(material, "_CAVITYMAP", "_UseCavityMap", false);
            return material;
        }

        static void BuildSurface(string surface)
        {
            var material = LoadOrCreate($"M_Surface_{surface}", ToonLit);
            var albedo = SetTexture(material, "_BaseMap", $"{SurfaceRoot}/{surface}_Albedo.png", true);
            var normal = SetTexture(material, "_BumpMap", $"{SurfaceRoot}/{surface}_Normal.png", false);
            var cavity = SetTexture(material, "_CavityMap", $"{SurfaceRoot}/{surface}_Cavity.png", false);
            var floor = Array.IndexOf(FloorSurfaces, surface) >= 0;
            // Floors sit a value step below the actors and keep their relief subtle so enemies, balls and labels pop.
            material.SetColor("_BaseColor", albedo ? (floor ? FloorTint : Color.white) : SurfacePlaceholderColor(surface));
            SetKeyword(material, "_NORMALMAP", "_UseNormalMap", normal);
            material.SetFloat("_BumpScale", floor ? 1.1f : 2f);
            SetKeyword(material, "_CAVITYMAP", "_UseCavityMap", cavity);
            material.SetFloat("_CavityStrength", floor ? 0.4f : 0.75f);
            material.SetFloat("_Tiling", SurfaceTiling(surface));
            material.SetFloat("_Bands", 4f);
            material.SetColor("_ShadowTint", floor ? FloorShadowTint : ActorShadowTint);
            material.SetColor("_RimColor", new Color(1f, 0.95f, 0.85f, floor ? 0.05f : 0.15f));
            SetKeyword(material, "_EMISSION", "_UseEmission", false);
        }

        static void BuildBall(BallType type)
        {
            var material = LoadOrCreate($"M_Ball_{type}", ToonLit);
            var color = new Color(0.85f, 0.85f, 0.85f);
            var glow = new Color(0.4f, 0.4f, 0.4f);
            var definition = AssetDatabase.LoadAssetAtPath<BallDefinition>($"{BallConfigRoot}/Ball_{type}.asset");
            if (definition != null)
            {
                color = definition.Color;
                glow = definition.GlowColor;
            }
            else
            {
                Warn($"Ball_{type}.asset missing (ConfigAssetsBuilder); M_Ball_{type} uses neutral colours.");
            }

            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Bands", 4f);
            // Balls never fall into a dark shadow band: they must stay the brightest thing on the table.
            material.SetColor("_ShadowTint", new Color(0.7f, 0.68f, 0.8f));
            material.SetColor("_RimColor", new Color(1f, 1f, 1f, 0.6f));
            material.SetFloat("_RimPower", 3f);
            SetKeyword(material, "_EMISSION", "_UseEmission", true);
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_EmissionColor", glow * BallEmission);
            material.SetFloat("_EmissionStrength", 1f);
            SetKeyword(material, "_NORMALMAP", "_UseNormalMap", false);
            SetKeyword(material, "_CAVITYMAP", "_UseCavityMap", false);
        }

        static void BuildLightShaft()
        {
            var material = LoadOrCreate("M_LightShaft", "BilliardRogue/LightShaft");
            // Layouts drive the final brightness (shaft intensity 0.3–0.35 via MaterialPropertyBlock); these are the
            // resting values for a 10 m shaft in the act dioramas.
            material.SetColor("_Color", new Color(1f, 0.9f, 0.7f));
            material.SetFloat("_Intensity", 0.6f);
            material.SetTexture("_NoiseTex", NoiseTexture());
            material.SetVector("_NoiseScroll", new Vector4(0.02f, 0.08f, -0.015f, 0.05f));
            material.SetFloat("_EdgeSoftness", 0.35f);
            material.SetFloat("_EdgePower", 1.5f);
            material.SetFloat("_FadeDistance", 3f);
            SetKeyword(material, "_DEPTH_FADE", "_UseDepthFade", false);
        }

        static void BuildAimGuide()
        {
            var material = LoadOrCreate("M_AimGuide", "BilliardRogue/AimGuide");
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f) * 1.8f);
            material.SetFloat("_DashRatio", 0.5f);
            material.SetFloat("_ScrollSpeed", 2f);
            material.SetFloat("_FadeStart", 6f);
            material.SetFloat("_FadeEnd", 14f);
            material.SetFloat("_EdgeSoftness", 0.2f);
        }

        static void BuildGlowParticle()
        {
            var material = LoadOrCreate("M_GlowParticle_Default", "BilliardRogue/GlowParticle");
            material.SetColor("_BaseColor", Color.white * 2f);
            material.SetFloat("_Intensity", 1f);
            SetKeyword(material, "_SOFT_DISC", "_SoftDisc", true);
        }

        // Ball trail: vertex colours are clamped to 1, so the HDR headroom that makes the streak bloom lives here.
        static void BuildBallTrail()
        {
            var material = LoadOrCreate("M_BallTrail", "BilliardRogue/GlowParticle");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Intensity", 2.4f);
            SetKeyword(material, "_SOFT_DISC", "_SoftDisc", false);
        }

        // Bevelled Base part of every arena floor cell and the launch pad (EnvironmentArenaBuilder): a dark, matte
        // slab so the seams read as a calm grid under the lighter surface tops.
        static void BuildFloorBase()
        {
            var material = LoadOrCreate("M_ArenaFloorBase", ToonLit);
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", new Color(0.2f, 0.2f, 0.25f));
            material.SetFloat("_Bands", 3f);
            material.SetColor("_ShadowTint", FloorShadowTint);
            material.SetFloat("_AmbientStrength", 0.3f);
            material.SetColor("_RimColor", new Color(1f, 0.95f, 0.85f, 0.05f));
            material.SetFloat("_RimPower", 4f);
            SetKeyword(material, "_EMISSION", "_UseEmission", false);
            SetKeyword(material, "_NORMALMAP", "_UseNormalMap", false);
            SetKeyword(material, "_CAVITYMAP", "_UseCavityMap", false);
        }

        static void BuildLitParticle()
        {
            var material = LoadOrCreate("M_LitParticle_Default", "BilliardRogue/LitParticle");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_LightInfluence", 0.7f);
            material.SetFloat("_EmissionStrength", 0f);
            material.SetFloat("_Bands", 3f);
        }

        static void BuildDangerTile()
        {
            var material = LoadOrCreate("M_DangerTile", ToonLit);
            material.SetColor("_BaseColor", new Color(0.45f, 0.12f, 0.14f));
            material.SetFloat("_Bands", 3f);
            SetKeyword(material, "_EMISSION", "_UseEmission", true);
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.1f) * 2f);
            material.SetFloat("_EmissionStrength", 0f);   // pulsed by the danger-row script
        }

        #endregion

        #region Helpers

        static Material LoadOrCreate(string name, string shaderName)
        {
            var path = $"{MaterialRoot}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Warn($"Shader '{shaderName}' not found; {name} falls back to {FallbackShader}.");
                shader = Shader.Find(FallbackShader);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static bool SetTexture(Material material, string property, string path, bool warnWhenMissing)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            material.SetTexture(property, texture);
            if (texture == null && warnWhenMissing) Warn($"{path} missing; {material.name}.{property} left empty.");
            return texture != null;
        }

        static void SetKeyword(Material material, string keyword, string toggleProperty, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
            if (material.HasProperty(toggleProperty)) material.SetFloat(toggleProperty, enabled ? 1f : 0f);
        }

        /// <summary>The fixed set plus every surface the 2D pipeline manifest (surfaces.json "surfaces") delivered, in a stable order.</summary>
        static List<string> SurfaceNames()
        {
            var names = new List<string>(Surfaces);
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{SurfaceRoot}/surfaces.json");
            if (json == null) return names;
            // Keys of the "surfaces" object: `"Name": {` at the top level of that block.
            var block = json.text.IndexOf("\"surfaces\"", StringComparison.Ordinal);
            if (block < 0) return names;
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(json.text.Substring(block), "\"([A-Za-z0-9_]+)\"\\s*:\\s*\\{"))
            {
                var name = match.Groups[1].Value;
                if (name == "surfaces" || name == "maps" || names.Contains(name)) continue;
                if (AssetDatabase.LoadAssetAtPath<Texture2D>($"{SurfaceRoot}/{name}_Albedo.png") != null) names.Add(name);
            }

            return names;
        }

        // surfaces.json: "<Name>": { ... "tiling": 0.5 ... } — 1 UV = 1 m on *_Surface meshes, so _Tiling = 1 / tileMetres.
        static float SurfaceTiling(string surface)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{SurfaceRoot}/surfaces.json");
            if (json != null)
            {
                var match = Regex.Match(json.text, $"\"{surface}\"\\s*:\\s*\\{{[^}}]*?\"tiling\"\\s*:\\s*([0-9.]+)", RegexOptions.Singleline);
                if (match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var tiling)) return tiling;
            }

            Warn($"surfaces.json has no tiling for {surface}; M_Surface_{surface} uses _Tiling 1.");
            return 1f;
        }

        static Color SurfacePlaceholderColor(string surface)
        {
            return surface switch
            {
                "Grass" => new Color(0.35f, 0.55f, 0.25f),
                "Dirt" => new Color(0.45f, 0.35f, 0.25f),
                "WoodPlank" => new Color(0.55f, 0.4f, 0.25f),
                "CrystalRock" => new Color(0.45f, 0.4f, 0.6f),
                "MossyBrick" => new Color(0.4f, 0.45f, 0.35f),
                _ => new Color(0.5f, 0.5f, 0.52f),
            };
        }

        /// <summary>Tileable 64×64 value noise (3 octaves) for the light shafts; created once as a texture asset.</summary>
        static Texture2D NoiseTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(NoiseTexturePath);
            if (existing != null) return existing;
            BuilderAssets.EnsureFolder(GeneratedRoot);
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.R8, false)
            {
                name = "T_Noise64",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var random = new System.Random(1234);
            var pixels = new Color32[size * size];
            var lattice = new float[3][];
            var cells = new[] { 4, 8, 16 };
            for (var o = 0; o < 3; o++)
            {
                lattice[o] = new float[cells[o] * cells[o]];
                for (var i = 0; i < lattice[o].Length; i++)
                {
                    lattice[o][i] = (float)random.NextDouble();
                }
            }

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var value = 0f;
                    var amplitude = 0.5f;
                    for (var o = 0; o < 3; o++)
                    {
                        value += SampleLattice(lattice[o], cells[o], x / (float)size, y / (float)size) * amplitude;
                        amplitude *= 0.5f;
                    }

                    var v = (byte)Mathf.Clamp(Mathf.RoundToInt(value / 0.875f * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, NoiseTexturePath);
            return texture;
        }

        static float SampleLattice(float[] lattice, int cells, float u, float v)
        {
            var fx = u * cells;
            var fy = v * cells;
            var x0 = (int)fx;
            var y0 = (int)fy;
            var tx = Mathf.SmoothStep(0f, 1f, fx - x0);
            var ty = Mathf.SmoothStep(0f, 1f, fy - y0);
            var x1 = (x0 + 1) % cells;
            var y1 = (y0 + 1) % cells;
            var a = Mathf.Lerp(lattice[y0 * cells + x0], lattice[y0 * cells + x1], tx);
            var b = Mathf.Lerp(lattice[y1 * cells + x0], lattice[y1 * cells + x1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        static void Warn(string message)
        {
            warnings.Add(message);
            Debug.LogWarning("[MaterialsBuilder] " + message);
        }

        #endregion
    }
}
