#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One cursor's slash test against a round target in screen pixels: a stroke that stays above the speed threshold,
    /// starts outside the target, passes through it and leaves it. A resting or slow cursor never slashes.
    /// </summary>
    public sealed class SlashDetector
    {
        bool hasLast;
        Vector2 last;
        int continuity;
        bool inStroke;
        bool startedOutside;
        bool touched;

        /// <summary>The last frame's cursor movement in pixels (the slash direction on the frame it fires).</summary>
        public Vector2 LastStep { get; private set; }

        #region Public API

        public void Reset()
        {
            hasLast = false;
            inStroke = false;
            touched = false;
        }

        /// <param name="cursorContinuity">HandCursor.Continuity; a change (reappeared, new signal) restarts the stroke.</param>
        /// <returns>True on the frame the stroke has crossed the target.</returns>
        public bool Update(Vector2 position, float speed, int cursorContinuity, Vector2 center, float radius, float speedThreshold)
        {
            if (!hasLast || cursorContinuity != continuity)
            {
                hasLast = true;
                continuity = cursorContinuity;
                last = position;
                inStroke = false;
                touched = false;
                LastStep = Vector2.zero;
                return false;
            }

            var previous = last;
            last = position;
            LastStep = position - previous;
            if (speed < speedThreshold)
            {
                inStroke = false;
                touched = false;
                return false;
            }

            if (!inStroke)
            {
                inStroke = true;
                startedOutside = (previous - center).magnitude > radius;
                touched = false;
            }

            if (!startedOutside) return false;
            touched |= DistanceToSegment(center, previous, position) <= radius;
            if (!touched || (position - center).magnitude <= radius) return false;
            inStroke = false;
            touched = false;
            return true;
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
