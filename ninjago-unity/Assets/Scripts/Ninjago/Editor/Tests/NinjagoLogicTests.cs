#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Nex.Ninjago.Editor.Tests
{
    public class SlipDetectorTests
    {
        [Test]
        public void LeanTowardSafeSidePastThresholdSlips()
        {
            var detector = new SlipDetector();
            detector.Begin(SweepSide.Left, 4f);
            Assert.AreEqual(SlipDetector.Outcome.Pending, detector.Update(-2f));
            Assert.AreEqual(SlipDetector.Outcome.Slipped, detector.Update(-4.5f));
        }

        [Test]
        public void StandingStillStaysPending()
        {
            var detector = new SlipDetector();
            detector.Begin(SweepSide.Right, 4f);
            for (var i = 0; i < 30; i++) detector.Update(Mathf.Sin(i) * 1.5f);
            Assert.AreEqual(SlipDetector.Outcome.Pending, detector.Current);
        }

        [Test]
        public void WrongWayLocksEvenIfCorrectedLater()
        {
            var detector = new SlipDetector();
            detector.Begin(SweepSide.Right, 4f);
            Assert.AreEqual(SlipDetector.Outcome.WrongWay, detector.Update(-5f));
            Assert.AreEqual(SlipDetector.Outcome.WrongWay, detector.Update(6f));
        }
    }

    public class HandStormMeterTests
    {
        static HandStormMeter NewMeter()
        {
            var meter = new HandStormMeter();
            meter.Begin(60f, 55f, 0.35f, 0.25f);
            return meter;
        }

        // Circles of the given radius (inches) at the given frequency, sampled at 30 Hz for the duration.
        static void Circle(HandStormMeter meter, int hand, float radius, float hz, float seconds, double start = 0)
        {
            for (var i = 0; i <= seconds * 30f; i++)
            {
                var t = i / 30f;
                var angle = t * hz * Mathf.PI * 2f;
                meter.AddSample(hand, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, start + t, HandStormMeter.Source.HandDetector);
            }
        }

        [Test]
        public void SmallWiggleDoesNotFill()
        {
            var meter = NewMeter();
            Circle(meter, 0, 1.5f, 3f, 1f);
            Circle(meter, 1, 1.5f, 3f, 1f);
            Assert.IsFalse(meter.IsFilled);
            Assert.Less(meter.PeakSpeed, 55f);
        }

        [Test]
        public void BigStormWithOneHandFills()
        {
            var meter = NewMeter();
            Circle(meter, 0, 10f, 1.5f, 1f);
            Assert.IsTrue(meter.IsFilled);
        }

        [Test]
        public void TwoHandsFillFasterThanOne()
        {
            var one = NewMeter();
            Circle(one, 0, 8f, 1.5f, 0.4f);
            var two = NewMeter();
            Circle(two, 0, 8f, 1.5f, 0.4f);
            Circle(two, 1, 8f, 1.5f, 0.4f);
            Assert.Greater(two.TravelInches, one.TravelInches * 1.8f);
        }

        [Test]
        public void JitterBelowFloorAddsNothing()
        {
            var meter = NewMeter();
            for (var i = 0; i < 60; i++)
            {
                meter.AddSample(0, new Vector2(i % 2 == 0 ? 0.1f : -0.1f, 0f), i / 30d, HandStormMeter.Source.HandDetector);
            }

            Assert.AreEqual(0f, meter.TravelInches);
        }

        [Test]
        public void SourceSwitchAndGapsDoNotAddTravel()
        {
            var meter = NewMeter();
            meter.AddSample(0, Vector2.zero, 0d, HandStormMeter.Source.HandDetector);
            meter.AddSample(0, new Vector2(20f, 0f), 0.033d, HandStormMeter.Source.BodyNode);
            meter.AddSample(0, new Vector2(40f, 0f), 1d, HandStormMeter.Source.BodyNode);
            Assert.AreEqual(0f, meter.TravelInches);
        }

        [Test]
        public void TravelWithoutSpeedPeakDoesNotFill()
        {
            var meter = NewMeter();
            // 80 inches of slow travel at 30 in/s.
            for (var i = 0; i <= 80; i++) meter.AddSample(0, new Vector2(i, 0f), i / 30d, HandStormMeter.Source.HandDetector);
            Assert.GreaterOrEqual(meter.TravelInches, 60f);
            Assert.IsFalse(meter.IsFilled);
        }
    }

    public class ChestSteerTests
    {
        [Test]
        public void DeadzoneSwallowsBreathing()
        {
            Assert.AreEqual(0f, ChestSteer.Axis(1.2f, 1.5f, 7f));
            Assert.AreEqual(0f, ChestSteer.Axis(-1.4f, 1.5f, 7f));
        }

        [Test]
        public void FullLeanSaturatesAndKeepsSign()
        {
            Assert.AreEqual(1f, ChestSteer.Axis(9f, 1.5f, 7f));
            Assert.AreEqual(-1f, ChestSteer.Axis(-7f, 1.5f, 7f));
            Assert.AreEqual(0.5f, ChestSteer.Axis(4.25f, 1.5f, 7f), 1e-4f);
        }

        [Test]
        public void CarIgnoresVerticalLean()
        {
            var steer = ChestSteer.Steer(new Vector2(0f, 8f), false, 1.5f, 7f, 4f);
            Assert.AreEqual(Vector2.zero, steer);
            var sky = ChestSteer.Steer(new Vector2(0f, 8f), true, 1.5f, 7f, 4f);
            Assert.AreEqual(1f, sky.y);
        }
    }

    public class SharedSteerTests
    {
        [Test]
        public void EqualShareAveragesBothLeans()
        {
            var steer = new SharedSteer();
            var blended = steer.Blend(new Vector2?[] { new Vector2(-1f, 0f), new Vector2(0f, 0f) }, 0.5f, 0.1f);
            Assert.AreEqual(-0.5f, blended.x, 1e-4f);
            Assert.AreEqual(0, steer.DominantPlayer);
        }

        [Test]
        public void LostPlayerHandsFullSteerToTheOther()
        {
            var steer = new SharedSteer();
            var blended = steer.Blend(new Vector2?[] { null, new Vector2(1f, 0f) }, 0.5f, 0.1f);
            Assert.AreEqual(1f, blended.x, 1e-4f);
        }

        [Test]
        public void EffortShareTracksWhoSteered()
        {
            var steer = new SharedSteer();
            for (var i = 0; i < 10; i++) steer.Blend(new Vector2?[] { new Vector2(1f, 0f), new Vector2(0.25f, 0f) }, 0.5f, 0.1f);
            Assert.AreEqual(0.8f, steer.EffortShare(0), 1e-3f);
            Assert.AreEqual(0.2f, steer.EffortShare(1), 1e-3f);
        }
    }
}
