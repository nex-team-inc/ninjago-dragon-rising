#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Fill = Nex.BilliardRogue.Editor.UiPrefabKit.Fill;
using Kit = Nex.BilliardRogue.Editor.UiPrefabKit;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>Overlay/result view prefabs: StageIntro, Reward, TrackingLost, Summary (1920x1080 reference units).</summary>
    public static class UiOverlayViewsBuilder
    {
        #region Stage intro

        public static string BuildStageIntro(Kit kit, string path)
        {
            var theme = kit.Theme;
            var root = UiViewsBuilder.OpenRoot(kit, path, "StageIntroView");
            var view = UiViewsBuilder.ViewRoot<StageIntroView>(kit, root, out var content);
            var c = content.transform;
            var bandGo = kit.Ui("Band", c);
            var band = (RectTransform)bandGo.transform;
            band.anchorMin = new Vector2(0f, 0.5f);
            band.anchorMax = new Vector2(1f, 0.5f);
            band.anchoredPosition = new Vector2(0f, 40f);
            band.sizeDelta = new Vector2(64f, 336f);
            var bandImage = kit.Configure(bandGo.AddComponent<Image>(), null, Fill.Simple, theme.StageBandColor);
            bandImage.preserveAspect = false;
            Line(kit, band, "LineTop", 1f);
            Line(kit, band, "LineBottom", 0f);

            var text = kit.Ui("Text", c);
            Kit.Place(text, Kit.Center, new Vector2(0f, 40f), new Vector2(1600f, 336f));
            var header = kit.Label(text.transform, "Header", LocKeys.StageIntro.Header, 96, theme.TextPrimary, Kit.Center,
                new Vector2(0f, 56f), new Vector2(1200f, 128f));
            var ribbon = UiMenuViewsBuilder.Banner(kit, text.transform, "Subtitle", LocKeys.Act.Names[0], new Vector2(0f, -88f), 768f, Kit.Center);
            var subtitle = ribbon.GetComponentInChildren<TextLabel>();

            var normal = kit.Ui("NormalDecor", text.transform);
            Kit.Stretch(normal);
            kit.Image(normal.transform, "BallLeft", theme.Ball, Kit.Center, new Vector2(-512f, 56f), new Vector2(96f, 96f));
            kit.Image(normal.transform, "BallRight", theme.Ball, Kit.Center, new Vector2(512f, 56f), new Vector2(96f, 96f));
            var boss = kit.Ui("BossDecor", text.transform);
            Kit.Stretch(boss);
            kit.Image(boss.transform, "SkullLeft", theme.Skull, Kit.Center, new Vector2(-512f, 56f), new Vector2(96f, 96f));
            kit.Image(boss.transform, "SkullRight", theme.Skull, Kit.Center, new Vector2(512f, 56f), new Vector2(96f, 96f));
            var bossIcon = kit.Image(boss.transform, "BossIcon", null, Kit.Center, new Vector2(0f, 232f), new Vector2(144f, 144f));
            boss.SetActive(false);

            Kit.Set(view, "band", band);
            Kit.Set(view, "bandImage", bandImage);
            Kit.Set(view, "textGroup", text.transform);
            Kit.Set(view, "headerLabel", header);
            Kit.Set(view, "subtitleLabel", subtitle);
            Kit.Set(view, "bossDecor", boss);
            Kit.Set(view, "bossIcon", bossIcon);
            Kit.Set(view, "normalDecor", normal);
            Kit.Set(view, "keyResponder", null);
            Kit.Set(view, "popTarget", null);
            return UiViewsBuilder.SaveRoot(root, path);
        }

        static void Line(Kit kit, RectTransform band, string name, float anchorY)
        {
            var go = kit.Ui(name, band);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, anchorY);
            rect.anchorMax = new Vector2(1f, anchorY);
            rect.pivot = new Vector2(0.5f, anchorY);
            rect.sizeDelta = new Vector2(0f, 6f);
            kit.Configure(go.AddComponent<Image>(), null, Fill.Simple, kit.Theme.Accent).preserveAspect = false;
        }

        #endregion

        #region Reward

        public static string BuildReward(Kit kit, string path)
        {
            var theme = kit.Theme;
            var root = UiViewsBuilder.OpenRoot(kit, path, "RewardView");
            var view = UiViewsBuilder.ViewRoot<RewardView>(kit, root, out var content);
            var c = content.transform;
            kit.DimLayers(c);
            UiMenuViewsBuilder.Banner(kit, c, "Header", LocKeys.Reward.Header, new Vector2(0f, -64f), 832f);
            var cardsGo = kit.Ui("Cards", c);
            Kit.Place(cardsGo, Kit.Center, new Vector2(0f, -24f), new Vector2(1424f, 720f));
            var layout = cardsGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 64f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var group = kit.Group(cardsGo, false, 1);
            var cards = new List<Object>();
            for (var i = 0; i < 3; i++)
            {
                cards.Add(RewardCard(kit, cardsGo.transform, i));
            }

            kit.Image(c, "ArrowLeft", theme.ArrowLeft, Kit.Center, new Vector2(-800f, -24f), new Vector2(96f, 96f));
            kit.Image(c, "ArrowRight", theme.ArrowRight, Kit.Center, new Vector2(800f, -24f), new Vector2(96f, 96f));
            kit.Label(c, "Hint", LocKeys.Reward.ChooseHint, 32, theme.TextMuted, Kit.Bottom, new Vector2(0f, 40f), new Vector2(1200f, 48f));

            Kit.SetArray(view, "cards", cards);
            Kit.Set(view, "cardsGroup", group);
            Kit.Set(view, "keyResponder", group);
            Kit.Set(view, "popTarget", cardsGo.transform);
            return UiViewsBuilder.SaveRoot(root, path);
        }

        static RewardCard RewardCard(Kit kit, Transform parent, int index)
        {
            var theme = kit.Theme;
            var size = new Vector2(432f, 720f);
            var go = kit.Ui($"Card{index}", parent);
            Kit.Place(go, Kit.Center, Vector2.zero, size);
            var bodyGo = kit.Ui("Body", go.transform);
            Kit.Stretch(bodyGo);
            var bodyGroup = bodyGo.AddComponent<CanvasGroup>();
            var body = bodyGo.transform;
            var glow = kit.StretchImage(body, "Glow", theme.ButtonFocused, -18f, Fill.Sliced);
            glow.gameObject.SetActive(false);
            var frame = kit.StretchImage(body, "Frame", theme.Card, 0f, Fill.Tiled);
            frame.raycastTarget = true;
            var f = frame.transform;

            var kindChip = kit.Image(f, "KindChip", theme.Chip, Kit.Top, new Vector2(0f, -48f), new Vector2(288f, 48f), Fill.Sliced);
            var kind = kit.Label(kindChip.transform, "Label", LocKeys.Reward.KindNewBall, 32, theme.TextPrimary, Kit.Center, new Vector2(0f, 2f), new Vector2(272f, 48f));
            var slot = kit.Image(f, "IconSlot", theme.Slot, Kit.Center, new Vector2(0f, 150f), new Vector2(216f, 216f), Fill.Sliced);
            var icon = kit.Image(slot.transform, "Icon", theme.Ball, Kit.Center, Vector2.zero, new Vector2(192f, 192f));
            var name = kit.Label(f, "Name", LocKeys.Ball.Names[0], 48, theme.TextDark, Kit.Center, new Vector2(0f, 8f), new Vector2(400f, 64f), shadow: false);
            var level = kit.Label(f, "Level", LocKeys.Reward.Level, 32, theme.TextDark, Kit.Center, new Vector2(0f, -40f), new Vector2(400f, 48f), shadow: false);
            var starsRow = kit.Ui("Stars", f);
            Kit.Place(starsRow, Kit.Center, new Vector2(0f, -88f), new Vector2(192f, 48f));
            var stars = new List<Object>();
            for (var i = 0; i < SimConstants.MaxBallLevel; i++)
            {
                stars.Add(kit.Label(starsRow.transform, $"Star{i}", null, 48, i == 0 ? theme.Accent : theme.Disabled, Kit.Center,
                    new Vector2(-56f + 56f * i, 0f), new Vector2(48f, 48f), numbersPreview: "★"));
            }

            var description = kit.Label(f, "Description", LocKeys.Ball.Description(BallType.Basic, 1), 32, theme.TextDark, Kit.Center,
                new Vector2(0f, -196f), new Vector2(368f, 144f), TextAlignmentOptions.Top, wrap: true, shadow: false);
            var rarityChip = kit.Image(f, "RarityChip", theme.Chip, Kit.Bottom, new Vector2(0f, 48f), new Vector2(224f, 48f), Fill.Sliced);
            var rarity = kit.Label(rarityChip.transform, "Label", LocKeys.Reward.RarityCommon, 32, theme.RarityColor(BallRarity.Common),
                Kit.Center, new Vector2(0f, 2f), new Vector2(208f, 48f));

            var button = kit.PlainButton(go, frame);
            var cursor = kit.Cursor(body, size.x + 24f);
            var highlight = kit.Highlight(go, go.transform, null, null, glow.gameObject, cursor);
            var responder = kit.Responder(go, button, highlight);
            var card = go.AddComponent<RewardCard>();
            Kit.Set(card, "button", button);
            Kit.Set(card, "responder", responder);
            Kit.Set(card, "body", body);
            Kit.Set(card, "bodyGroup", bodyGroup);
            Kit.Set(card, "kindLabel", kind);
            Kit.Set(card, "icon", icon);
            Kit.Set(card, "nameLabel", name);
            Kit.Set(card, "levelLabel", level);
            Kit.SetArray(card, "stars", stars);
            Kit.Set(card, "starsRow", starsRow);
            Kit.Set(card, "descriptionLabel", description);
            Kit.Set(card, "rarityChip", rarityChip.gameObject);
            Kit.Set(card, "rarityLabel", rarity);
            return card;
        }

        #endregion

        #region Tracking lost

        public static string BuildTrackingLost(Kit kit, string path)
        {
            var theme = kit.Theme;
            var root = UiViewsBuilder.OpenRoot(kit, path, "TrackingLostView");
            var view = UiViewsBuilder.ViewRoot<TrackingLostView>(kit, root, out var content);
            var c = content.transform;
            kit.DimLayers(c);
            var panel = kit.Image(c, "Panel", theme.Panel, Kit.Center, Vector2.zero, new Vector2(1280f, 576f), Fill.Tiled);
            var p = panel.transform;
            kit.Label(p, "Header", LocKeys.TrackingLost.Header, 96, theme.Accent, Kit.Center, new Vector2(0f, 176f), new Vector2(1200f, 128f));
            var portrait = kit.Image(p, "Portrait", theme.PortraitP1, Kit.Center, new Vector2(-424f, -40f), new Vector2(256f, 256f));
            var chip = kit.Image(p, "PlayerChip", theme.Chip, Kit.Center, new Vector2(-424f, -212f), new Vector2(160f, 64f), Fill.Sliced);
            var chipLabel = kit.Label(chip.transform, "Label", LocKeys.Hud.PlayerTag, 48, theme.PlayerColor(0), Kit.Center, new Vector2(0f, 3f), new Vector2(144f, 64f));
            var waiting = kit.Ui("Waiting", p);
            Kit.Place(waiting, Kit.Center, new Vector2(152f, -72f), new Vector2(832f, 256f));
            var body = kit.Label(waiting.transform, "Body", LocKeys.TrackingLost.Body, 48, theme.TextPrimary, Kit.Center, new Vector2(0f, 48f), new Vector2(832f, 64f));
            kit.Label(waiting.transform, "Hint", LocKeys.TrackingLost.Hint, 32, theme.TextMuted, Kit.Center, new Vector2(0f, -56f),
                new Vector2(736f, 112f), TextAlignmentOptions.Top, wrap: true);
            var resuming = kit.Ui("Resuming", p);
            Kit.Place(resuming, Kit.Center, new Vector2(152f, -72f), new Vector2(832f, 256f));
            kit.Label(resuming.transform, "Label", LocKeys.TrackingLost.Resuming, 48, theme.Positive, Kit.Center, new Vector2(0f, 48f), new Vector2(832f, 64f));
            resuming.SetActive(false);

            Kit.Set(view, "bodyLabel", body);
            Kit.Set(view, "playerChip", chip.gameObject);
            Kit.Set(view, "playerChipLabel", chipLabel);
            Kit.Set(view, "portrait", portrait);
            Kit.Set(view, "waitingGroup", waiting);
            Kit.Set(view, "resumingGroup", resuming);
            Kit.Set(view, "keyResponder", null);
            Kit.Set(view, "popTarget", panel.transform);
            return UiViewsBuilder.SaveRoot(root, path);
        }

        #endregion

        #region Summary

        static readonly string[] statKeys =
        {
            LocKeys.Summary.Turns, LocKeys.Summary.Shots, LocKeys.Summary.Hits, LocKeys.Summary.Kills, LocKeys.Summary.DamageDealt,
            LocKeys.Summary.DamageTaken, LocKeys.Summary.BestCombo, LocKeys.Summary.Bosses, LocKeys.Summary.Time,
        };

        public static string BuildSummary(Kit kit, string path)
        {
            var theme = kit.Theme;
            var root = UiViewsBuilder.OpenRoot(kit, path, "SummaryView");
            var view = UiViewsBuilder.ViewRoot<SummaryView>(kit, root, out var content);
            var c = content.transform;
            kit.DimLayers(c);
            var title = kit.Label(c, "Title", LocKeys.Summary.Victory, 96, theme.Accent, Kit.Center, new Vector2(0f, 424f), new Vector2(1200f, 128f));
            var stage = kit.Label(c, "Stage", LocKeys.Summary.StageReached, 48, theme.TextPrimary, Kit.Center, new Vector2(0f, 336f), new Vector2(1400f, 64f));
            var record = kit.Image(c, "NewRecord", theme.Chip, Kit.Center, new Vector2(560f, 448f), new Vector2(352f, 64f), Fill.Sliced);
            record.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            kit.Label(record.transform, "Label", LocKeys.Summary.NewRecord, 32, theme.Accent, Kit.Center, new Vector2(0f, 2f), new Vector2(336f, 48f));
            record.gameObject.SetActive(false);

            var panel = kit.Image(c, "StatsPanel", theme.Panel, Kit.Center, new Vector2(0f, 56f), new Vector2(1152f, 464f), Fill.Tiled);
            var p = panel.transform;
            kit.Label(p, "Header", LocKeys.Summary.StatsHeader, 32, theme.Accent, Kit.Center, new Vector2(0f, 168f), new Vector2(1000f, 48f));
            var values = new List<Object>();
            for (var i = 0; i < statKeys.Length; i++)
            {
                var columnX = i < 5 ? -276f : 276f;
                var y = 104f - (i % 5) * 68f;
                kit.Label(p, $"Stat{i}Label", statKeys[i], 32, theme.TextMuted, Kit.Center, new Vector2(columnX - 60f, y), new Vector2(384f, 48f),
                    TextAlignmentOptions.Left);
                values.Add(kit.Label(p, $"Stat{i}Value", null, 48, theme.TextPrimary, Kit.Center, new Vector2(columnX + 150f, y + 2f),
                    new Vector2(192f, 64f), TextAlignmentOptions.Right));
            }

            var unlocks = kit.Ui("Unlocks", c);
            Kit.Place(unlocks, Kit.Center, new Vector2(0f, -256f), new Vector2(1400f, 160f));
            kit.Label(unlocks.transform, "Header", LocKeys.Summary.Unlocked, 32, theme.Accent, Kit.Center, new Vector2(0f, 48f), new Vector2(1000f, 48f));
            var row = kit.Ui("Slots", unlocks.transform);
            Kit.Place(row, Kit.Center, new Vector2(0f, -24f), new Vector2(1400f, 96f));
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 32f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
            var slots = new List<Object>();
            var icons = new List<Object>();
            var names = new List<Object>();
            for (var i = 0; i < 4; i++)
            {
                var slot = kit.Ui($"Unlock{i}", row.transform);
                Kit.Place(slot, Kit.Center, Vector2.zero, new Vector2(320f, 96f));
                icons.Add(kit.Image(slot.transform, "Icon", theme.Ball, Kit.Left, Vector2.zero, new Vector2(96f, 96f)));
                names.Add(kit.Label(slot.transform, "Name", LocKeys.Ball.Names[5 + i], 32, theme.TextPrimary, Kit.Left, new Vector2(112f, 2f),
                    new Vector2(208f, 48f), TextAlignmentOptions.Left));
                slots.Add(slot);
            }

            unlocks.SetActive(false);

            var buttons = kit.Ui("Buttons", c);
            Kit.Place(buttons, Kit.Center, new Vector2(0f, -440f), new Vector2(1024f, 96f));
            var group = kit.Group(buttons, false);
            var again = kit.MenuButton(buttons.transform, "PlayAgainButton", LocKeys.Summary.PlayAgain, new Vector2(480f, 96f));
            var toTitle = kit.MenuButton(buttons.transform, "TitleButton", LocKeys.Summary.ToTitle, new Vector2(480f, 96f));
            ((RectTransform)again.transform).anchoredPosition = new Vector2(-272f, 0f);
            ((RectTransform)toTitle.transform).anchoredPosition = new Vector2(272f, 0f);

            Kit.Set(view, "titleLabel", title);
            Kit.Set(view, "stageLabel", stage);
            Kit.Set(view, "newRecordChip", record.transform);
            Kit.SetArray(view, "statValues", values);
            Kit.Set(view, "unlockGroup", unlocks);
            Kit.SetArray(view, "unlockSlots", slots);
            Kit.SetArray(view, "unlockIcons", icons);
            Kit.SetArray(view, "unlockNames", names);
            Kit.Set(view, "playAgainButton", again);
            Kit.Set(view, "titleButton", toTitle);
            Kit.Set(view, "keyResponder", group);
            Kit.Set(view, "popTarget", panel.transform);
            return UiViewsBuilder.SaveRoot(root, path);
        }

        #endregion
    }
}
