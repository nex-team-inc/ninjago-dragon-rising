#nullable enable

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds OnePlayerDetectionEngine_Hidden.prefab (TDD D6, §17): a prefab variant of the starter
    /// OnePlayerDetectionEngine whose original and smoothed nodes are HiddenPoseNode, so no debug circles render in
    /// the 3D arena. The ReferenceFrame (17.78 × 10, DistancePerInch = 10 / 58) is inherited unchanged. Regenerated
    /// over its path (the GUID stays); the temporary instance lives in a preview scene so the open scene stays clean.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.DetectionPrefabsBuilder.Run();'
    /// </summary>
    public static class DetectionPrefabsBuilder
    {
        public const string HiddenEnginePath = "Assets/Prefabs/BilliardRogue/Detection/OnePlayerDetectionEngine_Hidden.prefab";

        const string StarterEnginePath = "Assets/Prefabs/Detection/DetectionEngine/OnePlayerDetectionEngine.prefab";
        const string HiddenNodePath = "Assets/Prefabs/Detection/DetectionEngine/HiddenPoseNode.prefab";

        [MenuItem("Nex/Billiard Rogue/Detection Prefabs", priority = 40)]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var starter = AssetDatabase.LoadAssetAtPath<GameObject>(StarterEnginePath);
            var hiddenNode = AssetDatabase.LoadAssetAtPath<GameObject>(HiddenNodePath);
            if (starter == null || hiddenNode == null)
            {
                var missing = $"[DetectionPrefabsBuilder] starter prefab missing ({StarterEnginePath} or {HiddenNodePath}); nothing built.";
                Debug.LogWarning(missing);
                return missing;
            }

            BuilderAssets.EnsureFolder("Assets/Prefabs/BilliardRogue/Detection");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(starter, scene);
                instance.name = "OnePlayerDetectionEngine_Hidden";
                var so = new SerializedObject(instance.GetComponent<OnePlayerDetectionEngine>());
                so.FindProperty("originalNodePrefab").objectReferenceValue = hiddenNode;
                so.FindProperty("smoothedNodePrefab").objectReferenceValue = hiddenNode;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(instance, HiddenEnginePath, out var success);
                if (!success) throw new InvalidOperationException($"Saving {HiddenEnginePath} failed");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            AssetDatabase.SaveAssets();
            return Describe();
        }

        static string Describe()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(HiddenEnginePath);
            var so = new SerializedObject(asset.GetComponent<OnePlayerDetectionEngine>());
            var smoothed = so.FindProperty("smoothedNodePrefab").objectReferenceValue;
            var original = so.FindProperty("originalNodePrefab").objectReferenceValue;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(asset);
            return $"[DetectionPrefabsBuilder] {HiddenEnginePath}: {PrefabUtility.GetPrefabAssetType(asset)} of "
                   + $"{(source != null ? AssetDatabase.GetAssetPath(source) : "-")}, original={NameOf(original)}, smoothed={NameOf(smoothed)}";
        }

        static string NameOf(UnityEngine.Object? value)
        {
            return value != null ? value.name : "-";
        }
    }
}
