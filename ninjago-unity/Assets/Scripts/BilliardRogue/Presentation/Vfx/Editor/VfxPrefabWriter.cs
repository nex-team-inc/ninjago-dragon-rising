#nullable enable

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Writes one VfxRecipe to Assets/Prefabs/BilliardRogue/Vfx/&lt;name&gt;.prefab (TDD §17). An existing prefab is edited in
    /// place: the root and every child whose name still matches a layer keep their file IDs, so references to them
    /// (VfxManager registry, ActDefinition ambient slots, gallery overrides) survive a rebuild and an unchanged recipe
    /// rewrites nothing. Children the recipe no longer has are deleted, missing ones created.
    /// </summary>
    public static class VfxPrefabWriter
    {
        public const string PrefabRoot = "Assets/Prefabs/BilliardRogue/Vfx";
        const string FloorPlaneName = "FloorPlane";

        // BoardEventPlayer spawns bursts at the enemy centre height; the plane starts there and VfxFloorPlane pins it.
        const float TypicalSpawnHeight = 0.45f;
        const float RootEndMargin = 0.05f;

        public static string PathOf(VfxRecipe recipe) => $"{PrefabRoot}/{recipe.prefabName}.prefab";

        /// <summary>Returns true when the prefab was created or its object structure changed (children added or removed).</summary>
        public static bool Write(VfxRecipe recipe, VfxMaterialLibrary library, Scene staging, int worldLayer)
        {
            var path = PathOf(recipe);
            var existing = File.Exists(path);
            BuilderAssets.EnsureFolder(PrefabRoot);
            var root = existing ? PrefabUtility.LoadPrefabContents(path) : NewObject(recipe.prefabName, staging);
            try
            {
                var structureChanged = Rebuild(root, recipe, library) || !existing;
                WorldLayers.Apply(root, worldLayer);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return structureChanged;
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        #region Rebuild

        static bool Rebuild(GameObject root, VfxRecipe recipe, VfxMaterialLibrary library)
        {
            var rootTransform = root.transform;
            var changed = RemoveStaleChildren(rootTransform, recipe);
            StripExtraComponents(root, false);
            rootTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            rootTransform.localScale = Vector3.one;
            var rootSystem = root.GetComponent<ParticleSystem>();
            if (rootSystem == null) rootSystem = root.AddComponent<ParticleSystem>();

            var sibling = 0;
            Transform? plane = null;
            if (recipe.NeedsFloorPlane())
            {
                plane = EnsureChild(rootTransform, FloorPlaneName, sibling++, ref changed).transform;
                ConfigureFloorPlane(plane, recipe.IsAmbient);
            }

            var latestEnd = 0f;
            for (var i = 0; i < recipe.layers.Count; i++)
            {
                var layer = recipe.layers[i];
                ParticleSystem system;
                if (i == 0)
                {
                    system = rootSystem;
                }
                else
                {
                    var child = EnsureChild(rootTransform, layer.name, sibling++, ref changed);
                    system = child.GetComponent<ParticleSystem>();
                    if (system == null) system = child.AddComponent<ParticleSystem>();
                }

                VfxParticleFactory.Configure(system, layer, library.Sheet(layer.sheet), library.Material(layer.shading, layer.sheet), plane, i == 0, recipe.IsAmbient);
                latestEnd = Mathf.Max(latestEnd, layer.LatestEnd);
            }

            if (!recipe.IsAmbient) VfxParticleFactory.FitRootDuration(rootSystem, latestEnd + RootEndMargin);
            return changed;
        }

        /// <summary>Deletes children that no layer (or the floor plane) claims, including duplicates of a claimed name.</summary>
        static bool RemoveStaleChildren(Transform root, VfxRecipe recipe)
        {
            var wanted = new HashSet<string>();
            if (recipe.NeedsFloorPlane()) wanted.Add(FloorPlaneName);
            for (var i = 1; i < recipe.layers.Count; i++)
            {
                wanted.Add(recipe.layers[i].name);
            }

            var removed = false;
            var seen = new HashSet<string>();
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (wanted.Contains(child.name) && seen.Add(child.name)) continue;
                Object.DestroyImmediate(child.gameObject);
                removed = true;
                i--;
            }

            return removed;
        }

        static GameObject EnsureChild(Transform root, string name, int siblingIndex, ref bool changed)
        {
            var child = root.Find(name);
            if (child == null)
            {
                child = NewObject(name, root.gameObject.scene, root).transform;
                changed = true;
            }

            child.SetSiblingIndex(siblingIndex);
            child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            child.localScale = Vector3.one;
            for (var i = child.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(child.GetChild(i).gameObject);
                changed = true;
            }

            StripExtraComponents(child.gameObject, name == FloorPlaneName);
            return child.gameObject;
        }

        /// <summary>Ambient: a static plane at the root (the ambient never moves). Burst: pinned to the floor at runtime.</summary>
        static void ConfigureFloorPlane(Transform plane, bool ambient)
        {
            var pin = plane.GetComponent<VfxFloorPlane>();
            if (ambient)
            {
                if (pin != null) Object.DestroyImmediate(pin);
                return;
            }

            // Burst: starts at the floor below the typical spawn height; the runtime helper re-pins it after every move.
            plane.localPosition = new Vector3(0f, -TypicalSpawnHeight, 0f);
            if (pin == null) plane.gameObject.AddComponent<VfxFloorPlane>();
        }

        /// <summary>Keeps only Transform, ParticleSystem and its renderer (no audio, lights or scripts; TDD D13), plus the pin on the plane.</summary>
        static void StripExtraComponents(GameObject target, bool isFloorPlane)
        {
            foreach (var component in target.GetComponents<Component>())
            {
                if (component is Transform) continue;
                if (isFloorPlane ? component is VfxFloorPlane : component is ParticleSystem or ParticleSystemRenderer) continue;
                Object.DestroyImmediate(component);
            }
        }

        #endregion

        #region Helpers

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
