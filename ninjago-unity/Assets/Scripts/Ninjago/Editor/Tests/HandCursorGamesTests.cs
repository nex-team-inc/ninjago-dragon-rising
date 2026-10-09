#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nex.Ninjago.Editor.Tests
{
    public class ReachBoxTests
    {
        static readonly Vector2 screen = new(1920f, 1080f);

        [Test]
        public void ChestHeightHandSitsBelowRegionCenter()
        {
            var box = ReachBox.For(new Rect(0f, 0f, 1f, 1f), screen, 30f, 5f);
            var position = box.ToScreen(Vector2.zero);
            Assert.AreEqual(0.5f, position.x, 1e-4f);
            Assert.AreEqual(0.5f - 5f / 30f, position.y, 1e-4f);
        }

        [Test]
        public void HalfRegionKeepsTheMappingUniform()
        {
            var box = ReachBox.For(new Rect(0.5f, 0f, 0.5f, 1f), screen, 30f, 0f);
            // 960 x 1080 pixels: the box is 30 inches tall and 26.67 inches wide.
            Assert.AreEqual(30f * 960f / 1080f, box.size.x, 1e-3f);
            var right = box.ToScreen(new Vector2(box.size.x * 0.5f, 0f));
            Assert.AreEqual(1f, right.x, 1e-4f);
        }

        [Test]
        public void OutsideTheReachSticksToTheRegionEdge()
        {
            var box = ReachBox.For(new Rect(0f, 0f, 0.5f, 1f), screen, 30f, 0f);
            Assert.AreEqual(0.5f, box.ToScreen(new Vector2(500f, 0f)).x, 1e-4f);
            Assert.AreEqual(0f, box.ToScreen(new Vector2(0f, -500f)).y, 1e-4f);
        }

        [Test]
        public void ScreenAndInchesRoundTrip()
        {
            var box = ReachBox.For(new Rect(0f, 0f, 1f, 1f), screen, 28f, 4f);
            var screenPosition = new Vector2(0.3f, 0.7f);
            var back = box.ToScreen(box.ToInches(screenPosition));
            Assert.AreEqual(screenPosition.x, back.x, 1e-4f);
            Assert.AreEqual(screenPosition.y, back.y, 1e-4f);
        }
    }

    public class SlashDetectorTests
    {
        static readonly Vector2 center = new(500f, 500f);
        const float radius = 100f;
        const float threshold = 50f;
        const float maxCross = 0.4f;

        static bool Step(SlashDetector detector, Vector2 position, float speed, float time, int continuity = 1)
        {
            return detector.Update(position, speed, continuity, center, radius, time, threshold, maxCross);
        }

        // 60 Hz samples along a straight line at a constant reported speed.
        static bool Stroke(SlashDetector detector, Vector2 from, Vector2 to, float speed, int steps = 10)
        {
            for (var i = 0; i <= steps; i++)
            {
                if (Step(detector, Vector2.Lerp(from, to, i / (float)steps), speed, i / 60f)) return true;
            }

            return false;
        }

        [Test]
        public void FastStrokeAcrossTheRockSlashes()
        {
            Assert.IsTrue(Stroke(new SlashDetector(), new Vector2(300f, 500f), new Vector2(700f, 500f), 80f));
        }

        [Test]
        public void SlowStrokeAcrossTheRockDoesNothing()
        {
            Assert.IsFalse(Stroke(new SlashDetector(), new Vector2(300f, 500f), new Vector2(700f, 500f), 20f));
        }

        [Test]
        public void RestingCursorOnTheRockDoesNothing()
        {
            var detector = new SlashDetector();
            for (var i = 0; i < 60; i++) Assert.IsFalse(Step(detector, center + Vector2.one * Mathf.Sin(i), 3f, i / 60f));
        }

        [Test]
        public void FastStrokeThatMissesTheRockDoesNothing()
        {
            Assert.IsFalse(Stroke(new SlashDetector(), new Vector2(300f, 750f), new Vector2(700f, 750f), 80f));
        }

        [Test]
        public void CursorAlreadyOnTheRockMustEnterFromOutside()
        {
            Assert.IsFalse(Stroke(new SlashDetector(), center, new Vector2(800f, 500f), 80f));
        }

        [Test]
        public void SpeedThatPeaksInsideTheRockStillSlashes()
        {
            // The smoothed cursor speeds up while already crossing (the playtest case).
            var detector = new SlashDetector();
            var speeds = new[] { 20f, 30f, 42f, 65f, 86f, 88f, 81f, 69f };
            for (var i = 0; i < speeds.Length; i++)
            {
                var slashed = Step(detector, new Vector2(370f + i * 40f, 500f), speeds[i], i / 60f);
                Assert.AreEqual(i == 6, slashed, $"sample {i}");
            }
        }

        [Test]
        public void LingeringInsideTheRockIsNotASlash()
        {
            var detector = new SlashDetector();
            Assert.IsFalse(Step(detector, new Vector2(350f, 500f), 80f, 0f));
            Assert.IsFalse(Step(detector, new Vector2(450f, 500f), 80f, 0.02f));
            Assert.IsFalse(Step(detector, new Vector2(470f, 500f), 5f, 0.6f));
            Assert.IsFalse(Step(detector, new Vector2(700f, 500f), 80f, 0.62f));
        }

        [Test]
        public void OneFrameJumpThroughTheRockSlashes()
        {
            var detector = new SlashDetector();
            Assert.IsFalse(Step(detector, new Vector2(300f, 500f), 80f, 0f));
            Assert.IsTrue(Step(detector, new Vector2(700f, 500f), 80f, 0.02f));
        }

        [Test]
        public void SignalChangeMidStrokeRestarts()
        {
            var detector = new SlashDetector();
            Assert.IsFalse(Step(detector, new Vector2(300f, 500f), 80f, 0f));
            Assert.IsFalse(Step(detector, new Vector2(700f, 500f), 80f, 0.02f, 2));
        }
    }

    public class KickDetectorTests
    {
        static readonly KickDetector.Settings settings = new(4f, 0.5f, 0.3f, 0.18f, 2f);
        const float standing = 17f;

        static KickDetector NewDetector()
        {
            var detector = new KickDetector();
            detector.Begin(new Vector2(standing, standing));
            return detector;
        }

        // Left knee rises by lift inches over riseSeconds, holds, then drops back; 30 Hz samples.
        static int Kicks(KickDetector detector, ref float time, float lift, float riseSeconds, float holdSeconds)
        {
            var kicks = 0;
            var now = time;
            void Sample(float leftLift)
            {
                if (detector.Update(new Vector2(standing - leftLift, standing), now, settings) == KickDetector.Pulse.Kick) kicks++;
                now += 1f / 30f;
            }

            for (var t = 0f; t < riseSeconds; t += 1f / 30f) Sample(lift * t / riseSeconds);
            for (var t = 0f; t < holdSeconds; t += 1f / 30f) Sample(lift);
            for (var t = 0f; t < 0.2f; t += 1f / 30f) Sample(0f);
            time = now;
            return kicks;
        }

        [Test]
        public void FastKneePulseIsOneKick()
        {
            var time = 0f;
            Assert.AreEqual(1, Kicks(NewDetector(), ref time, 7f, 0.15f, 0.1f));
        }

        [Test]
        public void HoldingTheKneeUpIsStillOneKick()
        {
            var time = 0f;
            Assert.AreEqual(1, Kicks(NewDetector(), ref time, 7f, 0.15f, 2f));
        }

        [Test]
        public void SlowKneeRaiseIsNotAKick()
        {
            var time = 0f;
            Assert.AreEqual(0, Kicks(NewDetector(), ref time, 7f, 2f, 0.2f));
        }

        [Test]
        public void SmallKneeLiftIsNotAKick()
        {
            var time = 0f;
            Assert.AreEqual(0, Kicks(NewDetector(), ref time, 3f, 0.1f, 0.2f));
        }

        [Test]
        public void ThreeQuickPulsesAreThreeKicks()
        {
            var detector = NewDetector();
            var time = 0f;
            var kicks = 0;
            for (var i = 0; i < 3; i++) kicks += Kicks(detector, ref time, 7f, 0.1f, 0.05f);
            Assert.AreEqual(3, kicks);
        }

        [Test]
        public void BothKneesTogetherIsASquatNotAKick()
        {
            var detector = NewDetector();
            var kicks = 0;
            for (var i = 0; i < 20; i++)
            {
                var lift = Mathf.Min(8f, i * 2f);
                if (detector.Update(new Vector2(standing - lift, standing - lift), i / 30f, settings) == KickDetector.Pulse.Kick) kicks++;
            }

            Assert.AreEqual(0, kicks);
        }

        [Test]
        public void PulseInsideTheCooldownIsBlocked()
        {
            var detector = NewDetector();
            Assert.AreEqual(KickDetector.Pulse.Kick, detector.External(1f, 0.18f));
            Assert.AreEqual(KickDetector.Pulse.Blocked, detector.External(1.1f, 0.18f));
            Assert.AreEqual(KickDetector.Pulse.Kick, detector.External(1.25f, 0.18f));
        }
    }

    public class SealWallTests
    {
        static EarthSealConfig.WaveSettings Wave(int maxCracks = 1, float chance = 1f, int spacing = 1)
        {
            return new EarthSealConfig.WaveSettings
            {
                gridSize = 3, waveSeconds = 20f, beatSeconds = 1f, crackChance = chance, maxCracks = maxCracks, minCrackSpacing = spacing,
                warningSeconds = 1f, sealSeconds = 2f, breakthroughSeconds = 3f,
            };
        }

        sealed class Harness
        {
            public readonly SealWall wall = new();
            public readonly List<int> counts = new();
            public readonly List<int> masks = new();
            public readonly List<SealWall.Event> events = new();

            public Harness(EarthSealConfig.WaveSettings wave)
            {
                wall.BeginWave(wave, new System.Random(7));
                for (var i = 0; i < wall.Tiles.Count; i++)
                {
                    counts.Add(0);
                    masks.Add(0);
                }
            }

            public int FirstLive()
            {
                for (var i = 0; i < wall.Tiles.Count; i++)
                {
                    if (wall.Tiles[i].IsLive) return i;
                }

                return -1;
            }

            public void Hold(int tile, int cursors, int mask)
            {
                counts[tile] = cursors;
                masks[tile] = mask;
            }

            public void Run(float seconds, float drain = 4f)
            {
                for (var t = 0f; t < seconds; t += 0.02f) wall.Tick(0.02f, counts, masks, drain, events);
            }

            public int Count(SealWall.EventType type)
            {
                var count = 0;
                foreach (var wallEvent in events)
                {
                    if (wallEvent.type == type) count++;
                }

                return count;
            }
        }

        [Test]
        public void HeldCrackSealsAfterTheSealTime()
        {
            var harness = new Harness(Wave());
            harness.Run(1.01f);
            var tile = harness.FirstLive();
            Assert.GreaterOrEqual(tile, 0);
            harness.Hold(tile, 1, 1);
            harness.Run(1.9f);
            Assert.AreEqual(SealWall.TileState.Pushing, harness.wall.Tiles[tile].State);
            harness.Run(0.2f);
            Assert.AreEqual(SealWall.TileState.Sealed, harness.wall.Tiles[tile].State);
        }

        [Test]
        public void TwoCursorsDoNotSealFaster()
        {
            var harness = new Harness(Wave());
            harness.Run(1.01f);
            var tile = harness.FirstLive();
            harness.Hold(tile, 2, 3);
            harness.Run(1.9f);
            Assert.AreNotEqual(SealWall.TileState.Sealed, harness.wall.Tiles[tile].State);
            harness.Run(0.2f);
            Assert.AreEqual(SealWall.TileState.Sealed, harness.wall.Tiles[tile].State);
            foreach (var wallEvent in harness.events)
            {
                if (wallEvent.type == SealWall.EventType.SealCompleted) Assert.AreEqual(2, wallEvent.cursorCount);
            }
        }

        [Test]
        public void UnheldCrackBreaksThroughAfterWarningAndTimer()
        {
            var harness = new Harness(Wave());
            harness.Run(1.01f);
            var tile = harness.FirstLive();
            harness.Run(4.1f);
            Assert.AreEqual(SealWall.TileState.Broken, harness.wall.Tiles[tile].State);
            Assert.AreEqual(1, harness.Count(SealWall.EventType.Breakthrough));
        }

        [Test]
        public void ShortWobbleKeepsMostOfTheSeal()
        {
            var harness = new Harness(Wave());
            harness.Run(1.01f);
            var tile = harness.FirstLive();
            harness.Hold(tile, 1, 1);
            harness.Run(1f);
            harness.Hold(tile, 0, 0);
            harness.Run(0.2f);
            Assert.AreEqual(0.45f, harness.wall.Tiles[tile].Fill, 0.02f);
            Assert.AreEqual(0, harness.Count(SealWall.EventType.SealDropped));
        }

        [Test]
        public void WalkingAwayDrainsAndDropsTheSeal()
        {
            var harness = new Harness(Wave());
            harness.Run(1.01f);
            var tile = harness.FirstLive();
            harness.Hold(tile, 1, 2);
            harness.Run(0.5f);
            harness.Hold(tile, 0, 0);
            harness.Run(1.2f, 4f);
            Assert.AreEqual(0f, harness.wall.Tiles[tile].Fill);
            Assert.AreEqual(1, harness.Count(SealWall.EventType.SealDropped));
            Assert.AreEqual(1, harness.wall.Tiles[tile].LastHolder);
        }

        [Test]
        public void NeverMoreLiveCracksThanTheWaveAllows()
        {
            var harness = new Harness(Wave(maxCracks: 2));
            for (var i = 0; i < 400; i++)
            {
                harness.Run(0.05f);
                Assert.LessOrEqual(harness.wall.LiveCount, 2);
            }
        }

        [Test]
        public void SpacedCracksNeverTouch()
        {
            var harness = new Harness(Wave(maxCracks: 2, spacing: 2));
            for (var step = 0; step < 300; step++)
            {
                harness.Run(0.05f);
                var tiles = harness.wall.Tiles;
                for (var a = 0; a < tiles.Count; a++)
                {
                    for (var b = a + 1; b < tiles.Count; b++)
                    {
                        if (!tiles[a].IsLive || !tiles[b].IsLive) continue;
                        Assert.GreaterOrEqual(Mathf.Max(Mathf.Abs(tiles[a].X - tiles[b].X), Mathf.Abs(tiles[a].Y - tiles[b].Y)), 2);
                    }
                }
            }
        }
    }
}
