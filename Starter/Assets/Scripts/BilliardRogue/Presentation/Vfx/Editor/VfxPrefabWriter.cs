#nullable enable

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Writes one VfxRecipe to Assets/Prefabs/BilliardRogue/Vfx/&lt;name&gt;.prefab (TDD §17). An existing prefab is edited in
    /// place (children rebuilt, root kept) so the root GameObject and ParticleSystem keep their file IDs and every
    /// reference to them (VfxManager registry, ActDefinition ambient slots) survives a rebuild.
    /// </summary>
    public static class VfxPrefabWriter
    {
        public const string PrefabRoot = "Assets/Prefabs/BilliardRogue/Vfx";

        // BoardEventPlayer spawns bursts at the enemy centre height; the plane starts there and VfxFloorPlane pins it.
        const float TypicalSpawnHeight = 0.45f;
        const float RootEndMargin = 0.05f;

        public static string PathOf(VfxRecipe recipe) => $"{PrefabRoot}/{recipe.prefabName}.prefab";

        public static GameObject Write(VfxRecipe recipe, VfxMaterialLibrary library, Scene staging, int worldLayer)
        {
            var path = PathOf(recipe);
            var existing = File.Exists(path);
            BuilderAssets.EnsureFolder(PrefabRoot);
            var root = existing ? PrefabUtility.LoadPrefabContents(path) : NewObject(recipe.prefabName, staging);
            try
            {
                Rebuild(root, recipe, library);
                WorldLayers.Apply(root, worldLayer);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        #region Helpers

        static void Rebuild(GameObject root, VfxRecipe recipe, VfxMaterialLibrary library)
        {
            for (var i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            foreach (var component in root.GetComponents<Component>())
            {
                if (component is Transform or ParticleSystem or ParticleSystemRenderer) continue;
                Object.DestroyImmediate(component);
            }

            root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            var rootSystem = root.GetComponent<ParticleSystem>();
            if (rootSystem == null) rootSystem = root.AddComponent<ParticleSystem>();

            var plane = recipe.NeedsFloorPlane() ? CreateFloorPlane(root.transform, recipe.IsAmbient) : null;
            var latestEnd = 0f;
            for (var i = 0; i < recipe.layers.Count; i++)
            {
                var layer = recipe.layers[i];
                var system = i == 0 ? rootSystem : NewObject(layer.name, root.scene, root.transform).AddComponent<ParticleSystem>();
                var sheet = library.Sheet(layer.sheet);
                VfxParticleFactory.Configure(system, layer, sheet, library.Material(layer.shading, layer.sheet), plane, i == 0, recipe.IsAmbient);
                latestEnd = Mathf.Max(latestEnd, layer.LatestEnd);
            }

            if (!recipe.IsAmbient) VfxParticleFactory.FitRootDuration(rootSystem, latestEnd + RootEndMargin);
        }

        static Transform CreateFloorPlane(Transform root, bool ambient)
        {
            var plane = NewObject("FloorPlane", root.gameObject.scene, root);
            if (ambient) return plane.transform;
            plane.transform.localPosition = new Vector3(0f, -TypicalSpawnHeight, 0f);
            plane.AddComponent<VfxFloorPlane>();
            return plane.transform;
        }

        /// <summary>Creates the object directly in the builder's scene so the Editor's open scene never gets dirty.</summary>
        static GameObject NewObject(string name, Scene scene, Transform? parent = null)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        #endregion
    }
}
