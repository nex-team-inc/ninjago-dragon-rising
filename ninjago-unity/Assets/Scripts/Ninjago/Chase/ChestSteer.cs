#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Maps a chest lean (body inches from the calibrated origin) to a steer in -1..1 with a deadzone.</summary>
    public static class ChestSteer
    {
        public static float Axis(float leanInches, float deadzoneInches, float fullInches)
        {
            var beyondDeadzone = Mathf.Abs(leanInches) - deadzoneInches;
            if (beyondDeadzone <= 0f) return 0f;
            var range = Mathf.Max(0.01f, fullInches - deadzoneInches);
            return Mathf.Sign(leanInches) * Mathf.Clamp01(beyondDeadzone / range);
        }

        /// <summary>The car passes useVertical = false: chest height does nothing for it, on purpose.</summary>
        public static Vector2 Steer(Vector2 leanInches, bool useVertical, float deadzoneInches, float fullLeanInches, float fullRiseInches)
        {
            var x = Axis(leanInches.x, deadzoneInches, fullLeanInches);
            var y = useVertical ? Axis(leanInches.y, deadzoneInches, fullRiseInches) : 0f;
            return new Vector2(x, y);
        }
    }
}
