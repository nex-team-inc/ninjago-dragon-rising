#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Body-relative hand position → normalized screen point for the paw pointer (GDD v2 §4).</summary>
    public static class PawPointerMath
    {
        /// <summary>
        /// Maps a hand offset from the chest (inches, x = screen right, y = up) to 0..1 screen space: `center` lands
        /// on the screen centre and center ± halfRange on the edges (clamped), the same mapping for both hands so
        /// hands brought together meet on screen.
        /// </summary>
        public static Vector2 ToScreen01(Vector2 handInches, Vector2 center, Vector2 halfRange)
        {
            var x = 0.5f + (handInches.x - center.x) / (2f * Mathf.Max(halfRange.x, 1e-3f));
            var y = 0.5f + (handInches.y - center.y) / (2f * Mathf.Max(halfRange.y, 1e-3f));
            return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
        }
    }
}
