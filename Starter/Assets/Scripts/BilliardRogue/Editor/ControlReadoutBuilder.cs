#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds ControlReadoutOverlay.prefab (control lab readout): a Screen Space Overlay canvas above the PiP with two
    /// ControlReadoutPanels (P1, P2) the width of the right HUD column, anchored bottom right. Pixel font at 32 px,
    /// two labels per row (left / right aligned), anchored-rect bars with threshold ticks. Called by
    /// FlowPrefabsBuilder, which wires the prefab into the coordinator. Regenerated over its path (the GUID stays).
    /// </summary>
    public static class ControlReadoutBuilder
    {
        public const string PrefabPath = FlowUiFactory.FlowFolder + "/ControlReadoutOverlay.prefab";

        const int SortingOrder = 30;
        const int Players = 2;
        const float PanelWidth = UiHudBuilder.ColumnWidth;
        const float PanelHeight = 476f;
        const float Inset = 20f;
        const float RowHeight = 40f;
        const float LogPitch = 36f;
        const float BarHeight = 20f;
        const int FontSize = 32;

        static readonly Color panelColor = new(0.03f, 0.04f, 0.1f, 0.86f);
        static readonly Color barColor = new(0.16f, 0.18f, 0.3f, 1f);
        static readonly Color textColor = new(1f, 0.957f, 0.863f, 1f);
        static readonly Color mutedColor = new(0.72f, 0.77f, 0.9f, 1f);
        static readonly Color tickColor = new(1f, 1f, 1f, 0.95f);
        static readonly Color powerTickColor = new(1f, 0.45f, 0.95f, 1f);
        static readonly Color peakColor = new(1f, 1f, 1f, 0.55f);

        public static ControlReadoutOverlay Build()
        {
            var canvas = FlowUiFactory.CreateOverlayCanvasRoot("ControlReadoutOverlay", SortingOrder);
            var root = canvas.gameObject;
            var group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var overlay = root.AddComponent<ControlReadoutOverlay>();
            var panels = new List<Object>();
            for (var i = 0; i < Players; i++)
            {
                panels.Add(BuildPanel(root.transform, i));
            }

            UiFields.Set(overlay, "group", group);
            UiFields.SetArray(overlay, "panels", panels);
            return FlowUiFactory.SavePrefab(root, PrefabPath).GetComponent<ControlReadoutOverlay>();
        }

        #region Panel

        static ControlReadoutPanel BuildPanel(Transform parent, int playerIndex)
        {
            var go = FlowUiFactory.CreateUIObject($"PanelP{playerIndex + 1}", parent);
            FlowUiFactory.Place(go, new Vector2(1f, 0f), new Vector2(-UiHudBuilder.Margin, UiHudBuilder.Margin), new Vector2(PanelWidth, PanelHeight));
            var background = go.AddComponent<Image>();
            background.color = panelColor;
            background.raycastTarget = false;
            var t = go.transform;
            var panel = go.AddComponent<ControlReadoutPanel>();

            UiFields.Set(panel, "headerLabel", Left(t, "Header", -10f, $"P{playerIndex + 1} PAW", textColor));
            UiFields.Set(panel, "trackingLabel", Right(t, "Tracking", -10f, "NO TRACKING: CAMERA", mutedColor));
            UiFields.Set(panel, "launchLabel", Left(t, "Launch", -50f, "X 0.50", textColor));
            UiFields.Set(panel, "aimLabel", Label(t, "Aim", new Vector2(0.5f, 1f), new Vector2(0f, -50f), TextAlignmentOptions.Center, "AIM 90°", textColor));
            UiFields.Set(panel, "handLabel", Right(t, "Hand", -50f, "R-HAND", mutedColor));

            UiFields.Set(panel, "speedLabel", Left(t, "Speed", -94f, "SPEED 0 in/s", textColor));
            UiFields.Set(panel, "speedNeedLabel", Right(t, "SpeedNeed", -94f, "NEED 35", mutedColor));
            var speedBar = Bar(t, "SpeedBar", -138f, out var speedFill);
            UiFields.Set(panel, "speedFill", speedFill);
            UiFields.Set(panel, "speedPeak", Marker(speedBar, "Peak", 0f, 6f, 0f, peakColor));
            UiFields.Set(panel, "speedThresholdTick", Marker(speedBar, "Threshold", 0.4f, 4f, 6f, tickColor));
            UiFields.Set(panel, "speedPowerTick", Marker(speedBar, "Power", 0.8f, 4f, 6f, powerTickColor));

            UiFields.Set(panel, "distanceLabel", Left(t, "Distance", -166f, "DIST 0.0 in", textColor));
            UiFields.Set(panel, "distanceNeedLabel", Right(t, "DistanceNeed", -166f, "CONTACT 5.0", mutedColor));
            var distanceBar = Bar(t, "DistanceBar", -210f, out var distanceFill);
            UiFields.Set(panel, "distanceFill", distanceFill);
            UiFields.Set(panel, "distanceContactTick", Marker(distanceBar, "Contact", 0.25f, 4f, 6f, tickColor));
            UiFields.Set(panel, "distanceArmTick", Marker(distanceBar, "Arm", 0.5f, 4f, 6f, mutedColor));

            UiFields.Set(panel, "stateLabel", Left(t, "State", -238f, "OPEN PAWS", textColor));
            UiFields.Set(panel, "strikesLabel", Right(t, "Strikes", -238f, "STRIKES 0", textColor));
            UiFields.Set(panel, "lastLabel", Left(t, "Last", -278f, "LAST -", mutedColor));

            var logs = new List<Object>();
            for (var i = 0; i < 4; i++)
            {
                var label = Left(t, $"Log{i}", -322f - i * LogPitch, i == 0 ? "thrust right paw into left" : "", mutedColor);
                ((RectTransform)label.transform).sizeDelta = new Vector2(PanelWidth - 2f * Inset, LogPitch);
                logs.Add(label);
            }

            UiFields.SetArray(panel, "logLabels", logs);
            return panel;
        }

        #endregion

        #region Widgets

        static TextMeshProUGUI Left(Transform parent, string name, float y, string text, Color color) =>
            Label(parent, name, new Vector2(0f, 1f), new Vector2(Inset, y), TextAlignmentOptions.Left, text, color);

        static TextMeshProUGUI Right(Transform parent, string name, float y, string text, Color color) =>
            Label(parent, name, new Vector2(1f, 1f), new Vector2(-Inset, y), TextAlignmentOptions.Right, text, color);

        static TextMeshProUGUI Label(Transform parent, string name, Vector2 anchor, Vector2 position, TextAlignmentOptions alignment, string text, Color color)
        {
            var label = FlowUiFactory.CreateLabel(name, parent, FontSize, alignment, null, text);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = color;
            FlowUiFactory.Place(label.gameObject, anchor, position, new Vector2(PanelWidth - 2f * Inset, RowHeight));
            return label;
        }

        /// <summary>Bar background with a left-anchored fill (the panel sets its anchorMax.x).</summary>
        static RectTransform Bar(Transform parent, string name, float y, out Image fill)
        {
            var bar = FlowUiFactory.CreateUIObject(name, parent);
            var rt = FlowUiFactory.Place(bar, new Vector2(0f, 1f), new Vector2(Inset, y), new Vector2(PanelWidth - 2f * Inset, BarHeight));
            var background = bar.AddComponent<Image>();
            background.color = barColor;
            background.raycastTarget = false;

            var fillGo = FlowUiFactory.CreateUIObject("Fill", bar.transform);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fill = fillGo.AddComponent<Image>();
            fill.color = mutedColor;
            fill.raycastTarget = false;
            return rt;
        }

        /// <summary>Vertical marker at x01 of the bar; overhang extends it above and below the bar.</summary>
        static RectTransform Marker(RectTransform bar, string name, float x01, float width, float overhang, Color color)
        {
            var go = FlowUiFactory.CreateUIObject(name, bar);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(x01, 0f);
            rt.anchorMax = new Vector2(x01, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(-width * 0.5f, -overhang);
            rt.offsetMax = new Vector2(width * 0.5f, overhang);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rt;
        }

        #endregion
    }
}
