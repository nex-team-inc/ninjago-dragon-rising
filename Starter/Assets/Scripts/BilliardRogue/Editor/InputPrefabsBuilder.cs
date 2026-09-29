#nullable enable

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds PlayerShotInput.prefab (TDD §17): ShotInputRouter (first, so GetComponent&lt;IShotInput&gt;() returns
    /// it) + PawShotInput + DebugShotInput + AutoAimBot + MotionEnergyMeter + PawPointer (GDD v2 §3-§4) on one root,
    /// with the router's slots wired. Every
    /// component starts disabled and enables itself in Initialize, so nothing updates before its dependencies are
    /// injected. One instance per player at runtime (the coordinator calls ShotInputRouter.Initialize). Regenerated
    /// over its path (the GUID stays) inside a preview scene.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.InputPrefabsBuilder.Run();'
    /// </summary>
    public static class InputPrefabsBuilder
    {
        public const string PlayerShotInputPath = "Assets/Prefabs/BilliardRogue/Input/PlayerShotInput.prefab";

        [MenuItem("Nex/Billiard Rogue/Input Prefabs", priority = 41)]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            BuilderAssets.EnsureFolder("Assets/Prefabs/BilliardRogue/Input");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("PlayerShotInput");
                SceneManager.MoveGameObjectToScene(root, scene);
                var router = root.AddComponent<ShotInputRouter>();
                var paw = root.AddComponent<PawShotInput>();
                var debug = root.AddComponent<DebugShotInput>();
                var bot = root.AddComponent<AutoAimBot>();
                var motion = root.AddComponent<MotionEnergyMeter>();
                var pointer = root.AddComponent<PawPointer>();
                router.enabled = false;
                paw.enabled = false;
                debug.enabled = false;
                bot.enabled = false;
                motion.enabled = false;
                pointer.enabled = false;

                var so = new SerializedObject(router);
                so.FindProperty("pawInput").objectReferenceValue = paw;
                so.FindProperty("debugInput").objectReferenceValue = debug;
                so.FindProperty("botInput").objectReferenceValue = bot;
                so.FindProperty("motionMeter").objectReferenceValue = motion;
                so.FindProperty("pawPointer").objectReferenceValue = pointer;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerShotInputPath, out var success);
                if (!success) throw new InvalidOperationException($"Saving {PlayerShotInputPath} failed");
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
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerShotInputPath);
            var router = asset.GetComponent<ShotInputRouter>();
            var so = new SerializedObject(router);
            var wired = so.FindProperty("pawInput").objectReferenceValue != null
                        && so.FindProperty("debugInput").objectReferenceValue != null
                        && so.FindProperty("botInput").objectReferenceValue != null
                        && so.FindProperty("motionMeter").objectReferenceValue != null
                        && so.FindProperty("pawPointer").objectReferenceValue != null;
            var first = asset.GetComponent<IShotInput>();
            return $"[InputPrefabsBuilder] {PlayerShotInputPath}: sources wired={wired}, GetComponent<IShotInput>()={first.GetType().Name}";
        }
    }
}
