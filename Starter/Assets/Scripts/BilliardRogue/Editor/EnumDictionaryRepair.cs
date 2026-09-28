#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Makes the singleton prefabs' EnumDictionaries contain every enum key in ascending order (existing values are
    /// kept, new keys get empty values) so the managers keep loading after enum additions (TDD D11).
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.EnumDictionaryRepair.Run();'
    /// </summary>
    public static class EnumDictionaryRepair
    {
        const string SfxPrefab = "Assets/Prefabs/Singletons/SfxManager.prefab";
        const string BgmPrefab = "Assets/Prefabs/Singletons/BgmManager.prefab";
        const string VfxPrefab = "Assets/Prefabs/Singletons/VfxManager.prefab";

        #region Entry Point

        public static string Run()
        {
            var report = new StringBuilder("[EnumDictionaryRepair]");
            report.Append(" sfx +").Append(Repair<SfxManager, SfxManager.SoundEffect>(SfxPrefab, "soundEffectDict", ResetSfx));
            report.Append(", bgm +").Append(Repair<BgmManager, BgmManager.BgmType>(BgmPrefab, "bgmDict", ResetObjectReference));
            report.Append(", stingers +").Append(Repair<BgmManager, BgmManager.StingerType>(BgmPrefab, "stingerDict", ResetObjectReference));
            report.Append(", vfx +").Append(Repair<VfxManager, VfxManager.VisualEffect>(VfxPrefab, "effectSpecs", ResetVfx));
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
            return report.ToString();
        }

        #endregion

        #region Repair

        /// <summary>Inserts missing keys (values reset by resetValue), drops unknown keys, keeps existing values. Returns the number of keys added.</summary>
        public static int EnsureAllKeys(SerializedProperty dictProperty, Type enumType, Action<SerializedProperty> resetValue)
        {
            var keys = new List<int>();
            foreach (var value in Enum.GetValues(enumType))
            {
                keys.Add(Convert.ToInt32(value));
            }

            keys.Sort();
            var pairs = dictProperty.FindPropertyRelative("pairs");
            var pairIndex = 0;
            var added = 0;
            foreach (var key in keys)
            {
                while (pairIndex < pairs.arraySize && KeyAt(pairs, pairIndex) < key)
                {
                    pairs.DeleteArrayElementAtIndex(pairIndex);
                }

                if (pairIndex < pairs.arraySize && KeyAt(pairs, pairIndex) == key)
                {
                    pairIndex++;
                    continue;
                }

                pairs.InsertArrayElementAtIndex(pairIndex);
                var pair = pairs.GetArrayElementAtIndex(pairIndex);
                pair.FindPropertyRelative("key").intValue = key;
                resetValue(pair.FindPropertyRelative("value"));
                pairIndex++;
                added++;
            }

            while (pairs.arraySize > pairIndex)
            {
                pairs.DeleteArrayElementAtIndex(pairs.arraySize - 1);
            }

            return added;
        }

        static int KeyAt(SerializedProperty pairs, int index)
        {
            return pairs.GetArrayElementAtIndex(index).FindPropertyRelative("key").intValue;
        }

        static int Repair<TComponent, TEnum>(string prefabPath, string dictField, Action<SerializedProperty> resetValue)
            where TComponent : Component where TEnum : Enum
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var so = new SerializedObject(root.GetComponent<TComponent>());
                var added = EnsureAllKeys(so.FindProperty(dictField), typeof(TEnum), resetValue);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return added;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        #endregion

        #region Value resets

        static void ResetSfx(SerializedProperty value)
        {
            value.FindPropertyRelative("clips").arraySize = 0;
        }

        static void ResetObjectReference(SerializedProperty value)
        {
            value.objectReferenceValue = null;
        }

        static void ResetVfx(SerializedProperty value)
        {
            value.FindPropertyRelative("prefab").objectReferenceValue = null;
            value.FindPropertyRelative("defaultPoolSize").intValue = 2;
            value.FindPropertyRelative("maxPoolSize").intValue = 8;
        }

        #endregion
    }
}
