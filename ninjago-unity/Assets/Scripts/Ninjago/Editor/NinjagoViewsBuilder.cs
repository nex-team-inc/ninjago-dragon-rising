#nullable enable

using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Nex.KeyboardNavigation;
using Nex.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// Every Ninjago view prefab. Views start as a copy of PressButtonToWinView (same canvas, scaler and fade
    /// animators) and get their UI rebuilt; widgets are built from scratch.
    /// </summary>
    public static class NinjagoViewsBuilder
    {
        const string TemplatePath = "Assets/Prefabs/Gameplay/Games/PressButtonToWin/PressButtonToWinView.prefab";
        internal const string ViewsRoot = PrefabsRoot + "/Views";
        internal const string WidgetsRoot = ViewsRoot + "/Widgets";
        public const string PlayerCountViewPath = ViewsRoot + "/PlayerCountView.prefab";
        public const string SetupViewPath = ViewsRoot + "/NinjagoSetupView.prefab";
        public const string FightViewPath = ViewsRoot + "/FightView.prefab";
        public const string RunnerViewPath = ViewsRoot + "/RunnerView.prefab";
        public const string ResultViewPath = ViewsRoot + "/NinjagoResultView.prefab";
        static readonly Color safeGreen = new(0.3f, 1f, 0.35f);
        static readonly Color warnRed = new(1f, 0.3f, 0.25f);

        static GameObject template = null!;
        static Material outline = null!;

        #region Entry Point

        public static void Build()
        {
            WithTemplate(() =>
            {
                var heart = BuildHeart();
                var hud = BuildFightHud(heart);
                var status = BuildSetupStatus();
                var line = BuildResultLine();
                BuildView<PlayerCountView>(PlayerCountViewPath, BuildPlayerCount);
                BuildView<NinjagoSetupView>(SetupViewPath, (view, ui) => BuildSetup(view, ui, status));
                BuildView<FightView>(FightViewPath, (view, ui) => BuildFight(view, ui, hud));
                BuildView<RunnerView>(RunnerViewPath, BuildRunner);
                BuildView<NinjagoResultView>(ResultViewPath, (view, ui) => BuildResult(view, ui, line));
            });
        }

        /// <summary>Runs view building with the template view and the outlined text material loaded.</summary>
        internal static void WithTemplate(System.Action build)
        {
            outline = OutlineTextMaterial();
            template = PrefabUtility.LoadPrefabContents(TemplatePath);
            try
            {
                build();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(template);
            }
        }

        internal static void BuildView<T>(string path, System.Action<T, RectTransform> build) where T : SimpleCanvasView
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                EnsureFolder(ViewsRoot);
                AssetDatabase.CopyAsset(TemplatePath, path);
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var oldView in root.GetComponents<View>()) Object.DestroyImmediate(oldView);
                var ui = (RectTransform)root.transform.Find("UI");
                for (var i = ui.childCount - 1; i >= 0; i--) Object.DestroyImmediate(ui.GetChild(i).gameObject);
                foreach (var proxy in ui.GetComponents<KeyResponder>()) Object.DestroyImmediate(proxy);

                var view = root.AddComponent<T>();
                Set(view, "entryAnimator", root.transform.Find("Animators/EntryAnimator").GetComponent<MMF_Player>());
                Set(view, "toBackgroundAnimator", root.transform.Find("Animators/BackgroundAnimator").GetComponent<MMF_Player>());
                build(view, ui);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        #endregion

        #region Views

        static void BuildPlayerCount(PlayerCountView view, RectTransform ui)
        {
            Title(ui, "ninjago.players.title", "How many players?", new Vector2(0f, 180f));
            var row = NewRect("Buttons", ui);
            Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1000f, 200f));
            var one = CloneButton(row, "OnePlayer", "ninjago.players.one", "1 Player", new Vector2(-260f, 0f));
            var two = CloneButton(row, "TwoPlayers", "ninjago.players.two", "2 Players", new Vector2(260f, 0f));
            var group = Group(row, one, two);
            Set(view, "onePlayerButton", one.GetComponent<Button>());
            Set(view, "twoPlayersButton", two.GetComponent<Button>());
            Set(view, "buttonsGroup", group);
            Set(view, "keyResponder", group);
        }

        internal static void BuildSetup(NinjagoSetupView view, RectTransform ui, SetupPlayerStatus statusPrefab)
        {
            var statusRow = NewRect("PlayerStatuses", ui);
            Place(statusRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1200f, 90f));
            var layout = statusRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 80f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            var statuses = new List<Object>();
            for (var i = 0; i < SimulatedBody.MaxPlayers; i++)
            {
                var status = (SetupPlayerStatus)PrefabUtility.InstantiatePrefab(statusPrefab, statusRow);
                statuses.Add(status);
            }

            Set(view, "previewsManagerPrefab", LoadComponent<PreviewsManager>("Assets/Prefabs/Detection/Preview/PreviewsManager.prefab"));
            SetArray(view, "playerStatuses", statuses);
            SetLocalized(view, "standPrompt", "ninjago.setup.stand");
            SetLocalized(view, "holdPrompt", "ninjago.setup.hold");
            SetLocalized(view, "readyPrompt", "ninjago.setup.ready");
            SetLocalized(view, "soloPrompt", "ninjago.setup.solo");
            Set(view, "keyResponder", BackProxy(ui));
        }

        static void BuildFight(FightView view, RectTransform ui, FightHud hudPrefab)
        {
            var solo = Feed(ui, "SoloFeed", Vector2.zero, Vector2.one);
            var left = Feed(ui, "LeftFeed", Vector2.zero, new Vector2(0.5f, 1f));
            var right = Feed(ui, "RightFeed", new Vector2(0.5f, 0f), Vector2.one);
            var divider = NewImage(ui, "SplitDivider", null, new Color(0.08f, 0.08f, 0.1f));
            Place(divider.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 0f));
            var pip = CameraPreview(ui, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(384f, 216f), out var pipFrame);
            var banner = Banner(ui, "GetReady", "ninjago.fight.get_ready", "Get ready!", 120f, Color.white, new Vector2(0f, 120f));

            Set(view, "soloFeed", solo);
            Set(view, "soloHudSlot", solo.rectTransform);
            Set(view, "leftFeed", left);
            Set(view, "leftHudSlot", left.rectTransform);
            Set(view, "rightFeed", right);
            Set(view, "rightHudSlot", right.rectTransform);
            Set(view, "splitDivider", divider.gameObject);
            Set(view, "hudPrefab", hudPrefab);
            Set(view, "lanePrefab", LoadComponent<FightLane>(NinjagoWorldBuilder.CourtyardPath));
            Set(view, "pipFrame", pipFrame);
            Set(view, "pipIndicators", pip);
            Set(view, "getReadyBanner", banner.gameObject);
            Set(view, "keyResponder", BackProxy(ui));
        }

        static void BuildRunner(RunnerView view, RectTransform ui)
        {
            var feed = Feed(ui, "WorldFeed", Vector2.zero, Vector2.one);
            var vehicle = Label(ui, "VehicleLabel", "ninjago.chase.car", "Car", 72f, Color.white, outline, TextAlignmentOptions.Left);
            Place((RectTransform)vehicle.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -30f), new Vector2(600f, 90f));
            var time = PlainText(ui, "TimeLeft", "20", 120f, Color.white, outline);
            Place(time.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(300f, 140f));
            var dodges = Label(ui, "DodgesLabel", null, "Dodges 0", 56f, safeGreen, outline, TextAlignmentOptions.Right);
            Place((RectTransform)dodges.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -30f), new Vector2(500f, 70f));
            var hits = Label(ui, "HitsLabel", null, "Hits 0", 56f, warnRed, outline, TextAlignmentOptions.Right);
            Place((RectTransform)hits.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -100f), new Vector2(500f, 70f));
            var lean = Banner(ui, "LeanHint", "ninjago.chase.lean_to_steer", "Lean to steer", 100f, Color.white, new Vector2(0f, 160f));
            var chest = Banner(ui, "WholeChestHint", "ninjago.chase.whole_chest", "Use your whole chest", 100f, new Color(1f, 0.9f, 0.35f), new Vector2(0f, 160f));
            var pip = CameraPreview(ui, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(384f, 216f), out var pipFrame);

            Set(view, "feed", feed);
            Set(view, "worldPrefab", LoadComponent<RunnerWorld>(NinjagoWorldBuilder.RunnerWorldPath));
            Set(view, "vehicleLabel", vehicle);
            SetLocalized(view, "carName", "ninjago.chase.car");
            SetLocalized(view, "skycraftName", "ninjago.chase.skycraft");
            Set(view, "timeLeft", time);
            Set(view, "dodgesLabel", dodges);
            SetLocalized(view, "dodgesText", "ninjago.chase.dodges");
            Set(view, "hitsLabel", hits);
            SetLocalized(view, "hitsText", "ninjago.chase.hits");
            Set(view, "leanHint", lean.gameObject);
            Set(view, "wholeChestHint", chest.gameObject);
            Set(view, "pipFrame", pipFrame);
            Set(view, "pipIndicators", pip);
            Set(view, "keyResponder", BackProxy(ui));
        }

        static void BuildResult(NinjagoResultView view, RectTransform ui, ResultLine linePrefab)
        {
            var title = Title(ui, "ninjago.result.victory", "Victory!", new Vector2(0f, 330f));
            var lines = NewRect("Lines", ui);
            Place(lines, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 220f), new Vector2(1700f, 520f));
            var layout = lines.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            var row = NewRect("Buttons", ui);
            Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1000f, 160f));
            var retry = CloneButton(row, "Retry", "ninjago.result.retry", "Retry", new Vector2(-220f, 0f));
            var menu = CloneButton(row, "Menu", "ninjago.result.menu", "Menu", new Vector2(220f, 0f));
            var group = Group(row, retry, menu);

            Set(view, "title", title);
            Set(view, "linesRoot", lines);
            Set(view, "linePrefab", linePrefab);
            Set(view, "retryButton", retry.GetComponent<Button>());
            Set(view, "menuButton", menu.GetComponent<Button>());
            Set(view, "buttonsGroup", group);
            Set(view, "keyResponder", group);
        }

        #endregion

        #region Widgets

        static Image BuildHeart()
        {
            var root = NewRect("Heart", null!);
            root.sizeDelta = new Vector2(60f, 60f);
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = GetSprite("Heart");
            image.raycastTarget = false;
            return SavePrefab(root.gameObject, $"{WidgetsRoot}/Heart.prefab").GetComponent<Image>();
        }

        static FightHud BuildFightHud(Image heartPrefab)
        {
            var root = NewRect("FightHud", null!);
            var hud = root.gameObject.AddComponent<FightHud>();
            var tag = NinjagoWorldBuilder.BuildPlayerTag(root, Vector2.zero, 72f);
            Place((RectTransform)tag.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(160f, 90f));
            var hearts = NewRect("Hearts", root);
            Place(hearts, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -36f), new Vector2(500f, 64f));
            var layout = hearts.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            var sweep = Label(root, "SweepLabel", null, "Sweep 1/4", 54f, Color.white, outline, TextAlignmentOptions.Right);
            Place((RectTransform)sweep.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -32f), new Vector2(420f, 70f));
            var leftArrow = SafeArrow(root, "LeftArrow", true, out var leftHint);
            var rightArrow = SafeArrow(root, "RightArrow", false, out var rightHint);
            var spin = Banner(root, "SpinHint", "ninjago.fight.spin_hint", "Move your hands!", 76f, new Color(1f, 0.9f, 0.35f), new Vector2(0f, -40f));
            var outBanner = Banner(root, "OutBanner", "ninjago.fight.out", "OUT", 170f, warnRed, new Vector2(0f, 80f));
            var finished = Banner(root, "FinishedBanner", "ninjago.fight.finished", "Finished!", 96f, safeGreen, new Vector2(0f, 260f));

            Set(hud, "playerTag", tag);
            Set(hud, "heartsRow", hearts);
            Set(hud, "heartPrefab", heartPrefab);
            Set(hud, "sweepLabel", sweep);
            SetLocalized(hud, "sweepText", "ninjago.fight.sweep");
            Set(hud, "leftArrow", leftArrow);
            Set(hud, "rightArrow", rightArrow);
            Set(hud, "leftSlipHint", leftHint);
            Set(hud, "rightSlipHint", rightHint);
            Set(hud, "spinHint", spin.gameObject);
            Set(hud, "outBanner", outBanner.gameObject);
            Set(hud, "finishedBanner", finished.gameObject);
            Stretch(root);
            return SavePrefab(root.gameObject, $"{WidgetsRoot}/FightHud.prefab").GetComponent<FightHud>();
        }

        // Big green arrow on the safe side, with the SLIP word under it for the first sweep.
        static GameObject SafeArrow(RectTransform parent, string name, bool left, out GameObject slipHint)
        {
            var anchor = new Vector2(left ? 0f : 1f, 0.5f);
            var rect = NewRect(name, parent);
            Place(rect, anchor, anchor, new Vector2(0.5f, 0.5f), new Vector2(left ? 170f : -170f, 40f), new Vector2(260f, 200f));
            var arrow = NewImage(rect, "Arrow", GetSprite("Arrow"), safeGreen);
            Stretch(arrow.rectTransform);
            if (left) arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            var hint = Label(rect, "SlipHint", "ninjago.fight.slip", "SLIP", 110f, safeGreen, outline);
            Place((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(360f, 130f));
            slipHint = hint.gameObject;
            return rect.gameObject;
        }

        static SetupPlayerStatus BuildSetupStatus()
        {
            var root = NewRect("SetupPlayerStatus", null!);
            root.sizeDelta = new Vector2(440f, 90f);
            var status = root.gameObject.AddComponent<SetupPlayerStatus>();
            var check = NewImage(root, "ReadyCheck", GetSprite("Circle"), Color.white);
            Place(check.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(54f, 54f));
            var tag = NinjagoWorldBuilder.BuildPlayerTag(root, Vector2.zero, 64f);
            Place((RectTransform)tag.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(110f, 80f));
            var text = Label(root, "Status", "ninjago.setup.status_waiting", "Step in", 56f, Color.white, outline, TextAlignmentOptions.Left);
            Place((RectTransform)text.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(190f, 0f), new Vector2(-190f, 80f));
            Set(status, "playerTag", tag);
            Set(status, "statusLabel", text);
            Set(status, "readyCheck", check);
            SetLocalized(status, "waitingText", "ninjago.setup.status_waiting");
            SetLocalized(status, "holdingText", "ninjago.setup.status_holding");
            SetLocalized(status, "readyText", "ninjago.setup.status_ready");
            return SavePrefab(root.gameObject, $"{WidgetsRoot}/SetupPlayerStatus.prefab").GetComponent<SetupPlayerStatus>();
        }

        static ResultLine BuildResultLine()
        {
            var root = NewRect("ResultLine", null!);
            root.sizeDelta = new Vector2(1700f, 76f);
            var line = root.gameObject.AddComponent<ResultLine>();
            var label = Label(root, "Text", null, "New best!", 54f, Color.white, outline);
            Stretch((RectTransform)label.transform);
            Set(line, "label", label);
            Set(line, "text", label.GetComponent<TextMeshProUGUI>());
            return SavePrefab(root.gameObject, $"{WidgetsRoot}/ResultLine.prefab").GetComponent<ResultLine>();
        }

        #endregion

        #region Helpers

        internal static NexLocalizedString Title(RectTransform ui, string key, string preview, Vector2 position)
        {
            var title = Object.Instantiate(template.transform.Find("UI/Title").gameObject, ui);
            title.name = "Title";
            var rect = (RectTransform)title.transform;
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(1600f, 160f));
            title.GetComponent<TextMeshProUGUI>().text = preview;
            var label = title.GetComponent<NexLocalizedString>();
            SetLocalized(label, "m_StringReference", key);
            return label;
        }

        internal static GameObject CloneButton(RectTransform parent, string name, string key, string preview, Vector2 position)
        {
            var button = Object.Instantiate(template.transform.Find("UI/WinButton").gameObject, parent);
            button.name = name;
            ((RectTransform)button.transform).anchoredPosition = position;
            var text = button.transform.Find("Text");
            text.GetComponent<TextMeshProUGUI>().text = preview;
            text.GetComponent<TextMeshProUGUI>().fontSize = 64f;
            SetLocalized(text.GetComponent<NexLocalizedString>(), "m_StringReference", key);
            return button;
        }

        internal static GroupKeyResponder Group(RectTransform row, params GameObject[] buttons)
        {
            var group = row.gameObject.AddComponent<GroupKeyResponder>();
            var responders = new List<Object>();
            foreach (var button in buttons) responders.Add(button.GetComponent<ButtonKeyResponder>());
            SetArray(group, "responders", responders);
            Set(group, "axis", property => property.enumValueIndex = 0);
            return group;
        }

        internal static NexLocalizedString Banner(RectTransform parent, string name, string key, string preview, float size, Color color, Vector2 position)
        {
            var label = Label(parent, name, key, preview, size, color, outline);
            Place((RectTransform)label.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(1400f, size * 1.3f));
            return label;
        }

        // Corner camera feed with player indicators (AreaPreviewFrame + PlayerIndicatorsManager, as in ARGameExample).
        internal static PlayerIndicatorsManager CameraPreview(RectTransform ui, Vector2 anchor, Vector2 position, Vector2 size, out AreaPreviewFrame frame)
        {
            var border = NewImage(ui, "CameraPreview", GetSprite("Panel"), new Color(0.08f, 0.08f, 0.1f, 0.9f));
            border.type = UnityEngine.UI.Image.Type.Sliced;
            Place(border.rectTransform, anchor, anchor, anchor, position, size + new Vector2(16f, 16f));
            var frameRect = NewRect("PreviewFrame", border.rectTransform);
            Place(frameRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(-16f, -16f));
            frameRect.anchoredPosition = new Vector2(8f, 8f);
            var group = frameRect.gameObject.AddComponent<CanvasGroup>();
            frame = frameRect.gameObject.AddComponent<AreaPreviewFrame>();
            var raw = NewRect("RawImage", frameRect).gameObject.AddComponent<RawImage>();
            Stretch(raw.rectTransform);
            raw.raycastTarget = false;
            Set(frame, "rawImage", raw);
            Set(frame, "canvasGroup", group);
            var indicators = border.gameObject.AddComponent<PlayerIndicatorsManager>();
            Set(indicators, "playerIndicatorPrefab", LoadComponent<PreviewFramePlayerIndicator>("Assets/Prefabs/Detection/Preview/PreviewFramePlayerIndicator.prefab"));
            Set(indicators, "playerIndicatorSizeRatioToPreviewHeight", property => property.floatValue = 0.2f);
            return indicators;
        }

        #endregion
    }
}
