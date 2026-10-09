#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One hand of one player as a screen cursor. The hand offset from the chest (body inches) is smoothed with a One
    /// Euro filter, then mapped through that player's reach box. Hit tests read ScreenPosition and the stroke speed.
    /// </summary>
    public sealed class HandCursor
    {
        public enum Source
        {
            HandDetector,
            BodyNode,
            Simulated,
        }

        // Short time constant: per-frame speed jumps when a 15-30 Hz hand sample lands on a 60 Hz frame.
        const float speedSmoothingSeconds = 0.05f;

        OneEuroFilter filterX = null!;
        OneEuroFilter filterY = null!;
        float filterMinCutoff = -1f;
        float filterBeta = -1f;
        Source? source;
        float lastUpdateTime;
        Vector2 velocity;

        public int PlayerIndex { get; }
        /// <summary>0 = the hand on the screen's left (the player's left in the mirrored frame), 1 = right.</summary>
        public int Hand { get; }
        public bool IsVisible { get; private set; }
        /// <summary>Full-screen normalized position (0..1, y up).</summary>
        public Vector2 ScreenPosition { get; private set; }
        /// <summary>Smoothed hand offset from the chest in body inches.</summary>
        public Vector2 Inches { get; private set; }
        public float SpeedInchesPerSecond => velocity.magnitude;
        public float LastSampleTime { get; private set; } = float.NegativeInfinity;
        /// <summary>Changes whenever the motion is not continuous (reappeared, or another signal source took over).</summary>
        public int Continuity { get; private set; }

        public HandCursor(int playerIndex, int hand)
        {
            PlayerIndex = playerIndex;
            Hand = hand;
        }

        #region Public API

        public void Track(Vector2 rawInches, Source sampleSource, float time, in ReachBox box, float minCutoffHz, float beta)
        {
            var restart = !IsVisible || source != sampleSource;
            if (restart || minCutoffHz != filterMinCutoff || beta != filterBeta)
            {
                filterMinCutoff = minCutoffHz;
                filterBeta = beta;
                filterX = new OneEuroFilter(minCutoffHz, beta);
                filterY = new OneEuroFilter(minCutoffHz, beta);
            }

            if (restart)
            {
                Continuity++;
                velocity = Vector2.zero;
                Inches = rawInches;
                lastUpdateTime = time;
            }

            source = sampleSource;
            var smoothed = new Vector2(filterX.Filter(rawInches.x, time), filterY.Filter(rawInches.y, time));
            var deltaTime = time - lastUpdateTime;
            if (deltaTime > 0f)
            {
                var instant = (smoothed - Inches) / deltaTime;
                velocity = Vector2.Lerp(velocity, instant, 1f - Mathf.Exp(-deltaTime / speedSmoothingSeconds));
            }

            Inches = smoothed;
            ScreenPosition = box.ToScreen(smoothed);
            lastUpdateTime = time;
            LastSampleTime = time;
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
            velocity = Vector2.zero;
        }

        #endregion
    }
}
