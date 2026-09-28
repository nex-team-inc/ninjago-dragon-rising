#nullable enable

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>Shared asset helpers for the Billiard Rogue builders (idempotent folder/asset creation).</summary>
    public static class BuilderAssets
    {
        public const string ConfigRoot = "Assets/Configs/BilliardRogue";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Loads the asset at path or creates it; created is true only for a fresh asset (never overwrite designer values).</summary>
        public static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                created = false;
                return existing;
            }

            EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            created = true;
            return asset;
        }

        /// <summary>Sets an object reference only when the slot is still empty.</summary>
        public static bool FillIfNull(SerializedObject so, string propertyPath, Object value)
        {
            var property = so.FindProperty(propertyPath);
            if (property.objectReferenceValue != null) return false;
            property.objectReferenceValue = value;
            return true;
        }
    }
}
