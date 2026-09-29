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
    /// GameplayHud pieces for POWER (GDD v2 §3, §17): the meter panel at the bottom of the left column (title, dance
    /// energy on the right, bar with tier ticks at 0.25 / 0.55 / 0.85, tier callout) and the MOVE! prompt (dancing cat + label) above the arena bottom,
    /// clear of the arena centre and the launch line. 1920x1080 units.
    /// </summary>
    public static class UiHypeBuilder
    {
        const float Inset = 32f;
        const float MeterHeight = 208f;
        const float BarWidth = UiHudBuilder.ColumnWidth - 2f * Inset;
        static readonly float[] tierThresholds = { 0.25f, 0.55f, 0.85f };

        /// <summary>Screen position of the MOVE! prompt centre from the bottom centre (lower board rows, above the cat).</summary>
        public static readonly Vector2 MovePromptPosition = new(0f, 300f);
        public static readonly Vector2 MovePromptSize = new(880f, 208f);

        public static void BuildMeter(Kit kit, Transform leftColumn, GameplayHud hud, float columnHeight)
        {
            var theme = kit.Theme;
            var panel = kit.Image(leftColumn, "HypeMeter", theme.Panel, Kit.TopLeft, new Vector2(0f, -(columnHeight - MeterHeight)),
                new Vector2(UiHudBuilder.ColumnWidth, MeterHeight), Fill.Tiled);
            UiHudBuilder.IsolateCanvas(panel.gameObject);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            var p = panel.transform;
            kit.Image(p, "Icon", theme.Strike, Kit.TopLeft, new Vector2(Inset - 4f, -20f), new Vector2(48f, 48f));
            kit.Label(p, "Title", LocKeys.Hud.Hype, 32, theme.TextMuted, Kit.TopLeft, new Vector2(Inset + 52f, -20f), new Vector2(160f, 48f),
                TextAlignmentOptions.Left);
            var energyLabel = kit.Label(p, "Energy", LocKeys.Hud.Energy, 32, theme.Positive, Kit.TopRight, new Vector2(-Inset, -20f),
                new Vector2(BarWidth - 168f, 48f), TextAlignmentOptions.Right);
            var (fill, ghost) = UiHudBuilder.Bar(kit, p, theme.HypeFill, new Vector2(Inset, -72f), BarWidth);
            Object.DestroyImmediate(ghost.gameObject);
            fill.fillAmount = 0f;
            var ticks = new List<Object>();
            var bar = (RectTransform)fill.transform.parent;
            foreach (var threshold in tierThresholds)
            {
                var tick = kit.Image(bar, $"Tick{ticks.Count + 1}", null, new Vector2(threshold, 0.5f), Vector2.zero, new Vector2(6f, 42f),
                    color: theme.Disabled);
                tick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                tick.preserveAspect = false;
                ticks.Add(tick);
            }

            var callout = kit.Ui("Callout", p);
            Kit.Place(callout, Kit.Top, new Vector2(0f, -150f), new Vector2(BarWidth, 64f), Kit.Center);
            var calloutLabel = kit.Label(callout.transform, "Label", LocKeys.Hud.HypeTier1, 48, theme.Accent, Kit.Center, Vector2.zero,
                new Vector2(BarWidth, 64f), bold: true);

            var widget = panel.gameObject.AddComponent<HypeMeterWidget>();
            UiFields.Set(widget, "theme", theme);
            UiFields.Set(widget, "group", group);
            UiFields.Set(widget, "fill", fill);
            UiFields.SetArray(widget, "ticks", ticks);
            UiFields.Set(widget, "callout", callout.transform);
            UiFields.Set(widget, "calloutLabel", calloutLabel);
            UiFields.Set(widget, "energyLabel", energyLabel);
            UiFields.Set(hud, "hypeMeter", widget);
        }

        public static void BuildMovePrompt(Kit kit, Transform root, GameplayHud hud)
        {
            var theme = kit.Theme;
            var go = kit.Ui("MovePrompt", root);
            Kit.Place(go, Kit.Bottom, MovePromptPosition, MovePromptSize, Kit.Center);
            UiHudBuilder.IsolateCanvas(go);
            var group = go.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            var content = kit.Ui("Content", go.transform);
            Kit.Stretch(content);
            var c = content.transform;
            // Soft navy strip so the label reads over the board without hiding it.
            var strip = kit.StretchImage(c, "Strip", theme.GlowDisc, 0f, Fill.Simple, new Color(0.03f, 0.04f, 0.1f, 0.55f));
            strip.preserveAspect = false;

            var dancer = kit.Ui("Dancer", c);
            Kit.Place(dancer, Kit.Center, new Vector2(-240f, 0f), new Vector2(320f, 208f), Kit.Center);
            var pawLeft = kit.Image(dancer.transform, "PawLeft", theme.PawOpen(0), Kit.Center, new Vector2(-116f, -20f), new Vector2(72f, 80f));
            var pawRight = kit.Image(dancer.transform, "PawRight", theme.PawOpen(0), Kit.Center, new Vector2(116f, -20f), new Vector2(72f, 80f));
            var cat = kit.Image(dancer.transform, "Cat", theme.PortraitP1, Kit.Center, new Vector2(0f, 8f), new Vector2(128f, 128f));
            cat.rectTransform.pivot = new Vector2(0.5f, 0.1f);

            var label = kit.Ui("Label", c);
            Kit.Place(label, Kit.Center, new Vector2(150f, 24f), new Vector2(480f, 112f), Kit.Center);
            kit.Label(label.transform, "Text", LocKeys.Hud.MovePrompt, 96, theme.Accent, Kit.Center, Vector2.zero, new Vector2(480f, 112f), bold: true);
            kit.Label(c, "Hint", LocKeys.Hud.MoveHint, 32, theme.TextPrimary, Kit.Center, new Vector2(150f, -60f), new Vector2(560f, 48f));

            var widget = go.AddComponent<MovePromptWidget>();
            UiFields.Set(widget, "theme", theme);
            UiFields.Set(widget, "group", group);
            UiFields.Set(widget, "content", content);
            UiFields.Set(widget, "cat", cat.rectTransform);
            UiFields.Set(widget, "catImage", cat);
            UiFields.Set(widget, "pawLeft", pawLeft.rectTransform);
            UiFields.Set(widget, "pawRight", pawRight.rectTransform);
            UiFields.Set(widget, "pawLeftImage", pawLeft);
            UiFields.Set(widget, "pawRightImage", pawRight);
            UiFields.Set(widget, "label", label.transform);
            UiFields.Set(hud, "movePrompt", widget);
            content.SetActive(false);
        }
    }
}
