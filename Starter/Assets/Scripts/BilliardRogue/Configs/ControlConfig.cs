#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Paw mapping and strike detection tunables (GDD §3.1). Distances are body-normalized inches.</summary>
    [CreateAssetMenu(fileName = "ControlConfig", menuName = "Nex/Billiard Rogue/Control Config", order = 52)]
    public sealed class ControlConfig : ScriptableObject
    {
        [Header("Launch position")]
        [Tooltip("Left-paw x offset from the chest mapped to the left edge of the launch line.")]
        [SerializeField, Range(-40f, 0f)] float launchXMinInches = -26f;
        [Tooltip("Left-paw x offset from the chest mapped to the right edge of the launch line.")]
        [SerializeField, Range(0f, 40f)] float launchXMaxInches = 8f;

        [Header("Aim smoothing (OneEuro)")]
        [SerializeField, Range(0.1f, 10f)] float aimMinCutoff = 1f;
        [SerializeField, Range(0f, 1f)] float aimBeta = 0.05f;
        [Tooltip("Below this paw distance the previous aim is kept.")]
        [SerializeField, Range(0f, 20f)] float minAimDistanceInches = 4f;
        [Tooltip("Aim is sampled this long before the strike started so the thrust does not wobble it.")]
        [SerializeField, Range(0f, 0.5f)] float aimSampleDelaySeconds = 0.12f;
        // The aim angle clamp is ArenaRules.minAimAngleDeg; input clamps through ArenaGeometry.ClampAim(arenaRules, dir).

        [Header("Strike detection")]
        [Tooltip("Right-paw closing speed toward the left paw that fires a strike.")]
        [SerializeField, Range(5f, 200f)] float strikeSpeedInchesPerSec = 35f;
        [Tooltip("Paw distance at which the strike fires.")]
        [SerializeField, Range(1f, 20f)] float contactDistanceInches = 5f;
        [Tooltip("Paws must be at least this far apart before a new strike can arm.")]
        [SerializeField, Range(2f, 30f)] float armDistanceInches = 10f;
        [SerializeField, Range(0.05f, 2f)] float rearmSeconds = 0.35f;
        [Tooltip("Closing speed ≥ threshold × this multiplier counts as a power strike.")]
        [SerializeField, Range(1f, 5f)] float powerShotSpeedMultiplier = 2f;
        [Tooltip("Closing speed mapped to power 1.")]
        [SerializeField, Range(20f, 400f)] float fullPowerSpeedInchesPerSec = 120f;

        [Header("Tracking")]
        [SerializeField, Range(0.2f, 5f)] float trackingLostSeconds = 1.2f;

        [Header("Debug & bot")]
        [Tooltip("Mouse/keyboard aim sensitivity in degrees per second (arrow keys).")]
        [SerializeField, Range(10f, 360f)] float debugAimDegPerSec = 90f;
        [Tooltip("Candidate angles the auto-aim bot scores with PredictPath.")]
        [SerializeField, Range(3, 128)] int botSampleCount = 24;
        [SerializeField, Range(0f, 5f)] float botThinkSeconds = 0.6f;

        public float LaunchXMinInches => launchXMinInches;
        public float LaunchXMaxInches => launchXMaxInches;
        public float AimMinCutoff => aimMinCutoff;
        public float AimBeta => aimBeta;
        public float MinAimDistanceInches => minAimDistanceInches;
        public float AimSampleDelaySeconds => aimSampleDelaySeconds;
        public float StrikeSpeedInchesPerSec => strikeSpeedInchesPerSec;
        public float ContactDistanceInches => contactDistanceInches;
        public float ArmDistanceInches => armDistanceInches;
        public float RearmSeconds => rearmSeconds;
        public float PowerShotSpeedMultiplier => powerShotSpeedMultiplier;
        public float FullPowerSpeedInchesPerSec => fullPowerSpeedInchesPerSec;
        public float TrackingLostSeconds => trackingLostSeconds;
        public float DebugAimDegPerSec => debugAimDegPerSec;
        public int BotSampleCount => botSampleCount;
        public float BotThinkSeconds => botThinkSeconds;
    }
}
