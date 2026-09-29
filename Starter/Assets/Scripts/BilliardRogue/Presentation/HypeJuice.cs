#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>One frame's Hype-derived presentation values (computed once by HypeJuice, read by every view).</summary>
    public readonly struct HypeLook
    {
        public readonly float hype01;
        public readonly int tier;
        public readonly float glow;
        public readonly float size;
        public readonly float trailTime;
        public readonly float trailWidth;
        public readonly float trailTint;
        public readonly Color tierColor;

        public HypeLook(float aHype01, int aTier, JuiceConfig.HypeSettings s, Color aTierColor)
        {
            hype01 = aHype01;
            tier = aTier;
            glow = Mathf.Lerp(1f, s.glowMax, aHype01);
            size = Mathf.Lerp(1f, s.sizeMax, aHype01);
            trailTime = Mathf.Lerp(1f, s.trailTimeMax, aHype01);
            trailWidth = Mathf.Lerp(1f, s.trailWidthMax, aHype01);
            // Fades in over the first tier so a barely moving player keeps the ball's own trail colour.
            trailTint = s.trailTint * Mathf.Clamp01(aHype01 / Mathf.Max(0.01f, s.tier1));
            tierColor = aTierColor;
        }
    }

    /// <summary>
    /// Hype (GDD v2 §3) → juice. Smooths the gameplay Hype, detects tier rises and pushes the scaled look to the ball
    /// views (glow, size, trail), BoardEventPlayer (hit VFX scale, extra sparks), CameraShaker (shake multiplier),
    /// WorldLabelLayer (damage number size / colour / pop), the tier-3 rim aura and the cats (dance, tier pose).
    /// Allocation-free; ticked by BoardPresenter.Update with unscaled time so hit-stop never freezes the fade.
    /// </summary>
    public sealed class HypeJuice
    {
        const float TierDownHysteresis = 0.05f;
        const float ApplyEpsilon = 0.002f;

        readonly JuiceConfig.HypeSettings settings;
        readonly BoardViews views;
        readonly BoardEventPlayer eventPlayer;
        readonly CameraShaker shaker;
        readonly WorldLabelLayer labels;
        readonly CatView[] cats;
        readonly HypeAuraView? aura;
        float target;
        float current;
        float applied = -1f;
        int appliedTier;
        int tier;

        public HypeJuice(JuiceConfig juice, BoardViews aViews, BoardEventPlayer aEventPlayer, CameraShaker aShaker, WorldLabelLayer aLabels, CatView[] aCats, HypeAuraView? aAura)
        {
            settings = juice.Hype;
            views = aViews;
            eventPlayer = aEventPlayer;
            shaker = aShaker;
            labels = aLabels;
            cats = aCats;
            aura = aAura;
        }

        public float Target => target;
        public float Current => current;
        public int Tier => tier;

        public void SetTarget(float hype01)
        {
            target = Mathf.Clamp01(hype01);
            var next = TierOf(target, tier);
            if (next > tier) OnTierReached(next);
            tier = next;
        }

        /// <summary>Instantly back to the v1 look (board cleared, stage transition).</summary>
        public void ResetHype()
        {
            target = current = 0f;
            tier = 0;
            var look = Look();
            ApplyBalls(look);
            Apply(look);
        }

        public void Tick(float unscaledDt)
        {
            var time = target > current ? settings.riseTime : settings.fallTime;
            current += (target - current) * (1f - Mathf.Exp(-unscaledDt / Mathf.Max(0.001f, time)));
            if (target <= 0f && current < 0.001f) current = 0f;
            // The ball views get the look every frame (new balls spawn un-hyped; BallView skips unchanged looks);
            // the rest only when the smoothed value moved.
            var look = Look();
            ApplyBalls(look);
            if (Mathf.Abs(current - applied) < ApplyEpsilon && tier == appliedTier) return;
            Apply(look);
        }

        public Color TierColor(int aTier)
        {
            return aTier >= 3 ? settings.tier3Color : aTier == 2 ? settings.tier2Color : settings.tier1Color;
        }

        HypeLook Look() => new(current, tier, settings, TierColor(tier));

        void ApplyBalls(in HypeLook look)
        {
            var balls = views.Balls;
            for (var i = 0; i < balls.Count; i++)
            {
                balls[i].ApplyHype(look);
            }
        }

        void Apply(in HypeLook look)
        {
            applied = current;
            appliedTier = tier;
            eventPlayer.SetHype(Mathf.Lerp(1f, settings.hitVfxScaleMax, current), tier >= settings.extraSparksTier ? tier : 0);
            shaker.SetHypeMultiplier(Mathf.Lerp(1f, settings.shakeMax, current), settings.shakeCapPixels);
            labels.SetHype(tier, look.tierColor, settings);
            if (aura != null) aura.SetVisible(tier >= 3, settings.tier3Color);
            for (var i = 0; i < cats.Length; i++)
            {
                cats[i].SetHype(current);
            }
        }

        void OnTierReached(int newTier)
        {
            for (var i = 0; i < cats.Length; i++)
            {
                var cat = cats[i];
                if (!cat.gameObject.activeInHierarchy) continue;
                cat.PlayTierPose(newTier);
                eventPlayer.PlayVfx(VfxManager.VisualEffect.LevelUpBurst, cat.Center, settings.tierPoseVfxScale * (0.6f + 0.2f * newTier));
            }
        }

        int TierOf(float hype01, int currentTier)
        {
            var up = hype01 >= settings.tier3 ? 3 : hype01 >= settings.tier2 ? 2 : hype01 >= settings.tier1 ? 1 : 0;
            if (up >= currentTier) return up;
            // Falling: only drop a tier once Hype is clearly below its threshold, so the tier look does not flicker.
            var down = hype01 + TierDownHysteresis;
            var held = down >= settings.tier3 ? 3 : down >= settings.tier2 ? 2 : down >= settings.tier1 ? 1 : 0;
            return Mathf.Min(currentTier, held);
        }
    }
}
