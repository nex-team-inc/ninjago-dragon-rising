#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Pure strike detection on raw paw samples (TDD §6, GDD §3.1, GDD v2 §2). A strike starts when the cue paw moves
    /// toward the ball paw at ≥ strikeSpeed, heading within angleToleranceDeg of it; the approach direction is then
    /// fixed. It fires when the paws come within contactDistance, or when the cue paw passes the ball paw's line
    /// (through the ball paw, across the approach direction) at most lineCrossMaxOffset beside it. Positions are
    /// body-relative inches (chest origin), so leaning or stepping moves both paws and never fires. The speed is the
    /// smaller of the cue paw's own speed toward the ball paw and the speed relative to the ball paw: moving the ball
    /// paw into the cue, or both paws together, is not a strike. Contact is tested on the swept relative segment so a
    /// 30 Hz sample that jumps past the ball paw still counts. Allocation-free. Readout reports live values and why a
    /// move did not fire (control readout); that bookkeeping never changes what fires.
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
        Vector2 approachAxis;
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

            // rel = cue paw relative to the ball paw; toward = unit direction from the cue paw to the ball paw.
            var dt = (float)(time - previousTime);
            var previousRel = previousCue - previousBall;
            var rel = cue - ball;
            var previousDistance = previousRel.magnitude;
            var distance = rel.magnitude;
            var toward = previousDistance > MinDirectionLength ? -previousRel / previousDistance : Vector2.zero;
            var cueVelocity = (cue - previousCue) / dt;
            var relVelocity = (rel - previousRel) / dt;
            var closingSpeed = ClosingSpeed(cueVelocity, relVelocity, toward);
            var sweptDistance = DistanceToSegment(previousRel, rel);
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
                    if (closingSpeed < settings.strikeSpeed || !HeadsToward(cueVelocity, toward, settings.angleToleranceDeg))
                    {
                        if (contactEntered && time - lastMissTime > MissRepeatSeconds)
                        {
                            ReportMiss(StrikeMiss.TooSlow, time, recentPeak, sweptDistance);
                        }
                        return false;
                    }
                    approachStartTime = previousSampleTime;
                    approachStartDistance = previousDistance;
                    approachAxis = toward;
                    peakSpeed = closingSpeed;
                    approachClosest = previousDistance;
                    approachMissed = false;
                    SetState(StrikeState.Approaching, previousSampleTime);
                    return TryLand(time, previousRel, rel, sweptDistance, out result);
                case StrikeState.Approaching:
                    var axisSpeed = ClosingSpeed(cueVelocity, relVelocity, approachAxis);
                    peakSpeed = Mathf.Max(peakSpeed, axisSpeed);
                    if (TryLand(time, previousRel, rel, sweptDistance, out result)) return true;
                    var stalled = axisSpeed < settings.strikeSpeed * settings.sustainSpeedFraction;
                    // Past the ball paw's line without landing: it went by too far beside the ball paw.
                    var passedWide = Vector2.Dot(rel, approachAxis) >= 0f;
                    if (stalled || passedWide || time - approachStartTime > settings.maxStrikeSeconds)
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

        /// <summary>Lands on contact (swept distance) or on passing the ball paw's line close enough beside it.</summary>
        bool TryLand(double time, Vector2 previousRel, Vector2 rel, float sweptDistance, out StrikeResult result)
        {
            result = default;
            var settings = Settings;
            approachClosest = Mathf.Min(approachClosest, sweptDistance);
            var byLineCross = false;
            if (sweptDistance <= settings.contactDistance)
            {
                if (approachStartDistance - sweptDistance < settings.minTravel)
                {
                    if (!approachMissed)
                    {
                        approachMissed = true;
                        ReportMiss(StrikeMiss.ShortThrust, time, peakSpeed, sweptDistance);
                    }
                    return false;
                }
            }
            else
            {
                // Along the fixed axis the cue paw travelled approachStartDistance by the time it reaches the line.
                if (!CrossesLine(previousRel, rel, approachAxis, out var offset)) return false;
                if (offset > settings.lineCrossMaxOffset || approachStartDistance < settings.minTravel) return false;
                byLineCross = true;
            }

            result = new StrikeResult
            {
                startTime = approachStartTime,
                time = time,
                peakSpeed = peakSpeed,
                power01 = Mathf.InverseLerp(settings.strikeSpeed, settings.fullPowerSpeed, peakSpeed),
                isPowerShot = peakSpeed >= settings.strikeSpeed * settings.powerMultiplier,
                byLineCross = byLineCross,
            };
            readout.strikeCount++;
            readout.lastStrikeSpeed = peakSpeed;
            readout.lastStrikeWasPower = result.isPowerShot;
            readout.lastStrikeByLineCross = byLineCross;
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

        /// <summary>The smaller of the cue paw's own and its relative speed along `direction` (toward the ball paw).</summary>
        static float ClosingSpeed(Vector2 cueVelocity, Vector2 relVelocity, Vector2 direction)
        {
            // relVelocity is the cue relative to the ball paw, so closing on it means moving along +direction.
            return Mathf.Min(Vector2.Dot(cueVelocity, direction), Vector2.Dot(relVelocity, direction));
        }

        /// <summary>The cue paw's motion points within toleranceDeg of `toward`.</summary>
        static bool HeadsToward(Vector2 cueVelocity, Vector2 toward, float toleranceDeg)
        {
            var speed = cueVelocity.magnitude;
            if (speed < MinDirectionLength) return false;
            return Vector2.Dot(cueVelocity, toward) >= speed * Mathf.Cos(Mathf.Clamp(toleranceDeg, 0f, 90f) * Mathf.Deg2Rad);
        }

        /// <summary>
        /// The relative segment passes the ball paw's line (through the origin, perpendicular to `axis`) in the
        /// direction of `axis`; offset is how far beside the ball paw it passes.
        /// </summary>
        static bool CrossesLine(Vector2 previousRel, Vector2 rel, Vector2 axis, out float offset)
        {
            offset = 0f;
            var before = Vector2.Dot(previousRel, axis);
            var after = Vector2.Dot(rel, axis);
            if (before >= 0f || after < 0f) return false;
            var t = -before / (after - before);
            offset = Vector2.Lerp(previousRel, rel, t).magnitude;
            return true;
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
