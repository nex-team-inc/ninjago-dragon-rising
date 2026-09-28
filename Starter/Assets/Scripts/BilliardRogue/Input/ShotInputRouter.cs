#nullable enable

using System;
using UnityEngine;

// Implemented by Input module.
namespace Nex.BilliardRogue
{
    /// <summary>
    /// Per-player input selector: paw tracking by default, the debug input in the Editor / debug builds, the
    /// auto-aim bot when DebugSettings.autoAimBot is on. Raises TrackingLost after ControlConfig.trackingLostSeconds
    /// without a pose and TrackingRestored when the paws are back.
    /// </summary>
    public sealed class ShotInputRouter : MonoBehaviour, IShotInput
    {
#pragma warning disable CS0067
        public event Action<int>? TrackingLost;
        public event Action<int>? TrackingRestored;
#pragma warning restore CS0067

        IShotInput paw = null!;
        IShotInput debug = null!;
        IShotInput bot = null!;

        public int PlayerIndex { get; private set; }

        public void Initialize(int playerIndex, IShotInput aPaw, IShotInput aDebug, IShotInput aBot)
        {
            PlayerIndex = playerIndex;
            paw = aPaw;
            debug = aDebug;
            bot = aBot;
        }

        public bool IsTracking => Active.IsTracking;
        public float LaunchX01 => Active.LaunchX01;
        public Vector2 AimDirection => Active.AimDirection;

        public bool TryConsumeStrike(out StrikeInfo strike) => Active.TryConsumeStrike(out strike);

        public void ResetStrike()
        {
            paw.ResetStrike();
            debug.ResetStrike();
            bot.ResetStrike();
        }

        // Stub selection until the Input module implements DebugSettings / tracking based routing.
        IShotInput Active => paw;
    }
}
