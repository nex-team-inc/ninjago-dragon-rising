#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// POWER (playtest 4, replacing the body-motion Hype of GDD v2 §3): every enemy hit while balls fly adds to the
    /// combo (PlayerTurnLoop.AddHits; every ball in the air counts, both players in 2P) and HypeConfig turns it into
    /// power 0..1, rising quickly on unscaled time. Every player-turn frame it pushes the ball speed / damage multipliers
    /// to the simulation, the juice value to the board, the meter tier to the HUD and the hit-stop scaling to
    /// TimeScaleController, and feeds the per-shot analytics. It resets to 0 (neutral pushes, once) as soon as nothing
    /// flies: no ball in the air and no volley still launching. Debug: DebugOverride (DebugHooks.SetHype) or
    /// DebugSettings.forceHype (≥ 0) replace the combo value, still only while balls fly. Allocation-free.
    /// </summary>
    public sealed class HypeController
    {
        const float Epsilon = 0.0005f;

        readonly SessionServices services;
        readonly HypeConfig config;
        float hype;
        int tier;
        int comboHits;
        bool pushed;

        public HypeController(SessionServices aServices)
        {
            services = aServices;
            var configured = aServices.Config.Hype;
            config = configured != null ? configured : ScriptableObject.CreateInstance<HypeConfig>();
        }

        /// <summary>Current power 0..1 (0 whenever nothing flies).</summary>
        public float Hype => hype;

        /// <summary>Current meter tier 0..HypeConfig.TierCount.</summary>
        public int Tier => tier;

        /// <summary>Enemy hits since the balls started flying.</summary>
        public int ComboHits => comboHits;

        /// <summary>Debug override 0..1 (DebugHooks.SetHype); negative = off (DebugSettings.forceHype then applies).</summary>
        public float DebugOverride { get; set; } = -1f;

        #region Public Methods

        public void AddHits(int hits)
        {
            comboHits += hits;
        }

        /// <summary>One player-turn frame, before the simulation step (the step then flies with this frame's power).</summary>
        public void Tick(float unscaledDeltaTime, float scaledDeltaTime, bool flying)
        {
            if (!flying)
            {
                Reset();
                return;
            }

            var forced = ForcedHype();
            if (forced >= 0f)
            {
                hype = forced;
            }
            else
            {
                var target = config.TargetFor(comboHits);
                hype += (target - hype) * (1f - Mathf.Exp(-Mathf.Max(0f, unscaledDeltaTime) / Mathf.Max(0.001f, config.AttackSeconds)));
                hype = hype < Epsilon ? 0f : Mathf.Clamp01(hype);
            }

            tier = TierFor(hype, tier);
            Push();
            services.Tracker.SampleHype(hype, scaledDeltaTime);
        }

        /// <summary>Back to 0 with neutral pushes (nothing flies, turn end); a no-op once already neutral.</summary>
        public void Reset()
        {
            comboHits = 0;
            if (!pushed && hype <= 0f) return;
            hype = 0f;
            tier = 0;
            pushed = false;
            services.Sim.Balls.SetHype(1f, 1f, 0);
            services.Board.SetHype(0f);
            services.Hud.SetHype(0f, 0);
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
