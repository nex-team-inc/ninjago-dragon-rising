#nullable enable

using System.Text;
using Nex.KeyboardNavigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Key = Nex.KeyboardNavigation.KeyboardNavigationController.Key;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds the Flow prefabs (TDD §17): BilliardRogueViewManager (variant of the starter MainViewManager with the
    /// secret code), GameplayPip, CalibrationView, GameplayView and BilliardRogueCoordinator. Prefabs are regenerated
    /// over their existing paths (GUIDs stay); other modules' prefabs are referenced by path and tolerated when missing.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.FlowPrefabsBuilder.Run();'
    /// </summary>
    public static class FlowPrefabsBuilder
    {
        public const string CoordinatorPath = FlowUiFactory.FlowFolder + "/BilliardRogueCoordinator.prefab";
        public const string ViewManagerPath = FlowUiFactory.FlowFolder + "/BilliardRogueViewManager.prefab";
        public const string PipPath = FlowUiFactory.FlowFolder + "/GameplayPip.prefab";
        public const string CalibrationViewPath = FlowUiFactory.ViewsFolder + "/CalibrationView.prefab";
        public const string GameplayViewPath = FlowUiFactory.ViewsFolder + "/GameplayView.prefab";

        const string MainViewManagerPath = "Assets/Prefabs/Coordinators/MainViewManager.prefab";
        const string DetectionManagerPath = "Assets/Prefabs/Detection/Core/DetectionManager.prefab";
        const string StarterEnginePath = "Assets/Prefabs/Detection/DetectionEngine/OnePlayerDetectionEngine.prefab";
        const string HiddenEnginePath = "Assets/Prefabs/BilliardRogue/Detection/OnePlayerDetectionEngine_Hidden.prefab";
        const string IndicatorPrefabPath = "Assets/Prefabs/Detection/Preview/PreviewFramePlayerIndicator.prefab";
        const string PlayerShotInputPath = "Assets/Prefabs/BilliardRogue/Input/PlayerShotInput.prefab";
        const string ConfigPath = BuilderAssets.ConfigRoot + "/BilliardRogueConfig.asset";

        static readonly Key[] secretCode = { Key.Up, Key.Up, Key.Down, Key.Down, Key.Left, Key.Right, Key.Left, Key.Right };

        #region Entry Point

        [MenuItem("Nex/Billiard Rogue/Flow Prefabs", priority = 60)]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var report = new StringBuilder("[FlowPrefabsBuilder]");
            BuilderAssets.EnsureFolder(FlowUiFactory.FlowFolder);
            BuilderAssets.EnsureFolder(FlowUiFactory.ViewsFolder);

            BuildViewManagerVariant();
            report.Append(" view manager variant");
            var pip = FlowUiFactory.SavePrefab(BuildPip(), PipPath).GetComponent<GameplayPip>();
            report.Append(", pip");
            var calibration = FlowViewPrefabsBuilder.BuildCalibrationView(CalibrationViewPath);
            report.Append(", calibration view");
            var gameplay = FlowViewPrefabsBuilder.BuildGameplayView(GameplayViewPath, pip);
            report.Append(", gameplay view");
            BuildCoordinator(calibration, gameplay);
            report.Append(", coordinator");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
            return report.ToString();
        }

        #endregion

        #region View manager variant

        // A prefab instance saved as a new asset becomes a variant; the detector's config[0] is what ViewManager binds
        // to OpenDebugSettings in debug builds (an empty configs array would throw in its Awake).
        static void BuildViewManagerVariant()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(MainViewManagerPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "BilliardRogueViewManager";
            if (!instance.TryGetComponent<SecretCodeSequenceDetector>(out var detector))
            {
                detector = instance.AddComponent<SecretCodeSequenceDetector>();
            }

            var so = new SerializedObject(detector);
            var configs = so.FindProperty("configs");
            configs.arraySize = 1;
            var sequence = configs.GetArrayElementAtIndex(0).FindPropertyRelative("sequence");
            sequence.arraySize = secretCode.Length;
            for (var i = 0; i < secretCode.Length; i++)
            {
                sequence.GetArrayElementAtIndex(i).enumValueIndex = (int)secretCode[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            FlowUiFactory.SavePrefab(instance, ViewManagerPath);
        }

        #endregion

        #region PiP

        // Only the feed lives on this overlay canvas (TDD D5): the camera panel around it (frame, header, P1/P2 chips,
        // placeholder shown until the first frame) is part of GameplayHud, so both builders share the feed rect.
        static GameObject BuildPip()
        {
            var canvas = FlowUiFactory.CreateOverlayCanvasRoot("GameplayPip", 5);
            var root = canvas.gameObject;
            var group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var pip = root.AddComponent<GameplayPip>();

            var previewGo = FlowUiFactory.CreateUIObject("PreviewFrame", root.transform);
            FlowUiFactory.Place(previewGo, new Vector2(0f, 1f), UiHudBuilder.PipFeedScreenPosition, UiHudBuilder.PipFeedSize);
            var previewGroup = previewGo.AddComponent<CanvasGroup>();
            var preview = previewGo.AddComponent<AreaPreviewFrame>();
            var rawImageGo = FlowUiFactory.CreateUIObject("RawImage", previewGo.transform);
            FlowUiFactory.Stretch(rawImageGo);
            var rawImage = rawImageGo.AddComponent<RawImage>();
            rawImage.raycastTarget = false;
            rawImage.color = Color.white;

            var previewSo = new SerializedObject(preview);
            previewSo.FindProperty("rawImage").objectReferenceValue = rawImage;
            previewSo.FindProperty("canvasGroup").objectReferenceValue = previewGroup;
            previewSo.FindProperty("enableSmoothing").boolValue = false;
            previewSo.ApplyModifiedPropertiesWithoutUndo();

            // Indicators are instantiated under the preview frame by the manager (they fade in with the feed).
            var indicators = root.AddComponent<PlayerIndicatorsManager>();
            var indicatorPrefab = FlowUiFactory.LoadPrefabComponent<PreviewFramePlayerIndicator>(IndicatorPrefabPath, "starter");
            var indicatorsSo = new SerializedObject(indicators);
            indicatorsSo.FindProperty("playerIndicatorPrefab").objectReferenceValue = indicatorPrefab;
            indicatorsSo.FindProperty("playerIndicatorSizeRatioToPreviewHeight").floatValue = 0.18f;
            indicatorsSo.ApplyModifiedPropertiesWithoutUndo();

            var pipSo = new SerializedObject(pip);
            pipSo.FindProperty("previewFrame").objectReferenceValue = preview;
            pipSo.FindProperty("indicators").objectReferenceValue = indicators;
            pipSo.FindProperty("canvasGroup").objectReferenceValue = group;
            pipSo.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        #endregion

        #region Coordinator

        static void BuildCoordinator(CalibrationView calibration, GameplayView gameplay)
        {
            var root = new GameObject("BilliardRogueCoordinator");
            var coordinator = root.AddComponent<BilliardRogueCoordinator>();
            var cameraSession = new GameObject("CameraSession").AddComponent<CameraSession>();
            cameraSession.transform.SetParent(root.transform, false);
            var detectionRoot = new GameObject("DetectionRoot").transform;
            detectionRoot.SetParent(root.transform, false);
            // Engine reference frames are 17.8x10 units: keep them clear of the arena even with hidden nodes.
            detectionRoot.localPosition = new Vector3(0f, -1000f, 0f);

            var engine = FlowUiFactory.LoadPrefabComponent<OnePlayerDetectionEngine>(HiddenEnginePath, "Input");
            if (engine == null) engine = AssetDatabase.LoadAssetAtPath<GameObject>(StarterEnginePath).GetComponent<OnePlayerDetectionEngine>();

            var so = new SerializedObject(coordinator);
            so.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BilliardRogueConfig>(ConfigPath);
            so.FindProperty("cameraSession").objectReferenceValue = cameraSession;
            so.FindProperty("calibrationViewPrefab").objectReferenceValue = calibration;
            so.FindProperty("gameplayViewPrefab").objectReferenceValue = gameplay;
            so.FindProperty("detectionManagerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(DetectionManagerPath).GetComponent<DetectionManager>();
            so.FindProperty("detectionEnginePrefab").objectReferenceValue = engine;
            so.FindProperty("detectionRoot").objectReferenceValue = detectionRoot;
            so.FindProperty("titleViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<TitleView>(FlowUiFactory.ViewsFolder + "/TitleView.prefab", "UI-Views");
            so.FindProperty("playerModeViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<PlayerModeView>(FlowUiFactory.ViewsFolder + "/PlayerModeView.prefab", "UI-Views");
            so.FindProperty("summaryViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<SummaryView>(FlowUiFactory.ViewsFolder + "/SummaryView.prefab", "UI-Views");
            so.FindProperty("settingsViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<SettingsView>(FlowUiFactory.ViewsFolder + "/SettingsView.prefab", "UI-Views");
            so.FindProperty("playerShotInputPrefab").objectReferenceValue = FlowUiFactory.LoadPrefab(PlayerShotInputPath, "Input");
            so.ApplyModifiedPropertiesWithoutUndo();

            if (so.FindProperty("config").objectReferenceValue == null)
            {
                Debug.LogWarning($"[FlowPrefabs] {ConfigPath} missing: run ConfigAssetsBuilder, then FlowPrefabsBuilder again.");
            }

            FlowUiFactory.SavePrefab(root, CoordinatorPath);
        }

        #endregion
    }
}
