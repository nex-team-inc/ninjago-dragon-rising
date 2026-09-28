#nullable enable

using Nex.KeyboardNavigation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Fill = Nex.BilliardRogue.Editor.UiPrefabKit.Fill;
using Kit = Nex.BilliardRogue.Editor.UiPrefabKit;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>Menu-style view prefabs: Title, PlayerMode, Settings, Pause (layout in 1920x1080 reference units).</summary>
    public static class UiMenuViewsBuilder
    {
        #region Title

        public static string BuildTitle(Kit kit, string path)
        {
            var theme = kit.Theme;
            var root = UiViewsBuilder.OpenRoot(kit, path, "TitleView");
            var view = UiViewsBuilder.ViewRoot<TitleView>(kit, root, out var content);
            Kit.SetBool(view, "disableCanvasOnBackground", true);
            var c = content.transform;
            kit.StretchImage(c, "Vignette", theme.Vignette, 0f, Fill.Simple, theme.VignetteColor).preserveAspect = false;
            kit.Image(c, "Logo", theme.Logo, Kit.Top, new Vector2(0f, -32f), new Vector2(1280f, 400f));
            kit.Label(c, "Tagline", LocKeys.Title.Tagline, 32, theme.TextPrimary, Kit.Top, new Vector2(0f, -448f), new Vector2(1200f, 48f));
            kit.Image(c, "PortraitP1", theme.PortraitP1, Kit.Center, new Vector2(-608f, -176f), new Vector2(256f, 256f));
            var p2 = kit.Image(c, "PortraitP2", theme.PortraitP2, Kit.Center, new Vector2(608f, -176f), new Vector2(256f, 256f));
            p2.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            var menu = kit.Ui("Menu", c);
            Kit.Place(menu, Kit.Center, new Vector2(0f, 40f), new Vector2(640f, 400f), Kit.Top);
            var layout = menu.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var group = kit.Group(menu, true);
            var continueButton = kit.MenuButton(menu.transform, "ContinueButton", LocKeys.Title.Continue, new Vector2(560f, 144f), new Vector2(0f, 22f));
            var info = kit.Label(continueButton.transform, "Info", LocKeys.Title.ContinueInfo, 32, theme.TextMuted, Kit.Center,
                new Vector2(0f, -30f), new Vector2(512f, 48f));
            var newRun = kit.MenuButton(menu.transform, "NewRunButton", LocKeys.Title.NewRun, new Vector2(560f, 96f));
            var settings = kit.MenuButton(menu.transform, "SettingsButton", LocKeys.Title.Settings, new Vector2(560f, 96f));

            var record = kit.Image(c, "RecordPanel", theme.Panel, Kit.Bottom, new Vector2(0f, 24f), new Vector2(1008f, 144f), Fill.Tiled);
            var best = kit.Label(record.transform, "Best", LocKeys.Title.Best, 32, theme.Accent, Kit.Center, new Vector2(0f, 18f), new Vector2(944f, 48f));
            var runs = kit.Label(record.transform, "Runs", LocKeys.Title.Runs, 32, theme.TextMuted, Kit.Center, new Vector2(0f, -22f), new Vector2(944f, 48f));

            var graph = kit.GraphWithControlProxy(root.transform, group, TopLevelControlPanel.ControlConfig.Exit);
            Kit.Set(view, "continueButton", continueButton);
            Kit.Set(view, "newRunButton", newRun);
            Kit.Set(view, "settingsButton", settings);
            Kit.Set(view, "continueInfo", info);
            Kit.Set(view, "bestLabel", best);
            Kit.Set(view, "runsLabel", runs);
            Kit.Set(view, "keyResponder", graph);
            Kit.Set(view, "popTarget", null);
            return UiViewsBuilder.SaveRoot(root, path);
        }

        #endregion

        #region Player mode

        public static string BuildPlayerMode(Kit kit, string path)
        {
            var theme = kit.Theme;
            var root = UiViewsBuilder.OpenRoot(kit, path, "PlayerModeView");
            var view = UiViewsBuilder.ViewRoot<PlayerModeView>(kit, root, out var content);
            var c = content.transform;
            kit.DimLayers(c);
            Banner(kit, c, "Header", LocKeys.PlayerMode.Prompt, new Vector2(0f, -72f), 896f);

            var cards = kit.Ui("Cards", c);
            Kit.Place(cards, Kit.Center, new Vector2(0f, -40f), new Vector2(1200f, 640f));
            var group = kit.Group(cards, false);
            var one = ChoiceCard(kit, cards.transform, "OnePlayerCard", new Vector2(-312f, 0f), new Vector2(528f, 600f), out var oneFrame);
            kit.Image(oneFrame, "Portrait", theme.PortraitP1, Kit.Center, new Vector2(0f, 90f), new Vector2(256f, 256f));
            CardTexts(kit, oneFrame, LocKeys.PlayerMode.OnePlayer, LocKeys.PlayerMode.SoloHint);
            var two = ChoiceCard(kit, cards.transform, "TwoPlayersCard", new Vector2(312f, 0f), new Vector2(528f, 600f), out var twoFrame);
            kit.Image(twoFrame, "PortraitP1", theme.PortraitP1, Kit.Center, new Vector2(-96f, 90f), new Vector2(256f, 256f));
            var p2 = kit.Image(twoFrame, "PortraitP2", theme.PortraitP2, Kit.Center, new Vector2(96f, 90f), new Vector2(256f, 256f));
            p2.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            CardTexts(kit, twoFrame, LocKeys.PlayerMode.TwoPlayers, LocKeys.PlayerMode.CoopHint);

            var graph = kit.GraphWithControlProxy(root.transform, group, TopLevelControlPanel.ControlConfig.Back);
            Kit.Set(view, "onePlayerButton", one);
            Kit.Set(view, "twoPlayersButton", two);
            Kit.Set(view, "cardsGroup", group);
            Kit.Set(view, "keyResponder", graph);
            Kit.Set(view, "popTarget", cards.transform);
            return UiViewsBuilder.SaveRoot(root, path);
        }

        static void CardTexts(Kit kit, Transform frame, string titleKey, string hintKey)
        {
            var theme = kit.Theme;
            kit.Label(frame, "Title", titleKey, 48, theme.TextDark, Kit.Center, new Vector2(0f, -104f), new Vector2(448f, 64f), shadow: false);
            kit.Label(frame, "Hint", hintKey, 32, theme.TextDark, Kit.Center, new Vector2(0f, -188f), new Vector2(432f, 96f),
                TextAlignmentOptions.Top, wrap: true, shadow: false);
        }

        /// <summary>
        /// Focusable parchment card: root (Button + highlight + responder) → Glow (focus ring, hidden) → Frame (card art,
        /// raycast target, content parent) → Cursor. Returns the Button; frame receives the content.
        /// </summary>
        public static Button ChoiceCard(Kit kit, Transform parent, string name, Vector2 pos, Vector2 size, out Transform frame)
        {
            var theme = kit.Theme;
            var go = kit.Ui(name, parent);
            Kit.Place(go, Kit.Center, pos, size);
            var glow = kit.StretchImage(go.transform, "Glow", theme.ButtonFocused, -18f, Fill.Sliced);
            glow.gameObject.SetActive(false);
            var frameImage = kit.StretchImage(go.transform, "Frame", theme.Card, 0f, Fill.Tiled);
            frameImage.raycastTarget = true;
            var button = kit.PlainButton(go, frameImage);
            var cursor = kit.Cursor(go.transform, size.x + 24f);
            var highlight = kit.Highlight(go, go.transform, null, null, glow.gameObject, cursor);
            kit.Responder(go, button, highlight);
            frame = frameImage.transform;
            return button;
        }

        #endregion

        #region Settings

        public static SettingsView BuildSettings(Kit kit, string path)
        {
            var theme = kit.Theme;
            var root = UiViewsBuilder.OpenRoot(kit, path, "SettingsView");
            var view = UiViewsBuilder.ViewRoot<SettingsView>(kit, root, out var content);
            var c = content.transform;
            kit.DimLayers(c);
            var panel = kit.Image(c, "Panel", theme.Panel, Kit.Center, new Vector2(0f, -24f), new Vector2(1184f, 960f), Fill.Tiled);
            Banner(kit, panel.transform, "Header", LocKeys.Settings.Header, new Vector2(0f, 48f), 704f);

            var rows = kit.Ui("Rows", panel.transform);
            Kit.Place(rows, Kit.Top, new Vector2(0f, -112f), new Vector2(1056f, 744f));
            var group = kit.Group(rows, true);
            var r = rows.transform;
            var language = Row(kit, r, "LanguageRow", LocKeys.Settings.Language, 0, false);
            var master = Row(kit, r, "MasterVolumeRow", LocKeys.Settings.MasterVolume, 1, true);
            var music = Row(kit, r, "MusicVolumeRow", LocKeys.Settings.MusicVolume, 2, true);
            var sfx = Row(kit, r, "SfxVolumeRow", LocKeys.Settings.SfxVolume, 3, true);
            var aim = Row(kit, r, "AimGuideRow", LocKeys.Settings.AimGuide, 4, false);
            var leftHanded = Row(kit, r, "LeftHandedRow", LocKeys.Settings.LeftHanded, 5, false);
            var shake = Row(kit, r, "ScreenShakeRow", LocKeys.Settings.ScreenShake, 6, false);
            var hint = kit.Label(panel.transform, "Hint", LocKeys.Settings.Hint, 32, theme.TextMuted, Kit.Bottom, new Vector2(0f, 36f), new Vector2(1056f, 48f));
            hint.transform.SetAsLastSibling();

            var graph = kit.GraphWithControlProxy(root.transform, group, TopLevelControlPanel.ControlConfig.Back);
            Kit.Set(view, "languageRow", language);
            Kit.Set(view, "masterRow", master);
            Kit.Set(view, "musicRow", music);
            Kit.Set(view, "sfxRow", sfx);
            Kit.Set(view, "aimGuideRow", aim);
            Kit.Set(view, "leftHandedRow", leftHanded);
            Kit.Set(view, "screenShakeRow", shake);
            Kit.Set(view, "keyResponder", graph);
            Kit.Set(view, "popTarget", panel.transform);
            UiViewsBuilder.SaveRoot(root, path);
            return AssetDatabase.LoadAssetAtPath<SettingsView>(path);
        }

        static SettingRow Row(Kit kit, Transform parent, string name, string labelKey, int index, bool bar)
        {
            var theme = kit.Theme;
            const float width = 1056f;
            var go = kit.Ui(name, parent);
            Kit.Place(go, Kit.Top, new Vector2(0f, -index * 108f - 48f), new Vector2(width, 96f), Kit.Center);
            var frame = kit.Configure(go.AddComponent<Image>(), theme.Button, Fill.Sliced, Color.white);
            kit.Label(go.transform, "Label", labelKey, 48, theme.TextPrimary, Kit.Left, new Vector2(40f, 3f), new Vector2(560f, 64f),
                TextAlignmentOptions.Left);
            var left = kit.Image(go.transform, "ArrowLeft", theme.ArrowLeft, Kit.Center, new Vector2(88f, 0f), new Vector2(48f, 48f));
            var right = kit.Image(go.transform, "ArrowRight", theme.ArrowRight, Kit.Center, new Vector2(472f, 0f), new Vector2(48f, 48f));
            var dim = new Color(1f, 1f, 1f, 0.45f);
            left.color = dim;
            right.color = dim;
            TextLabel? value = null;
            Image? fill = null;
            TextLabel? percent = null;
            if (bar)
            {
                var background = kit.Image(go.transform, "Bar", theme.BarBackground, Kit.Center, new Vector2(236f, 0f), new Vector2(200f, 42f), Fill.Sliced);
                fill = kit.StretchImage(background.transform, "Fill", theme.HpFill, 9f, Fill.Horizontal);
                percent = kit.Label(go.transform, "Percent", null, 32, theme.TextPrimary, Kit.Center, new Vector2(388f, 2f), new Vector2(96f, 48f),
                    numbersPreview: "100%");
            }
            else
            {
                value = kit.Label(go.transform, "Value", LocKeys.Settings.On, 48, theme.TextPrimary, Kit.Center, new Vector2(280f, 3f), new Vector2(288f, 64f));
            }

            var cursor = kit.Cursor(go.transform, width);
            var highlight = kit.Highlight(go, go.transform, frame, theme.ButtonFocused, null, cursor, left, right);
            var row = go.AddComponent<SettingRow>();
            Kit.Set(row, "highlight", highlight);
            Kit.Set(row, "valueLabel", value);
            Kit.Set(row, "bar", fill);
            Kit.Set(row, "percentLabel", percent);
            return row;
        }

        #endregion

        #region Pause

        public static string BuildPause(Kit kit, string path, SettingsView settingsPrefab)
        {
            var theme = kit.Theme;
            var root = UiViewsBuilder.OpenRoot(kit, path, "PauseView");
            var view = UiViewsBuilder.ViewRoot<PauseView>(kit, root, out var content);
            var c = content.transform;
            kit.DimLayers(c);
            var panel = kit.Image(c, "Panel", theme.Panel, Kit.Center, new Vector2(0f, -8f), new Vector2(704f, 560f), Fill.Tiled);
            Banner(kit, panel.transform, "Header", LocKeys.Pause.Header, new Vector2(0f, 48f), 576f);
            var menu = kit.Ui("Menu", panel.transform);
            Kit.Place(menu, Kit.Center, new Vector2(0f, -24f), new Vector2(544f, 336f));
            var group = kit.Group(menu, true);
            var resume = kit.MenuButton(menu.transform, "ResumeButton", LocKeys.Pause.Resume, new Vector2(544f, 96f));
            var settings = kit.MenuButton(menu.transform, "SettingsButton", LocKeys.Pause.Settings, new Vector2(544f, 96f));
            var saveQuit = kit.MenuButton(menu.transform, "SaveQuitButton", LocKeys.Pause.SaveQuit, new Vector2(544f, 96f));
            ((RectTransform)resume.transform).anchoredPosition = new Vector2(0f, 120f);
            ((RectTransform)saveQuit.transform).anchoredPosition = new Vector2(0f, -120f);

            var graph = kit.GraphWithControlProxy(root.transform, group, TopLevelControlPanel.ControlConfig.Back);
            Kit.Set(view, "resumeButton", resume);
            Kit.Set(view, "settingsButton", settings);
            Kit.Set(view, "saveQuitButton", saveQuit);
            Kit.Set(view, "settingsViewPrefab", settingsPrefab);
            Kit.Set(view, "keyResponder", graph);
            Kit.Set(view, "popTarget", panel.transform);
            return UiViewsBuilder.SaveRoot(root, path);
        }

        #endregion

        /// <summary>Red ribbon (Frame_Banner, native 96 tall) with a centred 48 label (text rows 6..21 → +6 units).</summary>
        public static Image Banner(Kit kit, Transform parent, string name, string key, Vector2 pos, float width, Vector2? anchor = null)
        {
            var banner = kit.Image(parent, name, kit.Theme.Banner, anchor ?? Kit.Top, pos, new Vector2(width, 96f), Fill.Sliced);
            kit.Label(banner.transform, "Label", key, 48, kit.Theme.TextPrimary, Kit.Center, new Vector2(0f, 6f), new Vector2(width - 144f, 64f));
            return banner;
        }
    }
}
