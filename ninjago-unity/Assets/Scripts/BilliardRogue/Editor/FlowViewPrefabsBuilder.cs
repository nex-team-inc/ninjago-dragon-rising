#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Fill = Nex.BilliardRogue.Editor.UiPrefabKit.Fill;
using Kit = Nex.BilliardRogue.Editor.UiPrefabKit;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// The two Flow views, drawn with the UI-Views kit (UiTheme sprites, drop-shadow labels bound to LocKeys):
    /// CalibrationView (controls card, camera panel hosting the nested starter PreviewsManager and the player cards,
    /// step pips, prompt, curtain) and GameplayView (label layer, nested GameplayHud, session host, input root, overlay
    /// prefab slots, curtain). Both are regenerated over their existing paths by FlowPrefabsBuilder.
    /// Layout in 1920x1080 units.
    /// </summary>
    public static class FlowViewPrefabsBuilder
    {
        const string PreviewsManagerPath = "Assets/Prefabs/Detection/Preview/PreviewsManager.prefab";
        const string GameplayHudPath = "Assets/Prefabs/BilliardRogue/UI/GameplayHud.prefab";

        const float MainTop = 208f;
        const float MainHeight = 672f;
        const float SideMargin = 64f;
        const float CardWidth = 640f;
        static readonly Vector2 cameraPanelSize = new(1088f, MainHeight);
        /// <summary>Setup previews area inside the camera panel (the tip line sits under it).</summary>
        const float PanelInset = 32f;
        static readonly Vector2 previewsSize = new(1024f, 528f);

        #region Calibration view

        public static CalibrationView BuildCalibrationView(string path)
        {
            var kit = new Kit(LoadTheme());
            var theme = kit.Theme;
            var root = FlowUiFactory.CreateViewRoot<CalibrationView>("CalibrationView", out var view);
            var rootGroup = root.GetComponent<CanvasGroup>();
            var animators = FlowUiFactory.CreateUIObject("Animators", root.transform);
            var entry = FlowUiFactory.CreateFadeAnimator("EntryAnimator", animators.transform, rootGroup, 0f, 1f);
            var graph = FlowUiFactory.CreateGraphWithBackProxy(root.transform, null, TopLevelControlPanel.ControlConfig.Back);

            var ui = kit.Ui("UI", root.transform);
            Kit.Stretch(ui);
            var u = ui.transform;
            kit.StretchImage(u, "Vignette", theme.Vignette, 0f, Fill.Simple, theme.VignetteColor).preserveAspect = false;
            UiMenuViewsBuilder.Banner(kit, u, "Header", LocKeys.Calibration.Header, new Vector2(0f, -32f), 896f);
            var pips = BuildStepPips(kit, u);
            var illustration = BuildControlsCard(kit, u);
            var camera = kit.Image(u, "CameraPanel", theme.Panel, Kit.TopRight, new Vector2(-SideMargin, -MainTop), cameraPanelSize, Fill.Tiled).transform;
            var placeholder = BuildPlaceholder(kit, camera, out var setupTip);
            var players = BuildPlayerCards(kit, camera, out var cards);
            // Navy strip behind the prompt: the cat and the arena show through the bottom of the screen.
            kit.Image(u, "PromptBacking", theme.Chip, Kit.Bottom, new Vector2(0f, 88f), new Vector2(1280f, 80f), Fill.Sliced, new Color(1f, 1f, 1f, 0.92f));
            var prompt = kit.Label(u, "Prompt", LocKeys.Calibration.Starting, 48, theme.TextPrimary, Kit.Bottom, new Vector2(0f, 96f), new Vector2(1728f, 64f));
            var hint = kit.Label(u, "Hint", LocKeys.Calibration.TutorialHint, 32, theme.Accent, Kit.Bottom, new Vector2(0f, 40f), new Vector2(1728f, 48f));

            var inputRoot = FlowUiFactory.CreateUIObject("InputRoot", root.transform);
            var previews = FlowUiFactory.InstantiateNested(PreviewsManagerPath, root.transform, "starter");
            if (previews != null) FitPreviews(previews);
            var curtain = Curtain(kit, root.transform);

            var so = new SerializedObject(view);
            so.FindProperty("entryAnimator").objectReferenceValue = entry;
            so.FindProperty("keyResponder").objectReferenceValue = graph;
            so.FindProperty("previewsManager").objectReferenceValue = root.GetComponentInChildren<PreviewsManager>(true);
            so.FindProperty("illustration").objectReferenceValue = illustration;
            so.FindProperty("hintLabel").objectReferenceValue = hint;
            so.FindProperty("placeholderGroup").objectReferenceValue = placeholder;
            so.FindProperty("setupTipGroup").objectReferenceValue = setupTip;
            so.FindProperty("playersGroup").objectReferenceValue = players;
            var cardsProperty = so.FindProperty("playerCards");
            cardsProperty.arraySize = cards.Count;
            for (var i = 0; i < cards.Count; i++)
            {
                cardsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }

            so.FindProperty("stepPips").objectReferenceValue = pips;
            so.FindProperty("promptLabel").objectReferenceValue = prompt;
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("curtain").objectReferenceValue = curtain;
            so.FindProperty("inputRoot").objectReferenceValue = inputRoot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var warning in kit.Warnings) Debug.LogWarning("[FlowPrefabs] " + warning);
            return FlowUiFactory.SavePrefab(root, path).GetComponent<CalibrationView>();
        }

        /// <summary>1 move in · 2 raise hand · 3 pose · 4 test strike, joined by short links, under the header ribbon.</summary>
        static CalibrationStepPips BuildStepPips(Kit kit, Transform parent)
        {
            var theme = kit.Theme;
            const int count = 4;
            const float pip = 64f;
            const float link = 48f;
            var width = count * pip + (count - 1) * link;
            var row = kit.Ui("Steps", parent);
            Kit.Place(row, Kit.Top, new Vector2(0f, -140f), new Vector2(width, 56f));
            var pipImages = new List<Object>();
            var numbers = new List<Object>();
            var links = new List<Object>();
            for (var i = 0; i < count; i++)
            {
                var x = -width * 0.5f + pip * 0.5f + i * (pip + link);
                if (i > 0)
                {
                    var bar = kit.Image(row.transform, $"Link{i}", null, Kit.Center, new Vector2(x - (pip + link) * 0.5f, 0f), new Vector2(link - 8f, 6f),
                        color: theme.Disabled);
                    bar.preserveAspect = false;
                    links.Add(bar);
                }

                var image = kit.Image(row.transform, $"Pip{i + 1}", theme.Chip, Kit.Center, new Vector2(x, 0f), new Vector2(pip, 56f), Fill.Sliced);
                pipImages.Add(image);
                numbers.Add(kit.Label(image.transform, "Number", null, 48, theme.Disabled, Kit.Center, new Vector2(0f, 3f), new Vector2(pip, 56f),
                    numbersPreview: (i + 1).ToString()));
            }

            var widget = row.AddComponent<CalibrationStepPips>();
            UiFields.Set(widget, "theme", theme);
            UiFields.SetArray(widget, "pips", pipImages);
            UiFields.SetArray(widget, "numbers", numbers);
            UiFields.SetArray(widget, "links", links);
            return widget;
        }

        /// <summary>Parchment card: title, the animated paws on a navy stage (clipped), and the three control rows.</summary>
        static CalibrationTutorialIllustration BuildControlsCard(Kit kit, Transform parent)
        {
            var theme = kit.Theme;
            var card = kit.Image(parent, "ControlsCard", theme.Card, Kit.TopLeft, new Vector2(SideMargin, -MainTop), new Vector2(CardWidth, MainHeight), Fill.Tiled).transform;
            kit.Label(card, "Title", LocKeys.Calibration.ControlsHeader, 48, theme.TextDark, Kit.Top, new Vector2(0f, -24f), new Vector2(560f, 64f), shadow: false);

            var stage = kit.Image(card, "Stage", theme.BarBackground, Kit.Top, new Vector2(0f, -104f), new Vector2(576f, 288f), Fill.Sliced);
            stage.gameObject.AddComponent<RectMask2D>();
            var s = stage.transform;
            // GDD v2 §16, §19: cat arms rise into the stage; the upper paw holds the ball, the lower paw grips the cue
            // and thrusts it up into the ball paw.
            var ballPaw = CatArm(kit, s, "BallPaw", new Vector2(-24f, 20f), -15f, theme.PawOpen(0), false, LocKeys.Calibration.BallPawTag, new Vector2(-128f, 0f));
            var ball = kit.Image(s, "Ball", theme.Ball, Kit.Center, new Vector2(-24f, 92f), new Vector2(64f, 64f));
            var ballGroup = ball.gameObject.AddComponent<CanvasGroup>();
            var burst = kit.Image(s, "Burst", theme.Strike, Kit.Center, Vector2.zero, new Vector2(64f, 64f));
            var burstGroup = burst.gameObject.AddComponent<CanvasGroup>();
            burstGroup.alpha = 0f;
            // The cue points from the fist up at the ball paw (the rest offset is 72 right, 104 down).
            var cuePaw = CatArm(kit, s, "CuePaw", new Vector2(48f, -84f), 35f, theme.PawGrab(0), true, LocKeys.Calibration.CuePawTag, new Vector2(128f, 0f));
            ball.transform.SetAsLastSibling();

            var illustration = stage.gameObject.AddComponent<CalibrationTutorialIllustration>();
            UiFields.Set(illustration, "ballPaw", ballPaw);
            UiFields.Set(illustration, "cuePaw", cuePaw);
            UiFields.Set(illustration, "ball", ball.rectTransform);
            UiFields.Set(illustration, "ballGroup", ballGroup);
            UiFields.Set(illustration, "burst", burst.rectTransform);
            UiFields.Set(illustration, "burstGroup", burstGroup);
            var so = new SerializedObject(illustration);
            so.FindProperty("restOffset").vector2Value = new Vector2(72f, -104f);
            so.FindProperty("contactOffset").vector2Value = new Vector2(8f, -64f);
            so.FindProperty("ballOffset").vector2Value = new Vector2(0f, 72f);
            so.FindProperty("ballFlyHeight").floatValue = 112f;
            so.ApplyModifiedPropertiesWithoutUndo();

            ControlRow(kit, card, "BallRow", theme.Ball, LocKeys.Calibration.ControlBall, -408f);
            ControlRow(kit, card, "CueRow", theme.Cue, LocKeys.Calibration.ControlCue, -488f);
            ControlRow(kit, card, "StrikeRow", theme.Strike, LocKeys.Calibration.ControlStrike, -568f);
            return illustration;
        }

        /// <summary>
        /// Arm root (moved by the illustration): a rotated cat arm (P1 fur sleeve running down out of the clipped stage,
        /// the paw sprite at its end, optionally a cue stick gripped in the fist and pointing along the arm) and an
        /// unrotated Ball / Cue tag beside it.
        /// </summary>
        static RectTransform CatArm(Kit kit, Transform parent, string name, Vector2 pos, float rotation, Sprite? pawSprite, bool holdsCue,
            string tagKey, Vector2 tagOffset)
        {
            // make_arms.py sprites (sleeve 24 px wide, paw 36x40 px, cuff bottom 6 px above the paw's lower edge) at 3x.
            const float scale = 3f;
            var theme = kit.Theme;
            var root = kit.Ui(name, parent);
            var rect = Kit.Place(root, Kit.Center, pos, new Vector2(36f * scale, 40f * scale));
            var arm = kit.Ui("Arm", root.transform);
            Kit.Place(arm, Kit.Center, Vector2.zero, new Vector2(36f * scale, 40f * scale));
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var sleeve = kit.Image(arm.transform, "Sleeve", theme.Arm(0), Kit.Center, new Vector2(0f, -14f * scale), new Vector2(24f * scale, 400f));
            sleeve.rectTransform.pivot = new Vector2(0.5f, 1f);
            sleeve.type = Image.Type.Tiled;
            sleeve.preserveAspect = false;
            sleeve.pixelsPerUnitMultiplier = 3f / scale;
            if (holdsCue)
            {
                var stick = kit.Image(arm.transform, "Cue", null, Kit.Center, new Vector2(0f, -8f * scale), new Vector2(10f, 136f), color: new Color(0.62f, 0.39f, 0.2f, 1f));
                stick.rectTransform.pivot = new Vector2(0.5f, 0f);
                var tip = kit.Image(stick.transform, "Tip", null, Kit.Top, Vector2.zero, new Vector2(10f, 12f), color: new Color(0.93f, 0.95f, 1f, 1f));
                tip.rectTransform.pivot = new Vector2(0.5f, 1f);
            }

            kit.Image(arm.transform, "Paw", pawSprite, Kit.Center, Vector2.zero, new Vector2(36f * scale, 40f * scale));
            var tag = kit.Image(root.transform, "Tag", theme.Chip, Kit.Center, tagOffset, new Vector2(128f, 48f), Fill.Sliced);
            kit.Label(tag.transform, "Label", tagKey, 32, theme.Accent, Kit.Center, new Vector2(0f, 2f), new Vector2(120f, 48f));
            return rect;
        }

        static void ControlRow(Kit kit, Transform card, string name, Sprite? icon, string key, float y)
        {
            var theme = kit.Theme;
            var row = kit.Ui(name, card);
            Kit.Place(row, Kit.Top, new Vector2(0f, y), new Vector2(576f, 72f));
            var slot = kit.Image(row.transform, "IconSlot", theme.Slot, Kit.Left, Vector2.zero, new Vector2(72f, 72f), Fill.Sliced);
            kit.Image(slot.transform, "Icon", icon, Kit.Center, Vector2.zero, new Vector2(48f, 48f));
            kit.Label(row.transform, "Text", key, 32, theme.TextDark, Kit.Left, new Vector2(96f, 2f), new Vector2(480f, 72f),
                TextAlignmentOptions.Left, wrap: true, shadow: false);
        }

        /// <summary>Camera glyph on an empty screen (until the setup previews move in) + the tip line under them.</summary>
        static CanvasGroup BuildPlaceholder(Kit kit, Transform panel, out CanvasGroup setupTip)
        {
            var theme = kit.Theme;
            var go = kit.Ui("Placeholder", panel);
            Kit.Stretch(go);
            var group = go.AddComponent<CanvasGroup>();
            // Same rect as the previews container (FitPreviews): the setup frames cover the glyph once the camera runs.
            kit.Image(go.transform, "Screen", theme.BarBackground, Kit.Top, new Vector2(0f, -PanelInset), previewsSize, Fill.Sliced);
            kit.Image(go.transform, "Glyph", theme.CameraGlyph, Kit.Top, new Vector2(0f, -PanelInset - (previewsSize.y - 192f) * 0.5f),
                new Vector2(192f, 192f), color: new Color(1f, 1f, 1f, 0.55f));
            var tip = kit.Label(panel, "SetupTip", LocKeys.TrackingLost.Hint, 32, theme.TextMuted, Kit.Bottom, new Vector2(0f, 28f), new Vector2(1024f, 48f));
            setupTip = tip.gameObject.AddComponent<CanvasGroup>();
            return group;
        }

        /// <summary>Test-strike cards (one per player; CalibrationView places them for 1P or 2P).</summary>
        static CanvasGroup BuildPlayerCards(Kit kit, Transform panel, out List<CalibrationPlayerCard> cards)
        {
            var theme = kit.Theme;
            var go = kit.Ui("Players", panel);
            Kit.Stretch(go);
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            cards = new List<CalibrationPlayerCard>();
            for (var i = 0; i < 2; i++)
            {
                var cardGo = kit.Ui($"PlayerCard{i + 1}", go.transform);
                Kit.Place(cardGo, Kit.Center, new Vector2(-232f + 464f * i, 0f), new Vector2(400f, 528f));
                var glow = kit.StretchImage(cardGo.transform, "ReadyGlow", theme.ButtonFocused, -18f, Fill.Sliced);
                glow.gameObject.SetActive(false);
                var frame = kit.StretchImage(cardGo.transform, "Frame", theme.Card, 0f, Fill.Tiled).transform;
                var portrait = kit.Image(frame, "Portrait", theme.Portrait(i), Kit.Top, new Vector2(0f, -24f), new Vector2(256f, 256f));
                if (i == 1) portrait.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                var tag = kit.Image(frame, "PlayerTag", theme.Chip, Kit.Top, new Vector2(0f, -296f), new Vector2(112f, 64f), Fill.Sliced);
                var tagLabel = kit.Label(tag.transform, "Label", LocKeys.Hud.PlayerTag, 48, theme.PlayerColor(i), Kit.Center, new Vector2(0f, 3f),
                    new Vector2(104f, 64f));
                var statusChip = kit.Image(frame, "Status", theme.Chip, Kit.Bottom, new Vector2(0f, 32f), new Vector2(352f, 88f), Fill.Sliced);
                var status = kit.Label(statusChip.transform, "Label", LocKeys.Calibration.Waiting, 32, theme.TextMuted, Kit.Center, new Vector2(0f, 2f),
                    new Vector2(320f, 80f), wrap: true);
                var card = cardGo.AddComponent<CalibrationPlayerCard>();
                UiFields.Set(card, "theme", theme);
                UiFields.Set(card, "statusLabel", status);
                UiFields.Set(card, "readyGlow", glow.gameObject);
                UiFields.Set(card, "portrait", portrait.rectTransform);
                UiFields.Set(card, "tagLabel", tagLabel);
                cards.Add(card);
            }

            return group;
        }

        /// <summary>
        /// The starter PreviewsManager is a plain-Transform root with its own canvas child ("UI"), made for a scene root:
        /// nested under this view it collapsed to zero size and scale. Give that canvas the reference screen and put the previews
        /// container on the camera panel's inner rect; hide its prompt (the view's prompt label says it).
        /// </summary>
        static void FitPreviews(GameObject previews)
        {
            var ui = (RectTransform)previews.transform.Find("UI");
            ui.anchorMin = ui.anchorMax = ui.pivot = new Vector2(0.5f, 0.5f);
            ui.sizeDelta = new Vector2(1920f, 1080f);
            ui.anchoredPosition3D = Vector3.zero;
            // A root canvas stores scale 0 (Unity drives it); nested, nothing does.
            ui.localScale = Vector3.one;
            var container = (RectTransform)ui.Find("UIRoot/PreviewsContainer");
            var left = 1920f - SideMargin - cameraPanelSize.x + PanelInset;
            var right = left + previewsSize.x;
            var top = 1080f - MainTop - PanelInset;
            var bottom = top - previewsSize.y;
            container.anchorMin = new Vector2(left / 1920f, bottom / 1080f);
            container.anchorMax = new Vector2(right / 1920f, top / 1080f);
            container.offsetMin = container.offsetMax = Vector2.zero;
            ui.Find("UIRoot/SetupText").gameObject.SetActive(false);
        }

        #endregion

        #region Gameplay view

        public static GameplayView BuildGameplayView(string path, GameplayPip pipPrefab)
        {
            var kit = new Kit(LoadTheme());
            var root = FlowUiFactory.CreateViewRoot<GameplayView>("GameplayView", out var view);
            var rootGroup = root.GetComponent<CanvasGroup>();
            var animators = FlowUiFactory.CreateUIObject("Animators", root.transform);
            var entry = FlowUiFactory.CreateFadeAnimator("EntryAnimator", animators.transform, rootGroup, 0f, 1f);
            // Escape → top-level Back → GameplayView.OnBackButton → pause.
            var graph = FlowUiFactory.CreateGraphWithBackProxy(root.transform, null, TopLevelControlPanel.ControlConfig.Back);

            // First child so labels draw behind the HUD.
            var labelLayer = FlowUiFactory.CreateUIObject("WorldLabelLayer", root.transform);
            FlowUiFactory.Stretch(labelLayer);

            // GameplayView calls the HUD directly: build UI-Views (UiViewsBuilder) first.
            var hud = FlowUiFactory.InstantiateNested(GameplayHudPath, root.transform, "UI-Views");
            if (hud == null)
            {
                Object.DestroyImmediate(root);
                throw new System.InvalidOperationException($"{GameplayHudPath} missing: run UiViewsBuilder first.");
            }

            hud.name = "Hud";

            var sessionGo = FlowUiFactory.CreateUIObject("Session", root.transform);
            var session = sessionGo.AddComponent<GameSession>();
            var timeScale = sessionGo.AddComponent<TimeScaleController>();
            var inputRoot = FlowUiFactory.CreateUIObject("InputRoot", root.transform);
            var curtain = Curtain(kit, root.transform);

            var so = new SerializedObject(view);
            so.FindProperty("entryAnimator").objectReferenceValue = entry;
            so.FindProperty("keyResponder").objectReferenceValue = graph;
            so.FindProperty("worldLabelLayer").objectReferenceValue = labelLayer.transform;
            so.FindProperty("hud").objectReferenceValue = hud.GetComponent<GameplayHud>();
            so.FindProperty("session").objectReferenceValue = session;
            so.FindProperty("timeScale").objectReferenceValue = timeScale;
            so.FindProperty("inputRoot").objectReferenceValue = inputRoot.transform;
            so.FindProperty("pipPrefab").objectReferenceValue = pipPrefab;
            so.FindProperty("theme").objectReferenceValue = kit.Theme;
            so.FindProperty("curtain").objectReferenceValue = curtain;
            so.FindProperty("stageIntroViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<StageIntroView>(FlowUiFactory.ViewsFolder + "/StageIntroView.prefab", "UI-Views");
            so.FindProperty("rewardViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<RewardView>(FlowUiFactory.ViewsFolder + "/RewardView.prefab", "UI-Views");
            so.FindProperty("trackingLostViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<TrackingLostView>(FlowUiFactory.ViewsFolder + "/TrackingLostView.prefab", "UI-Views");
            so.FindProperty("pauseViewPrefab").objectReferenceValue = FlowUiFactory.LoadPrefabComponent<PauseView>(FlowUiFactory.ViewsFolder + "/PauseView.prefab", "UI-Views");
            so.ApplyModifiedPropertiesWithoutUndo();

            return FlowUiFactory.SavePrefab(root, path).GetComponent<GameplayView>();
        }

        #endregion

        #region Helpers

        static UiTheme LoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiViewsBuilder.ThemePath);
            if (theme == null) throw new System.InvalidOperationException($"{UiViewsBuilder.ThemePath} missing: run UiViewsBuilder first.");
            return theme;
        }

        /// <summary>Full-screen opaque cover (last child, so it draws over the view), transparent until faded in.</summary>
        static CanvasGroup Curtain(Kit kit, Transform root)
        {
            var image = kit.StretchImage(root, "Curtain", null, 0f, Fill.Simple, kit.Theme.CurtainColor);
            image.preserveAspect = false;
            var group = image.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        #endregion
    }
}
