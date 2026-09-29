#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Hype (GDD v2 §3): while at least one ball flies, the players' whole-body motion (ShotInputRouter.MotionEnergy,
    /// the maximum across players; a missing source counts as 0) charges Hype 0..1 through HypeConfig (energy curve,
    /// fast attack / slower release on unscaled time). Every player-turn frame it pushes the ball speed / damage
    /// multipliers to the simulation, the juice value to the board, the meter tier and MOVE prompt to the HUD and
    /// the hit-stop scaling to TimeScaleController, and feeds the per-shot Hype analytics. It resets to 0 (neutral
    /// pushes, once) as soon as no ball flies. Debug: DebugOverride (DebugHooks.SetHype) or DebugSettings.forceHype
    /// (≥ 0) replace the body value, still only while balls fly. Never touches aim or strikes. Allocation-free.
    /// </summary>
    public sealed class HypeController
    {
        const float Epsilon = 0.0005f;

        readonly SessionServices services;
        readonly HypeConfig config;
        float hype;
        int tier;
        float stillSeconds;
        bool movePrompt;
        bool pushed;

        public HypeController(SessionServices aServices)
        {
            services = aServices;
            var configured = aServices.Config.Hype;
            config = configured != null ? configured : ScriptableObject.CreateInstance<HypeConfig>();
        }

        /// <summary>Current Hype 0..1 (0 whenever no ball flies).</summary>
        public float Hype => hype;

        /// <summary>Current meter tier 0..HypeConfig.TierCount.</summary>
        public int Tier => tier;

        public bool MovePromptVisible => movePrompt;

        /// <summary>Debug override 0..1 (DebugHooks.SetHype); negative = off (DebugSettings.forceHype then applies).</summary>
        public float DebugOverride { get; set; } = -1f;

        #region Public Methods

        /// <summary>One player-turn frame, before the simulation step (the step then flies with this frame's Hype).</summary>
        public void Tick(float unscaledDeltaTime, float scaledDeltaTime)
        {
            if (services.Sim.ActiveBalls == 0)
            {
                Reset();
                return;
            }

            var forced = ForcedHype();
            var tracked = forced >= 0f;
            if (tracked)
            {
                hype = forced;
            }
            else
            {
                var target = config.TargetFor(MaxEnergy(out tracked));
                var tau = target > hype ? config.AttackSeconds : config.ReleaseSeconds;
                hype += (target - hype) * (1f - Mathf.Exp(-Mathf.Max(0f, unscaledDeltaTime) / Mathf.Max(0.001f, tau)));
                hype = hype < Epsilon ? 0f : Mathf.Clamp01(hype);
            }

            tier = TierFor(hype, tier);
            Push();
            UpdateMovePrompt(unscaledDeltaTime, tracked);
            services.Tracker.SampleHype(hype, scaledDeltaTime);
        }

        /// <summary>Back to 0 with neutral pushes (no ball in flight, turn end); a no-op once already neutral.</summary>
        public void Reset()
        {
            stillSeconds = 0f;
            if (!pushed && hype <= 0f && !movePrompt) return;
            hype = 0f;
            tier = 0;
            pushed = false;
            services.Sim.Balls.SetHype(1f, 1f, 0);
            services.Board.SetHype(0f);
            services.Hud.SetHype(0f, 0);
            SetMovePrompt(false);
            services.TimeScale.SetHitStopScale(1f, config.HitStopExtraCapPerSecond);
        }

        #endregion

        #region Helpers

        void Push()
        {
            pushed = true;
            var minBonus = tier >= config.MinBonusTier ? config.MinBonusDamage : 0;
            services.Sim.Balls.SetHype(config.SpeedMultiplier(hype), config.DamageMultiplier(hype), minBonus);
            services.Board.SetHype(hype);
            services.Hud.SetHype(hype, tier);
            services.TimeScale.SetHitStopScale(config.HitStopMultiplier(hype), config.HitStopExtraCapPerSecond);
        }

        // The prompt needs someone who can answer it: a tracked motion source (or the debug override).
        void UpdateMovePrompt(float unscaledDeltaTime, bool canMove)
        {
            if (!canMove || hype >= config.MovePromptBelow)
            {
                stillSeconds = 0f;
                SetMovePrompt(false);
                return;
            }

            stillSeconds += unscaledDeltaTime;
            SetMovePrompt(stillSeconds >= config.MovePromptDelay);
        }

        void SetMovePrompt(bool visible)
        {
            movePrompt = visible;
            services.Hud.ShowMovePrompt(visible);
        }

        float MaxEnergy(out bool anyTracked)
        {
            anyTracked = false;
            var best = 0f;
            var inputs = services.Inputs;
            for (var i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] is not ShotInputRouter router) continue;
                var energy = router.MotionEnergy;
                if (energy == null) continue;
                anyTracked |= energy.IsTracked;
                best = Mathf.Max(best, energy.Energy01);
            }

            return best;
        }

        float ForcedHype()
        {
            var forced = DebugOverride >= 0f ? DebugOverride : services.Debug.forceHype;
            return forced >= 0f ? Mathf.Clamp01(forced) : -1f;
        }

        int TierFor(float value, int current)
        {
            var result = 0;
            for (var t = 1; t <= HypeConfig.TierCount; t++)
            {
                var threshold = config.TierThreshold(t);
                var holds = t <= current && value >= threshold - config.TierHysteresis;
                if (value < threshold && !holds) break;
                result = t;
            }

            return result;
        }

        #endregion
    }
}
