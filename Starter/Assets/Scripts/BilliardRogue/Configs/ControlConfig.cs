#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Paw mapping, strike detection, debug input and auto-aim bot tunables (GDD §3.1, TDD §6). Distances are
    /// body-normalized inches relative to the chest; times are unscaled seconds. The aim angle clamp is
    /// ArenaRules.minAimAngleDeg (inputs clamp through ArenaGeometry.ClampAim).
    /// </summary>
    [CreateAssetMenu(fileName = "ControlConfig", menuName = "Nex/Billiard Rogue/Control Config", order = 52)]
    public sealed class ControlConfig : ScriptableObject
    {
        [Header("Launch position")]
        [Tooltip("Ball-paw x offset from the chest mapped to the left edge of the launch line (right-handed; mirrored for left-handed).")]
        [SerializeField, Range(-40f, 0f)] float launchXMinInches = -26f;
        [Tooltip("Ball-paw x offset from the chest mapped to the right edge of the launch line (right-handed; mirrored for left-handed).")]
        [SerializeField, Range(0f, 40f)] float launchXMaxInches = 8f;
        [Tooltip("OneEuro min cutoff (Hz) of the launch position filter.")]
        [SerializeField, Range(0.1f, 10f)] float launchXMinCutoff = 1f;
        [Tooltip("OneEuro beta of the launch position filter (higher = less lag on fast moves).")]
        [SerializeField, Range(0f, 1f)] float launchXBeta = 0.05f;

        [Header("Aim smoothing (OneEuro)")]
        [Tooltip("OneEuro min cutoff (Hz) of the cue→ball paw vector filter.")]
        [SerializeField, Range(0.1f, 10f)] float aimMinCutoff = 1f;
        [Tooltip("OneEuro beta of the cue→ball paw vector filter.")]
        [SerializeField, Range(0f, 1f)] float aimBeta = 0.05f;
        [Tooltip("Below this paw distance the previous aim is kept.")]
        [SerializeField, Range(0f, 20f)] float minAimDistanceInches = 4f;
        [Tooltip("Aim is sampled this long before the strike started so the thrust does not wobble it.")]
        [SerializeField, Range(0f, 0.5f)] float aimSampleDelaySeconds = 0.12f;

        [Header("Strike detection")]
        [Tooltip("Cue-paw closing speed toward the ball paw that starts a strike.")]
        [SerializeField, Range(5f, 200f)] float strikeSpeedInchesPerSec = 35f;
        [Tooltip("Paw distance at which the strike fires.")]
        [SerializeField, Range(1f, 20f)] float contactDistanceInches = 5f;
        [Tooltip("Paws must be at least this far apart before a new strike can arm.")]
        [SerializeField, Range(2f, 30f)] float armDistanceInches = 10f;
        [Tooltip("The paws must close by at least this much during one strike (filters camera jitter near contact).")]
        [SerializeField, Range(0f, 20f)] float minStrikeTravelInches = 4f;
        [Tooltip("Minimum time between a strike and the next arm.")]
        [SerializeField, Range(0.05f, 2f)] float rearmSeconds = 0.35f;
        [Tooltip("A fast approach that has not reached contact after this long is dropped.")]
        [SerializeField, Range(0.1f, 1.5f)] float maxStrikeSeconds = 0.4f;
        [Tooltip("Closing speed ≥ threshold × this multiplier counts as a power strike.")]
        [SerializeField, Range(1f, 5f)] float powerShotSpeedMultiplier = 2f;
        [Tooltip("Closing speed mapped to power 1.")]
        [SerializeField, Range(20f, 400f)] float fullPowerSpeedInchesPerSec = 120f;
        [Tooltip("Raw camera samples further apart than this are a tracking gap (no speed is measured across it).")]
        [SerializeField, Range(0.05f, 1f)] float maxSampleGapSeconds = 0.25f;
        [Tooltip("A strike nobody consumed within this time is dropped (pause, enemy phase, the other player's turn).")]
        [SerializeField, Range(0.05f, 2f)] float strikeExpirySeconds = 0.3f;

        [Header("Tracking")]
        [Tooltip("Chest and both paws must stay visible this long before tracking counts as regained.")]
        [SerializeField, Range(0f, 2f)] float trackingAcquireSeconds = 0.2f;
        [Tooltip("Paw nodes may be missing this long before tracking counts as lost (the engine already hides nodes after 0.2 s).")]
        [SerializeField, Range(0f, 2f)] float trackingDropSeconds = 0.15f;
        [Tooltip("No new camera frame for this long counts as not tracked (camera stopped or muted).")]
        [SerializeField, Range(0.1f, 3f)] float staleFrameSeconds = 0.5f;
        [Tooltip("Untracked this long raises ShotInputRouter.TrackingLost (the gameplay pause overlay uses the same value).")]
        [SerializeField, Range(0.2f, 5f)] float trackingLostSeconds = 1.2f;

        [Header("Debug input (Editor / debug builds)")]
        [Tooltip("A/D aim rotation speed in degrees per second.")]
        [SerializeField, Range(10f, 360f)] float debugAimDegPerSec = 90f;
        [Tooltip("Left/Right arrow launch position speed (full launch line widths per second).")]
        [SerializeField, Range(0.1f, 3f)] float debugLaunchPerSec = 0.8f;
        [Tooltip("Power of a plain Space strike (Shift + Space is a power strike at power 1).")]
        [SerializeField, Range(0f, 1f)] float debugStrikePower = 0.6f;

        [Header("Auto-aim bot (debug, automated playtests)")]
        [Tooltip("Candidate aim angles per launch position scored with BallSimulator.PredictPath.")]
        [SerializeField, Range(3, 128)] int botSampleCount = 24;
        [Tooltip("Candidate launch positions along the launch line.")]
        [SerializeField, Range(1, 16)] int botLaunchSamples = 5;
        [Tooltip("Candidates scored per frame (spreads the search over frames; ~0.1 ms each on a desktop CPU).")]
        [SerializeField, Range(1, 256)] int botCandidatesPerFrame = 4;
        [Tooltip("Predicted path length in sim units.")]
        [SerializeField, Range(4f, 80f)] float botPathLength = 30f;
        [Tooltip("Predicted reflections per candidate.")]
        [SerializeField, Range(1, 24)] int botPathBounces = 8;
        [Tooltip("Human-like pause before each shot.")]
        [SerializeField, Range(0f, 5f)] float botThinkSeconds = 0.6f;
        [Tooltip("Random extra think time, 0..this.")]
        [SerializeField, Range(0f, 2f)] float botThinkJitterSeconds = 0.25f;
        [Tooltip("Time the bot takes to walk the cat and sweep the cue to the chosen shot.")]
        [SerializeField, Range(0f, 2f)] float botAimSweepSeconds = 0.35f;
        [Tooltip("Random aim error in degrees (0 = perfect).")]
        [SerializeField, Range(0f, 10f)] float botAimJitterDeg = 1f;
        [Tooltip("Chance that a bot strike is a power strike.")]
        [SerializeField, Range(0f, 1f)] float botPowerShotChance = 0.15f;

        [Header("Auto-aim bot scoring")]
        [Tooltip("Score per predicted damaging enemy contact.")]
        [SerializeField, Range(0f, 10f)] float botHitScore = 1f;
        [Tooltip("Extra score per contact scaled by how far down the enemy stands (0 top row … 1 danger row).")]
        [SerializeField, Range(0f, 10f)] float botRowWeight = 0.8f;
        [Tooltip("Extra score for hitting an enemy standing in the danger row.")]
        [SerializeField, Range(0f, 10f)] float botDangerRowBonus = 1.5f;
        [Tooltip("Extra score when the predicted contacts kill an enemy (scaled up for lower rows).")]
        [SerializeField, Range(0f, 10f)] float botKillBonus = 1.5f;
        [Tooltip("Penalty per predicted contact with a shielded face (BLOCK).")]
        [SerializeField, Range(0f, 10f)] float botShieldPenalty = 1.5f;
        [Tooltip("Score per predicted crate contact.")]
        [SerializeField, Range(0f, 5f)] float botCrateScore = 0.3f;
        [Tooltip("Score per pickup the predicted path crosses.")]
        [SerializeField, Range(0f, 5f)] float botPickupScore = 1.2f;

        #region Accessors

        public float LaunchXMinInches => launchXMinInches;
        public float LaunchXMaxInches => launchXMaxInches;
        public float LaunchXMinCutoff => launchXMinCutoff;
        public float LaunchXBeta => launchXBeta;
        public float AimMinCutoff => aimMinCutoff;
        public float AimBeta => aimBeta;
        public float MinAimDistanceInches => minAimDistanceInches;
        public float AimSampleDelaySeconds => aimSampleDelaySeconds;
        public float StrikeSpeedInchesPerSec => strikeSpeedInchesPerSec;
        public float ContactDistanceInches => contactDistanceInches;
        public float ArmDistanceInches => armDistanceInches;
        public float MinStrikeTravelInches => minStrikeTravelInches;
        public float RearmSeconds => rearmSeconds;
        public float MaxStrikeSeconds => maxStrikeSeconds;
        public float PowerShotSpeedMultiplier => powerShotSpeedMultiplier;
        public float FullPowerSpeedInchesPerSec => fullPowerSpeedInchesPerSec;
        public float MaxSampleGapSeconds => maxSampleGapSeconds;
        public float StrikeExpirySeconds => strikeExpirySeconds;
        public float TrackingAcquireSeconds => trackingAcquireSeconds;
        public float TrackingDropSeconds => trackingDropSeconds;
        public float StaleFrameSeconds => staleFrameSeconds;
        public float TrackingLostSeconds => trackingLostSeconds;
        public float DebugAimDegPerSec => debugAimDegPerSec;
        public float DebugLaunchPerSec => debugLaunchPerSec;
        public float DebugStrikePower => debugStrikePower;
        public int BotSampleCount => botSampleCount;
        public int BotLaunchSamples => botLaunchSamples;
        public int BotCandidatesPerFrame => botCandidatesPerFrame;
        public float BotPathLength => botPathLength;
        public int BotPathBounces => botPathBounces;
        public float BotThinkSeconds => botThinkSeconds;
        public float BotThinkJitterSeconds => botThinkJitterSeconds;
        public float BotAimSweepSeconds => botAimSweepSeconds;
        public float BotAimJitterDeg => botAimJitterDeg;
        public float BotPowerShotChance => botPowerShotChance;
        public float BotHitScore => botHitScore;
        public float BotRowWeight => botRowWeight;
        public float BotDangerRowBonus => botDangerRowBonus;
        public float BotKillBonus => botKillBonus;
        public float BotShieldPenalty => botShieldPenalty;
        public float BotCrateScore => botCrateScore;
        public float BotPickupScore => botPickupScore;

        /// <summary>The strike thresholds for StrikeDetector (a fresh copy, so live Inspector edits apply).</summary>
        public StrikeSettings StrikeSettings => new()
        {
            strikeSpeed = strikeSpeedInchesPerSec,
            contactDistance = contactDistanceInches,
            armDistance = armDistanceInches,
            rearmSeconds = rearmSeconds,
            powerMultiplier = powerShotSpeedMultiplier,
            fullPowerSpeed = fullPowerSpeedInchesPerSec,
            minTravel = minStrikeTravelInches,
            maxStrikeSeconds = maxStrikeSeconds,
            maxSampleGapSeconds = maxSampleGapSeconds,
        };

        #endregion
    }
}
