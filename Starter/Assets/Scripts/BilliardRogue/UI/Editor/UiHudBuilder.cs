#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Fill = Nex.BilliardRogue.Editor.UiPrefabKit.Fill;
using Kit = Nex.BilliardRogue.Editor.UiPrefabKit;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// GameplayHud prefab (1920x1080 units): two 448-wide columns at 32 from the screen edges with 16 between panels,
    /// ribbons at the top centre; the centre band (x 480..1440) stays free for the arena.
    /// Left: camera panel (header + P1/P2 chips + the 384x216 screen the PiP feed overlay covers) → stage/turn → HP →
    /// tracking warnings. Right (vertical layout, so the boss bar collapses on normal stages): boss bar → ball queue
    /// (sized to the turn's shots) → chips. Hype meter at the bottom of the left column, MOVE! prompt above the arena
    /// bottom (UiHypeBuilder).
    /// </summary>
    public static class UiHudBuilder
    {
        public const float Margin = 32f;
        public const float ColumnWidth = 448f;
        public const float Gutter = 16f;
        /// <summary>Content inset inside a Frame_Panel (its 30-unit border is mostly flat navy).</summary>
        const float Inset = 32f;

        /// <summary>Camera feed rect from the screen's top-left corner (FlowPrefabsBuilder places the PiP overlay on it).</summary>
        public static readonly Vector2 PipFeedScreenPosition = new(Margin + Inset, -(Margin + PipFeedTop));
        public static readonly Vector2 PipFeedSize = new(384f, 216f);
        const float PipFeedTop = 80f;
        const float PipPanelHeight = PipFeedTop + 216f + Inset;

        const int QueueSlots = 12;
        const int QueueColumns = 6;
        const float SlotSize = 64f;
        const float SlotPitch = 68f;
        const float GridTop = 224f;
        const float QueueBottomPadding = 20f;

        public static string Build(Kit kit, string path)
        {
            var theme = kit.Theme;
            var root = UiViewsBuilder.OpenRoot(kit, path, "GameplayHud");
            root.layer = LayerMask.NameToLayer("UI");
            Kit.Stretch(root);
            var canvas = UiViewsBuilder.GetOrAdd<Canvas>(root);
            canvas.additionalShaderChannels = (AdditionalCanvasShaderChannels)27;
            var hud = UiViewsBuilder.GetOrAdd<GameplayHud>(root);
            UiFields.Set(hud, "theme", theme);

            var left = kit.Ui("LeftColumn", root.transform);
            Kit.Place(left, Kit.TopLeft, new Vector2(Margin, -Margin), new Vector2(ColumnWidth, 1080f - 2f * Margin));
            UiFields.Set(hud, "leftColumn", left.transform);
            UiFields.Set(hud, "leftGroup", left.AddComponent<CanvasGroup>());
            var y = 0f;
            y = BuildCameraPanel(kit, left.transform, hud, y);
            y = BuildStagePanel(kit, left.transform, hud, y);
            y = BuildHpPanel(kit, left.transform, hud, y);
            var warnings = kit.Ui("TrackingWarnings", left.transform);
            Kit.Place(warnings, Kit.TopLeft, new Vector2(0f, y), new Vector2(ColumnWidth, 144f));
            Column(warnings, TextAnchor.UpperLeft);
            IsolateCanvas(warnings);
            var warningChips = new List<Object>
            {
                Chip(kit, warnings.transform, "WarningP1", LocKeys.Hud.TrackingWarningPlayer, theme.Danger, new Vector2(ColumnWidth, 64f), true),
                Chip(kit, warnings.transform, "WarningP2", LocKeys.Hud.TrackingWarningPlayer, theme.Danger, new Vector2(ColumnWidth, 64f), true),
            };
            UiFields.SetArray(hud, "trackingWarnings", warningChips);
            UiHypeBuilder.BuildMeter(kit, left.transform, hud, 1080f - 2f * Margin);

            var right = kit.Ui("RightColumn", root.transform);
            Kit.Place(right, Kit.TopRight, new Vector2(-Margin, -Margin), new Vector2(ColumnWidth, 1080f - 2f * Margin));
            Column(right, TextAnchor.UpperRight);
            UiFields.Set(hud, "rightColumn", right.transform);
            UiFields.Set(hud, "rightGroup", right.AddComponent<CanvasGroup>());
            BuildBossBar(kit, right.transform, hud);
            BuildBallsPanel(kit, right.transform, hud);
            var chips = kit.Ui("Chips", right.transform);
            Kit.Place(chips, Kit.TopLeft, Vector2.zero, new Vector2(ColumnWidth, 224f));
            Column(chips, TextAnchor.UpperLeft);
            IsolateCanvas(chips);
            UiFields.Set(hud, "bonusBallsChip", Chip(kit, chips.transform, "BonusBalls", LocKeys.Hud.BonusBalls, theme.Positive, new Vector2(ColumnWidth, 64f), false));
            UiFields.Set(hud, "powerChip", Chip(kit, chips.transform, "PowerReady", LocKeys.Hud.PowerArmed, theme.Danger, new Vector2(ColumnWidth, 64f), true));
            UiFields.Set(hud, "fastForwardChip", Chip(kit, chips.transform, "FastForward", LocKeys.Hud.FastForward, theme.TextPrimary, new Vector2(ColumnWidth, 64f), true));

            UiHypeBuilder.BuildMovePrompt(kit, root.transform, hud);
            UiFields.Set(hud, "turnBanner", Ribbon(kit, root.transform, "TurnBanner", LocKeys.Hud.TurnBanner, new Vector2(0f, -40f), 704f));
            UiFields.Set(hud, "shooterBanner", Ribbon(kit, root.transform, "ShooterBanner", LocKeys.Hud.ShooterBanner, new Vector2(0f, -152f), 576f));
            return UiViewsBuilder.SaveRoot(root, path);
        }

        #region Left column

        /// <summary>Header (camera glyph, title, P1/P2 chips) over the feed screen; the placeholder shows until the first frame.</summary>
        static float BuildCameraPanel(Kit kit, Transform parent, GameplayHud hud, float y)
        {
            var theme = kit.Theme;
            var panel = kit.Image(parent, "CameraPanel", theme.Panel, Kit.TopLeft, new Vector2(0f, y), new Vector2(ColumnWidth, PipPanelHeight), Fill.Tiled).transform;
            kit.Image(panel, "Glyph", theme.CameraGlyph, Kit.TopLeft, new Vector2(Inset - 4f, -20f), new Vector2(48f, 48f));
            kit.Label(panel, "Title", LocKeys.Hud.PipTitle, 32, theme.TextMuted, Kit.TopLeft, new Vector2(Inset + 52f, -20f), new Vector2(176f, 48f),
                TextAlignmentOptions.Left);
            BuildPlayerTags(kit, panel, hud);

            var screen = kit.Image(panel, "Screen", theme.BarBackground, Kit.TopLeft, new Vector2(Inset, -PipFeedTop), PipFeedSize, Fill.Sliced).transform;
            var placeholder = new Color(1f, 1f, 1f, 0.55f);
            kit.Image(screen, "CameraGlyph", theme.CameraGlyph, Kit.Center, new Vector2(0f, 24f), new Vector2(96f, 96f), color: placeholder);
            kit.Label(screen, "Tip", LocKeys.Setup.ShowBothPaws, 32, theme.TextMuted, Kit.Center, new Vector2(0f, -56f), new Vector2(352f, 48f));
            return y - PipPanelHeight - Gutter;
        }

        /// <summary>2-player runs only (the widget hides itself otherwise): the active shooter's chip is full size and bouncing.</summary>
        static void BuildPlayerTags(Kit kit, Transform parent, GameplayHud hud)
        {
            var theme = kit.Theme;
            var root = kit.Ui("PlayerTags", parent);
            Kit.Place(root, Kit.TopRight, new Vector2(-Inset + 8f, -16f), new Vector2(208f, 56f));
            var tags = new List<Object>();
            var groups = new List<Object>();
            var tints = new List<Object>();
            var labels = new List<Object>();
            for (var i = 0; i < 2; i++)
            {
                var tag = kit.Image(root.transform, $"TagP{i + 1}", theme.Chip, Kit.Center, new Vector2(-52f + i * 104f, 0f), new Vector2(96f, 56f), Fill.Sliced);
                groups.Add(tag.gameObject.AddComponent<CanvasGroup>());
                var label = kit.Label(tag.transform, "Label", LocKeys.Hud.PlayerTag, 48, theme.PlayerColor(i), Kit.Center, new Vector2(0f, 3f), new Vector2(88f, 56f));
                tags.Add(tag.transform);
                tints.Add(label.Face);
                labels.Add(label);
            }

            var widget = root.AddComponent<PlayerTagsWidget>();
            UiFields.Set(widget, "theme", theme);
            UiFields.SetArray(widget, "tags", tags);
            UiFields.SetArray(widget, "tagGroups", groups);
            UiFields.SetArray(widget, "tagTints", tints);
            UiFields.SetArray(widget, "tagLabels", labels);
            UiFields.Set(hud, "playerTags", widget);
            root.SetActive(false);
        }

        static float BuildStagePanel(Kit kit, Transform parent, GameplayHud hud, float y)
        {
            var theme = kit.Theme;
            const float height = 128f;
            var panel = kit.Image(parent, "StagePanel", theme.Panel, Kit.TopLeft, new Vector2(0f, y), new Vector2(ColumnWidth, height), Fill.Tiled).transform;
            UiFields.Set(hud, "stageLabel", kit.Label(panel, "Stage", LocKeys.Hud.Stage, 32, theme.TextPrimary, Kit.TopLeft, new Vector2(Inset, -24f),
                new Vector2(272f, 48f), TextAlignmentOptions.Left));
            UiFields.Set(hud, "turnLabel", kit.Label(panel, "Turn", LocKeys.Hud.Turn, 32, theme.TextMuted, Kit.TopLeft, new Vector2(Inset, -64f),
                new Vector2(272f, 48f), TextAlignmentOptions.Left));
            UiFields.Set(hud, "bossStageChip", Chip(kit, panel, "BossChip", LocKeys.Hud.Boss, theme.Danger, new Vector2(112f, 48f), true,
                Kit.TopRight, new Vector2(-Inset + 8f, -24f)));
            return y - height - Gutter;
        }

        static float BuildHpPanel(Kit kit, Transform parent, GameplayHud hud, float y)
        {
            var theme = kit.Theme;
            const float height = 144f;
            var panel = kit.Image(parent, "HpPanel", theme.Panel, Kit.TopLeft, new Vector2(0f, y), new Vector2(ColumnWidth, height), Fill.Tiled);
            IsolateCanvas(panel.gameObject);
            var shake = kit.Ui("Content", panel.transform);
            Kit.Stretch(shake);
            var s = shake.transform;
            var heart = kit.Image(s, "Heart", theme.Heart, Kit.TopLeft, new Vector2(Inset - 4f, -24f), new Vector2(48f, 48f));
            kit.Label(s, "HpLabel", LocKeys.Hud.Hp, 32, theme.TextMuted, Kit.TopLeft, new Vector2(Inset + 52f, -24f), new Vector2(128f, 48f), TextAlignmentOptions.Left);
            var numbers = kit.Label(s, "Numbers", null, 48, theme.TextPrimary, Kit.TopRight, new Vector2(-Inset, -16f), new Vector2(224f, 64f),
                TextAlignmentOptions.Right, numbersPreview: "30/30");
            var (fill, ghost) = Bar(kit, s, theme.HpFill, new Vector2(Inset, -84f), ColumnWidth - 2f * Inset);
            var widget = panel.gameObject.AddComponent<HpBarWidget>();
            UiFields.Set(widget, "theme", theme);
            UiFields.Set(widget, "fill", fill);
            UiFields.Set(widget, "ghostFill", ghost);
            UiFields.Set(widget, "numbers", numbers);
            UiFields.Set(widget, "shakeTarget", shake.transform);
            UiFields.Set(widget, "heart", heart.transform);
            UiFields.Set(hud, "hpBar", widget);
            return y - height - Gutter;
        }

        #endregion

        #region Right column

        static void BuildBossBar(Kit kit, Transform parent, GameplayHud hud)
        {
            var theme = kit.Theme;
            var panel = kit.Image(parent, "BossBar", theme.Panel, Kit.TopLeft, Vector2.zero, new Vector2(ColumnWidth, 144f), Fill.Tiled);
            IsolateCanvas(panel.gameObject);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            var shake = kit.Ui("Content", panel.transform);
            Kit.Stretch(shake);
            var s = shake.transform;
            kit.Image(s, "Skull", theme.Skull, Kit.TopLeft, new Vector2(Inset - 4f, -24f), new Vector2(48f, 48f));
            var name = kit.Label(s, "Name", LocKeys.Enemy.Names[8], 32, theme.Accent, Kit.TopLeft, new Vector2(Inset + 52f, -24f), new Vector2(332f, 48f),
                TextAlignmentOptions.Left);
            var (fill, ghost) = Bar(kit, s, theme.BossFill, new Vector2(Inset, -84f), ColumnWidth - 2f * Inset);
            var numbers = kit.Label(fill.transform.parent, "Numbers", null, 32, theme.TextPrimary, Kit.Center, new Vector2(0f, 2f), new Vector2(368f, 48f),
                numbersPreview: "70/70");
            var widget = panel.gameObject.AddComponent<BossBarWidget>();
            UiFields.Set(widget, "theme", theme);
            UiFields.Set(widget, "group", group);
            UiFields.Set(widget, "nameLabel", name);
            UiFields.Set(widget, "fill", fill);
            UiFields.Set(widget, "ghostFill", ghost);
            UiFields.Set(widget, "numbers", numbers);
            UiFields.Set(widget, "shakeTarget", shake.transform);
            UiFields.Set(hud, "bossBar", widget);
            panel.gameObject.SetActive(false);
        }

        /// <summary>Header with the big counter, the NEXT slot (icon, name, level) and the grid of the turn's shots.</summary>
        static void BuildBallsPanel(Kit kit, Transform parent, GameplayHud hud)
        {
            var theme = kit.Theme;
            var gridWidth = QueueColumns * SlotPitch - (SlotPitch - SlotSize);
            var gridLeft = (ColumnWidth - gridWidth) * 0.5f;
            var heightWithoutGrid = GridTop + QueueBottomPadding - (SlotPitch - SlotSize);
            var panel = kit.Image(parent, "BallsPanel", theme.Panel, Kit.TopLeft, Vector2.zero,
                new Vector2(ColumnWidth, heightWithoutGrid + 2f * SlotPitch), Fill.Tiled);
            IsolateCanvas(panel.gameObject);
            var p = panel.transform;
            kit.Image(p, "Icon", theme.Ball, Kit.TopLeft, new Vector2(Inset - 4f, -24f), new Vector2(48f, 48f));
            kit.Label(p, "Label", LocKeys.Hud.BallsLabel, 32, theme.TextMuted, Kit.TopLeft, new Vector2(Inset + 52f, -24f), new Vector2(160f, 48f),
                TextAlignmentOptions.Left);
            UiFields.Set(hud, "ballsCounter", kit.Label(p, "Counter", null, 64, theme.TextPrimary, Kit.TopRight, new Vector2(-Inset, -8f), new Vector2(208f, 80f),
                TextAlignmentOptions.Right, numbersPreview: "12/12"));

            // NEXT: pivot centred so the pop on a new ball scales in place. Icon at 5x inside the 12-unit slot border.
            const float heroSize = 104f;
            var hero = kit.Image(p, "Next", theme.SlotActive, Kit.TopLeft, Vector2.zero, new Vector2(heroSize, heroSize), Fill.Sliced);
            Kit.Place(hero.gameObject, Kit.TopLeft, new Vector2(Inset + heroSize * 0.5f, -104f - heroSize * 0.5f), new Vector2(heroSize, heroSize), Kit.Center);
            var heroIcon = kit.Image(hero.transform, "Icon", theme.Ball, Kit.Center, Vector2.zero, new Vector2(80f, 80f));
            // Widest name at 48: "Boule blanche" (252 units).
            const float textLeft = Inset + heroSize + 12f;
            var textWidth = ColumnWidth - textLeft - 20f;
            var caption = kit.Label(p, "NextCaption", LocKeys.Hud.Next, 32, theme.Accent, Kit.TopLeft, new Vector2(textLeft, -96f), new Vector2(textWidth, 40f),
                TextAlignmentOptions.Left);
            var name = kit.Label(p, "NextName", LocKeys.Ball.Names[0], 48, theme.TextPrimary, Kit.TopLeft, new Vector2(textLeft, -128f), new Vector2(textWidth, 64f),
                TextAlignmentOptions.Left);
            var level = kit.Label(p, "NextLevel", LocKeys.Reward.Level, 32, theme.TextMuted, Kit.TopLeft, new Vector2(textLeft, -184f), new Vector2(textWidth, 40f),
                TextAlignmentOptions.Left);

            var queue = kit.Ui("Queue", p);
            Kit.Place(queue, Kit.TopLeft, new Vector2(gridLeft, -GridTop), new Vector2(gridWidth, 2f * SlotPitch));
            var frames = new List<Object>();
            var icons = new List<Object>();
            var levels = new List<Object>();
            var bonusTags = new List<Object>();
            var groups = new List<Object>();
            RectTransform? marker = null;
            for (var i = 0; i < QueueSlots; i++)
            {
                var pos = new Vector2(i % QueueColumns * SlotPitch + SlotSize * 0.5f, -(i / QueueColumns) * SlotPitch - SlotSize * 0.5f);
                var frame = kit.Image(queue.transform, $"Slot{i}", theme.Slot, Kit.TopLeft, Vector2.zero, new Vector2(SlotSize, SlotSize), Fill.Sliced);
                Kit.Place(frame.gameObject, Kit.TopLeft, pos, new Vector2(SlotSize, SlotSize), Kit.Center);
                groups.Add(frame.gameObject.AddComponent<CanvasGroup>());
                icons.Add(kit.Image(frame.transform, "Icon", theme.Ball, Kit.Center, Vector2.zero, new Vector2(48f, 48f)));
                levels.Add(kit.Label(frame.transform, "Level", null, 32, theme.Accent, new Vector2(1f, 0f), new Vector2(0f, -4f), new Vector2(28f, 40f),
                    TextAlignmentOptions.Right, numbersPreview: "2"));
                var bonus = kit.Label(frame.transform, "Bonus", null, 32, theme.Positive, Kit.TopLeft, new Vector2(4f, 4f), new Vector2(28f, 40f),
                    TextAlignmentOptions.Left, numbersPreview: "+");
                bonusTags.Add(bonus.gameObject);
                frames.Add(frame);
                if (i != 0) continue;
                var cursor = kit.Image(frame.transform, "NextMarker", theme.Cursor, new Vector2(0f, 0f), new Vector2(-4f, -4f), new Vector2(32f, 32f));
                marker = cursor.rectTransform;
            }

            var widget = panel.gameObject.AddComponent<BallQueueWidget>();
            UiFields.Set(widget, "theme", theme);
            UiFields.Set(widget, "panel", panel.rectTransform);
            UiFields.SetFloat(widget, "heightWithoutGrid", heightWithoutGrid);
            UiFields.SetFloat(widget, "rowPitch", SlotPitch);
            UiFields.SetInt(widget, "columns", QueueColumns);
            UiFields.Set(widget, "heroRoot", hero.rectTransform);
            UiFields.Set(widget, "heroFrame", hero);
            UiFields.Set(widget, "heroIcon", heroIcon);
            UiFields.Set(widget, "heroCaption", caption);
            UiFields.Set(widget, "heroName", name);
            UiFields.Set(widget, "heroLevel", level);
            UiFields.SetArray(widget, "frames", frames);
            UiFields.SetArray(widget, "icons", icons);
            UiFields.SetArray(widget, "levels", levels);
            UiFields.SetArray(widget, "bonusTags", bonusTags);
            UiFields.SetArray(widget, "groups", groups);
            UiFields.Set(widget, "nextMarker", marker);
            UiFields.Set(hud, "ballQueue", widget);
        }

        #endregion

        #region Pieces

        internal static (Image Fill, Image Ghost) Bar(Kit kit, Transform parent, Sprite? fillSprite, Vector2 pos, float width)
        {
            var background = kit.Image(parent, "Bar", kit.Theme.BarBackground, Kit.TopLeft, pos, new Vector2(width, 42f), Fill.Sliced);
            var ghost = kit.StretchImage(background.transform, "Ghost", fillSprite, 9f, Fill.Horizontal, new Color(1f, 1f, 1f, 0.4f));
            var fill = kit.StretchImage(background.transform, "Fill", fillSprite, 9f, Fill.Horizontal);
            return (fill, ghost);
        }

        static HudChip Chip(Kit kit, Transform parent, string name, string key, Color color, Vector2 size, bool pulse,
            Vector2? anchor = null, Vector2? pos = null)
        {
            var image = kit.Image(parent, name, kit.Theme.Chip, anchor ?? Kit.Center, pos ?? Vector2.zero, size, Fill.Sliced);
            var group = image.gameObject.AddComponent<CanvasGroup>();
            var label = kit.Label(image.transform, "Label", key, 32, color, Kit.Center, new Vector2(0f, 2f), new Vector2(size.x - 32f, 48f));
            var chip = image.gameObject.AddComponent<HudChip>();
            UiFields.Set(chip, "theme", kit.Theme);
            UiFields.Set(chip, "group", group);
            UiFields.Set(chip, "label", label);
            UiFields.SetBool(chip, "pulse", pulse);
            image.gameObject.SetActive(false);
            return chip;
        }

        static HudBanner Ribbon(Kit kit, Transform parent, string name, string key, Vector2 pos, float width)
        {
            var banner = UiMenuViewsBuilder.Banner(kit, parent, name, key, pos, width);
            IsolateCanvas(banner.gameObject);
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            var widget = banner.gameObject.AddComponent<HudBanner>();
            UiFields.Set(widget, "theme", kit.Theme);
            UiFields.Set(widget, "banner", banner.rectTransform);
            UiFields.Set(widget, "group", group);
            UiFields.Set(widget, "label", banner.GetComponentInChildren<TextLabel>());
            banner.gameObject.SetActive(false);
            return widget;
        }

        /// <summary>
        /// Own canvas for a subtree that changes at runtime (bar tweens, counters, chips, banners), so its rebuilds
        /// leave the static HUD frames alone. Same shader channels as the root: TMP needs them on every canvas.
        /// </summary>
        internal static void IsolateCanvas(GameObject go)
        {
            go.AddComponent<Canvas>().additionalShaderChannels = (AdditionalCanvasShaderChannels)27;
        }

        /// <summary>Stacks active children top-down with the panel gutter (inactive ones collapse).</summary>
        static void Column(GameObject go, TextAnchor alignment)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = Gutter;
            layout.childAlignment = alignment;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        }

        #endregion
    }
}
