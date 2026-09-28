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
    /// GameplayHud prefab: left column below the 384x216 PiP reserve (top-left), right column at the top-right, ribbons at the
    /// top centre; the centre band (x 544..1376 at 1920 wide) stays free for the arena.
    /// </summary>
    public static class UiHudBuilder
    {
        const int QueueSlots = 12;
        const int QueueColumns = 5;
        const float SlotSize = 80f;
        const float SlotPitch = 88f;

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
            Kit.Place(left, Kit.TopLeft, new Vector2(32f, -256f), new Vector2(480f, 792f));
            BuildStagePanel(kit, left.transform, hud);
            BuildHpPanel(kit, left.transform, hud);
            BuildPlayerTags(kit, left.transform, hud);
            var warnings = kit.Ui("TrackingWarnings", left.transform);
            Kit.Place(warnings, Kit.TopLeft, new Vector2(0f, -480f), new Vector2(480f, 144f));
            Column(warnings);
            var warningChips = new List<Object>
            {
                Chip(kit, warnings.transform, "WarningP1", LocKeys.Hud.TrackingWarningPlayer, theme.Danger, new Vector2(480f, 64f), true),
                Chip(kit, warnings.transform, "WarningP2", LocKeys.Hud.TrackingWarningPlayer, theme.Danger, new Vector2(480f, 64f), true),
            };
            UiFields.SetArray(hud, "trackingWarnings", warningChips);

            var right = kit.Ui("RightColumn", root.transform);
            Kit.Place(right, Kit.TopRight, new Vector2(-32f, -32f), new Vector2(480f, 1016f));
            BuildBossBar(kit, right.transform, hud);
            BuildBallsPanel(kit, right.transform, hud);
            var chips = kit.Ui("Chips", right.transform);
            Kit.Place(chips, Kit.TopLeft, new Vector2(0f, -632f), new Vector2(480f, 224f));
            Column(chips);
            UiFields.Set(hud, "bonusBallsChip", Chip(kit, chips.transform, "BonusBalls", LocKeys.Hud.BonusBalls, theme.Accent, new Vector2(480f, 64f), false));
            UiFields.Set(hud, "powerChip", Chip(kit, chips.transform, "PowerReady", LocKeys.Hud.PowerArmed, theme.Accent, new Vector2(480f, 64f), true));
            UiFields.Set(hud, "fastForwardChip", Chip(kit, chips.transform, "FastForward", LocKeys.Hud.FastForward, theme.TextPrimary, new Vector2(480f, 64f), true));

            UiFields.Set(hud, "turnBanner", Ribbon(kit, root.transform, "TurnBanner", LocKeys.Hud.TurnBanner, new Vector2(0f, -40f), 704f));
            UiFields.Set(hud, "shooterBanner", Ribbon(kit, root.transform, "ShooterBanner", LocKeys.Hud.ShooterBanner, new Vector2(0f, -152f), 576f));
            return UiViewsBuilder.SaveRoot(root, path);
        }

        #region Left column

        static void BuildStagePanel(Kit kit, Transform parent, GameplayHud hud)
        {
            var theme = kit.Theme;
            var panel = kit.Image(parent, "StagePanel", theme.Panel, Kit.TopLeft, Vector2.zero, new Vector2(480f, 144f), Fill.Tiled).transform;
            UiFields.Set(hud, "stageLabel", kit.Label(panel, "Stage", LocKeys.Hud.Stage, 32, theme.TextPrimary, Kit.TopLeft, new Vector2(36f, -28f),
                new Vector2(320f, 48f), TextAlignmentOptions.Left));
            UiFields.Set(hud, "turnLabel", kit.Label(panel, "Turn", LocKeys.Hud.Turn, 32, theme.TextMuted, Kit.TopLeft, new Vector2(36f, -76f),
                new Vector2(320f, 48f), TextAlignmentOptions.Left));
            UiFields.Set(hud, "bossStageChip", Chip(kit, panel, "BossChip", LocKeys.Hud.Boss, theme.Danger, new Vector2(128f, 48f), true,
                Kit.TopRight, new Vector2(-24f, -28f)));
        }

        static void BuildHpPanel(Kit kit, Transform parent, GameplayHud hud)
        {
            var theme = kit.Theme;
            var panel = kit.Image(parent, "HpPanel", theme.Panel, Kit.TopLeft, new Vector2(0f, -160f), new Vector2(480f, 176f), Fill.Tiled);
            var shake = kit.Ui("Content", panel.transform);
            Kit.Stretch(shake);
            var s = shake.transform;
            var heart = kit.Image(s, "Heart", theme.Heart, Kit.TopLeft, new Vector2(32f, -32f), new Vector2(48f, 48f));
            kit.Label(s, "HpLabel", LocKeys.Hud.Hp, 32, theme.TextMuted, Kit.TopLeft, new Vector2(96f, -32f), new Vector2(96f, 48f), TextAlignmentOptions.Left);
            var numbers = kit.Label(s, "Numbers", null, 48, theme.TextPrimary, Kit.TopRight, new Vector2(-32f, -24f), new Vector2(240f, 64f),
                TextAlignmentOptions.Right, numbersPreview: "30/30");
            var (fill, ghost) = Bar(kit, s, theme.HpFill, new Vector2(32f, -100f), 416f);
            var widget = panel.gameObject.AddComponent<HpBarWidget>();
            UiFields.Set(widget, "theme", theme);
            UiFields.Set(widget, "fill", fill);
            UiFields.Set(widget, "ghostFill", ghost);
            UiFields.Set(widget, "numbers", numbers);
            UiFields.Set(widget, "shakeTarget", shake.transform);
            UiFields.Set(widget, "heart", heart.transform);
            UiFields.Set(hud, "hpBar", widget);
        }

        static void BuildPlayerTags(Kit kit, Transform parent, GameplayHud hud)
        {
            var theme = kit.Theme;
            var panel = kit.Image(parent, "PlayerTags", theme.Panel, Kit.TopLeft, new Vector2(0f, -352f), new Vector2(480f, 112f), Fill.Tiled);
            kit.Label(panel.transform, "Label", LocKeys.Hud.Shooter, 32, theme.TextMuted, Kit.Left, new Vector2(36f, 2f), new Vector2(160f, 48f), TextAlignmentOptions.Left);
            var tags = new List<Object>();
            var groups = new List<Object>();
            var tints = new List<Object>();
            var labels = new List<Object>();
            for (var i = 0; i < 2; i++)
            {
                var tag = kit.Image(panel.transform, $"TagP{i + 1}", theme.Chip, Kit.Center, new Vector2(56f + i * 136f, 0f), new Vector2(120f, 64f), Fill.Sliced);
                groups.Add(tag.gameObject.AddComponent<CanvasGroup>());
                var label = kit.Label(tag.transform, "Label", LocKeys.Hud.PlayerTag, 48, theme.PlayerColor(i), Kit.Center, new Vector2(0f, 3f), new Vector2(112f, 64f));
                tags.Add(tag.transform);
                tints.Add(label.Face);
                labels.Add(label);
            }

            var widget = panel.gameObject.AddComponent<PlayerTagsWidget>();
            UiFields.Set(widget, "theme", theme);
            UiFields.SetArray(widget, "tags", tags);
            UiFields.SetArray(widget, "tagGroups", groups);
            UiFields.SetArray(widget, "tagTints", tints);
            UiFields.SetArray(widget, "tagLabels", labels);
            UiFields.Set(hud, "playerTags", widget);
            panel.gameObject.SetActive(false);
        }

        #endregion

        #region Right column

        static void BuildBossBar(Kit kit, Transform parent, GameplayHud hud)
        {
            var theme = kit.Theme;
            var panel = kit.Image(parent, "BossBar", theme.Panel, Kit.TopLeft, Vector2.zero, new Vector2(480f, 176f), Fill.Tiled);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            var shake = kit.Ui("Content", panel.transform);
            Kit.Stretch(shake);
            var s = shake.transform;
            kit.Image(s, "Skull", theme.Skull, Kit.TopLeft, new Vector2(32f, -32f), new Vector2(48f, 48f));
            var name = kit.Label(s, "Name", LocKeys.Enemy.Names[8], 32, theme.Accent, Kit.TopLeft, new Vector2(96f, -32f), new Vector2(352f, 48f),
                TextAlignmentOptions.Left);
            var (fill, ghost) = Bar(kit, s, theme.BossFill, new Vector2(32f, -100f), 416f);
            var numbers = kit.Label(fill.transform.parent, "Numbers", null, 32, theme.TextPrimary, Kit.Center, new Vector2(0f, 2f), new Vector2(400f, 48f),
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

        static void BuildBallsPanel(Kit kit, Transform parent, GameplayHud hud)
        {
            var theme = kit.Theme;
            var panel = kit.Image(parent, "BallsPanel", theme.Panel, Kit.TopLeft, new Vector2(0f, -192f), new Vector2(480f, 424f), Fill.Tiled);
            var p = panel.transform;
            kit.Image(p, "Icon", theme.Ball, Kit.TopLeft, new Vector2(32f, -32f), new Vector2(48f, 48f));
            kit.Label(p, "Label", LocKeys.Hud.BallsLabel, 32, theme.TextMuted, Kit.TopLeft, new Vector2(96f, -32f), new Vector2(200f, 48f), TextAlignmentOptions.Left);
            UiFields.Set(hud, "ballsCounter", kit.Label(p, "Counter", null, 48, theme.TextPrimary, Kit.TopRight, new Vector2(-32f, -24f), new Vector2(200f, 64f),
                TextAlignmentOptions.Right, numbersPreview: "4/4"));

            var queue = kit.Ui("Queue", p);
            Kit.Place(queue, Kit.TopLeft, new Vector2(24f, -112f), new Vector2(432f, 256f));
            var frames = new List<Object>();
            var icons = new List<Object>();
            var levels = new List<Object>();
            var groups = new List<Object>();
            RectTransform? marker = null;
            for (var i = 0; i < QueueSlots; i++)
            {
                var pos = new Vector2(i % QueueColumns * SlotPitch, -(i / QueueColumns) * SlotPitch);
                var frame = kit.Image(queue.transform, $"Slot{i}", theme.Slot, Kit.TopLeft, pos, new Vector2(SlotSize, SlotSize), Fill.Sliced);
                groups.Add(frame.gameObject.AddComponent<CanvasGroup>());
                icons.Add(kit.Image(frame.transform, "Icon", theme.Ball, Kit.Center, Vector2.zero, new Vector2(64f, 64f)));
                levels.Add(kit.Label(frame.transform, "Level", null, 32, theme.Accent, new Vector2(1f, 0f), new Vector2(-2f, 0f), new Vector2(32f, 40f),
                    TextAlignmentOptions.Right, numbersPreview: "2"));
                frames.Add(frame);
                if (i != 0) continue;
                var cursor = kit.Image(frame.transform, "NextMarker", theme.Cursor, Kit.Top, new Vector2(0f, 36f), new Vector2(48f, 48f));
                cursor.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                marker = cursor.rectTransform;
            }

            var widget = panel.gameObject.AddComponent<BallQueueWidget>();
            UiFields.Set(widget, "theme", theme);
            UiFields.SetArray(widget, "frames", frames);
            UiFields.SetArray(widget, "icons", icons);
            UiFields.SetArray(widget, "levels", levels);
            UiFields.SetArray(widget, "groups", groups);
            UiFields.Set(widget, "nextMarker", marker);
            UiFields.Set(hud, "ballQueue", widget);
        }

        #endregion

        #region Pieces

        static (Image Fill, Image Ghost) Bar(Kit kit, Transform parent, Sprite? fillSprite, Vector2 pos, float width)
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
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            var widget = banner.gameObject.AddComponent<HudBanner>();
            UiFields.Set(widget, "theme", kit.Theme);
            UiFields.Set(widget, "banner", banner.rectTransform);
            UiFields.Set(widget, "group", group);
            UiFields.Set(widget, "label", banner.GetComponentInChildren<TextLabel>());
            banner.gameObject.SetActive(false);
            return widget;
        }

        static void Column(GameObject go)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        }

        #endregion
    }
}
