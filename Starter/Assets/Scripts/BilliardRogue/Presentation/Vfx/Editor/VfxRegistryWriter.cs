#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Registers the burst prefabs in the VfxManager singleton prefab's EnumDictionary (TDD D11: every key, ascending,
    /// max pool > 0) and links the ambient prefabs into empty ActDefinition slots.
    /// </summary>
    public static class VfxRegistryWriter
    {
        const string VfxManagerPrefab = "Assets/Prefabs/Singletons/VfxManager.prefab";
        const string ActPathFormat = BuilderAssets.ConfigRoot + "/Acts/Act_{0}.asset";

        /// <summary>Returns the number of registered keys.</summary>
        public static int RegisterBursts(IReadOnlyList<VfxRecipe> recipes, List<string> warnings)
        {
            var byKey = new Dictionary<int, VfxRecipe>();
            foreach (var recipe in recipes)
            {
                if (recipe.effect.HasValue) byKey[(int)recipe.effect.Value] = recipe;
            }

            var root = PrefabUtility.LoadPrefabContents(VfxManagerPrefab);
            try
            {
                var so = new SerializedObject(root.GetComponent<VfxManager>());
                var dict = so.FindProperty("effectSpecs");
                EnumDictionaryRepair.EnsureAllKeys(dict, typeof(VfxManager.VisualEffect), ResetSpec);
                var pairs = dict.FindPropertyRelative("pairs");
                var registered = 0;
                for (var i = 0; i < pairs.arraySize; i++)
                {
                    var pair = pairs.GetArrayElementAtIndex(i);
                    var key = pair.FindPropertyRelative("key").intValue;
                    if (!byKey.TryGetValue(key, out var recipe))
                    {
                        warnings.Add($"VisualEffect {(VfxManager.VisualEffect)key} has no recipe; its registry entry is left as is.");
                        continue;
                    }

                    var value = pair.FindPropertyRelative("value");
                    var prefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(VfxPrefabWriter.PathOf(recipe));
                    value.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                    value.FindPropertyRelative("defaultPoolSize").intValue = recipe.defaultPoolSize;
                    value.FindPropertyRelative("maxPoolSize").intValue = Math.Max(1, recipe.maxPoolSize);
                    if (prefab != null) registered++;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, VfxManagerPrefab);
                return registered;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Fills ActDefinition.ambientParticlesPrefab only where it is still empty; returns how many were set.</summary>
        public static int LinkAmbient(IReadOnlyList<VfxRecipe> recipes, List<string> warnings)
        {
            var linked = 0;
            for (var act = 1; act <= 3; act++)
            {
                var definition = AssetDatabase.LoadAssetAtPath<ActDefinition>(string.Format(ActPathFormat, act));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VfxPrefabWriter.PathOf(VfxRecipe.Ambient(act)));
                if (definition == null || prefab == null)
                {
                    warnings.Add($"Act {act}: ActDefinition or Vfx_Ambient_Act{act}.prefab missing; ambient slot not linked.");
                    continue;
                }

                var so = new SerializedObject(definition);
                if (!BuilderAssets.FillIfNull(so, "ambientParticlesPrefab", prefab)) continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
                linked++;
            }

            return linked;
        }

        static void ResetSpec(SerializedProperty value)
        {
            value.FindPropertyRelative("prefab").objectReferenceValue = null;
            value.FindPropertyRelative("defaultPoolSize").intValue = 2;
            value.FindPropertyRelative("maxPoolSize").intValue = 8;
        }
    }
}
