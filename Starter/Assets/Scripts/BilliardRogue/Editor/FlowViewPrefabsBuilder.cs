#nullable enable

using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// The two Flow views: CalibrationView (nested starter PreviewsManager + pose tutorial panel) and GameplayView
    /// (label layer, nested GameplayHud, session host, input root, overlay prefab slots). Both are regenerated over
    /// their existing paths by FlowPrefabsBuilder.
    /// </summary>
    public static class FlowViewPrefabsBuilder
    {
        const string PreviewsManagerPath = "Assets/Prefabs/Detection/Preview/PreviewsManager.prefab";
        const string GameplayHudPath = "Assets/Prefabs/BilliardRogue/UI/GameplayHud.prefab";

        const int HeaderFontSize = 48;
        const int BodyFontSize = 32;
        static readonly Color ballColor = new(1f, 0.93f, 0.55f);
        static readonly Color cueColor = new(0.95f, 0.6f, 0.35f);
        static readonly Color hintColor = new(0.85f, 0.85f, 0.95f);

        #region Calibration view

        public static CalibrationView BuildCalibrationView(string path)
        {
            var root = FlowUiFactory.CreateViewRoot<CalibrationView>("CalibrationView", out var view);
            var rootGroup = root.GetComponent<CanvasGroup>();
            var animators = FlowUiFactory.CreateUIObject("Animators", root.transform);
            var entry = FlowUiFactory.CreateFadeAnimator("EntryAnimator", animators.transform, rootGroup, 0f, 1f);
            var graph = FlowUiFactory.CreateGraphWithBackProxy(root.transform, null, TopLevelControlPanel.ControlConfig.Back);

            var ui = FlowUiFactory.CreateUIObject("UI", root.transform);
            FlowUiFactory.Stretch(ui);
            var header = FlowUiFactory.CreateLabel("Header", ui.transform, HeaderFontSize, TextAlignmentOptions.Center, LocKeys.Calibration.Header, "Camera setup");
            FlowUiFactory.Place(header.gameObject, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(1600f, 80f));
            var prompt = FlowUiFactory.CreateLabel("Prompt", ui.transform, BodyFontSize, TextAlignmentOptions.Center, null, "Starting the camera…");
            FlowUiFactory.Place(prompt.gameObject, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(1600f, 96f));

            var tutorial = BuildTutorialPanel(ui.transform, out var illustration, out var hint, out var statusLabels);
            var inputRoot = FlowUiFactory.CreateUIObject("InputRoot", root.transform);
            FlowUiFactory.InstantiateNested(PreviewsManagerPath, root.transform, "starter");

            var so = new SerializedObject(view);
            so.FindProperty("entryAnimator").objectReferenceValue = entry;
            so.FindProperty("keyResponder").objectReferenceValue = graph;
            so.FindProperty("previewsManager").objectReferenceValue = root.GetComponentInChildren<PreviewsManager>(true);
            so.FindProperty("tutorialGroup").objectReferenceValue = tutorial;
            so.FindProperty("illustration").objectReferenceValue = illustration;
            so.FindProperty("promptLabel").objectReferenceValue = prompt;
            so.FindProperty("hintLabel").objectReferenceValue = hint;
            var labels = so.FindProperty("playerStatusLabels");
            labels.arraySize = statusLabels.Length;
            for (var i = 0; i < statusLabels.Length; i++)
            {
                labels.GetArrayElementAtIndex(i).objectReferenceValue = statusLabels[i];
            }

            so.FindProperty("inputRoot").objectReferenceValue = inputRoot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return FlowUiFactory.SavePrefab(root, path).GetComponent<CalibrationView>();
        }

        static CanvasGroup BuildTutorialPanel(Transform parent, out CalibrationTutorialIllustration illustration,
            out TextMeshProUGUI hint, out TextMeshProUGUI[] statusLabels)
        {
            var panelGo = FlowUiFactory.CreateUIObject("Tutorial", parent);
            FlowUiFactory.Place(panelGo, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(960f, 560f));
            var group = panelGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var frame = FlowUiFactory.CreateImage("Panel", panelGo.transform, "Frame_Panel", Color.white);
            FlowUiFactory.Stretch(frame.gameObject);

            var illustrationGo = FlowUiFactory.CreateUIObject("Illustration", panelGo.transform);
            FlowUiFactory.Place(illustrationGo, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(640f, 300f));
            illustration = illustrationGo.AddComponent<CalibrationTutorialIllustration>();
            var leftPaw = CreatePaw("LeftPaw", illustrationGo.transform, "Icon_Ball", ballColor, new Vector2(-110f, 30f));
            var rightPaw = CreatePaw("RightPaw", illustrationGo.transform, "Cursor", cueColor, new Vector2(120f, -100f));
            var leftTag = FlowUiFactory.CreateLabel("LeftTag", leftPaw.transform, 16, TextAlignmentOptions.Center, null, "L");
            FlowUiFactory.Place(leftTag.gameObject, new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(120f, 32f));
            var rightTag = FlowUiFactory.CreateLabel("RightTag", rightPaw.transform, 16, TextAlignmentOptions.Center, null, "R");
            FlowUiFactory.Place(rightTag.gameObject, new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(120f, 32f));
            FlowUiFactory.SetReference(illustration, "leftPaw", leftPaw.transform);
            FlowUiFactory.SetReference(illustration, "rightPaw", rightPaw.transform);

            hint = FlowUiFactory.CreateLabel("Hint", panelGo.transform, BodyFontSize, TextAlignmentOptions.Center, null, "Left paw = ball · Right paw = cue");
            hint.color = hintColor;
            FlowUiFactory.Place(hint.gameObject, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(880f, 48f));

            var statusRow = FlowUiFactory.CreateUIObject("PlayerStatus", panelGo.transform);
            FlowUiFactory.Place(statusRow, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(880f, 48f));
            var layout = statusRow.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 48f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            statusLabels = new TextMeshProUGUI[2];
            for (var i = 0; i < statusLabels.Length; i++)
            {
                statusLabels[i] = FlowUiFactory.CreateLabel($"Player{i + 1}Status", statusRow.transform, BodyFontSize, TextAlignmentOptions.Center, null, $"Waiting for player {i + 1}…");
                statusLabels[i].gameObject.SetActive(false);
            }

            return group;
        }

        static Image CreatePaw(string name, Transform parent, string spriteName, Color color, Vector2 position)
        {
            var image = FlowUiFactory.CreateImage(name, parent, spriteName, color);
            image.preserveAspect = true;
            FlowUiFactory.Place(image.gameObject, new Vector2(0.5f, 0.5f), position, new Vector2(96f, 96f));
            return image;
        }

        #endregion

        #region Gameplay view

        public static GameplayView BuildGameplayView(string path, GameplayPip pipPrefab)
        {
            var root = FlowUiFactory.CreateViewRoot<GameplayView>("GameplayView", out var view);
            var rootGroup = root.GetComponent<CanvasGroup>();
            var animators = FlowUiFactory.CreateUIObject("Animators", root.transform);
            var entry = FlowUiFactory.CreateFadeAnimator("EntryAnimator", animators.transform, rootGroup, 0f, 1f);
            // Escape → top-level Back → GameplayView.OnBackButton → pause.
            var graph = FlowUiFactory.CreateGraphWithBackProxy(root.transform, null, TopLevelControlPanel.ControlConfig.Back);

            // First child so labels draw behind the HUD.
            var labelLayer = FlowUiFactory.CreateUIObject("WorldLabelLayer", root.transform);
            FlowUiFactory.Stretch(labelLayer);

            var hud = FlowUiFactory.InstantiateNested(GameplayHudPath, root.transform, "UI-Views");
            if (hud != null) hud.name = "Hud";

            var sessionGo = FlowUiFactory.CreateUIObject("Session", root.transform);
            var session = sessionGo.AddComponent<GameSession>();
            var timeScale = sessionGo.AddComponent<TimeScaleController>();
            var inputRoot = FlowUiFactory.CreateUIObject("InputRoot", root.transform);

            var so = new SerializedObject(view);
            so.FindProperty("entryAnimator").objectReferenceValue = entry;
            so.FindProperty("keyResponder").objectReferenceValue = graph;
            so.FindProperty("worldLabelLayer").objectReferenceValue = labelLayer.transform;
            so.FindProperty("session").objectReferenceValue = session;
            so.FindProperty("timeScale").objectReferenceValue = timeScale;
            so.FindProperty("inputRoot").objectReferenceValue = inputRoot.transform;
            so.FindProperty("pipPrefab").objectReferenceValue = pipPrefab;
            so.FindProperty("stageIntroViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<StageIntroView>(FlowUiFactory.ViewsFolder + "/StageIntroView.prefab", "UI-Views");
            so.FindProperty("rewardViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<RewardView>(FlowUiFactory.ViewsFolder + "/RewardView.prefab", "UI-Views");
            so.FindProperty("trackingLostViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<TrackingLostView>(FlowUiFactory.ViewsFolder + "/TrackingLostView.prefab", "UI-Views");
            so.FindProperty("pauseViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<PauseView>(FlowUiFactory.ViewsFolder + "/PauseView.prefab", "UI-Views");
            so.ApplyModifiedPropertiesWithoutUndo();

            return FlowUiFactory.SavePrefab(root, path).GetComponent<GameplayView>();
        }

        #endregion
    }
}
