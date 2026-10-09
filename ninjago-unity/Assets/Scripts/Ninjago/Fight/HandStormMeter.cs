#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The fight-back signal: sums how far both hands travel and records the fastest hand speed. It fills when the
    /// travel reaches the target and the hands got fast at least once. No pose, no target to touch.
    /// </summary>
    public sealed class HandStormMeter
    {
        public enum Source
        {
            HandDetector,
            BodyNode,
            Simulated,
        }

        struct Track
        {
            public bool hasAnchor;
            public Vector2 anchor;
            public double anchorTime;
            public Source source;
        }

        readonly Track[] tracks = new Track[2];
        float requiredTravelInches;
        float speedPeakInchesPerSecond;
        float noiseFloorInches;
        float maxGapSeconds;

        public float TravelInches { get; private set; }
        public float PeakSpeed { get; private set; }
        public float Fill01 => requiredTravelInches <= 0f ? 1f : Mathf.Clamp01(TravelInches / requiredTravelInches);
        public bool IsFilled => TravelInches >= requiredTravelInches && PeakSpeed >= speedPeakInchesPerSecond;

        #region Public API

        public void Begin(float aRequiredTravelInches, float aSpeedPeakInchesPerSecond, float aNoiseFloorInches, float aMaxGapSeconds)
        {
            requiredTravelInches = aRequiredTravelInches;
            speedPeakInchesPerSecond = aSpeedPeakInchesPerSecond;
            noiseFloorInches = aNoiseFloorInches;
            maxGapSeconds = aMaxGapSeconds;
            TravelInches = 0f;
            PeakSpeed = 0f;
            tracks[0] = default;
            tracks[1] = default;
        }

        /// <param name="hand">0 = left, 1 = right.</param>
        /// <param name="inches">Hand position relative to the chest, in body inches.</param>
        /// <param name="time">Capture time in seconds; samples that are not newer are ignored.</param>
        public void AddSample(int hand, Vector2 inches, double time, Source source)
        {
            ref var track = ref tracks[hand];
            // A new source, a gap, or the first sample only re-anchors: positions from different sources do not line up.
            if (!track.hasAnchor || track.source != source || time - track.anchorTime > maxGapSeconds)
            {
                Anchor(ref track, inches, time, source);
                return;
            }

            if (time <= track.anchorTime) return;
            var step = Vector2.Distance(inches, track.anchor);
            // Jitter keeps the old anchor, so slow real motion still adds up once it clears the floor.
            if (step < noiseFloorInches) return;

            TravelInches += step;
            PeakSpeed = Mathf.Max(PeakSpeed, step / (float)(time - track.anchorTime));
            Anchor(ref track, inches, time, source);
        }

        #endregion

        #region Helpers

        static void Anchor(ref Track track, Vector2 inches, double time, Source source)
        {
            track.hasAnchor = true;
            track.anchor = inches;
            track.anchorTime = time;
            track.source = source;
        }

        #endregion
    }
}
