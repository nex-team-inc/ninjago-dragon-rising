#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One cursor's slash test against a round target in screen pixels: the cursor enters the target from outside and
    /// leaves it within the crossing time, and its speed peaks above the threshold during the crossing. A cursor that
    /// rests on the target, lingers in it or moves slowly never slashes.
    /// </summary>
    public sealed class SlashDetector
    {
        bool hasLast;
        Vector2 last;
        int continuity;
        bool crossing;
        float enterTime;
        float peakSpeed;

        /// <summary>The last frame's cursor movement in pixels (the slash direction on the frame it fires).</summary>
        public Vector2 LastStep { get; private set; }

        #region Public API

        public void Reset()
        {
            hasLast = false;
            crossing = false;
        }

        /// <param name="cursorContinuity">HandCursor.Continuity; a change (reappeared, new signal) restarts the test.</param>
        /// <returns>True on the frame the cursor leaves the target after a fast crossing.</returns>
        public bool Update(Vector2 position, float speed, int cursorContinuity, Vector2 center, float radius, float time, float speedThreshold,
            float maxCrossSeconds)
        {
            if (!hasLast || cursorContinuity != continuity)
            {
                hasLast = true;
                continuity = cursorContinuity;
                last = position;
                crossing = false;
                LastStep = Vector2.zero;
                return false;
            }

            var previous = last;
            last = position;
            LastStep = position - previous;
            var wasInside = (previous - center).magnitude <= radius;
            var isInside = (position - center).magnitude <= radius;
            // Entered from outside this frame, or passed straight through between two samples.
            if (!wasInside && (isInside || DistanceToSegment(center, previous, position) <= radius))
            {
                crossing = true;
                enterTime = time;
                peakSpeed = 0f;
            }

            if (!crossing) return false;
            peakSpeed = Mathf.Max(peakSpeed, speed);
            if (isInside)
            {
                if (time - enterTime > maxCrossSeconds) crossing = false;
                return false;
            }

            crossing = false;
            return peakSpeed >= speedThreshold;
        }

        #endregion

        #region Helpers

        static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSquared = ab.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon) return (point - a).magnitude;
            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
            return (point - (a + ab * t)).magnitude;
        }

        #endregion
    }
}
