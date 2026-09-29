#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.InputCore.Tests
{
    public class MotionEnergyFilterTests
    {
        const int Nodes = 11;
        const double FrameSeconds = 1.0 / 30.0;

        #region Helpers

        // The v2 ControlConfig defaults (GDD v2 §3).
        static MotionEnergySettings DefaultSettings() => new()
        {
            deadzone = 8f,
            fullSpeed = 30f,
            maxNodeSpeed = 200f,
            attackSeconds = 0.1f,
            releaseSeconds = 0.5f,
            minNodes = 4,
            maxSampleGapSeconds = 0.25f,
        };

        /// <summary>30 Hz driver: every node follows `motion(node, time)`; Tick runs once per sample.</summary>
        sealed class Body
        {
            public readonly MotionEnergyFilter filter = new(Nodes, DefaultSettings());
            public readonly Vector2[] positions = new Vector2[Nodes];
            public readonly bool[] detected = new bool[Nodes];
            public double time = 5.0;

            public Body()
            {
                for (var i = 0; i < Nodes; i++)
                {
                    positions[i] = new Vector2(i * 3f, i * 2f);
                    detected[i] = true;
                }
            }

            public void Run(int frames, System.Func<int, double, Vector2> offset)
            {
                for (var f = 0; f < frames; f++)
                {
                    time += FrameSeconds;
                    for (var i = 0; i < Nodes; i++)
                    {
                        positions[i] = new Vector2(i * 3f, i * 2f) + offset(i, time);
                    }
                    filter.AddSample(time, positions, detected);
                    filter.Tick((float)FrameSeconds);
                }
            }
        }

        /// <summary>Each node swings on a circle of `radius` at `hz`: speed = 2π·radius·hz in/s.</summary>
        static System.Func<int, double, Vector2> Swing(float radius, float hz)
        {
            return (i, t) =>
            {
                var phase = (float)(t * hz * 2.0 * System.Math.PI) + i;
                return radius * new Vector2(Mathf.Cos(phase), Mathf.Sin(phase));
            };
        }

        #endregion

        #region Tests

        [Test]
        public void StandingStillWithCameraJitterStaysAtZero()
        {
            var body = new Body();
            var random = new System.Random(3);
            // ±0.1 in per frame: at most ~6 in/s, under the 8 in/s deadzone.
            body.Run(60, (i, t) => new Vector2((float)random.NextDouble() * 0.2f - 0.1f, (float)random.NextDouble() * 0.2f - 0.1f));
            Assert.Less(body.filter.Energy01, 0.01f);
            Assert.AreEqual(Nodes, body.filter.MeasuredNodes);
        }

        [Test]
        public void VigorousWholeBodyMotionChargesFastToFull()
        {
            var body = new Body();
            // 2π·6·1.6 ≈ 60 in/s per node → 52 in/s above the deadzone, past fullSpeed.
            body.Run(9, Swing(6f, 1.6f));
            Assert.AreEqual(1f, body.filter.Target01, 1e-4f);
            Assert.Greater(body.filter.Energy01, 0.9f, "attack ~0.1 s: ~0.3 s of dancing is almost full");
        }

        [Test]
        public void ModerateArmWavingLandsMidRange()
        {
            var body = new Body();
            // Wrists (0, 1) and elbows (2, 3) wave at 60 / 35 in/s, the rest of the body stays still.
            body.Run(30, (i, t) =>
            {
                var radius = i < 2 ? 6f : i < 4 ? 3.5f : 0f;
                var phase = (float)(t * 1.6 * 2.0 * System.Math.PI);
                return radius * new Vector2(Mathf.Cos(phase), Mathf.Sin(phase));
            });
            Assert.That(body.filter.Energy01, Is.InRange(0.3f, 0.7f));
        }

        [Test]
        public void ReleaseIsSlowerThanAttack()
        {
            var body = new Body();
            body.Run(15, Swing(6f, 1.6f));
            var full = body.filter.Energy01;
            body.Run(3, (i, t) => Vector2.zero);
            Assert.Greater(body.filter.Energy01, 0.75f * full, "0.1 s after stopping the energy is still high");
            body.Run(60, (i, t) => Vector2.zero);
            Assert.Less(body.filter.Energy01, 0.05f);
        }

        [Test]
        public void OneMisdetectedFrameCannotMaxTheMeter()
        {
            var body = new Body();
            body.Run(10, (i, t) => Vector2.zero);
            // One node teleports 50 in for a single frame (1500 in/s), then snaps back.
            var glitchFrame = 0;
            body.Run(2, (i, t) => i == 5 && glitchFrame++ == 0 ? new Vector2(50f, 0f) : Vector2.zero);
            Assert.Less(body.filter.Energy01, 0.35f);
        }

        [Test]
        public void TooFewTrackedNodesMeasureNothing()
        {
            var body = new Body();
            for (var i = 3; i < Nodes; i++)
            {
                body.detected[i] = false;
            }
            body.Run(15, Swing(6f, 1.6f));
            Assert.AreEqual(0f, body.filter.Target01);
            Assert.AreEqual(0f, body.filter.Energy01);
            Assert.AreEqual(3, body.filter.MeasuredNodes);
        }

        [Test]
        public void GapLongerThanTheLimitMeasuresNoSpeedAcrossIt()
        {
            var body = new Body();
            body.Run(5, (i, t) => Vector2.zero);
            body.time += 0.5;
            body.Run(1, (i, t) => new Vector2(20f, 0f));
            Assert.AreEqual(0f, body.filter.Target01);
            body.Run(5, (i, t) => new Vector2(20f, 0f));
            Assert.AreEqual(0f, body.filter.Energy01, 1e-6f);
        }

        [Test]
        public void MarkNoBodyLetsTheEnergyFallToZero()
        {
            var body = new Body();
            body.Run(15, Swing(6f, 1.6f));
            body.filter.MarkNoBody();
            Assert.AreEqual(0f, body.filter.Target01);
            for (var i = 0; i < 90; i++)
            {
                body.filter.Tick((float)FrameSeconds);
            }
            Assert.Less(body.filter.Energy01, 0.01f);
        }

        [Test]
        public void SmoothUsesAttackRisingAndReleaseFalling()
        {
            var up = MotionEnergyFilter.Smooth(0f, 1f, 0.1f, 0.1f, 0.5f);
            var down = MotionEnergyFilter.Smooth(1f, 0f, 0.1f, 0.1f, 0.5f);
            Assert.AreEqual(1f - Mathf.Exp(-1f), up, 1e-5f);
            Assert.AreEqual(Mathf.Exp(-0.2f), down, 1e-5f);
            Assert.AreEqual(1f, MotionEnergyFilter.Smooth(0f, 1f, 0.1f, 0f, 0.5f), "a zero time constant snaps");
        }

        #endregion
    }
}
