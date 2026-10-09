#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Kicks from one player's knees against their hips: the hip-to-knee drop shrinks when a knee rises (up or toward
    /// the camera). A kick is a lift of a set fraction of that leg's resting drop, reached in time, while the other knee
    /// stays down, then a release; holding the knee up stays one kick, and a pulse inside the cooldown after an
    /// accepted kick is a rejected double count. Fractions of each leg's own drop scale with the player's size.
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
            /// <summary>Fraction of the resting hip-to-knee drop the knee must rise (0.75 = three quarters of the way to the hip).</summary>
            public readonly float liftRatio;
            public readonly float releaseRatio;
            public readonly float riseMaxSeconds;
            public readonly float cooldownSeconds;
            public readonly float restAdaptSeconds;

            public Settings(float liftRatio, float releaseRatio, float riseMaxSeconds, float cooldownSeconds, float restAdaptSeconds)
            {
                this.liftRatio = liftRatio;
                this.releaseRatio = releaseRatio;
                this.riseMaxSeconds = riseMaxSeconds;
                this.cooldownSeconds = cooldownSeconds;
                this.restAdaptSeconds = restAdaptSeconds;
            }

            public float LiftFor(float restingDrop) => restingDrop * liftRatio;

            public float ReleaseFor(float restingDrop) => LiftFor(restingDrop) * releaseRatio;
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
            var pulse = (leftPulse && IsDown(right, settings)) || (rightPulse && IsDown(left, settings));
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

        static bool IsDown(in Leg leg, in Settings settings) => leg.lift < settings.ReleaseFor(leg.baseline);

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
            var release = settings.ReleaseFor(leg.baseline);
            if (leg.raised)
            {
                if (leg.lift > release) return false;
                leg.raised = false;
                leg.restTime = time;
                return false;
            }

            if (leg.lift >= settings.LiftFor(leg.baseline))
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
