#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>
    /// The views and widgets of the hand cursor games: the setup view with its hand check, Stone Kick, Earth Seal.
    /// Views start from the same template as the other Ninjago views (NinjagoViewsBuilder).
    /// </summary>
    public static class CursorGamesViewsBuilder
    {
        public const string SetupViewPath = NinjagoViewsBuilder.ViewsRoot + "/CursorSetupView.prefab";
        public const string StoneKickViewPath = NinjagoViewsBuilder.ViewsRoot + "/StoneKickView.prefab";
        public const string EarthSealViewPath = NinjagoViewsBuilder.ViewsRoot + "/EarthSealView.prefab";
        const string WidgetsRoot = NinjagoViewsBuilder.WidgetsRoot;
        static readonly Color kickYellow = new(1f, 0.84f, 0.18f);
        static readonly Color warnRed = new(1f, 0.3f, 0.25f);
        static readonly Color safeGreen = new(0.3f, 1f, 0.35f);
        static readonly Color panelDark = new(0.08f, 0.08f, 0.1f);

        #region Entry Point

        public static void Build()
        {
            NinjagoViewsBuilder.WithTemplate(() =>
            {
                var mark = BuildCursorMark();
                var target = BuildTarget();
                var heart = LoadComponent<Image>(WidgetsRoot + "/Heart.prefab");
                var hud = BuildStoneKickHud(heart);
                var tag = BuildPlayerTagWidget();
                var status = LoadComponent<SetupPlayerStatus>(WidgetsRoot + "/SetupPlayerStatus.prefab");
                NinjagoViewsBuilder.BuildView<NinjagoSetupView>(SetupViewPath, (view, ui) => BuildCursorSetup(view, ui, status, mark, target));
                NinjagoViewsBuilder.BuildView<StoneKickView>(StoneKickViewPath, (view, ui) => BuildStoneKick(view, ui, hud, mark));
                NinjagoViewsBuilder.BuildView<EarthSealView>(EarthSealViewPath, (view, ui) => BuildEarthSeal(view, ui, heart, tag, mark));
            });
        }

        #endregion

        #region Views

        static void BuildCursorSetup(NinjagoSetupView view, RectTransform ui, SetupPlayerStatus statusPrefab, HandCursorMark mark, HandCursorTarget target)
        {
            NinjagoViewsBuilder.BuildSetup(view, ui, statusPrefab);
            var host = Stretch(NewRect("HandCheck", ui));
            var check = host.gameObject.AddComponent<HandCursorCheck>();
            var root = Stretch(NewRect("CheckRoot", host));
            var targets = Stretch(NewRect("Targets", root));
            var prompt = NinjagoViewsBuilder.Banner(root, "Prompt", "ninjago.setup.hands", "Raise a hand into your circle", 84f, Color.white, new Vector2(0f, 420f));
            // Top-left: the player status rows sit along the bottom of the setup view.
            var pip = NinjagoViewsBuilder.CameraPreview(root, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(384f, 216f), out var pipFrame);
            var layer = CursorLayer(root, mark);
            root.gameObject.SetActive(false);

            Set(check, "root", root.gameObject);
            Set(check, "cursorLayer", layer);
            Set(check, "targetPrefab", target);
            Set(check, "targetsRoot", targets);
            Set(check, "promptLabel", prompt);
            SetLocalized(check, "raisePrompt", "ninjago.setup.hands");
            SetLocalized(check, "readyPrompt", "ninjago.setup.ready");
            Set(check, "pipFrame", pipFrame);
            Set(check, "pipIndicators", pip);
            Set(view, "handCheck", check);
        }

        static void BuildStoneKick(StoneKickView view, RectTransform ui, StoneKickHud hudPrefab, HandCursorMark mark)
        {
            var solo = Feed(ui, "SoloFeed", Vector2.zero, Vector2.one);
            var left = Feed(ui, "LeftFeed", Vector2.zero, new Vector2(0.5f, 1f));
            var right = Feed(ui, "RightFeed", new Vector2(0.5f, 0f), Vector2.one);
            var divider = NewImage(ui, "SplitDivider", null, panelDark);
            Place(divider.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 0f));
            var pip = NinjagoViewsBuilder.CameraPreview(ui, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(320f, 180f), out var pipFrame);
            var banner = NinjagoViewsBuilder.Banner(ui, "GetReady", "ninjago.fight.get_ready", "Get ready!", 120f, Color.white, new Vector2(0f, 120f));
            var layer = CursorLayer(ui, mark);

            Set(view, "soloFeed", solo);
            Set(view, "leftFeed", left);
            Set(view, "rightFeed", right);
            Set(view, "splitDivider", divider.gameObject);
            Set(view, "hudPrefab", hudPrefab);
            Set(view, "lanePrefab", LoadComponent<StoneKickLane>(CursorGamesWorldBuilder.StoneKickLanePath));
            Set(view, "cursorLayer", layer);
            Set(view, "pipFrame", pipFrame);
            Set(view, "pipIndicators", pip);
            Set(view, "getReadyBanner", banner.gameObject);
            Set(view, "keyResponder", BackProxy(ui));
        }

        static void BuildEarthSeal(EarthSealView view, RectTransform ui, Image heartPrefab, PlayerTagLabel tagPrefab, HandCursorMark mark)
        {
            var outline = OutlineTextMaterial();
            var feed = Feed(ui, "WallFeed", Vector2.zero, Vector2.one);
            var hearts = Row(ui, "Hearts", new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(560f, 64f), 10f, TextAnchor.MiddleLeft);
            var wave = Label(ui, "WaveLabel", null, "Wave 1/4", 64f, Color.white, outline);
            Place((RectTransform)wave.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(600f, 84f));
            var tags = Row(ui, "PlayerTags", new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(400f, 90f), 40f, TextAnchor.MiddleCenter);
            var pip = NinjagoViewsBuilder.CameraPreview(ui, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(288f, 162f), out var pipFrame);
            var hint = NinjagoViewsBuilder.Banner(ui, "HoldHint", "ninjago.seal.hold_hint", "Hold a hand on the cracks!", 100f, kickYellow, new Vector2(0f, 0f));
            var banner = Label(ui, "WaveBanner", null, "Wave 1", 150f, Color.white, outline);
            Place((RectTransform)banner.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 200f));
            var layer = CursorLayer(ui, mark);

            Set(view, "feed", feed);
            Set(view, "worldPrefab", LoadComponent<EarthSealWorld>(CursorGamesWorldBuilder.EarthSealWorldPath));
            Set(view, "heartsRow", hearts);
            Set(view, "heartPrefab", heartPrefab);
            Set(view, "waveLabel", wave);
            SetLocalized(view, "waveText", "ninjago.seal.wave");
            Set(view, "playerTagsRow", tags);
            Set(view, "playerTagPrefab", tagPrefab);
            Set(view, "holdHint", hint.gameObject);
            Set(view, "waveBanner", banner);
            SetLocalized(view, "waveBannerText", "ninjago.seal.wave_banner");
            Set(view, "cursorLayer", layer);
            Set(view, "pipFrame", pipFrame);
            Set(view, "pipIndicators", pip);
            Set(view, "keyResponder", BackProxy(ui));
        }

        #endregion

        #region Widgets

        static HandCursorMark BuildCursorMark()
        {
            var root = NewRect("HandCursorMark", null);
            root.sizeDelta = new Vector2(104f, 104f);
            var rim = root.gameObject.AddComponent<Image>();
            rim.sprite = GetSprite("Circle");
            rim.color = new Color(0.04f, 0.04f, 0.06f, 0.95f);
            rim.raycastTarget = false;
            var disc = NewImage(root, "Disc", GetSprite("Circle"), Color.white);
            disc.rectTransform.sizeDelta = new Vector2(78f, 78f);
            var dot = NewImage(root, "Dot", GetSprite("Circle"), Color.white);
            dot.rectTransform.sizeDelta = new Vector2(24f, 24f);
            var mark = root.gameObject.AddComponent<HandCursorMark>();
            Set(mark, "rect", root);
            Set(mark, "disc", disc);
            return SavePrefab(root.gameObject, WidgetsRoot + "/HandCursorMark.prefab").GetComponent<HandCursorMark>();
        }

        static HandCursorTarget BuildTarget()
        {
            var root = NewRect("HandCursorTarget", null);
            root.sizeDelta = new Vector2(280f, 280f);
            var fill = NewImage(root, "Fill", GetSprite("Circle"), Color.white);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            var ring = NewImage(root, "Ring", GetSprite("Ring"), Color.white);
            Stretch(ring.rectTransform);
            var done = NewImage(root, "Done", GetSprite("Circle"), Color.white);
            done.rectTransform.sizeDelta = new Vector2(90f, 90f);
            var tag = NinjagoWorldBuilder.BuildPlayerTag(root, new Vector2(0f, 190f), 72f);
            var target = root.gameObject.AddComponent<HandCursorTarget>();
            Set(target, "rect", root);
            Set(target, "ring", ring);
            Set(target, "fill", fill);
            Set(target, "playerTag", tag);
            Set(target, "doneMark", done.gameObject);
            return SavePrefab(root.gameObject, WidgetsRoot + "/HandCursorTarget.prefab").GetComponent<HandCursorTarget>();
        }

        static PlayerTagLabel BuildPlayerTagWidget()
        {
            var label = Label(null!, "PlayerTag", null, "P1", 72f, Color.white, OutlineTextMaterial());
            ((RectTransform)label.transform).sizeDelta = new Vector2(120f, 86f);
            var tag = label.gameObject.AddComponent<PlayerTagLabel>();
            Set(tag, "label", label);
            Set(tag, "text", label.GetComponent<TextMeshProUGUI>());
            SetLocalized(tag, "tagText", "ninjago.player.tag");
            return SavePrefab(label.gameObject, WidgetsRoot + "/PlayerTag.prefab").GetComponent<PlayerTagLabel>();
        }

        static StoneKickHud BuildStoneKickHud(Image heartPrefab)
        {
            var outline = OutlineTextMaterial();
            var root = NewRect("StoneKickHud", null);
            var hud = root.gameObject.AddComponent<StoneKickHud>();
            var flash = NewImage(root, "HitFlash", null, new Color(1f, 0.15f, 0.1f, 0f));
            Stretch(flash.rectTransform);
            var tag = NinjagoWorldBuilder.BuildPlayerTag(root, Vector2.zero, 72f);
            Place((RectTransform)tag.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(160f, 90f));
            var hearts = Row(root, "Hearts", new Vector2(0f, 1f), new Vector2(200f, -36f), new Vector2(500f, 64f), 10f, TextAnchor.MiddleLeft);
            var throwLabel = Label(root, "ThrowLabel", null, "Throw 1/6", 54f, Color.white, outline, TextAlignmentOptions.Right);
            Place((RectTransform)throwLabel.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -32f), new Vector2(420f, 70f));
            // Above the piece row, which hangs at the middle of the half.
            var kick = NewRect("KickPrompt", root);
            Place(kick, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 135f), new Vector2(900f, 170f));
            var kickWord = Label(kick, "KickWord", "ninjago.kick.kick", "KICK", 150f, kickYellow, outline, TextAlignmentOptions.Right);
            Place((RectTransform)kickWord.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(100f, 0f), new Vector2(520f, 170f));
            var kickCount = Label(kick, "KickCount", null, "0/3", 110f, Color.white, outline, TextAlignmentOptions.Left);
            Place((RectTransform)kickCount.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(125f, -6f), new Vector2(300f, 150f));
            var slash = NinjagoViewsBuilder.Banner(root, "SlashHint", "ninjago.kick.slash_hint", "Slash the rock!", 84f, Color.white, new Vector2(0f, -220f));
            var outBanner = NinjagoViewsBuilder.Banner(root, "OutBanner", "ninjago.fight.out", "OUT", 170f, warnRed, new Vector2(0f, 80f));
            var finished = NinjagoViewsBuilder.Banner(root, "FinishedBanner", "ninjago.fight.finished", "Finished!", 96f, safeGreen, new Vector2(0f, 330f));

            Set(hud, "playerTag", tag);
            Set(hud, "heartsRow", hearts);
            Set(hud, "heartPrefab", heartPrefab);
            Set(hud, "throwLabel", throwLabel);
            SetLocalized(hud, "throwText", "ninjago.kick.throw");
            Set(hud, "kickPrompt", kick.gameObject);
            Set(hud, "kickCountLabel", kickCount);
            SetLocalized(hud, "kickCountText", "ninjago.kick.count");
            Set(hud, "slashHint", slash.gameObject);
            Set(hud, "hitFlash", flash);
            Set(hud, "outBanner", outBanner.gameObject);
            Set(hud, "finishedBanner", finished.gameObject);
            Stretch(root);
            return SavePrefab(root.gameObject, WidgetsRoot + "/StoneKickHud.prefab").GetComponent<StoneKickHud>();
        }

        #endregion

        #region Helpers

        static HandCursorLayer CursorLayer(RectTransform parent, HandCursorMark mark)
        {
            var rect = Stretch(NewRect("CursorLayer", parent));
            var layer = rect.gameObject.AddComponent<HandCursorLayer>();
            Set(layer, "layer", rect);
            Set(layer, "markPrefab", mark);
            return layer;
        }

        static RectTransform Row(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, float spacing, TextAnchor alignment)
        {
            var row = NewRect(name, parent);
            Place(row, anchor, anchor, anchor, position, size);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            return row;
        }

        #endregion
    }
}
