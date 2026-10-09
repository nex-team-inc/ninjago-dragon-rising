#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.InputCore.Tests
{
    public class StrikeDetectorTests
    {
        const double FrameSeconds = 1.0 / 30.0;
        static readonly Vector2 ballPaw = new(-8f, 4f);

        #region Helpers

        // The v2 ControlConfig defaults (GDD v2 §2).
        static StrikeSettings DefaultSettings() => new()
        {
            strikeSpeed = 19f,
            sustainSpeedFraction = 0.5f,
            contactDistance = 9f,
            angleToleranceDeg = 60f,
            lineCrossMaxOffset = 12f,
            armDistance = 14f,
            rearmSeconds = 0.25f,
            powerMultiplier = 3.5f,
            fullPowerSpeed = 110f,
            minTravel = 4f,
            maxStrikeSeconds = 0.6f,
            maxSampleGapSeconds = 0.25f,
        };

        /// <summary>30 Hz sample driver that counts the strikes the detector reports.</summary>
        sealed class Feed
        {
            public readonly StrikeDetector detector = new(DefaultSettings());
            public double time = 10.0;
            public int strikes;
            public StrikeResult last;

            public void Hold(Vector2 ball, Vector2 cue, int frames)
            {
                for (var i = 0; i < frames; i++)
                {
                    Sample(ball, cue);
                }
            }

            public void Sample(Vector2 ball, Vector2 cue)
            {
                time += FrameSeconds;
                if (!detector.AddSample(time, ball, cue, out var result)) return;
                strikes++;
                last = result;
            }

            /// <summary>Moves the cue in a straight line toward `to` at `speed`, one sample per frame.</summary>
            public void MoveCue(Vector2 ball, Vector2 from, Vector2 to, float speed)
            {
                var length = (to - from).magnitude;
                var frames = Mathf.Max(1, Mathf.CeilToInt(length / (speed * (float)FrameSeconds)));
                for (var i = 1; i <= frames; i++)
                {
                    Sample(ball, Vector2.Lerp(from, to, i / (float)frames));
                }
            }

            /// <summary>Moves the cue along a circle around the ball paw (an aim sweep) at `speed`.</summary>
            public void SweepCue(Vector2 ball, float radius, float fromDeg, float toDeg, float speed)
            {
                var arc = Mathf.Abs(toDeg - fromDeg) * Mathf.Deg2Rad * radius;
                var frames = Mathf.Max(1, Mathf.CeilToInt(arc / (speed * (float)FrameSeconds)));
                for (var i = 1; i <= frames; i++)
                {
                    var deg = Mathf.Lerp(fromDeg, toDeg, i / (float)frames);
                    Sample(ball, ball + radius * new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)));
                }
            }
        }

        static Vector2 CueStart => ballPaw + new Vector2(14f, -6f);

        static Vector2 TowardBall => (ballPaw - CueStart).normalized;

        /// <summary>`direction` rotated by `degrees` (counter-clockwise).</summary>
        static Vector2 Rotate(Vector2 direction, float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }

        #endregion

        #region Tests

        [Test]
        public void FastThrustIntoTheBallPawFiresExactlyOnce()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            Assert.AreEqual(StrikeState.Armed, feed.detector.State);
            feed.MoveCue(ballPaw, CueStart, ballPaw + new Vector2(1f, -0.5f), 60f);
            feed.Hold(ballPaw, ballPaw + new Vector2(1f, -0.5f), 10);
            Assert.AreEqual(1, feed.strikes);
            Assert.IsFalse(feed.last.isPowerShot);
            Assert.IsFalse(feed.last.byLineCross);
            Assert.That(feed.last.power01, Is.InRange(0.25f, 0.6f));
            Assert.Less(feed.last.startTime, feed.last.time);
        }

        [Test]
        public void SlowApproachNeverFires()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, ballPaw + new Vector2(0.5f, 0f), 15f);
            feed.Hold(ballPaw, ballPaw + new Vector2(0.5f, 0f), 5);
            Assert.AreEqual(0, feed.strikes);
        }

        [Test]
        public void MovingBothPawsTogetherNeverFires()
        {
            var feed = new Feed();
            var offset = CueStart - ballPaw;
            feed.Hold(ballPaw, CueStart, 5);
            for (var i = 1; i <= 20; i++)
            {
                // Both paws sweep fast along the ball→cue axis (a lean or swing): the paw distance stays constant.
                var shift = -offset.normalized * (i * 3f);
                feed.Sample(ballPaw + shift, CueStart + shift);
            }

            Assert.AreEqual(0, feed.strikes);
        }

        [Test]
        public void PushingTheBallPawIntoAStillCueNeverFires()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            for (var i = 1; i <= 6; i++)
            {
                feed.Sample(Vector2.Lerp(ballPaw, CueStart, i / 6f), CueStart);
            }

            Assert.AreEqual(0, feed.strikes);
        }

        [Test]
        public void VeryFastThrustThatJumpsPastTheBallPawStillFires()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            // One 30 Hz frame from 15 in away to 4 in past the ball paw (never sampled inside the contact radius).
            var beyond = ballPaw - (CueStart - ballPaw).normalized * 6f;
            feed.Sample(ballPaw, Vector2.Lerp(CueStart, ballPaw, 0.5f));
            feed.Sample(ballPaw, beyond);
            Assert.AreEqual(1, feed.strikes);
        }

        [Test]
        public void PowerMultiplierTimesTheThresholdIsAPowerShotAndFullPowerAboveTheConfiguredSpeed()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, ballPaw, 200f);
            Assert.AreEqual(1, feed.strikes);
            Assert.IsTrue(feed.last.isPowerShot);
            Assert.AreEqual(1f, feed.last.power01, 1e-4f);
        }

        [Test]
        public void NextStrikeNeedsTheRearmDelayAndTheCuePulledBack()
        {
            var feed = new Feed();
            var contact = ballPaw + new Vector2(1f, 0f);
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, contact, 90f);
            Assert.AreEqual(1, feed.strikes);

            // Jiggling at contact distance does not re-arm.
            feed.MoveCue(ballPaw, contact, ballPaw + new Vector2(7f, 0f), 90f);
            feed.MoveCue(ballPaw, ballPaw + new Vector2(7f, 0f), contact, 90f);
            Assert.AreEqual(1, feed.strikes);

            // Pulled back past armDistance (after the delay): the next thrust fires again.
            feed.MoveCue(ballPaw, contact, CueStart, 60f);
            feed.Hold(ballPaw, CueStart, 12);
            feed.MoveCue(ballPaw, CueStart, contact, 90f);
            Assert.AreEqual(2, feed.strikes);
        }

        [Test]
        public void GapLongerThanTheLimitResyncsWithoutAVelocityAcrossIt()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            feed.time += 0.5;
            // Right after the gap the cue is at contact: a naive finite difference would read a huge closing speed.
            feed.Sample(ballPaw, ballPaw + new Vector2(1f, 0f));
            feed.Hold(ballPaw, ballPaw + new Vector2(1f, 0f), 5);
            Assert.AreEqual(0, feed.strikes);
        }

        [Test]
        public void MarkGapDropsTheApproach()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            feed.Sample(ballPaw, Vector2.Lerp(CueStart, ballPaw, 0.3f));
            Assert.IsTrue(feed.detector.IsApproaching);
            feed.detector.MarkGap();
            Assert.AreEqual(StrikeState.Armed, feed.detector.State);
            feed.Sample(ballPaw, ballPaw + new Vector2(1f, 0f));
            Assert.AreEqual(0, feed.strikes);
        }

        [Test]
        public void OneMissingFrameMidThrustStillFires()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            feed.Sample(ballPaw, Vector2.Lerp(CueStart, ballPaw, 0.3f));
            Assert.IsTrue(feed.detector.IsApproaching);
            // Motion blur loses the paws for one camera frame at peak speed; contact is measured across the gap.
            feed.time += FrameSeconds;
            feed.detector.MarkMissingSample(feed.time);
            Assert.IsTrue(feed.detector.IsApproaching);
            feed.Sample(ballPaw, ballPaw + new Vector2(1f, -0.5f));
            Assert.AreEqual(1, feed.strikes);
        }

        [Test]
        public void MissingSamplesPastTheGapLimitDropTheApproach()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            feed.Sample(ballPaw, Vector2.Lerp(CueStart, ballPaw, 0.3f));
            // 7 frames (0.23 s) are still within maxSampleGapSeconds (0.25 s); the 8th is past it.
            for (var i = 0; i < 7; i++)
            {
                feed.time += FrameSeconds;
                feed.detector.MarkMissingSample(feed.time);
            }

            Assert.IsTrue(feed.detector.IsApproaching);
            feed.time += FrameSeconds;
            feed.detector.MarkMissingSample(feed.time);
            Assert.AreEqual(StrikeState.Armed, feed.detector.State);
            feed.Sample(ballPaw, ballPaw + new Vector2(1f, 0f));
            Assert.AreEqual(0, feed.strikes);
        }

        [Test]
        public void SampleClockSteppedBackResumesAfterMarkGap()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            // Wall-clock frame times jump back (system clock correction): PawShotInput calls MarkGap, then the
            // detector runs on the new clock instead of ignoring every sample until the old time is passed.
            feed.detector.MarkGap();
            feed.time -= 60.0;
            feed.Hold(ballPaw, CueStart, 3);
            Assert.AreEqual(StrikeState.Armed, feed.detector.State);
            feed.MoveCue(ballPaw, CueStart, ballPaw + new Vector2(1f, -0.5f), 60f);
            Assert.AreEqual(1, feed.strikes);
        }

        [Test]
        public void RepeatedTimestampsAreIgnored()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            var time = feed.time;
            Assert.IsFalse(feed.detector.AddSample(time, ballPaw, ballPaw, out _));
            Assert.IsFalse(feed.detector.AddSample(time - 0.01, ballPaw, ballPaw, out _));
            Assert.AreEqual(StrikeState.Armed, feed.detector.State);
        }

        [Test]
        public void ResetNeedsThePawsApartBeforeArming()
        {
            var feed = new Feed();
            var close = ballPaw + new Vector2(8f, 0f);
            feed.Hold(ballPaw, close, 3);
            Assert.AreEqual(StrikeState.Disarmed, feed.detector.State);
            feed.detector.Reset();
            feed.MoveCue(ballPaw, close, ballPaw + new Vector2(1f, 0f), 90f);
            Assert.AreEqual(0, feed.strikes);
            feed.Hold(ballPaw, CueStart, 3);
            Assert.AreEqual(StrikeState.Armed, feed.detector.State);
        }

        [Test]
        public void ApproachThatStopsShortIsDropped()
        {
            var feed = new Feed();
            var stop = ballPaw + (CueStart - ballPaw).normalized * 11f;
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, stop, 90f);
            feed.Hold(ballPaw, stop, 3);
            Assert.AreEqual(StrikeState.Armed, feed.detector.State);
            Assert.AreEqual(0, feed.strikes);
        }

        [Test]
        public void SlowContactIsReportedAsTooSlow()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, ballPaw + new Vector2(0.5f, 0f), 15f);
            feed.Hold(ballPaw, ballPaw + new Vector2(0.5f, 0f), 5);
            var readout = feed.detector.Readout;
            Assert.AreEqual(0, feed.strikes);
            Assert.AreEqual(1, readout.missCount, "one report per contact, not per sample");
            Assert.AreEqual(StrikeMiss.TooSlow, readout.lastMiss);
            Assert.That(readout.lastMissSpeed, Is.InRange(10f, 20f));
        }

        [Test]
        public void FastMoveThatStopsShortIsReportedAsTooFar()
        {
            var feed = new Feed();
            var stop = ballPaw + (CueStart - ballPaw).normalized * 11f;
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, stop, 90f);
            feed.Hold(ballPaw, stop, 3);
            var readout = feed.detector.Readout;
            Assert.AreEqual(StrikeMiss.TooFar, readout.lastMiss);
            Assert.AreEqual(11f, readout.lastMissDistance, 0.5f);
            Assert.GreaterOrEqual(readout.lastMissSpeed, 19f);
            Assert.AreEqual(11f, readout.pawDistance, 0.01f);
            Assert.AreEqual(0f, readout.closingSpeed, 0.01f);
        }

        [Test]
        public void StrikeIsCountedAndAFastContactDuringTheCooldownIsReported()
        {
            var feed = new Feed();
            var contact = ballPaw + new Vector2(1f, 0f);
            var open = ballPaw + new Vector2(15f, 0f);
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, contact, 90f);
            var readout = feed.detector.Readout;
            Assert.AreEqual(1, readout.strikeCount);
            Assert.AreEqual(feed.last.peakSpeed, readout.lastStrikeSpeed);
            Assert.AreEqual(StrikeState.Cooldown, readout.state);
            Assert.Greater(readout.cooldownRemaining, 0f);

            feed.MoveCue(ballPaw, contact, open, 180f);
            feed.MoveCue(ballPaw, open, contact, 180f);
            readout = feed.detector.Readout;
            Assert.AreEqual(1, feed.strikes);
            Assert.AreEqual(StrikeMiss.Cooldown, readout.lastMiss);
            Assert.AreEqual(1, readout.missCount, "the first strike's own contact is not a miss");
        }

        #endregion

        #region v2: easier strikes (GDD v2 §2)

        [Test]
        public void ThrustBelowTheV1ButAboveTheV2ThresholdFires()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, ballPaw + new Vector2(1f, -0.5f), 25f);
            Assert.AreEqual(1, feed.strikes);
            Assert.That(feed.last.peakSpeed, Is.InRange(19f, 35f));
        }

        [Test]
        public void ThrustThatStopsWithinTheLargerContactFires()
        {
            var feed = new Feed();
            var stop = ballPaw - TowardBall * 8f;
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, stop, 60f);
            feed.Hold(ballPaw, stop, 5);
            Assert.AreEqual(1, feed.strikes);
            Assert.IsFalse(feed.last.byLineCross);
        }

        [Test]
        public void ThrustThatPassesJustBesideTheBallPawFiresOnItsLine()
        {
            var feed = new Feed();
            // Parallel to the direct line, 9.5 in to the side: never within contact (9 in), but it crosses the ball
            // paw's line about 11 in beside it (≤ lineCrossMaxOffset).
            var side = new Vector2(-TowardBall.y, TowardBall.x);
            var start = ballPaw - TowardBall * 15f + side * 9.5f;
            var end = start + TowardBall * 30f;
            feed.Hold(ballPaw, start, 5);
            feed.MoveCue(ballPaw, start, end, 60f);
            Assert.AreEqual(1, feed.strikes);
            Assert.IsTrue(feed.last.byLineCross);
            Assert.IsTrue(feed.detector.Readout.lastStrikeByLineCross);
        }

        [Test]
        public void HeadingWithinTheAngleToleranceStartsAnApproachAndBeyondDoesNot()
        {
            var within = new Feed();
            within.Hold(ballPaw, CueStart, 5);
            within.Sample(ballPaw, CueStart + Rotate(TowardBall, 55f) * 3f);
            Assert.AreEqual(StrikeState.Approaching, within.detector.State);

            // 65° off: still 38 in/s toward the ball paw (above the 19 in/s threshold), but the heading is outside.
            var beyond = new Feed();
            beyond.Hold(ballPaw, CueStart, 5);
            beyond.Sample(ballPaw, CueStart + Rotate(TowardBall, 65f) * 3f);
            Assert.AreEqual(StrikeState.Armed, beyond.detector.State);
        }

        [Test]
        public void CurvedThrustStartingWithinTheToleranceFires()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            var bend = CueStart + Rotate(TowardBall, 50f) * 6f;
            feed.MoveCue(ballPaw, CueStart, bend, 60f);
            feed.MoveCue(ballPaw, bend, ballPaw + new Vector2(1f, 0f), 60f);
            Assert.AreEqual(1, feed.strikes);
        }

        [Test]
        public void TheAimFromBeforeTheThrustIsStillTheStrikeStart()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            var calmTime = feed.time;
            feed.MoveCue(ballPaw, CueStart, ballPaw, 60f);
            Assert.AreEqual(1, feed.strikes);
            Assert.AreEqual(calmTime, feed.last.startTime, 1e-9, "the last calm sample, where the aim is sampled");
        }

        #endregion

        #region v2: natural arm movement never fires

        [Test]
        public void FastAimSweepAroundTheBallPawNeverFires()
        {
            var feed = new Feed();
            var start = ballPaw + new Vector2(15f, 0f);
            feed.Hold(ballPaw, start, 5);
            feed.SweepCue(ballPaw, 15f, 0f, -120f, 80f);
            feed.SweepCue(ballPaw, 15f, -120f, 0f, 80f);
            Assert.AreEqual(0, feed.strikes);
        }

        [Test]
        public void RaisingTheCueArmNeverFires()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            // Straight up past the ball paw's height: 67° off the ball paw at first, then sideways and away.
            feed.MoveCue(ballPaw, CueStart, CueStart + new Vector2(0f, 26f), 70f);
            feed.Hold(ballPaw, CueStart + new Vector2(0f, 26f), 5);
            Assert.AreEqual(0, feed.strikes);
        }

        [Test]
        public void DroppingTheCueArmToTheSideNeverFires()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, CueStart + new Vector2(6f, -20f), 90f);
            Assert.AreEqual(0, feed.strikes);
        }

        [Test]
        public void SlowReachAcrossTheBodyNeverFires()
        {
            var feed = new Feed();
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, ballPaw + new Vector2(0.5f, -0.5f), 12f);
            feed.Hold(ballPaw, ballPaw + new Vector2(0.5f, -0.5f), 5);
            Assert.AreEqual(0, feed.strikes);
        }

        [Test]
        public void WaveFarAboveTheBallPawNeverFires()
        {
            var feed = new Feed();
            var start = ballPaw + new Vector2(14f, 16f);
            feed.Hold(ballPaw, start, 5);
            // Starts within the heading tolerance, but passes 16 in above the ball paw (its line ~24 in beside it).
            feed.MoveCue(ballPaw, start, ballPaw + new Vector2(-24f, 16f), 70f);
            Assert.AreEqual(0, feed.strikes);
            Assert.AreEqual(StrikeMiss.TooFar, feed.detector.Readout.lastMiss);
        }

        #endregion
    }
}
