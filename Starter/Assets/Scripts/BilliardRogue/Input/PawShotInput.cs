#nullable enable

using Jazz;
using Nex.BilliardRogue.Simulation;
using Nex.Utils;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Body input for one player (GDD §3.1, TDD §6 / D6). The ball paw (left, or right when left-handed) sets the
    /// launch position from its x offset to the chest; the cue→ball paw vector is the aim. Both come from the
    /// engine's smoothed nodes, filtered again with OneEuro. Strikes come from the raw nodes, sampled once per camera
    /// frame (original.frameTime changes, ~30 Hz) and fed to StrikeDetector; the strike direction is the aim from
    /// aimSampleDelaySeconds before the thrust started. Units are body-normalized inches, so height and distance to
    /// the camera do not matter. Allocation-free per frame.
    /// </summary>
    public sealed class PawShotInput : MonoBehaviour, IShotInput
    {
        const int AimHistoryCapacity = 32;

        readonly AimHistory aimHistory = new(AimHistoryCapacity);
        OnePlayerDetectionEngine engine = null!;
        ControlConfig config = null!;
        ArenaRules arena = null!;
        StrikeDetector detector = null!;
        OneEuroFilter launchFilter = null!;
        OneEuroFilter aimFilterX = null!;
        OneEuroFilter aimFilterY = null!;
        bool leftHanded;
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
        /// <summary>Held at the pre-strike aim while a strike is in progress so the guide does not wobble.</summary>
        public Vector2 AimDirection => detector.IsApproaching ? heldAim : liveAim;
        public bool LeftHanded => leftHanded;
        /// <summary>Strike detector state (debug display).</summary>
        public StrikeState StrikeState => detector.State;

        #region Life Cycle

        public void Initialize(int playerIndex, OnePlayerDetectionEngine aEngine, ControlConfig aConfig, ArenaRules aArena, bool aLeftHanded)
        {
            PlayerIndex = playerIndex;
            engine = aEngine;
            config = aConfig;
            arena = aArena;
            leftHanded = aLeftHanded;
            detector = new StrikeDetector(config.StrikeSettings);
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
            return Time.realtimeSinceStartup - pendingStrikeTime <= config.StrikeExpirySeconds;
        }

        public void ResetStrike()
        {
            hasPendingStrike = false;
            detector.Reset();
        }

        #endregion

        #region Public Methods

        /// <summary>Swaps the ball and cue paws (PlayerPreference.leftHandedCue changed).</summary>
        public void SetLeftHanded(bool value)
        {
            if (value == leftHanded) return;
            leftHanded = value;
            aimHistory.Clear();
            ResetFilters();
            ResetStrike();
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
                rawPoseDetected = ArePawsDetected(result.original);
            }

            if (rawPoseDetected && TrySamplePaws(true, out var ball, out var cue))
            {
                // A new tracking segment starts from scratch instead of easing in from the pose before the gap.
                if (now - lastPoseTime > config.TrackingDropSeconds)
                {
                    ResetFilters();
                }
                lastPoseTime = now;
                UpdateLaunch(ball.x, now);
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
            detector.Settings = config.StrikeSettings;
            var wasApproaching = detector.IsApproaching;
            if (detector.AddSample(frameTime, ball, cue, out var result))
            {
                if (IsTracking)
                {
                    QueueStrike(result, now);
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

        /// <summary>Chest, elbows and wrists (the engine derives each paw from elbow + wrist) detected in this camera frame.</summary>
        bool ArePawsDetected(BodyPoseDetection detection)
        {
            var pose = detection.GetPlayerPose(PlayerIndex)?.bodyPose;
            if (pose == null) return false;
            return pose.Chest().isDetected && pose.LeftElbow().isDetected && pose.LeftWrist().isDetected
                   && pose.RightElbow().isDetected && pose.RightWrist().isDetected;
        }

        /// <summary>Ball and cue paw positions in inches relative to the chest (x = screen right = the player's right).</summary>
        bool TrySamplePaws(bool smoothed, out Vector2 ball, out Vector2 cue)
        {
            ball = default;
            cue = default;
            var chest = engine.GetNodePosition(PoseNodeIndex.Chest, smoothed);
            var left = engine.GetNodePosition(PoseNodeIndex.LeftHand, smoothed);
            var right = engine.GetNodePosition(PoseNodeIndex.RightHand, smoothed);
            if (chest == null || left == null || right == null)
            {
                return false;
            }

            var root = engine.transform;
            var perInch = 1f / engine.DistancePerInch;
            var leftInches = (Vector2)root.InverseTransformVector(left.Value - chest.Value) * perInch;
            var rightInches = (Vector2)root.InverseTransformVector(right.Value - chest.Value) * perInch;
            ball = leftHanded ? rightInches : leftInches;
            cue = leftHanded ? leftInches : rightInches;
            return true;
        }

        void UpdateLaunch(float ballX, float now)
        {
            var x = launchFilter.Filter(ballX, now);
            var min = leftHanded ? -config.LaunchXMaxInches : config.LaunchXMinInches;
            var max = leftHanded ? -config.LaunchXMinInches : config.LaunchXMaxInches;
            LaunchX01 = RemapUtils.RemapAndClamp(x, min, max, 0f, 1f);
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
            launchFilter = new OneEuroFilter(config.LaunchXMinCutoff, config.LaunchXBeta);
            aimFilterX = new OneEuroFilter(config.AimMinCutoff, config.AimBeta);
            aimFilterY = new OneEuroFilter(config.AimMinCutoff, config.AimBeta);
        }

        #endregion
    }
}
