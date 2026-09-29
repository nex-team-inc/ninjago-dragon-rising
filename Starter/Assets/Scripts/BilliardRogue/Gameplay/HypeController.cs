#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// POWER (GDD v2 §17): while balls fly (or a volley still launches), the players' body motion (ShotInputRouter
    /// .MotionEnergy, the maximum across players; a missing source counts as 0) charges POWER 0..1 through
    /// HypeConfig.Charge on unscaled time, paid from the run's energy; with no energy dancing charges nothing. POWER
    /// never drains on its own: it holds until ResetTurn (every turn start and end). Every player-turn frame it pushes
    /// the ball speed / damage multipliers to the simulation, the juice value to the board, the meter tier, energy and
    /// MOVE! prompt to the HUD and the hit-stop scaling to TimeScaleController, and feeds the per-shot analytics.
    /// Debug: DebugOverride (DebugHooks.SetHype) or DebugSettings.forceHype (≥ 0) set POWER while balls fly, free of
    /// energy. Never touches aim or strikes. Allocation-free.
    /// </summary>
    public sealed class HypeController
    {
        readonly SessionServices services;
        readonly HypeConfig config;
        float hype;
        int tier;
        float stillSeconds;
        bool starved;

        public HypeController(SessionServices aServices)
        {
            services = aServices;
            var configured = aServices.Config.Hype;
            config = configured != null ? configured : ScriptableObject.CreateInstance<HypeConfig>();
        }

        /// <summary>Current POWER 0..1 (this turn's charge).</summary>
        public float Hype => hype;

        /// <summary>Current meter tier 0..HypeConfig.TierCount.</summary>
        public int Tier => tier;

        /// <summary>Debug override 0..1 (DebugHooks.SetHype); negative = off (DebugSettings.forceHype then applies).</summary>
        public float DebugOverride { get; set; } = -1f;

        #region Public Methods

        /// <summary>One player-turn frame, before the simulation step (the step then flies with this frame's POWER).</summary>
        public void Tick(float unscaledDeltaTime, float scaledDeltaTime, bool flying)
        {
            var run = services.Run;
            var motion = MaxMotion(out var tracked);
            if (flying)
            {
                var forced = ForcedHype();
                if (forced >= 0f)
                {
                    hype = forced;
                    tracked = true;
                }
                else
                {
                    hype = config.Charge(hype, run.energy, motion, unscaledDeltaTime, out var spent);
                    run.energy = Mathf.Max(0f, run.energy - spent);
                }

                tier = TierFor(hype, tier);
                Push();
                services.Tracker.SampleHype(hype, scaledDeltaTime);
            }

            var canCharge = flying && tracked && hype < 1f;
            starved = canCharge && run.energy <= 0f && motion >= config.NoEnergyMotion;
            services.Hud.SetEnergy(run.energy, starved);
            UpdateMovePrompt(unscaledDeltaTime, canCharge && run.energy > 0f, motion);
        }

        /// <summary>Turn start / end: POWER back to 0 with neutral pushes, the HUD showing the energy left.</summary>
        public void ResetTurn()
        {
            hype = 0f;
            tier = 0;
            stillSeconds = 0f;
            starved = false;
            services.Sim.Balls.SetHype(1f, 1f, 0);
            services.Board.SetHype(0f);
            services.Hud.SetHype(0f, 0);
            services.Hud.SetEnergy(services.Run.energy, false);
            services.Hud.ShowMovePrompt(false);
            services.TimeScale.SetHitStopScale(1f, config.HitStopExtraCapPerSecond);
        }

        #endregion

        #region Helpers

        void Push()
        {
            var minBonus = tier >= config.MinBonusTier ? config.MinBonusDamage : 0;
            services.Sim.Balls.SetHype(config.SpeedMultiplier(hype), config.DamageMultiplier(hype), minBonus);
            services.Board.SetHype(hype);
            services.Hud.SetHype(hype, tier);
            services.TimeScale.SetHitStopScale(config.HitStopMultiplier(hype), config.HitStopExtraCapPerSecond);
        }

        // MOVE! asks for dancing only when dancing would pay: balls flying, energy left, the bar not yet full.
        void UpdateMovePrompt(float unscaledDeltaTime, bool worthDancing, float motion)
        {
            if (!worthDancing || motion >= config.MovePromptBelowMotion)
            {
                stillSeconds = 0f;
                services.Hud.ShowMovePrompt(false);
                return;
            }

            stillSeconds += unscaledDeltaTime;
            services.Hud.ShowMovePrompt(stillSeconds >= config.MovePromptDelay);
        }

        float MaxMotion(out bool anyTracked)
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
