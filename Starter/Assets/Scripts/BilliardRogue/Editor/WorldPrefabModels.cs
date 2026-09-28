#nullable enable

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Asset helpers shared by WorldPrefabsBuilder and BoardPrefabBuilder: FBX model instantiation with primitive
    /// placeholders (TDD §14.1), material lookup with fallbacks (TDD §16), part lookup by child name, emissive
    /// tagging, sprite loading and prefab saving. Everything degrades with a warning when content is not built yet.
    /// </summary>
    public static class WorldPrefabModels
    {
        public const string ModelRoot = "Assets/Models/BilliardRogue";
        public const string MaterialRoot = "Assets/Materials/BilliardRogue";
        public const string PrefabRoot = "Assets/Prefabs/BilliardRogue";
        public const string IconRoot = "Assets/Sprites/BilliardRogue/Icons";
        public const string FontRoot = "Assets/Fonts/BilliardRogue";
        public const string FallbackFolder = PrefabRoot + "/Board/Fallback";
        const string PaletteTexture = "Assets/Textures/BilliardRogue/Palette/Palette_Main.png";
        const string PaletteP2Texture = "Assets/Textures/BilliardRogue/Palette/Palette_CatP2.png";

        static readonly string[] EmissiveTokens = { "Emissive", "Gem", "Orb", "Flame", "Crystal", "Fuse", "Eyes" };
        static readonly List<string> warnings = new();
        static Scene staging;
        static bool stagingOpen;

        public static IReadOnlyList<string> Warnings => warnings;

        public static void ResetWarnings() => warnings.Clear();

        public static void Warn(string message)
        {
            warnings.Add(message);
            Debug.LogWarning("[WorldPrefabsBuilder] " + message);
        }

        #region Staging scene

        /// <summary>Temporary prefab roots live in a preview scene so the builder never dirties the open scene.</summary>
        public static void BeginStaging()
        {
            if (stagingOpen) return;
            staging = EditorSceneManager.NewPreviewScene();
            stagingOpen = true;
        }

        public static void EndStaging()
        {
            if (!stagingOpen) return;
            EditorSceneManager.ClosePreviewScene(staging);
            stagingOpen = false;
        }

        public static GameObject NewRoot(string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            if (stagingOpen) SceneManager.MoveGameObjectToScene(go, staging);
            return go;
        }

        #endregion

        #region Layers & materials

        public static int WorldLayer()
        {
            var layer = LayerMask.NameToLayer(WorldLayers.WorldLayerName);
            if (layer < 0) Warn("Layer 'World' does not exist yet (RenderPipelineBuilder); prefabs keep the Default layer, pools apply it at runtime.");
            return layer;
        }

        public static Material Palette(bool player2)
        {
            var name = player2 ? "M_Palette_CatP2" : "M_Palette";
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat");
            if (material != null) return material;
            Warn($"{name}.mat missing (MaterialsBuilder); using a fallback URP Lit palette material.");
            return Fallback(player2 ? "M_Fallback_PaletteP2" : "M_Fallback_Palette", "Universal Render Pipeline/Lit", player2 ? PaletteP2Texture : PaletteTexture);
        }

        public static Material Glow()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/M_GlowParticle_Default.mat");
            if (material != null) return material;
            Warn("M_GlowParticle_Default.mat missing (MaterialsBuilder); using a fallback URP Unlit material.");
            return Fallback("M_Fallback_Glow", "Universal Render Pipeline/Unlit", null);
        }

        /// <summary>HDR additive trail behind balls in flight (MaterialsBuilder); the plain glow material when it is not built yet.</summary>
        public static Material BallTrail(Material glowFallback)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/M_BallTrail.mat");
            if (material != null) return material;
            Warn("M_BallTrail.mat missing (MaterialsBuilder); the ball trail uses the default glow material.");
            return glowFallback;
        }

        public static Material AimGuide()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/M_AimGuide.mat");
            if (material != null) return material;
            Warn("M_AimGuide.mat missing (MaterialsBuilder); the aim guide uses the glow fallback.");
            return Glow();
        }

        public static Material? Ball(Simulation.BallType type)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/M_Ball_{type}.mat");
        }

        static Material Fallback(string name, string shaderName, string? texturePath)
        {
            var path = $"{FallbackFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            BuilderAssets.EnsureFolder(FallbackFolder);
            var shader = Shader.Find(shaderName);
            var material = new Material(shader != null ? shader : Shader.Find("Standard"));
            if (texturePath != null)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture != null) material.SetTexture("_BaseMap", texture);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        #endregion

        #region Models

        /// <summary>
        /// Instantiates the FBX at ModelRoot/relativePath (nested prefab instance, so model re-exports flow through)
        /// or a primitive placeholder when the model is missing, inside a "name" container under parent. Views
        /// animate the container; parts are looked up below it. A default import merges a single top-level node
        /// into the FBX root, so part lookups fall back to FbxRoot(container) when a named body part is absent.
        /// </summary>
        public static GameObject InstantiateModel(string relativePath, Transform parent, string name, PrimitiveType placeholder, Vector3 placeholderScale, int layer, out bool isPlaceholder)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);
            var path = $"{ModelRoot}/{relativePath}";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, container.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                isPlaceholder = false;
            }
            else
            {
                Warn($"{path} missing; using a {placeholder} placeholder.");
                CreatePrimitive(container.transform, "Placeholder", placeholder, new Vector3(0f, placeholderScale.y * 0.5f, 0f), placeholderScale, null, layer);
                isPlaceholder = true;
            }

            WorldLayers.Apply(container, layer);
            return container;
        }

        /// <summary>The FBX instance (or placeholder) root inside a container made by InstantiateModel.</summary>
        public static Transform FbxRoot(GameObject container) => container.transform.GetChild(0);

        public static GameObject CreatePrimitive(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material? material, int layer)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            WorldLayers.Apply(go, layer);
            return go;
        }

        /// <summary>Assigns the material to every renderer slot under root and collects the renderers.</summary>
        public static void ApplyMaterial(GameObject root, Material material, List<Renderer> renderers)
        {
            renderers.Clear();
            root.GetComponentsInChildren(true, renderers);
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials.Length == 0 ? new[] { material } : materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        public static Transform? FindPart(Transform root, string partName)
        {
            if (root.name == partName) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindPart(root.GetChild(i), partName);
                if (found != null) return found;
            }

            return null;
        }

        public static Transform? FindFirstPart(Transform root, params string[] names)
        {
            foreach (var name in names)
            {
                var part = FindPart(root, name);
                if (part != null) return part;
            }

            return null;
        }

        public static List<Transform> FindParts(Transform root, params string[] names)
        {
            var result = new List<Transform>();
            foreach (var name in names)
            {
                var part = FindPart(root, name);
                if (part != null) result.Add(part);
            }

            return result;
        }

        /// <summary>Renderers whose part name contains an emissive token (TDD §14.1), plus explicit extra part names.</summary>
        public static List<Renderer> CollectEmissive(List<Renderer> renderers, params string[] extraNames)
        {
            var result = new List<Renderer>();
            foreach (var renderer in renderers)
            {
                var name = renderer.gameObject.name;
                var emissive = System.Array.IndexOf(extraNames, name) >= 0;
                if (!emissive)
                {
                    foreach (var token in EmissiveTokens)
                    {
                        if (!name.Contains(token)) continue;
                        emissive = true;
                        break;
                    }
                }

                if (emissive) result.Add(renderer);
            }

            return result;
        }

        public static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position + Vector3.up * 0.5f, Vector3.one);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        #endregion

        #region Sprites, fonts & prefabs

        /// <summary>Loads an icon sprite; converts a plain texture import to Sprite when ImportSettingsBuilder has not run yet.</summary>
        public static Sprite? LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) return sprite;
            }

            Warn($"Sprite {path} not available yet; slot left empty.");
            return null;
        }

        public static TMPro.TMP_FontAsset? LoadFont(string assetName)
        {
            return AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>($"{FontRoot}/{assetName}.asset");
        }

        /// <summary>Saves root as a prefab asset at path (replacing any previous version) and destroys the temp object.</summary>
        public static GameObject SavePrefab(GameObject root, string path)
        {
            try
            {
                BuilderAssets.EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        #endregion
    }
}
