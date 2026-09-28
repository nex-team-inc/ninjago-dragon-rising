#nullable enable

using UnityEngine;

// Implemented by Gameplay module.
namespace Nex.BilliardRogue
{
    /// <summary>
    /// The only writer of Time.timeScale during gameplay: hit-stop, slow-mo, fast-forward and pause compose into
    /// one value (pause wins, then hit-stop, then slow-mo × fast-forward). Overlays and BGM use unscaled time.
    /// </summary>
    public sealed class TimeScaleController : MonoBehaviour
    {
        /// <summary>Current gameplay multiplier (1 when idle, 0 while paused or in hit-stop).</summary>
        public float GameplayTimeScale => 1f;

        public void HitStop(float seconds)
        {
        }

        public void SlowMo(float scale, float seconds)
        {
        }

        public void SetFastForward(bool on)
        {
        }

        public void SetPaused(bool paused)
        {
        }
    }
}
