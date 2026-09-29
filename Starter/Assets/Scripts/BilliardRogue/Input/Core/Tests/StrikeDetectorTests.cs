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

        static StrikeSettings DefaultSettings() => new()
        {
            strikeSpeed = 35f,
            sustainSpeedFraction = 0.5f,
            contactDistance = 5f,
            armDistance = 10f,
            rearmSeconds = 0.35f,
            powerMultiplier = 2f,
            fullPowerSpeed = 120f,
            minTravel = 4f,
            maxStrikeSeconds = 0.4f,
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
        }

        static Vector2 CueStart => ballPaw + new Vector2(14f, -6f);

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
            Assert.That(feed.last.power01, Is.InRange(0.1f, 0.4f));
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
        public void TwiceTheThresholdIsAPowerShotAndFullPowerAboveTheConfiguredSpeed()
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
            var stop = ballPaw + (CueStart - ballPaw).normalized * 8f;
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
            var stop = ballPaw + (CueStart - ballPaw).normalized * 8f;
            feed.Hold(ballPaw, CueStart, 5);
            feed.MoveCue(ballPaw, CueStart, stop, 90f);
            feed.Hold(ballPaw, stop, 3);
            var readout = feed.detector.Readout;
            Assert.AreEqual(StrikeMiss.TooFar, readout.lastMiss);
            Assert.AreEqual(8f, readout.lastMissDistance, 0.5f);
            Assert.GreaterOrEqual(readout.lastMissSpeed, 35f);
            Assert.AreEqual(8f, readout.pawDistance, 0.01f);
            Assert.AreEqual(0f, readout.closingSpeed, 0.01f);
        }

        [Test]
        public void StrikeIsCountedAndAFastContactDuringTheCooldownIsReported()
        {
            var feed = new Feed();
            var contact = ballPaw + new Vector2(1f, 0f);
            var open = ballPaw + new Vector2(9f, 0f);
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
    }
}
