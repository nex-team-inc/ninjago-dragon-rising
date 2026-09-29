#nullable enable

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/BilliardRogue/Main.unity (TDD §17): SingletonSpawner as in GameUIExample, the inactive
    /// initializer, the view manager variant (its RootCamera is the only screen camera), the coordinator, the world
    /// prefab instances and the EventSystem, then puts the scene first in Build Settings. Idempotent: an existing
    /// scene is rebuilt in place (its GUID survives); missing prefabs of other modules are skipped with a warning.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.MainSceneBuilder.Run();'
    /// </summary>
    public static class MainSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/BilliardRogue/Main.unity";

        const string StartUpSingletonsPath = "Assets/Prefabs/Singletons/StartUpSingletons.prefab";
        const string CommonSingletonsPath = "Assets/Prefabs/Singletons/CommonSingletons.prefab";
        const string SplashAddressSuffix = "NexSplashScreen.prefab";
        const string WorldCameraRigPath = "Assets/Prefabs/BilliardRogue/World/WorldCameraRig.prefab";
        const string ArenaPath = "Assets/Prefabs/BilliardRogue/World/Arena.prefab";
        const string BoardPresenterPath = "Assets/Prefabs/BilliardRogue/World/BoardPresenter.prefab";
        const string EnvironmentPathFormat = "Assets/Prefabs/BilliardRogue/Environment/Env_Act{0}.prefab";

        #region Entry Point

        [MenuItem("Nex/Billiard Rogue/Main Scene", priority = 61)]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var report = new StringBuilder("[MainSceneBuilder]");
            BuilderAssets.EnsureFolder("Assets/Scenes/BilliardRogue");

            var scene = OpenOrCreateScene(out var wasOpen);
            var previousActive = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                ClearScene(scene);
                Populate(scene, report);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (previousActive.IsValid() && previousActive != scene) SceneManager.SetActiveScene(previousActive);
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }

            RegisterInBuildSettings();
            report.Append(", build index 0");
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
            return report.ToString();
        }

        #endregion

        #region Scene lifecycle

        // Additive so the user's open scene is never discarded; a scene that is already open is rebuilt in place.
        static Scene OpenOrCreateScene(out bool wasOpen)
        {
            var open = SceneManager.GetSceneByPath(ScenePath);
            if (open.IsValid() && open.isLoaded)
            {
                wasOpen = true;
                return open;
            }

            wasOpen = false;
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        static void ClearScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }
        }

        static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene> { new(ScenePath, true) };
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath) scenes.Add(existing);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        #endregion

        #region Population

        static void Populate(Scene scene, StringBuilder report)
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.3f, 0.38f);

            var initializer = new GameObject("BilliardRogueInitializer").AddComponent<BilliardRogueInitializer>();
            initializer.gameObject.SetActive(false);
            BuildSingletonSpawner(initializer.gameObject);

            var viewManagerInstance = InstantiatePrefab(FlowPrefabsBuilder.ViewManagerPath, "Flow", null);
            var viewManager = viewManagerInstance != null ? viewManagerInstance.GetComponent<ViewManager>() : null;
            var rootCamera = viewManagerInstance != null ? viewManagerInstance.GetComponentInChildren<Camera>(true) : null;
            BuildAudioListener();
            if (rootCamera != null) ClearBaseCameras(rootCamera);

            var world = new GameObject("World");
            InstantiatePrefab(WorldCameraRigPath, "Rendering", world.transform);
            InstantiatePrefab(ArenaPath, "Presentation-World", world.transform);
            var environment = new GameObject("Environment").transform;
            environment.SetParent(world.transform, false);
            for (var act = 1; act <= 3; act++)
            {
                var env = InstantiatePrefab(string.Format(EnvironmentPathFormat, act), "Presentation-World", environment);
                if (env != null) env.SetActive(false);
            }

            InstantiatePrefab(EnvironmentBuilder.WorldLightingPath, "Presentation-World", world.transform);
            EnvironmentBuilder.WireScene(world);
            var board = InstantiatePrefab(BoardPresenterPath, "Presentation-Core", world.transform);
            if (board != null) WireBoardPresenter(board.GetComponent<BoardPresenter>(), world);

            var coordinatorInstance = InstantiatePrefab(FlowPrefabsBuilder.CoordinatorPath, "Flow", null);
            if (coordinatorInstance != null)
            {
                var coordinator = coordinatorInstance.GetComponent<BilliardRogueCoordinator>();
                WireCoordinator(coordinator, viewManager, rootCamera, world);
                FlowUiFactory.SetReference(initializer, "coordinator", coordinator);
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            report.Append($" roots {scene.rootCount}");
        }

        // Same configs as GameUIExample: StartUp + Common singleton prefabs, then the initializer is activated.
        static void BuildSingletonSpawner(GameObject initializer)
        {
            var spawner = new GameObject("SingletonSpawner").AddComponent<SingletonSpawner>();
            var so = new SerializedObject(spawner);
            var configs = so.FindProperty("configs");
            configs.arraySize = 2;
            SetSingletonConfig(configs.GetArrayElementAtIndex(0), SingletonSpawner.SingletonType.StartUp, StartUpSingletonsPath);
            SetSingletonConfig(configs.GetArrayElementAtIndex(1), SingletonSpawner.SingletonType.Common, CommonSingletonsPath);
            var activate = so.FindProperty("activatePostSpawn");
            activate.arraySize = 1;
            activate.GetArrayElementAtIndex(0).objectReferenceValue = initializer;
            so.FindProperty("enablePostSpawn").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            var initializerSo = new SerializedObject(initializer.GetComponent<BilliardRogueInitializer>());
            initializerSo.FindProperty("nexSplashScreenReference.m_AssetGUID").stringValue = FindSplashGuid();
            initializerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSingletonConfig(SerializedProperty element, SingletonSpawner.SingletonType type, string prefabPath)
        {
            element.FindPropertyRelative("type").enumValueIndex = (int)type;
            element.FindPropertyRelative("prefab.m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(prefabPath);
        }

        static string FindSplashGuid()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                foreach (var group in settings.groups)
                {
                    if (group == null) continue;
                    foreach (var entry in group.entries)
                    {
                        if (entry.address.EndsWith(SplashAddressSuffix)) return entry.guid;
                    }
                }
            }

            Debug.LogWarning("[MainSceneBuilder] Nex splash screen Addressable entry not found; the splash is skipped.");
            return "";
        }

        // No Main Camera: the view manager's RootCamera is the only screen camera (a Base camera straight to the
        // backbuffer, FlowPrefabsBuilder) and the world renders through WorldCamera into the low-res RT (TDD D2). A second
        // Base camera would put the UI back into a 1080p camera stack (intermediate texture + final blit, final-perf.md
        // pass 3). The scene keeps one AudioListener.
        static void BuildAudioListener()
        {
            new GameObject("AudioListener").AddComponent<AudioListener>();
        }

        // The background-blur chain (starter CameraChainItem) retargets its base cameras; the RootCamera is its own base now
        // and no view asks for the blur (TDD D3).
        static void ClearBaseCameras(Camera rootCamera)
        {
            var chain = rootCamera.GetComponent<CameraChainItem>();
            var so = new SerializedObject(chain);
            so.FindProperty("baseCameras").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireCoordinator(BilliardRogueCoordinator coordinator, ViewManager? viewManager, Camera? rootCamera, GameObject world)
        {
            var so = new SerializedObject(coordinator);
            so.FindProperty("viewManager").objectReferenceValue = viewManager;
            so.FindProperty("rootCamera").objectReferenceValue = rootCamera;
            so.FindProperty("boardPresenter").objectReferenceValue = world.GetComponentInChildren<BoardPresenter>(true);
            so.FindProperty("arenaLayout").objectReferenceValue = world.GetComponentInChildren<ArenaLayout>(true);
            so.FindProperty("worldCameraRig").objectReferenceValue = world.GetComponentInChildren<WorldCameraRig>(true);
            so.FindProperty("actEnvironment").objectReferenceValue = world.GetComponentInChildren<ActEnvironmentController>(true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The danger-row glow lives on the Arena instance; the presenter drives it from the enemy rows.
        static void WireBoardPresenter(BoardPresenter presenter, GameObject world)
        {
            var so = new SerializedObject(presenter);
            so.FindProperty("arenaView").objectReferenceValue = world.GetComponentInChildren<ArenaView>(true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject? InstantiatePrefab(string path, string owner, Transform? parent)
        {
            var prefab = FlowUiFactory.LoadPrefab(path, owner);
            if (prefab == null) return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null) instance.transform.SetParent(parent, false);
            return instance;
        }

        #endregion
    }
}
