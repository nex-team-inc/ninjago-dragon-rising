#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Registers every prefab (bursts and the act ambients) in the VfxManager singleton prefab's EnumDictionary
    /// (TDD D11: every key, ascending, max pool > 0) and points each ActDefinition at its AmbientAct{n} effect.
    /// </summary>
    public static class VfxRegistryWriter
    {
        const string VfxManagerPrefab = "Assets/Prefabs/Singletons/VfxManager.prefab";
        const string ActPathFormat = BuilderAssets.ConfigRoot + "/Acts/Act_{0}.asset";

        /// <summary>Returns the number of registered keys.</summary>
        public static int Register(IReadOnlyList<VfxRecipe> recipes, List<string> warnings)
        {
            var byKey = new Dictionary<int, VfxRecipe>();
            foreach (var recipe in recipes)
            {
                byKey[(int)recipe.effect] = recipe;
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

        /// <summary>Points ActDefinition.ambientEffect of Act_1..3 at AmbientAct1..3; returns how many changed.</summary>
        public static int LinkAmbient(List<string> warnings)
        {
            var linked = 0;
            for (var act = 1; act <= 3; act++)
            {
                var definition = AssetDatabase.LoadAssetAtPath<ActDefinition>(string.Format(ActPathFormat, act));
                if (definition == null)
                {
                    warnings.Add($"Act {act}: ActDefinition missing (ActDefinitionsBuilder); ambient effect not linked.");
                    continue;
                }

                var so = new SerializedObject(definition);
                var property = so.FindProperty("ambientEffect");
                var effect = (int)VfxRecipe.AmbientEffect(act);
                if (property.intValue == effect) continue;
                property.intValue = effect;
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
