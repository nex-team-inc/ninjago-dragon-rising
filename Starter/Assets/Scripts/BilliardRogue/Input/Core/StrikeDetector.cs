#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Strike thresholds in body-normalized inches and seconds (built from ControlConfig).</summary>
    public struct StrikeSettings
    {
        /// <summary>Closing speed (in/s) that starts a strike; the strike must keep sustainSpeedFraction of it until contact.</summary>
        public float strikeSpeed;
        /// <summary>Fraction of strikeSpeed below which an approach counts as stalled and is dropped.</summary>
        public float sustainSpeedFraction;
        /// <summary>Paw distance at which a fast approach fires.</summary>
        public float contactDistance;
        /// <summary>Paws must be at least this far apart before a strike can arm (after a reset or a strike).</summary>
        public float armDistance;
        /// <summary>Minimum time between a strike and the next arm.</summary>
        public float rearmSeconds;
        /// <summary>Peak speed ≥ strikeSpeed × this is a power strike.</summary>
        public float powerMultiplier;
        /// <summary>Peak speed mapped to power 1.</summary>
        public float fullPowerSpeed;
        /// <summary>The paws must close by at least this much during the strike (filters camera jitter near contact).</summary>
        public float minTravel;
        /// <summary>An approach that has not reached contact after this long is dropped.</summary>
        public float maxStrikeSeconds;
        /// <summary>Samples further apart than this are a tracking gap: history resyncs, no velocity across it.</summary>
        public float maxSampleGapSeconds;
    }

    /// <summary>A detected strike. Times are the sample clock (camera frame time).</summary>
    public struct StrikeResult
    {
        /// <summary>Time of the last calm sample before the closing speed crossed the threshold.</summary>
        public double startTime;
        public double time;
        public float peakSpeed;
        public float power01;
        public bool isPowerShot;
    }

    public enum StrikeState
    {
        /// <summary>Waiting for the paws to separate by armDistance.</summary>
        Disarmed = 0,
        Armed = 1,
        /// <summary>The cue paw is closing fast; contact fires the strike.</summary>
        Approaching = 2,
        /// <summary>Just fired: waits rearmSeconds and armDistance before arming again.</summary>
        Cooldown = 3,
    }

    /// <summary>
    /// Pure strike detection on raw paw samples (TDD §6, GDD §3.1): the cue paw must close on the ball paw at
    /// ≥ strikeSpeed and reach contactDistance. Positions are body-relative inches (chest origin), so leaning or
    /// stepping moves both paws and never fires. The closing speed is the smaller of the cue paw's own speed toward
    /// the ball paw and the rate the paw distance shrinks: moving the ball paw into the cue, or both paws together,
    /// is not a strike. Contact is tested on the swept relative segment so a 30 Hz sample that jumps past the ball
    /// paw still counts. Allocation-free.
    /// </summary>
    public sealed class StrikeDetector
    {
        const float MinDirectionLength = 1e-3f;

        bool hasPrevious;
        double previousTime;
        Vector2 previousBall;
        Vector2 previousCue;
        double approachStartTime;
        float approachStartDistance;
        float peakSpeed;
        double stateTime;

        public StrikeDetector(StrikeSettings settings)
        {
            Settings = settings;
        }

        /// <summary>Thresholds; may be replaced at any time (live tuning).</summary>
        public StrikeSettings Settings { get; set; }

        public StrikeState State { get; private set; } = StrikeState.Disarmed;

        public bool IsApproaching => State == StrikeState.Approaching;

        /// <summary>Start time of the current approach (valid while IsApproaching).</summary>
        public double ApproachStartTime => approachStartTime;

        #region Public Methods

        /// <summary>
        /// Feeds one raw sample (ball paw and cue paw, inches) at a strictly increasing time; repeated or older times
        /// are ignored. Returns true exactly once per strike.
        /// </summary>
        public bool AddSample(double time, Vector2 ball, Vector2 cue, out StrikeResult result)
        {
            result = default;
            if (hasPrevious && time <= previousTime)
            {
                return false;
            }

            var settings = Settings;
            if (!hasPrevious || time - previousTime > settings.maxSampleGapSeconds)
            {
                if (State == StrikeState.Approaching)
                {
                    State = StrikeState.Armed;
                }
                Remember(time, ball, cue);
                return false;
            }

            var dt = (float)(time - previousTime);
            var previousOffset = previousBall - previousCue;
            var previousDistance = previousOffset.magnitude;
            var distance = (ball - cue).magnitude;
            var towardBall = previousDistance > MinDirectionLength ? previousOffset / previousDistance : Vector2.zero;
            var cueSpeed = Vector2.Dot((cue - previousCue) / dt, towardBall);
            var closingSpeed = Mathf.Min(cueSpeed, (previousDistance - distance) / dt);
            var sweptDistance = DistanceToSegment(previousCue - previousBall, cue - ball);
            var previousSampleTime = previousTime;
            Remember(time, ball, cue);

            switch (State)
            {
                case StrikeState.Disarmed:
                    if (distance >= settings.armDistance)
                    {
                        SetState(StrikeState.Armed, time);
                    }
                    return false;
                case StrikeState.Cooldown:
                    if (time - stateTime >= settings.rearmSeconds && distance >= settings.armDistance)
                    {
                        SetState(StrikeState.Armed, time);
                    }
                    return false;
                case StrikeState.Armed:
                    if (closingSpeed < settings.strikeSpeed) return false;
                    approachStartTime = previousSampleTime;
                    approachStartDistance = previousDistance;
                    peakSpeed = closingSpeed;
                    SetState(StrikeState.Approaching, previousSampleTime);
                    return TryLand(time, sweptDistance, out result);
                case StrikeState.Approaching:
                    peakSpeed = Mathf.Max(peakSpeed, closingSpeed);
                    if (TryLand(time, sweptDistance, out result)) return true;
                    var stalled = closingSpeed < settings.strikeSpeed * settings.sustainSpeedFraction;
                    if (stalled || time - approachStartTime > settings.maxStrikeSeconds)
                    {
                        SetState(StrikeState.Armed, time);
                    }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The sample at `time` is missing (paws not detected in that camera frame). A short dropout is bridged: the
        /// next sample measures its velocity across the gap (motion blur at peak thrust speed often loses one frame).
        /// Once the gap since the last sample exceeds maxSampleGapSeconds, it is a MarkGap.
        /// </summary>
        public void MarkMissingSample(double time)
        {
            if (hasPrevious && time - previousTime <= Settings.maxSampleGapSeconds) return;
            MarkGap();
        }

        /// <summary>Tracking gap (or a sample clock reset): drops the history and any approach, keeps arm/cooldown.</summary>
        public void MarkGap()
        {
            hasPrevious = false;
            if (State == StrikeState.Approaching)
            {
                State = StrikeState.Armed;
            }
        }

        /// <summary>Forgets everything; the paws must separate by armDistance before the next strike.</summary>
        public void Reset()
        {
            hasPrevious = false;
            State = StrikeState.Disarmed;
        }

        #endregion

        #region Helpers

        bool TryLand(double time, float sweptDistance, out StrikeResult result)
        {
            result = default;
            var settings = Settings;
            if (sweptDistance > settings.contactDistance) return false;
            if (approachStartDistance - sweptDistance < settings.minTravel) return false;

            result = new StrikeResult
            {
                startTime = approachStartTime,
                time = time,
                peakSpeed = peakSpeed,
                power01 = Mathf.InverseLerp(settings.strikeSpeed, settings.fullPowerSpeed, peakSpeed),
                isPowerShot = peakSpeed >= settings.strikeSpeed * settings.powerMultiplier,
            };
            SetState(StrikeState.Cooldown, time);
            return true;
        }

        void Remember(double time, Vector2 ball, Vector2 cue)
        {
            hasPrevious = true;
            previousTime = time;
            previousBall = ball;
            previousCue = cue;
        }

        void SetState(StrikeState state, double time)
        {
            State = state;
            stateTime = time;
        }

        /// <summary>Distance from the origin (ball paw) to the segment the cue paw swept relative to it.</summary>
        static float DistanceToSegment(Vector2 from, Vector2 to)
        {
            var segment = to - from;
            var lengthSq = segment.sqrMagnitude;
            if (lengthSq < MinDirectionLength * MinDirectionLength) return to.magnitude;
            var t = Mathf.Clamp01(-Vector2.Dot(from, segment) / lengthSq);
            return (from + segment * t).magnitude;
        }

        #endregion
    }
}
