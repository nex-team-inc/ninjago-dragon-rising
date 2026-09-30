#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    public struct StrikeInfo
    {
        /// <summary>Sim-space aim, normalized, always pointing up (clamped by ArenaGeometry.ClampAim).</summary>
        public Vector2 direction;
        /// <summary>Closing speed mapped to 0..1.</summary>
        public float power01;
        /// <summary>Closing speed ≥ powerShotSpeedMultiplier × threshold.</summary>
        public bool isPowerShot;
    }

    /// <summary>One player's shot input source (paws, debug keyboard/mouse or the auto-aim bot).</summary>
    public interface IShotInput
    {
        bool IsTracking { get; }
        /// <summary>Cat / ball position along the launch line, 0 = left wall, 1 = right wall.</summary>
        float LaunchX01 { get; }
        /// <summary>Small vertical nudge of the ball paw, 0.5 = the launch line (ArenaGeometry.LaunchOrigin, GDD v2 §21).</summary>
        float LaunchY01 { get; }
        /// <summary>Sim-space aim direction, normalized and clamped upward.</summary>
        Vector2 AimDirection { get; }
        /// <summary>Returns the strike detected since the last call (at most once per strike).</summary>
        bool TryConsumeStrike(out StrikeInfo strike);
        /// <summary>Drops any pending strike and re-arms (turn start, tracking regained).</summary>
        void ResetStrike();
    }
}
