#nullable enable

namespace Nex.BilliardRogue
{
    /// <summary>Strike thresholds in body-normalized inches and seconds (built from ControlConfig).</summary>
    public struct StrikeSettings
    {
        /// <summary>Speed (in/s) toward the ball paw that starts a strike (cue paw's own and relative to the ball paw).</summary>
        public float strikeSpeed;
        /// <summary>Fraction of strikeSpeed below which an approach counts as stalled and is dropped.</summary>
        public float sustainSpeedFraction;
        /// <summary>Paw distance at which a fast approach fires.</summary>
        public float contactDistance;
        /// <summary>Max angle (degrees) between the cue paw's motion and the direction to the ball paw that starts a strike.</summary>
        public float angleToleranceDeg;
        /// <summary>
        /// A fast approach that passes the ball paw's line (through the ball paw, across the approach direction)
        /// fires when it passes at most this far beside the ball paw; 0 = contact only.
        /// </summary>
        public float lineCrossMaxOffset;
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
        /// <summary>Fired by passing the ball paw's line rather than by contact.</summary>
        public bool byLineCross;
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
        /// <summary>A fast approach stalled, timed out or passed too far beside the ball paw.</summary>
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
}
