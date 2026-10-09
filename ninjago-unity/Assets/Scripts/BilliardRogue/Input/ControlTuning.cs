#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Live multipliers on ControlConfig (the control lab's DebugSettings rows; Identity in release builds). The
    /// asset stays untouched: PawShotInput applies config × scale every frame through ControlConfig's *For methods.
    /// Scales are clamped to [MinScale, MaxScale] and snapped to Step so the debug panel's float steps stay exact.
    /// </summary>
    public readonly struct ControlTuning
    {
        public const float MinScale = 0.25f;
        public const float MaxScale = 3f;
        const float Step = 0.05f;

        /// <summary>× strike speed and full-power speed (higher = a harder thrust is needed).</summary>
        public readonly float strikeSpeed;
        /// <summary>× contact distance (higher = the paws may stay further apart).</summary>
        public readonly float contactDistance;
        /// <summary>Divides the aim filter's min cutoff (higher = steadier aim, more lag).</summary>
        public readonly float aimSmoothing;
        /// <summary>× the ball-paw range mapped to the launch line, around its centre (lower = less arm travel).</summary>
        public readonly float launchRange;

        public ControlTuning(float aStrikeSpeed, float aContactDistance, float aAimSmoothing, float aLaunchRange)
        {
            strikeSpeed = Sanitize(aStrikeSpeed);
            contactDistance = Sanitize(aContactDistance);
            aimSmoothing = Sanitize(aAimSmoothing);
            launchRange = Sanitize(aLaunchRange);
        }

        public static ControlTuning Identity => new(1f, 1f, 1f, 1f);

        static float Sanitize(float scale)
        {
            if (float.IsNaN(scale)) return 1f;
            return Mathf.Clamp(Mathf.Round(scale / Step) * Step, MinScale, MaxScale);
        }
    }
}
