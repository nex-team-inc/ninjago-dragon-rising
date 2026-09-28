#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Stand-in shot input while the Input module's PlayerShotInput prefab is not built: never tracking, never
    /// strikes. Lets the flow reach gameplay for integration checks (DebugHooks.Shoot drives shots).
    /// </summary>
    public sealed class NullShotInput : IShotInput
    {
        public static readonly NullShotInput Instance = new();

        public bool IsTracking => false;
        public float LaunchX01 => 0.5f;
        public Vector2 AimDirection => Vector2.up;

        public bool TryConsumeStrike(out StrikeInfo strike)
        {
            strike = default;
            return false;
        }

        public void ResetStrike()
        {
        }
    }
}
