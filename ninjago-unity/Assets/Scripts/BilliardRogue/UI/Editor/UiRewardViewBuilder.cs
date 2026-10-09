#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;
using UnityEngine;
using UnityEngine.UI;
using Fill = Nex.BilliardRogue.Editor.UiPrefabKit.Fill;
using Kit = Nex.BilliardRogue.Editor.UiPrefabKit;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// RewardView prefab, the motion pick (GDD v2 §4), 1920x1080 units: header ribbon, "P2 picks!" banner (2P), three
    /// big balls floating across the upper middle (ball at 8x, name + short effect above, New / level chip below, hold
    /// ring and glow behind), two cat arms from shoulders below the bottom corners, hints at the bottom centre.
    /// </summary>
    public static class UiRewardViewBuilder
    {
        const float BallSize = 256f;          // 32 px icon at 8x
        const float BallsY = -40f;            // ball centres, from the screen centre
        const float BallSpacing = 520f;
        static readonly Vector2 optionSize = new(480f, 600f);
        /// <summary>Shoulders sit below the bottom edge, inset from the corners (arms layer space: origin at the centre).</summary>
        static readonly Vector2 shoulderLeft = new(-760f, -640f);
        static readonly Vector2 shoulderRight = new(760f, -640f);
        const float ArmScale = 4f;            // sleeve 24 px and paw 36x40 px at 4x

        public static string Build(Kit kit, string path)
        {
            var theme = kit.Theme;
            var root = UiViewsBuilder.OpenRoot(kit, path, "RewardView");
            var view = UiViewsBuilder.ViewRoot<RewardView>(kit, root, out var content);
            var c = content.transform;
            kit.DimLayers(c);
            UiMenuViewsBuilder.Banner(kit, c, "Header", LocKeys.Reward.PickHeader, new Vector2(0f, -64f), 832f);
            var chooser = kit.Image(c, "ChooserBanner", theme.Chip, Kit.Top, new Vector2(0f, -176f), new Vector2(384f, 64f), Fill.Sliced);
            var chooserLabel = kit.Label(chooser.transform, "Label", LocKeys.Reward.ChooserBanner, 48, theme.PlayerColor(1), Kit.Center,
                new Vector2(0f, 3f), new Vector2(368f, 64f));
            chooser.gameObject.SetActive(false);

            var optionsGo = kit.Ui("Balls", c);
            Kit.Stretch(optionsGo);
            var group = kit.Group(optionsGo, false, 1);
            var options = new List<Object>();
            for (var i = 0; i < 3; i++)
            {
                options.Add(Option(kit, optionsGo.transform, i, new Vector2((i - 1) * BallSpacing, BallsY)));
            }

            var armsGo = kit.Ui("Arms", c);
            Kit.Stretch(armsGo);
            var arms = new List<Object>
            {
                Arm(kit, armsGo.transform, "ArmLeft", shoulderLeft, new Vector2(0f, 260f)),
                Arm(kit, armsGo.transform, "ArmRight", shoulderRight, new Vector2(0f, 260f)),
            };

            kit.Label(c, "PawHint", LocKeys.Reward.PawHint, 48, theme.TextPrimary, Kit.Bottom, new Vector2(0f, 96f), new Vector2(1000f, 64f));
            kit.Label(c, "Hint", LocKeys.Reward.ChooseHint, 32, theme.TextMuted, Kit.Bottom, new Vector2(0f, 40f), new Vector2(1000f, 48f));

            UiFields.SetArray(view, "options", options);
            UiFields.Set(view, "optionsGroup", group);
            UiFields.SetArray(view, "arms", arms);
            UiFields.Set(view, "armsLayer", armsGo.transform);
            UiFields.Set(view, "chooserBanner", chooser.gameObject);
            UiFields.Set(view, "chooserLabel", chooserLabel);
            UiFields.Set(view, "keyResponder", group);
            UiFields.Set(view, "popTarget", optionsGo.transform);
            return UiViewsBuilder.SaveRoot(root, path);
        }

        /// <summary>Option root (button + focus) → Body (reveal/pick) → Float (bob) → Pulse (focus) → Visual (hover) + labels.</summary>
        static RewardBallOption Option(Kit kit, Transform parent, int index, Vector2 pos)
        {
            var theme = kit.Theme;
            var go = kit.Ui($"Ball{index}", parent);
            Kit.Place(go, Kit.Center, pos, optionSize, Kit.Center);
            // Transparent hit area: mouse clicks in the Editor count as a remote pick.
            var hit = kit.Configure(go.AddComponent<Image>(), null, Fill.Simple, new Color(1f, 1f, 1f, 0f));
            hit.raycastTarget = true;
            var bodyGo = kit.Ui("Body", go.transform);
            Kit.Stretch(bodyGo);
            var bodyGroup = bodyGo.AddComponent<CanvasGroup>();
            var body = bodyGo.transform;
            kit.Image(body, "Shadow", theme.ShadowBall, Kit.Center, new Vector2(0f, -BallSize * 0.5f - 28f), new Vector2(160f, 48f),
                color: new Color(0f, 0f, 0f, 0.55f)).preserveAspect = false;
            var floater = kit.Ui("Float", body);
            Kit.Stretch(floater);
            var pulse = kit.Ui("Pulse", floater.transform);
            Kit.Stretch(pulse);
            var visual = kit.Ui("Visual", pulse.transform);
            Kit.Place(visual, Kit.Center, Vector2.zero, new Vector2(BallSize, BallSize), Kit.Center);
            var v = visual.transform;
            var glow = kit.Image(v, "Glow", theme.GlowDisc, Kit.Center, Vector2.zero, new Vector2(384f, 384f));
            var track = kit.Image(v, "HoldTrack", theme.RingHold, Kit.Center, Vector2.zero, new Vector2(296f, 296f), color: new Color(0.1f, 0.12f, 0.24f, 0.7f));
            var fill = kit.Image(v, "HoldFill", theme.RingHold, Kit.Center, Vector2.zero, new Vector2(296f, 296f), color: theme.Accent);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 0f;
            fill.enabled = false;
            track.enabled = false;
            var focusRing = kit.Image(v, "FocusRing", theme.RingHold, Kit.Center, Vector2.zero, new Vector2(280f, 280f), color: new Color(1f, 1f, 1f, 0.9f));
            focusRing.gameObject.SetActive(false);
            var ball = kit.Image(v, "Ball", theme.Ball, Kit.Center, Vector2.zero, new Vector2(BallSize, BallSize));

            var f = floater.transform;
            var name = kit.Label(f, "Name", LocKeys.Ball.Names[0], 64, theme.TextPrimary, Kit.Center, new Vector2(0f, 256f), new Vector2(480f, 80f), bold: true);
            var shortLabel = kit.Label(f, "Short", LocKeys.Ball.Short(BallType.Basic), 48, theme.Accent, Kit.Center, new Vector2(0f, 196f), new Vector2(480f, 64f));
            var chip = kit.Image(f, "Chip", theme.Chip, Kit.Center, new Vector2(0f, -BallSize * 0.5f - 88f), new Vector2(320f, 64f), Fill.Sliced);
            var chipLabel = kit.Label(chip.transform, "Label", LocKeys.Reward.KindNewBall, 32, theme.RarityColor(BallRarity.Common), Kit.Center,
                new Vector2(0f, 2f), new Vector2(304f, 48f));

            var button = kit.PlainButton(go, hit);
            var highlight = kit.Highlight(go, pulse.transform, null, null, focusRing.gameObject, null);
            var responder = kit.Responder(go, button, highlight);
            var option = go.AddComponent<RewardBallOption>();
            UiFields.Set(option, "button", button);
            UiFields.Set(option, "responder", responder);
            UiFields.Set(option, "body", body);
            UiFields.Set(option, "bodyGroup", bodyGroup);
            UiFields.Set(option, "floater", floater.transform);
            UiFields.Set(option, "visual", visual.transform);
            UiFields.Set(option, "ball", ball);
            UiFields.Set(option, "glow", glow);
            UiFields.Set(option, "holdTrack", track);
            UiFields.Set(option, "holdFill", fill);
            UiFields.SetFloat(option, "radius", BallSize * 0.5f);
            UiFields.Set(option, "nameLabel", name);
            UiFields.Set(option, "shortLabel", shortLabel);
            UiFields.Set(option, "chip", chip.gameObject);
            UiFields.Set(option, "chipLabel", chipLabel);
            return option;
        }

        /// <summary>Shoulder (pivot, rotated) → tiled fur sleeve (bottom pivot) + paw (palm-centre pivot) at the sleeve end.</summary>
        static PawArm Arm(Kit kit, Transform parent, string name, Vector2 shoulder, Vector2 restOffset)
        {
            var theme = kit.Theme;
            var go = kit.Ui(name, parent);
            Kit.Place(go, Kit.Center, shoulder, new Vector2(24f * ArmScale, 24f * ArmScale), Kit.Center);
            var sleeve = kit.Image(go.transform, "Sleeve", theme.Arm(0), Kit.Center, Vector2.zero, new Vector2(24f * ArmScale, 400f));
            sleeve.rectTransform.pivot = new Vector2(0.5f, 0f);
            sleeve.type = Image.Type.Tiled;
            sleeve.preserveAspect = false;
            // UI sprites import at 3x (PPU 100/3); 0.75 tiles them at 4x.
            sleeve.pixelsPerUnitMultiplier = 3f / ArmScale;
            var paw = kit.Image(go.transform, "Paw", theme.PawOpen(0), Kit.Center, new Vector2(0f, 400f), new Vector2(36f * ArmScale, 40f * ArmScale));
            paw.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var arm = go.AddComponent<PawArm>();
            UiFields.Set(arm, "shoulder", go.transform);
            UiFields.Set(arm, "sleeve", sleeve.rectTransform);
            UiFields.Set(arm, "sleeveImage", sleeve);
            UiFields.Set(arm, "paw", paw.rectTransform);
            UiFields.Set(arm, "pawImage", paw);
            var so = new UnityEditor.SerializedObject(arm);
            so.FindProperty("restOffset").vector2Value = restOffset;
            so.ApplyModifiedPropertiesWithoutUndo();
            return arm;
        }
    }
}
