#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The only writer of Time.timeScale during gameplay: slow-mo, fast-forward, the enemy-phase debug scale and pause
    /// compose into one value (pause wins, then slow-mo × fast-forward × phase). No hit-stop since playtest 6.
    /// Overlays and BGM use unscaled time. GameSession drives Tick with unscaled time, so timers keep running while
    /// the scale is 0 and edit-mode smoke runs can tick it by hand. Time.timeScale is written only when the composed
    /// value changes and restored to 1 on destroy.
    /// </summary>
    public sealed class TimeScaleController : MonoBehaviour
    {
        const float MinScale = 0.05f;

        float fastForwardScale = 2f;
        float slowMoRemaining;
        float slowMoScale = 1f;
        float phaseScale = 1f;
        bool fastForward;
        bool paused;
        bool applied;

        /// <summary>Current gameplay multiplier (1 when idle, 0 while paused).</summary>
        public float GameplayTimeScale { get; private set; } = 1f;

        public bool IsPaused => paused;
        public bool IsFastForward => fastForward;

        #region Public Methods

        public void Initialize(float aFastForwardScale)
        {
            fastForwardScale = Mathf.Max(1f, aFastForwardScale);
            Apply();
        }

        /// <summary>Advances the slow-mo timer with unscaled time.</summary>
        public void Tick(float unscaledDeltaTime)
        {
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

        /// <summary>Drops slow-mo, fast-forward and the phase scale (turn end, stage change); pause stays.</summary>
        public void ResetEffects()
        {
            slowMoRemaining = 0f;
            slowMoScale = 1f;
            fastForward = false;
            phaseScale = 1f;
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
            var value = paused ? 0f : slowMoScale * (fastForward ? fastForwardScale : 1f) * phaseScale;
            if (applied && Mathf.Approximately(value, GameplayTimeScale)) return;
            GameplayTimeScale = value;
            Time.timeScale = value;
            applied = true;
        }

        #endregion
    }
}
