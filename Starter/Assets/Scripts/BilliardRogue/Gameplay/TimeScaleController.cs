#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The only writer of Time.timeScale during gameplay: hit-stop, slow-mo, fast-forward, the enemy-phase debug
    /// scale and pause compose into one value (pause wins, then hit-stop, then slow-mo × fast-forward × phase).
    /// Overlays and BGM use unscaled time. GameSession drives Tick with unscaled time, so timers keep running while
    /// the scale is 0 and edit-mode smoke runs can tick it by hand. Time.timeScale is written only when the composed
    /// value changes and restored to 1 on destroy.
    /// </summary>
    public sealed class TimeScaleController : MonoBehaviour
    {
        const float MinScale = 0.05f;

        float fastForwardScale = 2f;
        float hitStopRemaining;
        float slowMoRemaining;
        float slowMoScale = 1f;
        float phaseScale = 1f;
        float hitStopScale = 1f;
        float hitStopExtraCap;
        float hitStopExtraBudget;
        bool fastForward;
        bool paused;
        bool applied;

        /// <summary>Current gameplay multiplier (1 when idle, 0 while paused or in hit-stop).</summary>
        public float GameplayTimeScale { get; private set; } = 1f;

        public bool IsPaused => paused;
        public bool IsFastForward => fastForward;

        #region Public Methods

        public void Initialize(float aFastForwardScale)
        {
            fastForwardScale = Mathf.Max(1f, aFastForwardScale);
            Apply();
        }

        /// <summary>Advances the hit-stop and slow-mo timers with unscaled time.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            hitStopExtraBudget = Mathf.Min(hitStopExtraCap, hitStopExtraBudget + hitStopExtraCap * unscaledDeltaTime);
            if (hitStopRemaining > 0f)
            {
                hitStopRemaining = Mathf.Max(0f, hitStopRemaining - unscaledDeltaTime);
            }

            if (slowMoRemaining > 0f)
            {
                slowMoRemaining = Mathf.Max(0f, slowMoRemaining - unscaledDeltaTime);
                if (slowMoRemaining <= 0f)
                {
                    slowMoScale = 1f;
                }
            }

            Apply();
        }

        /// <summary>
        /// Freezes gameplay for seconds × the Hype hit-stop scale (SetHitStopScale). The base seconds always apply;
        /// the Hype extension only spends the per-second budget, so a Hype-driven hit storm cannot stall pacing.
        /// </summary>
        public void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            var extra = Mathf.Min(seconds * (hitStopScale - 1f), hitStopExtraBudget);
            var target = seconds + Mathf.Max(0f, extra);
            if (target <= hitStopRemaining) return;
            // Overlapping stops do not add up: only what exceeds the running stop and the base spends the budget.
            hitStopExtraBudget -= Mathf.Max(0f, target - Mathf.Max(hitStopRemaining, seconds));
            hitStopRemaining = target;
            Apply();
        }

        /// <summary>
        /// Hype hit-stop scaling (GDD v2 §3): later HitStop calls last up to seconds × multiplier (≥ 1), with at most
        /// extraCapPerSecond seconds of extension per real second. 1 = v1 behaviour.
        /// </summary>
        public void SetHitStopScale(float multiplier, float extraCapPerSecond)
        {
            hitStopScale = Mathf.Max(1f, multiplier);
            var cap = Mathf.Max(0f, extraCapPerSecond);
            if (cap > hitStopExtraCap) hitStopExtraBudget = Mathf.Min(cap, hitStopExtraBudget + (cap - hitStopExtraCap));
            hitStopExtraCap = cap;
            hitStopExtraBudget = Mathf.Min(hitStopExtraBudget, cap);
        }

        public void SlowMo(float scale, float seconds)
        {
            if (seconds <= 0f) return;
            slowMoScale = Mathf.Clamp(scale, MinScale, 1f);
            slowMoRemaining = Mathf.Max(slowMoRemaining, seconds);
            Apply();
        }

        public void SetFastForward(bool on)
        {
            fastForward = on;
            Apply();
        }

        /// <summary>Extra multiplier for a whole phase (debug fast enemy phase); 1 = off.</summary>
        public void SetPhaseScale(float scale)
        {
            phaseScale = Mathf.Max(MinScale, scale);
            Apply();
        }

        public void SetPaused(bool aPaused)
        {
            paused = aPaused;
            Apply();
        }

        /// <summary>Drops hit-stop, slow-mo, fast-forward, the phase scale and the Hype hit-stop scale (turn end, stage change); pause stays.</summary>
        public void ResetEffects()
        {
            hitStopRemaining = 0f;
            slowMoRemaining = 0f;
            slowMoScale = 1f;
            fastForward = false;
            phaseScale = 1f;
            hitStopScale = 1f;
            Apply();
        }

        #endregion

        #region Life Cycle

        void OnDestroy()
        {
            if (applied) Time.timeScale = 1f;
        }

        #endregion

        #region Helpers

        void Apply()
        {
            var value = paused || hitStopRemaining > 0f
                ? 0f
                : slowMoScale * (fastForward ? fastForwardScale : 1f) * phaseScale;
            if (applied && Mathf.Approximately(value, GameplayTimeScale)) return;
            GameplayTimeScale = value;
            Time.timeScale = value;
            applied = true;
        }

        #endregion
    }
}
