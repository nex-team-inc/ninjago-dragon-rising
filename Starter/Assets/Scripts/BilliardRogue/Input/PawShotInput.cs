#nullable enable

using System;
using Jazz;
using Nex.BilliardRogue.Simulation;
using Nex.Utils;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Body input for one player (GDD §3.1, TDD §6 / D6). The upper paw holds the ball and the lower paw is the cue
    /// (GDD v2 §16, PawRoles, decided on the raw camera frames); the ball paw sets the launch position from its x offset
    /// to the chest, the cue→ball paw vector is the aim. Both come from the
    /// engine's smoothed nodes, filtered again with OneEuro. Strikes come from the raw nodes, sampled once per camera
    /// frame (original.frameTime changes, ~30 Hz) and fed to StrikeDetector; the strike direction is the aim from
    /// aimSampleDelaySeconds before the thrust started. Units are body-normalized inches, so height and distance to
    /// the camera do not matter. Live tuning (ControlTuning, polled every frame) scales the config values; the
    /// readout properties feed the control lab overlay. Allocation-free per frame.
    /// </summary>
    public sealed class PawShotInput : MonoBehaviour, IShotInput
    {
        const int AimHistoryCapacity = 32;

        readonly AimHistory aimHistory = new(AimHistoryCapacity);
        OnePlayerDetectionEngine engine = null!;
        ControlConfig config = null!;
        ArenaRules arena = null!;
        Func<ControlTuning> tuningSource = null!;
        ControlTuning tuning = ControlTuning.Identity;
        StrikeDetector detector = null!;
        OneEuroFilter launchFilter = null!;
        OneEuroFilter launchYFilter = null!;
        OneEuroFilter aimFilterX = null!;
        OneEuroFilter aimFilterY = null!;
        PawRoles roles = null!;
        double lastRawFrameTime = double.NegativeInfinity;
        float lastPoseTime = float.NegativeInfinity;
        float lastRawFrameArrival = float.NegativeInfinity;
        float validSince;
        bool wasValid;
        bool rawPoseDetected;
        Vector2 liveAim = Vector2.up;
        Vector2 heldAim = Vector2.up;
        bool hasPendingStrike;
        StrikeInfo pendingStrike;
        float pendingStrikeTime;

        public int PlayerIndex { get; private set; }
        public bool IsTracking { get; private set; }
        public float LaunchX01 { get; private set; } = 0.5f;
        public float LaunchY01 { get; private set; } = 0.5f;
        /// <summary>Held at the pre-strike aim while a strike is in progress so the guide does not wobble.</summary>
        public Vector2 AimDirection => detector.IsApproaching ? heldAim : liveAim;
        /// <summary>The right paw holds the ball (it is the upper paw); the left paw is the cue.</summary>
        public bool BallIsRight => roles.BallIsRight;
        /// <summary>Strike detector state (debug display).</summary>
        public StrikeState StrikeState => detector.State;

        #region Readout

        /// <summary>Detector values, thresholds in use and the last strike / miss (control readout).</summary>
        public StrikeReadout StrikeReadout => detector.Readout;
        /// <summary>The newest camera frame showed the chest, both elbows and both wrists.</summary>
        public bool PawsDetected => rawPoseDetected;
        /// <summary>A camera frame arrived within ControlConfig.staleFrameSeconds.</summary>
        public bool HasCameraFrames => Time.realtimeSinceStartup - lastRawFrameArrival <= config.StaleFrameSeconds;
        /// <summary>Strikes the detector fired while tracking was not (yet) confirmed: never queued.</summary>
        public int UntrackedStrikeCount { get; private set; }
        /// <summary>Queued strikes dropped unfired (expired before a consumer took them, or reset).</summary>
        public int ExpiredStrikeCount { get; private set; }

        #endregion

        #region Life Cycle

        /// <summary>aTuning is polled every frame (ControlTuning.Identity keeps the asset values).</summary>
        public void Initialize(int playerIndex, OnePlayerDetectionEngine aEngine, ControlConfig aConfig, ArenaRules aArena, Func<ControlTuning> aTuning)
        {
            PlayerIndex = playerIndex;
            engine = aEngine;
            config = aConfig;
            arena = aArena;
            roles = new PawRoles(config.RoleSwapMarginInches, config.RoleSwapSeconds);
            tuningSource = aTuning;
            tuning = tuningSource();
            detector = new StrikeDetector(config.StrikeSettingsFor(tuning));
            ResetFilters();
            engine.NewDetectionCapturedAndProcessed += HandleDetection;
            enabled = true;
        }

        void OnDestroy()
        {
            engine.NewDetectionCapturedAndProcessed -= HandleDetection;
        }

        // Tracking is judged from the time of the last usable pose and camera frame, never by touching the engine
        // here: CameraSession may destroy it before this input.
        void Update()
        {
            ApplyTuning();
            var now = Time.realtimeSinceStartup;
            var valid = now - lastPoseTime <= config.TrackingDropSeconds && now - lastRawFrameArrival <= config.StaleFrameSeconds;
            if (valid && !wasValid)
            {
                validSince = now;
            }
            wasValid = valid;
            var tracking = valid && now - validSince >= config.TrackingAcquireSeconds;
            if (tracking == IsTracking) return;

            IsTracking = tracking;
            if (tracking)
            {
                ResetStrike();
            }
        }

        #endregion

        #region IShotInput

        public bool TryConsumeStrike(out StrikeInfo strike)
        {
            strike = pendingStrike;
            if (!hasPendingStrike) return false;

            hasPendingStrike = false;
            if (Time.realtimeSinceStartup - pendingStrikeTime <= config.StrikeExpirySeconds) return true;
            ExpiredStrikeCount++;
            return false;
        }

        public void ResetStrike()
        {
            if (hasPendingStrike)
            {
                ExpiredStrikeCount++;
            }
            hasPendingStrike = false;
            detector.Reset();
        }

        #endregion

        #region Detection

        // The smoother re-emits the last detection every Update; only a new camera frame is a new raw sample. Whether
        // the paws are really seen comes from that raw pose: the engine's node auto-hide runs on scaled time and
        // freezes while gameplay is paused (tracking-lost overlay). frameTime is wall-clock time (MDK
        // DetectionCoordinator), so any change is a new frame: a system clock stepped back restarts the sample clock
        // (detector and aim history resync) instead of freezing tracking until the clock passes the old value.
        void HandleDetection(BodyPoseDetectionResult result)
        {
            var now = Time.realtimeSinceStartup;
            var frameTime = result.original.frameTime;
            var newFrame = frameTime != lastRawFrameTime;
            if (newFrame)
            {
                if (frameTime < lastRawFrameTime)
                {
                    detector.MarkGap();
                    aimHistory.Clear();
                }

                lastRawFrameTime = frameTime;
                lastRawFrameArrival = now;
                rawPoseDetected = PawSampling.ArePawsDetected(result.original, PlayerIndex);
                if (rawPoseDetected) UpdateRoles(frameTime, now);
            }

            if (rawPoseDetected && TrySamplePaws(true, out var ball, out var cue))
            {
                // A new tracking segment starts from scratch instead of easing in from the pose before the gap.
                if (now - lastPoseTime > config.TrackingDropSeconds)
                {
                    ResetFilters();
                }
                lastPoseTime = now;
                UpdateLaunch(ball, now);
                UpdateAim(ball - cue, now);
            }

            if (newFrame)
            {
                SampleStrike(frameTime, now);
            }
        }

        void SampleStrike(double frameTime, float now)
        {
            if (!rawPoseDetected || !TrySamplePaws(false, out var ball, out var cue))
            {
                // One dropped frame mid-thrust is bridged; only a gap past maxSampleGapSeconds drops the approach.
                detector.MarkMissingSample(frameTime);
                return;
            }

            aimHistory.Add(frameTime, liveAim);
            var wasApproaching = detector.IsApproaching;
            if (detector.AddSample(frameTime, ball, cue, out var result))
            {
                if (IsTracking)
                {
                    QueueStrike(result, now);
                }
                else
                {
                    UntrackedStrikeCount++;
                }
                return;
            }

            if (!wasApproaching && detector.IsApproaching)
            {
                heldAim = AimBefore(detector.ApproachStartTime);
            }
        }

        void QueueStrike(StrikeResult result, float now)
        {
            pendingStrike = new StrikeInfo
            {
                direction = AimBefore(result.startTime),
                power01 = result.power01,
                isPowerShot = result.isPowerShot,
            };
            pendingStrikeTime = now;
            hasPendingStrike = true;
        }

        #endregion

        #region Helpers

        // Raw paws, once per camera frame. A new tracking segment gives the ball to the higher paw at once; a swap
        // later restarts the filters, the aim history and the strike (the paws' jobs changed under them).
        void UpdateRoles(double frameTime, float now)
        {
            if (!PawSampling.TrySampleHands(engine, false, out var left, out var right)) return;
            if (now - lastPoseTime > config.TrackingDropSeconds)
            {
                roles.Assign(left, right);
                return;
            }

            roles.SwapMarginInches = config.RoleSwapMarginInches;
            roles.SwapSeconds = config.RoleSwapSeconds;
            if (!roles.Update(frameTime, left, right, detector.IsApproaching)) return;
            aimHistory.Clear();
            ResetFilters();
            detector.Reset();
        }

        /// <summary>Ball and cue paw positions in inches relative to the chest (x = screen right = the player's right).</summary>
        bool TrySamplePaws(bool smoothed, out Vector2 ball, out Vector2 cue)
        {
            ball = default;
            cue = default;
            if (!PawSampling.TrySampleHands(engine, smoothed, out var leftInches, out var rightInches)) return false;
            ball = roles.Ball(leftInches, rightInches);
            cue = roles.Cue(leftInches, rightInches);
            return true;
        }

        void UpdateLaunch(Vector2 ball, float now)
        {
            var x = launchFilter.Filter(ball.x, now);
            config.LaunchRangeFor(tuning, out var rangeMin, out var rangeMax);
            var mirrored = roles.BallIsRight;
            var min = mirrored ? -rangeMax : rangeMin;
            var max = mirrored ? -rangeMin : rangeMax;
            LaunchX01 = RemapUtils.RemapAndClamp(x, min, max, 0f, 1f);
            // The ball paw's height nudges the launch point a little on y (GDD v2 §21); equal min/max turns it off.
            var y = launchYFilter.Filter(ball.y, now);
            LaunchY01 = Mathf.Approximately(config.LaunchYMaxInches, config.LaunchYMinInches)
                ? 0.5f
                : RemapUtils.RemapAndClamp(y, config.LaunchYMinInches, config.LaunchYMaxInches, 0f, 1f);
        }

        void UpdateAim(Vector2 cueToBall, float now)
        {
            var filtered = new Vector2(aimFilterX.Filter(cueToBall.x, now), aimFilterY.Filter(cueToBall.y, now));
            var length = filtered.magnitude;
            if (length < config.MinAimDistanceInches) return;
            liveAim = ArenaGeometry.ClampAim(arena, filtered / length);
        }

        Vector2 AimBefore(double strikeStartTime)
        {
            return aimHistory.TryGetAt(strikeStartTime - config.AimSampleDelaySeconds, out var aim) ? aim : liveAim;
        }

        void ResetFilters()
        {
            var aimCutoff = config.AimMinCutoffFor(tuning);
            launchFilter = new OneEuroFilter(config.LaunchXMinCutoff, config.LaunchXBeta);
            launchYFilter = new OneEuroFilter(config.LaunchXMinCutoff, config.LaunchXBeta);
            aimFilterX = new OneEuroFilter(aimCutoff, config.AimBeta);
            aimFilterY = new OneEuroFilter(aimCutoff, config.AimBeta);
        }

        // Every frame, so debug panel and Inspector edits apply at once (also before the first camera frame). The
        // OneEuro filters take their cutoff at construction: only an aim smoothing change rebuilds them (an
        // allocation on a debug panel edit only).
        void ApplyTuning()
        {
            var next = tuningSource();
            var smoothingChanged = !Mathf.Approximately(next.aimSmoothing, tuning.aimSmoothing);
            tuning = next;
            detector.Settings = config.StrikeSettingsFor(tuning);
            if (smoothingChanged)
            {
                ResetFilters();
            }
        }

        #endregion
    }
}
