#nullable enable

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Creates or refreshes one prefab variant per generated FBX, with every renderer using the shared
    /// palette material. Re-runnable: existing variants keep their GUID and any components added later.
    /// </summary>
    public static class BilliardRogueModelPrefabBuilder
    {
        const string ModelsRoot = "Assets/Models/BilliardRogue";
        const string PrefabsRoot = "Assets/Prefabs/BilliardRogue/Models";
        const string PaletteMaterialPath = "Assets/Materials/BilliardRogue/M_Palette.mat";

        [MenuItem("Nex/Billiard Rogue/Build Model Prefabs")]
        public static void BuildAll()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);
            if (material == null) throw new FileNotFoundException("Create the palette material first", PaletteMaterialPath);
            Directory.CreateDirectory(PrefabsRoot);
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot }))
            {
                var modelPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefabPath = $"{PrefabsRoot}/{Path.GetFileNameWithoutExtension(modelPath)}.prefab";
                Build(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), prefabPath, material);
            }
            AssetDatabase.SaveAssets();
        }

        static void Build(GameObject model, string prefabPath, Material material)
        {
            var exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(prefabPath) : (GameObject)PrefabUtility.InstantiatePrefab(model);
            foreach (var meshRenderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.On;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
            Debug.Log($"[PrefabBuilder] {prefabPath} <- {AssetDatabase.GetAssetPath(model)}");
        }
    }
}
