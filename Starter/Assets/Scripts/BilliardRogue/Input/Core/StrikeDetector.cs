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

    /// <summary>Why the last paw move did not fire (control readout only; detection never reads it).</summary>
    public enum StrikeMiss
    {
        None = 0,
        /// <summary>The paws met while armed, but the closing speed stayed under strikeSpeed.</summary>
        TooSlow = 1,
        /// <summary>A fast approach stalled or timed out before the paws reached contactDistance.</summary>
        TooFar = 2,
        /// <summary>Contact during a fast approach, but the paws closed by less than minTravel.</summary>
        ShortThrust = 3,
        /// <summary>A fast contact within rearmSeconds of the last strike.</summary>
        Cooldown = 4,
        /// <summary>A fast contact before the paws opened to armDistance (after a strike, a reset or the cooldown).</summary>
        NotArmed = 5,
    }

    /// <summary>StrikeDetector state for the control readout (debug display; detection never reads it).</summary>
    public struct StrikeReadout
    {
        public StrikeState state;
        /// <summary>The thresholds in use (live tuning included).</summary>
        public StrikeSettings settings;
        /// <summary>Closing speed of the last sample pair (in/s); 0 right after a gap.</summary>
        public float closingSpeed;
        /// <summary>Paw distance of the last sample (inches).</summary>
        public float pawDistance;
        /// <summary>Seconds (sample clock) until the cooldown allows arming again; 0 outside Cooldown.</summary>
        public float cooldownRemaining;
        public int strikeCount;
        public float lastStrikeSpeed;
        public bool lastStrikeWasPower;
        /// <summary>Incremented per reported miss; lastMiss* describe the latest one.</summary>
        public int missCount;
        public StrikeMiss lastMiss;
        /// <summary>Peak closing speed of the missed move (in/s).</summary>
        public float lastMissSpeed;
        /// <summary>Closest paw distance of the missed move (inches).</summary>
        public float lastMissDistance;
    }

    /// <summary>
    /// Pure strike detection on raw paw samples (TDD §6, GDD §3.1): the cue paw must close on the ball paw at
    /// ≥ strikeSpeed and reach contactDistance. Positions are body-relative inches (chest origin), so leaning or
    /// stepping moves both paws and never fires. The closing speed is the smaller of the cue paw's own speed toward
    /// the ball paw and the rate the paw distance shrinks: moving the ball paw into the cue, or both paws together,
    /// is not a strike. Contact is tested on the swept relative segment so a 30 Hz sample that jumps past the ball
    /// paw still counts. Allocation-free. Readout reports live values and why a move did not fire (control readout);
    /// that bookkeeping never changes what fires.
    /// </summary>
    public sealed class StrikeDetector
    {
        const float MinDirectionLength = 1e-3f;
        // Readout only: a contact is latched until the paws open past this multiple of contactDistance, the recent
        // peak speed spans this window, and a miss this soon after the previous one is not reported again.
        const float ContactReleaseFactor = 1.5f;
        const double RecentPeakSeconds = 0.3;
        const double MissRepeatSeconds = 0.4;

        bool hasPrevious;
        double previousTime;
        Vector2 previousBall;
        Vector2 previousCue;
        double approachStartTime;
        float approachStartDistance;
        float peakSpeed;
        double stateTime;
        float approachClosest;
        bool approachMissed;
        bool contactLatched;
        float recentPeak;
        double recentPeakTime;
        double lastMissTime = double.NegativeInfinity;
        StrikeReadout readout;

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

        /// <summary>Live values and the last strike / miss for the control readout (a copy; zero allocation).</summary>
        public StrikeReadout Readout
        {
            get
            {
                var copy = readout;
                copy.state = State;
                copy.settings = Settings;
                copy.cooldownRemaining = State == StrikeState.Cooldown
                    ? Mathf.Max(0f, Settings.rearmSeconds - (float)(previousTime - stateTime))
                    : 0f;
                return copy;
            }
        }

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
                readout.closingSpeed = 0f;
                readout.pawDistance = (ball - cue).magnitude;
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
            var contactEntered = Observe(time, closingSpeed, distance, sweptDistance, settings);

            switch (State)
            {
                case StrikeState.Disarmed:
                    if (contactEntered && recentPeak >= settings.strikeSpeed)
                    {
                        ReportMiss(StrikeMiss.NotArmed, time, recentPeak, sweptDistance);
                    }
                    if (distance >= settings.armDistance)
                    {
                        SetState(StrikeState.Armed, time);
                    }
                    return false;
                case StrikeState.Cooldown:
                    if (contactEntered && recentPeak >= settings.strikeSpeed)
                    {
                        // Past rearmSeconds the cooldown only waits for the paws to open to armDistance.
                        var miss = time - stateTime >= settings.rearmSeconds ? StrikeMiss.NotArmed : StrikeMiss.Cooldown;
                        ReportMiss(miss, time, recentPeak, sweptDistance);
                    }
                    if (time - stateTime >= settings.rearmSeconds && distance >= settings.armDistance)
                    {
                        SetState(StrikeState.Armed, time);
                    }
                    return false;
                case StrikeState.Armed:
                    if (closingSpeed < settings.strikeSpeed)
                    {
                        if (contactEntered && time - lastMissTime > MissRepeatSeconds)
                        {
                            ReportMiss(StrikeMiss.TooSlow, time, recentPeak, sweptDistance);
                        }
                        return false;
                    }
                    approachStartTime = previousSampleTime;
                    approachStartDistance = previousDistance;
                    peakSpeed = closingSpeed;
                    approachClosest = previousDistance;
                    approachMissed = false;
                    SetState(StrikeState.Approaching, previousSampleTime);
                    return TryLand(time, sweptDistance, out result);
                case StrikeState.Approaching:
                    peakSpeed = Mathf.Max(peakSpeed, closingSpeed);
                    if (TryLand(time, sweptDistance, out result)) return true;
                    var stalled = closingSpeed < settings.strikeSpeed * settings.sustainSpeedFraction;
                    if (stalled || time - approachStartTime > settings.maxStrikeSeconds)
                    {
                        if (!approachMissed)
                        {
                            ReportMiss(StrikeMiss.TooFar, time, peakSpeed, approachClosest);
                        }
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
            approachClosest = Mathf.Min(approachClosest, sweptDistance);
            if (sweptDistance > settings.contactDistance) return false;
            if (approachStartDistance - sweptDistance < settings.minTravel)
            {
                if (!approachMissed)
                {
                    approachMissed = true;
                    ReportMiss(StrikeMiss.ShortThrust, time, peakSpeed, sweptDistance);
                }
                return false;
            }

            result = new StrikeResult
            {
                startTime = approachStartTime,
                time = time,
                peakSpeed = peakSpeed,
                power01 = Mathf.InverseLerp(settings.strikeSpeed, settings.fullPowerSpeed, peakSpeed),
                isPowerShot = peakSpeed >= settings.strikeSpeed * settings.powerMultiplier,
            };
            readout.strikeCount++;
            readout.lastStrikeSpeed = peakSpeed;
            readout.lastStrikeWasPower = result.isPowerShot;
            SetState(StrikeState.Cooldown, time);
            return true;
        }

        /// <summary>Readout bookkeeping for one sample pair; true when the paws just came into contact.</summary>
        bool Observe(double time, float closingSpeed, float distance, float sweptDistance, in StrikeSettings settings)
        {
            readout.closingSpeed = closingSpeed;
            readout.pawDistance = distance;
            if (closingSpeed >= recentPeak || time - recentPeakTime > RecentPeakSeconds)
            {
                recentPeak = closingSpeed;
                recentPeakTime = time;
            }

            if (contactLatched)
            {
                if (distance > settings.contactDistance * ContactReleaseFactor)
                {
                    contactLatched = false;
                }
                return false;
            }

            if (sweptDistance > settings.contactDistance) return false;
            contactLatched = true;
            return true;
        }

        void ReportMiss(StrikeMiss miss, double time, float speed, float closestDistance)
        {
            readout.missCount++;
            readout.lastMiss = miss;
            readout.lastMissSpeed = speed;
            readout.lastMissDistance = closestDistance;
            lastMissTime = time;
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
