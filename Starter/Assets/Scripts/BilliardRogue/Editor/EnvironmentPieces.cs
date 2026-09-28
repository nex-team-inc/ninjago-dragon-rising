#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Environment-kit helpers for EnvironmentBuilder: Env_*.fbx instancing (nested model prefabs, cube placeholder
    /// when a model is missing), part lookup that survives the default FBX import merging a lone part into the root,
    /// TDD §16 materials with committed fallbacks while MaterialsBuilder has not run, static-batching and shadow flags.
    /// </summary>
    public static class EnvironmentPieces
    {
        public const string ModelRoot = "Assets/Models/BilliardRogue/Environment";
        public const string MaterialRoot = "Assets/Materials/BilliardRogue";
        public const string FallbackFolder = "Assets/Prefabs/BilliardRogue/Environment/Fallback";
        const string PaletteTexture = "Assets/Textures/BilliardRogue/Palette/Palette_Main.png";
        const string PaletteEmissionTexture = "Assets/Textures/BilliardRogue/Palette/Palette_Emission.png";
        const string SurfaceTextureFormat = "Assets/Textures/BilliardRogue/Surfaces/{0}_Albedo.png";
        const string LitShader = "Universal Render Pipeline/Lit";
        const string ParticlesUnlitShader = "Universal Render Pipeline/Particles/Unlit";

        /// <summary>Parts animated by DioramaAnimator: never static-batched.</summary>
        static readonly string[] AnimatedParts = { "Flame", "Canopy", "Cloth", "Water", "Water_Emissive", "Glints_Emissive" };

        static readonly List<string> warnings = new();
        static readonly HashSet<string> warned = new();
        static readonly Dictionary<string, Material> materials = new();

        public static IReadOnlyList<string> Warnings => warnings;

        public static void Reset()
        {
            warnings.Clear();
            warned.Clear();
            materials.Clear();
        }

        public static void Warn(string message)
        {
            if (!warned.Add(message)) return;
            warnings.Add(message);
            Debug.LogWarning("[EnvironmentBuilder] " + message);
        }

        #region Models

        public static GameObject? LoadModel(string piece)
        {
            var path = $"{ModelRoot}/{piece}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) Warn($"{path} missing; using a cube placeholder.");
            return model;
        }

        /// <summary>Instantiates model (a nested prefab instance so re-exports flow through) or a cube placeholder, named name, under parent.</summary>
        public static GameObject Instantiate(GameObject? model, string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            GameObject instance;
            if (model != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(instance.GetComponent<Collider>());
                instance.transform.SetParent(parent, false);
                instance.GetComponent<MeshRenderer>().sharedMaterial = Palette();
            }

            instance.name = name;
            var t = instance.transform;
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = localScale;
            return instance;
        }

        /// <summary>Renderer of a named part; a model whose only mesh sits on the root (default import merges a lone part) returns the root renderer.</summary>
        public static Renderer? PartRenderer(GameObject instance, string part)
        {
            var found = WorldPrefabModels.FindPart(instance.transform, part);
            if (found != null) return found.GetComponent<Renderer>();
            var rootRenderer = instance.GetComponent<Renderer>();
            return rootRenderer != null && instance.GetComponentsInChildren<Renderer>(true).Length == 1 ? rootRenderer : null;
        }

        public static Transform? Part(GameObject instance, string part) => WorldPrefabModels.FindPart(instance.transform, part);

        public static void SetMaterial(Renderer renderer, Material material)
        {
            var slots = renderer.sharedMaterials;
            if (slots.Length <= 1)
            {
                renderer.sharedMaterial = material;
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = material;
            }

            renderer.sharedMaterials = slots;
        }

        public static void SetAllMaterials(GameObject root, Material material)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                SetMaterial(renderer, material);
            }
        }

        public static bool IsAnimatedPart(string name) => System.Array.IndexOf(AnimatedParts, name) >= 0;

        /// <summary>Marks every GameObject under root BatchingStatic except animated parts (and their children).</summary>
        public static void SetStatic(GameObject root)
        {
            if (IsAnimatedPart(root.name)) return;
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            var t = root.transform;
            for (var i = 0; i < t.childCount; i++)
            {
                SetStatic(t.GetChild(i).gameObject);
            }
        }

        public static void SetShadows(GameObject root, bool cast, bool receive = true)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = receive;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        #endregion

        #region Materials

        public static Material Palette()
        {
            return Load("M_Palette", () => CreateLit("M_Fallback_Palette", PaletteTexture, 1f, Color.white));
        }

        public static Material Surface(string surface)
        {
            if (string.IsNullOrEmpty(surface)) return Palette();
            return Load($"M_Surface_{surface}", () => CreateLit($"M_Fallback_Surface_{surface}", string.Format(SurfaceTextureFormat, surface), 0.5f, null));
        }

        public static Material DangerTile()
        {
            return Load("M_DangerTile", () => CreateLit("M_Fallback_DangerTile", PaletteTexture, 1f, new Color(1.6f, 0.16f, 0.12f)));
        }

        public static Material LightShaft()
        {
            return Load("M_LightShaft", CreateShaftFallback);
        }

        static Material Load(string name, System.Func<Material> fallback)
        {
            if (materials.TryGetValue(name, out var cached)) return cached;
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat");
            if (material == null)
            {
                Warn($"{MaterialRoot}/{name}.mat missing (MaterialsBuilder); using a committed fallback in {FallbackFolder}.");
                material = fallback();
            }

            materials[name] = material;
            return material;
        }

        // URP Lit stand-ins for BilliardRogue/ToonLit: matte, palette emission map on every palette material (only its
        // emissive swatches are lit), metre-scale surface UVs tiled to the 2 m surface textures.
        static Material CreateLit(string name, string texturePath, float tiling, Color? emission)
        {
            return LoadOrCreateFallback(name, LitShader, material =>
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null) Warn($"{texturePath} missing; fallback {name} is untextured.");
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
                material.SetFloat("_Smoothness", 0.05f);
                material.SetFloat("_EnvironmentReflections", 0f);
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                if (texturePath != PaletteTexture) return;
                material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteEmissionTexture));
                material.SetColor("_EmissionColor", emission ?? Color.white);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
        }

        // Additive stand-in for BilliardRogue/LightShaft (no noise or edge fade): a faint tint only.
        static Material CreateShaftFallback()
        {
            return LoadOrCreateFallback("M_Fallback_LightShaft", ParticlesUnlitShader, material =>
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 2f);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.One);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetColor("_BaseColor", new Color(0.09f, 0.08f, 0.06f, 1f));
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.EnableKeyword("_BLENDMODE_ADD");
                material.renderQueue = (int)RenderQueue.Transparent;
            });
        }

        static Material LoadOrCreateFallback(string name, string shaderName, System.Action<Material> configure)
        {
            var path = $"{FallbackFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            BuilderAssets.EnsureFolder(FallbackFolder);
            material = new Material(Shader.Find(shaderName)) { name = name };
            configure(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        #endregion
    }
}
