#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Paw mapping, strike detection, body motion energy (Hype), paw pointer, debug input and auto-aim bot tunables
    /// (GDD §3.1, GDD v2 §2-§4, TDD §6). Distances are body-normalized inches relative to the chest; times are
    /// unscaled seconds. The aim angle clamp is ArenaRules.minAimAngleDeg (inputs clamp through ArenaGeometry.ClampAim).
    /// </summary>
    [CreateAssetMenu(fileName = "ControlConfig", menuName = "Nex/Billiard Rogue/Control Config", order = 52)]
    public sealed class ControlConfig : ScriptableObject
    {
        [Header("Paw roles (GDD v2 §16: the upper paw holds the ball, the lower paw is the cue)")]
        [Tooltip("The cue paw must be this much higher than the ball paw before the roles swap.")]
        [SerializeField, Range(0f, 20f)] float roleSwapMarginInches = 4f;
        [Tooltip("...and stay that much higher this long (never during a strike).")]
        [SerializeField, Range(0f, 2f)] float roleSwapSeconds = 0.25f;

        [Header("Launch position")]
        [Tooltip("Ball-paw x offset from the chest mapped to the left edge of the launch line while the left paw holds the ball (mirrored for the right paw).")]
        [SerializeField, Range(-40f, 0f)] float launchXMinInches = -26f;
        [Tooltip("Ball-paw x offset from the chest mapped to the right edge of the launch line while the left paw holds the ball (mirrored for the right paw).")]
        [SerializeField, Range(0f, 40f)] float launchXMaxInches = 8f;
        [Tooltip("Ball-paw y offset (inches, + above the chest) at the bottom of the accept region (GDD v2 §21); centred on the chest, so this is below it.")]
        [SerializeField, Range(-40f, 40f)] float launchYMinInches = -18f;
        [Tooltip("...and at the top of the region. A tall region around the chest means the hand nudges the ball comfortably; equal values turn the y nudge off.")]
        [SerializeField, Range(-40f, 40f)] float launchYMaxInches = 18f;
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
        [Tooltip("Cue-paw speed toward the ball paw that starts a strike (v2: 55% of the v1 35 in/s).")]
        [SerializeField, Range(5f, 200f)] float strikeSpeedInchesPerSec = 19f;
        [Tooltip("A started strike whose closing speed drops below this fraction of the strike speed before contact is dropped as stalled.")]
        [SerializeField, Range(0.1f, 1f)] float sustainSpeedFraction = 0.5f;
        [Tooltip("Paw distance at which the strike fires (v2: 1.8x the v1 5 in).")]
        [SerializeField, Range(1f, 20f)] float contactDistanceInches = 9f;
        [Tooltip("Max angle between the cue paw's motion and the direction to the ball paw that still starts a strike.")]
        [SerializeField, Range(10f, 90f)] float strikeAngleToleranceDeg = 60f;
        [Tooltip("A fast approach that passes the ball paw's line (across the thrust) fires when it passes at most this far beside the ball paw. 0 = contact only.")]
        [SerializeField, Range(0f, 40f)] float lineCrossMaxOffsetInches = 12f;
        [Tooltip("Paws must be at least this far apart before a new strike can arm (at least contact + min travel).")]
        [SerializeField, Range(2f, 30f)] float armDistanceInches = 14f;
        [Tooltip("The paws must close by at least this much during one strike (filters camera jitter near contact).")]
        [SerializeField, Range(0f, 20f)] float minStrikeTravelInches = 4f;
        [Tooltip("Minimum time between a strike and the next arm.")]
        [SerializeField, Range(0.05f, 2f)] float rearmSeconds = 0.25f;
        [Tooltip("A fast approach that has not reached contact after this long is dropped.")]
        [SerializeField, Range(0.1f, 1.5f)] float maxStrikeSeconds = 0.6f;
        [Tooltip("Closing speed ≥ threshold × this multiplier counts as a power strike.")]
        [SerializeField, Range(1f, 6f)] float powerShotSpeedMultiplier = 3.5f;
        [Tooltip("Closing speed mapped to power 1.")]
        [SerializeField, Range(20f, 400f)] float fullPowerSpeedInchesPerSec = 110f;
        [Tooltip("Raw camera samples further apart than this are a tracking gap (no speed is measured across it).")]
        [SerializeField, Range(0.05f, 1f)] float maxSampleGapSeconds = 0.25f;
        [Tooltip("A strike nobody consumed within this time is dropped (pause, enemy phase, the other player's turn). Must exceed PacingConfig.shotCooldown, or a strike made during the cooldown expires before it can fire.")]
        [SerializeField, Range(0.05f, 2f)] float strikeExpirySeconds = 0.6f;

        [Header("Tracking")]
        [Tooltip("Chest and both paws must stay visible this long before tracking counts as regained.")]
        [SerializeField, Range(0f, 2f)] float trackingAcquireSeconds = 0.2f;
        [Tooltip("Paw nodes may be missing this long before tracking counts as lost (the engine already hides nodes after 0.2 s).")]
        [SerializeField, Range(0f, 2f)] float trackingDropSeconds = 0.15f;
        [Tooltip("No new camera frame for this long counts as not tracked (camera stopped or muted).")]
        [SerializeField, Range(0.1f, 3f)] float staleFrameSeconds = 0.5f;
        [Tooltip("Untracked this long raises ShotInputRouter.TrackingLost (the gameplay pause overlay uses the same value).")]
        [SerializeField, Range(0.2f, 5f)] float trackingLostSeconds = 1.2f;

        [Header("Motion energy (Hype, GDD v2 §3)")]
        [Tooltip("Per-node speed (in/s) treated as camera jitter and subtracted before averaging.")]
        [SerializeField, Range(0f, 40f)] float motionDeadzoneInchesPerSec = 8f;
        [Tooltip("Mean per-node speed above the deadzone (in/s) mapped to energy 1.")]
        [SerializeField, Range(5f, 150f)] float motionFullInchesPerSec = 30f;
        [Tooltip("One node's speed is clamped to this (in/s) so a single mis-detected frame cannot max the meter.")]
        [SerializeField, Range(20f, 600f)] float motionMaxNodeInchesPerSec = 200f;
        [Tooltip("Time constant (s) while the energy rises.")]
        [SerializeField, Range(0.01f, 1f)] float motionAttackSeconds = 0.1f;
        [Tooltip("Time constant (s) while the energy falls.")]
        [SerializeField, Range(0.05f, 3f)] float motionReleaseSeconds = 0.5f;
        [Tooltip("Nodes that must be detected in two consecutive camera frames for a motion measurement (else energy falls to 0).")]
        [SerializeField, Range(1, 11)] int motionMinNodes = 4;

        [Header("Paw pointer (motion UI, GDD v2 §4)")]
        [Tooltip("Hand x offset from the chest (inches, + = screen right) mapped to the screen centre.")]
        [SerializeField, Range(-20f, 20f)] float pawPointerCenterXInches = 0f;
        [Tooltip("Hand y offset from the chest (inches, + = up) mapped to the screen centre.")]
        [SerializeField, Range(-20f, 20f)] float pawPointerCenterYInches = 4f;
        [Tooltip("Hand x distance from the centre (inches) mapped to the left / right screen edge (smaller = less arm travel).")]
        [SerializeField, Range(4f, 40f)] float pawPointerHalfWidthInches = 16f;
        [Tooltip("Hand y distance from the centre (inches) mapped to the bottom / top screen edge.")]
        [SerializeField, Range(4f, 40f)] float pawPointerHalfHeightInches = 12f;
        [Tooltip("OneEuro min cutoff (Hz) of the paw pointer filter (lower = steadier, more lag).")]
        [SerializeField, Range(0.1f, 10f)] float pawPointerMinCutoff = 1.5f;
        [Tooltip("OneEuro beta of the paw pointer filter (higher = less lag on fast moves).")]
        [SerializeField, Range(0f, 1f)] float pawPointerBeta = 0.08f;

        [Header("Debug input (Editor / debug builds)")]
        [Tooltip("A/D aim rotation speed in degrees per second.")]
        [SerializeField, Range(10f, 360f)] float debugAimDegPerSec = 90f;
        [Tooltip("Left/Right arrow launch position speed (full launch line widths per second).")]
        [SerializeField, Range(0.1f, 3f)] float debugLaunchPerSec = 0.8f;
        [Tooltip("Power of a plain Space strike (Shift + Space is a power strike at power 1).")]
        [SerializeField, Range(0f, 1f)] float debugStrikePower = 0.6f;
        [Tooltip("Mouse speed (screen heights per second) that simulates motion energy 1 (holding the Hype key is 1).")]
        [SerializeField, Range(0.1f, 10f)] float debugMotionFullScreensPerSec = 2f;
        [Tooltip("Debug paw pointer: each paw sits this far (screen widths) left / right of the mouse.")]
        [SerializeField, Range(0f, 0.2f)] float debugPawOffset01 = 0.03f;

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
        [Tooltip("How fast the bot's simulated motion energy wanders (Hz of its noise).")]
        [SerializeField, Range(0.01f, 2f)] float botMotionFrequencyHz = 0.2f;
        [Tooltip("Lowest simulated motion energy of the bot.")]
        [SerializeField, Range(0f, 1f)] float botMotionMin = 0.05f;
        [Tooltip("Highest simulated motion energy of the bot.")]
        [SerializeField, Range(0f, 1f)] float botMotionMax = 1f;

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

        public float RoleSwapMarginInches => roleSwapMarginInches;
        public float RoleSwapSeconds => roleSwapSeconds;
        public float LaunchXMinInches => launchXMinInches;
        public float LaunchXMaxInches => launchXMaxInches;
        public float LaunchYMinInches => launchYMinInches;
        public float LaunchYMaxInches => launchYMaxInches;
        public float LaunchXMinCutoff => launchXMinCutoff;
        public float LaunchXBeta => launchXBeta;
        public float AimMinCutoff => aimMinCutoff;
        public float AimBeta => aimBeta;
        public float MinAimDistanceInches => minAimDistanceInches;
        public float AimSampleDelaySeconds => aimSampleDelaySeconds;
        public float StrikeSpeedInchesPerSec => strikeSpeedInchesPerSec;
        public float ContactDistanceInches => contactDistanceInches;
        public float StrikeAngleToleranceDeg => strikeAngleToleranceDeg;
        public float LineCrossMaxOffsetInches => lineCrossMaxOffsetInches;
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
        public float DebugMotionFullScreensPerSec => debugMotionFullScreensPerSec;
        public float DebugPawOffset01 => debugPawOffset01;
        public float MotionAttackSeconds => motionAttackSeconds;
        public float MotionReleaseSeconds => motionReleaseSeconds;
        public Vector2 PawPointerCenterInches => new(pawPointerCenterXInches, pawPointerCenterYInches);
        public Vector2 PawPointerHalfRangeInches => new(pawPointerHalfWidthInches, pawPointerHalfHeightInches);
        public float PawPointerMinCutoff => pawPointerMinCutoff;
        public float PawPointerBeta => pawPointerBeta;
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
        public float BotMotionFrequencyHz => botMotionFrequencyHz;
        public float BotMotionMin => botMotionMin;
        public float BotMotionMax => botMotionMax;
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
            sustainSpeedFraction = sustainSpeedFraction,
            contactDistance = contactDistanceInches,
            angleToleranceDeg = strikeAngleToleranceDeg,
            lineCrossMaxOffset = lineCrossMaxOffsetInches,
            armDistance = armDistanceInches,
            rearmSeconds = rearmSeconds,
            powerMultiplier = powerShotSpeedMultiplier,
            fullPowerSpeed = fullPowerSpeedInchesPerSec,
            minTravel = minStrikeTravelInches,
            maxStrikeSeconds = maxStrikeSeconds,
            maxSampleGapSeconds = maxSampleGapSeconds,
        };

        /// <summary>Motion energy settings for MotionEnergyFilter (a fresh copy, so live Inspector edits apply).</summary>
        public MotionEnergySettings MotionEnergySettings => new()
        {
            deadzone = motionDeadzoneInchesPerSec,
            fullSpeed = motionFullInchesPerSec,
            maxNodeSpeed = motionMaxNodeInchesPerSec,
            attackSeconds = motionAttackSeconds,
            releaseSeconds = motionReleaseSeconds,
            minNodes = motionMinNodes,
            maxSampleGapSeconds = maxSampleGapSeconds,
        };

        #endregion

        #region Live tuning

        /// <summary>
        /// StrikeSettings with the live tuning applied: strike and full-power speed × tuning.strikeSpeed (the power
        /// curve keeps its shape), contact distance and line-cross offset × tuning.contactDistance; the arm distance
        /// stays at least contact + min travel so a strike can still arm when the contact distance grows.
        /// </summary>
        public StrikeSettings StrikeSettingsFor(in ControlTuning tuning)
        {
            var settings = StrikeSettings;
            settings.strikeSpeed *= tuning.strikeSpeed;
            settings.fullPowerSpeed *= tuning.strikeSpeed;
            settings.contactDistance *= tuning.contactDistance;
            settings.lineCrossMaxOffset *= tuning.contactDistance;
            settings.armDistance = Mathf.Max(settings.armDistance, settings.contactDistance + settings.minTravel);
            return settings;
        }

        /// <summary>Aim OneEuro min cutoff with the live tuning applied (a higher smoothing scale filters harder).</summary>
        public float AimMinCutoffFor(in ControlTuning tuning) => aimMinCutoff / tuning.aimSmoothing;

        /// <summary>Left-paw ball range (inches from the chest; mirror it for the right paw) scaled around its centre by tuning.launchRange.</summary>
        public void LaunchRangeFor(in ControlTuning tuning, out float minInches, out float maxInches)
        {
            var center = (launchXMinInches + launchXMaxInches) * 0.5f;
            var halfRange = (launchXMaxInches - launchXMinInches) * 0.5f * tuning.launchRange;
            minInches = center - halfRange;
            maxInches = center + halfRange;
        }

        #endregion
    }
}
