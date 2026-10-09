#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Kicks from one player's knees against their hips: the hip-to-knee drop shrinks when a knee rises (up or toward
    /// the camera). A kick is a fast lift past the threshold while the other knee stays down, then a release; holding
    /// the knee up stays one kick, and a pulse inside the cooldown after an accepted kick is a rejected double count.
    /// </summary>
    public sealed class KickDetector
    {
        public enum Pulse
        {
            None,
            Kick,
            Blocked,
        }

        public readonly struct Settings
        {
            public readonly float liftInches;
            public readonly float releaseRatio;
            public readonly float riseMaxSeconds;
            public readonly float cooldownSeconds;
            public readonly float restAdaptSeconds;

            public Settings(float liftInches, float releaseRatio, float riseMaxSeconds, float cooldownSeconds, float restAdaptSeconds)
            {
                this.liftInches = liftInches;
                this.releaseRatio = releaseRatio;
                this.riseMaxSeconds = riseMaxSeconds;
                this.cooldownSeconds = cooldownSeconds;
                this.restAdaptSeconds = restAdaptSeconds;
            }

            public float ReleaseInches => liftInches * releaseRatio;
        }

        struct Leg
        {
            public bool hasBaseline;
            public bool started;
            public float baseline;
            public bool raised;
            public float restTime;
            public float lastTime;
            public float lift;
        }

        Leg left;
        Leg right;
        float lastKickTime = float.NegativeInfinity;

        #region Public API

        /// <param name="restingDrops">Calibrated standing drops (x left, y right); null starts from the first sample.</param>
        public void Begin(Vector2? restingDrops)
        {
            left = default;
            right = default;
            lastKickTime = float.NegativeInfinity;
            if (!restingDrops.HasValue) return;
            left.hasBaseline = right.hasBaseline = true;
            left.baseline = restingDrops.Value.x;
            right.baseline = restingDrops.Value.y;
        }

        /// <param name="drops">Current hip-to-knee drops in body inches (x left, y right).</param>
        public Pulse Update(Vector2 drops, float time, in Settings settings)
        {
            var leftPulse = UpdateLeg(ref left, drops.x, time, settings);
            var rightPulse = UpdateLeg(ref right, drops.y, time, settings);
            // A kick stands on the other leg: both knees rising together is a squat or a hop.
            var release = settings.ReleaseInches;
            var pulse = (leftPulse && right.lift < release) || (rightPulse && left.lift < release);
            return pulse ? Accept(time, settings.cooldownSeconds) : Pulse.None;
        }

        /// <summary>A pulse from outside the knee reading (the simulated body); only the cooldown applies.</summary>
        public Pulse External(float time, float cooldownSeconds) => Accept(time, cooldownSeconds);

        #endregion

        #region Helpers

        Pulse Accept(float time, float cooldownSeconds)
        {
            if (time - lastKickTime < cooldownSeconds) return Pulse.Blocked;
            lastKickTime = time;
            return Pulse.Kick;
        }

        // True on the frame this knee completes a fast lift.
        static bool UpdateLeg(ref Leg leg, float drop, float time, in Settings settings)
        {
            if (!leg.hasBaseline)
            {
                leg.hasBaseline = true;
                leg.baseline = drop;
            }

            if (!leg.started)
            {
                leg.started = true;
                leg.restTime = time;
                leg.lastTime = time;
            }

            var deltaTime = time - leg.lastTime;
            leg.lastTime = time;
            leg.lift = leg.baseline - drop;
            var release = settings.ReleaseInches;
            if (leg.raised)
            {
                if (leg.lift > release) return false;
                leg.raised = false;
                leg.restTime = time;
                return false;
            }

            if (leg.lift >= settings.liftInches)
            {
                leg.raised = true;
                return time - leg.restTime <= settings.riseMaxSeconds;
            }

            if (leg.lift <= release)
            {
                leg.restTime = time;
                leg.baseline = Mathf.Lerp(leg.baseline, drop, 1f - Mathf.Exp(-deltaTime / settings.restAdaptSeconds));
            }

            return false;
        }

        #endregion
    }
}
