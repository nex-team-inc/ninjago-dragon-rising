#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation.Tests
{
    /// <summary>Reflection, shields, pierce, split, pillar bounces, anti-stall bounds and PredictPath vs Step (TDD §3.6).</summary>
    public class BallPhysicsTests
    {
        const float MaxFlightTargetSeconds = 9f;

        GameRules rules = null!;
        RunState run = null!;
        BoardOps ops = null!;
        BallSimulator sim = null!;

        [SetUp]
        public void SetUp()
        {
            rules = SimTest.Rules();
            run = SimTest.NewRun(rules);
            ops = new BoardOps(rules);
            sim = new BallSimulator(rules, ops);
        }

        [Test]
        public void WallsReflectTheHorizontalComponentAndTheBallExitsAtTheBottom()
        {
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 0.5f), 150f);
            var first = log.First(SimEventKind.BallWallBounce);
            Assert.AreEqual(rules.arena.ballRadius, first.position.x, 0.06f, "first bounce is on the left wall");
            Assert.AreEqual(0, first.sourceId, "arena walls have sourceId 0");
            Assert.AreEqual(1, first.value, "value counts wall bounces");
            Assert.AreEqual(1, log.Count(SimEventKind.BallExited));
            Assert.AreEqual(0, log.Count(SimEventKind.EnemyHit));
            Assert.That(log.Seconds, Is.LessThan(rules.arena.maxFlightSeconds));
        }

        [Test]
        public void EnemyFacesReflectTheBall()
        {
            var e = SimTest.Put(run, ops, EnemyType.Slime, 3, 4);
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f);
            Assert.AreEqual(1, log.HitsOn(e.id), "bottom face: one hit, then straight back out");
            Assert.AreEqual(1, log.Count(SimEventKind.BallExited));
            Assert.That(log.Seconds, Is.LessThan(1.2f));

            var events = new List<SimEvent>();
            sim.Launch(run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 1f), SimTest.Direction(74f), false, 0, events);
            var hit = false;
            for (var frame = 0; frame < 120 && !hit; frame++)
            {
                events.Clear();
                sim.Step(run, 1f / SimTest.Fps, events);
                hit = SimTest.Count(events, SimEventKind.EnemyHit) > 0;
            }
            Assert.IsTrue(hit, "the 74° shot reaches the left face");
            var velocity = SimTest.Velocity(sim);
            Assert.That(velocity.x, Is.LessThan(0f), "left face flips x");
            Assert.That(velocity.y, Is.GreaterThan(0f), "left face keeps y");
            sim.Clear();
        }

        [Test]
        public void ShieldBlocksHitsOnTheShieldedFaceOnly()
        {
            var knight = SimTest.Put(run, ops, EnemyType.ShieldKnight, 3, 4);
            var below = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f);
            Assert.AreEqual(1, below.Count(SimEventKind.EnemyBlocked));
            Assert.AreEqual(0, below.HitsOn(knight.id));
            Assert.AreEqual(SimTest.ImmortalHp, knight.hp);

            var side = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 1f), 74f);
            Assert.That(side.HitsOn(knight.id), Is.GreaterThanOrEqualTo(1), "the left face is not shielded");
            Assert.That(knight.hp, Is.LessThan(SimTest.ImmortalHp));
        }

        [Test]
        public void PiercerPassesThroughAColumnBottomToTop()
        {
            var bottom = SimTest.Put(run, ops, EnemyType.Slime, 3, 4);
            var middle = SimTest.Put(run, ops, EnemyType.Slime, 3, 3);
            var top = SimTest.Put(run, ops, EnemyType.Slime, 3, 2);
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Piercer), SimTest.LaunchAt(rules, 3.5f), 90f);
            var order = new List<int>();
            var firstWall = log.FirstFrame(SimEventKind.BallWallBounce);
            for (var i = 0; i < log.events.Count && order.Count < 3; i++)
            {
                if (log.events[i].kind != SimEventKind.EnemyHit) continue;
                Assert.AreEqual(2, log.events[i].value, "Piercer L1 deals 2");
                Assert.That(log.frames[i], Is.LessThan(firstWall), "the first three hits happen before the top wall");
                order.Add(log.events[i].targetId);
            }
            CollectionAssert.AreEqual(new[] { bottom.id, middle.id, top.id }, order);
        }

        [Test]
        public void SplitterReplacesTheParentWithMinisThatExitSeparately()
        {
            var e = SimTest.Put(run, ops, EnemyType.Slime, 3, 4);
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Splitter, 2), SimTest.LaunchAt(rules, 3.5f), 90f);
            var split = log.First(SimEventKind.BallSplit);
            Assert.AreEqual(1, log.Count(SimEventKind.BallSplit));
            Assert.AreEqual(3, split.value);
            Assert.AreEqual(3, log.Count(SimEventKind.BallExited), "every mini exits on its own");
            foreach (var ev in log.events)
            {
                if (ev.kind == SimEventKind.BallExited) Assert.IsTrue(ev.flag, "BallExited.flag = isMini");
            }
            Assert.AreEqual(1, log.HitsOn(e.id), "head-on: only the parent hit");
            Assert.AreEqual(0, log.stillActive);
        }

        [Test]
        public void SplitMinisNeverStrikeTheSplitEnemyAgainImmediately()
        {
            var e = SimTest.Put(run, ops, EnemyType.Slime, 3, 4);
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Splitter, 2), SimTest.LaunchAt(rules, 1.745f), 80f);
            var splitFrame = log.FirstFrame(SimEventKind.BallSplit);
            Assert.That(splitFrame, Is.GreaterThan(0), "the grazing shot splits on the left face");
            Assert.AreEqual(1, log.HitsOn(e.id, 0, splitFrame), "exactly one hit from the parent");
            Assert.AreEqual(0, log.HitsOn(e.id, splitFrame + 1, splitFrame + 18), "no mini re-hits the same face");
            Assert.AreEqual(3, log.Count(SimEventKind.BallExited));
        }

        [Test]
        public void PillarsEmitAWallBounceWithTheirIdAndCratesTakeDamage()
        {
            SimTest.PutObject(run, FieldObjectType.Pillar, 7, 3, 4);
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f);
            Assert.AreEqual(1, log.Count(SimEventKind.BallWallBounce));
            var bounce = log.First(SimEventKind.BallWallBounce);
            Assert.AreEqual(7, bounce.sourceId);
            Assert.AreEqual(0, bounce.value, "a pillar bounce does not count as a wall bounce");
            Assert.AreEqual(1, log.Count(SimEventKind.BallExited));

            run.board.fieldObjects.Clear();
            var crate = SimTest.PutObject(run, FieldObjectType.Crate, 9, 3, 4, 3);
            log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f);
            Assert.AreEqual(1, log.Count(SimEventKind.CrateHit));
            Assert.AreEqual(2, crate.hp);
            Assert.AreEqual(0, log.Count(SimEventKind.BallWallBounce));
        }

        [Test]
        public void AntiStallEndsAFlightTrappedAboveAFullRowWithinTheBound()
        {
            for (var c = 0; c < rules.arena.columns; c++)
            {
                SimTest.Put(run, ops, EnemyType.Slime, c, 3);
            }
            var bound = SimTest.ExitBoundSeconds(rules);
            Assert.That(bound, Is.LessThanOrEqualTo(MaxFlightTargetSeconds), "defaults keep the worst case at or under 9 s");
            foreach (var type in new[] { BallType.Basic, BallType.Iron })
            {
                var log = SimTest.Shoot(sim, run, SimTest.Ball(type), new Vector2(3.5f, 9.1f), 80f);
                Assert.AreEqual(0, log.stillActive, $"{type} still flying");
                Assert.AreEqual(1, log.Count(SimEventKind.BallExited), $"{type} exit event");
                Assert.That(log.Seconds, Is.LessThanOrEqualTo(bound + 2f / SimTest.Fps), $"{type} exit bound");
                Assert.That(log.Seconds, Is.LessThanOrEqualTo(MaxFlightTargetSeconds), $"{type} pacing target");
            }
        }

        [Test]
        public void VerticalShotIntoAColumnStallsAsAJuggleAndExitsEarly()
        {
            SimTest.Put(run, ops, EnemyType.Slime, 3, 4);
            SimTest.Put(run, ops, EnemyType.Slime, 3, 3);
            SimTest.Put(run, ops, EnemyType.Slime, 3, 2);
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Piercer), SimTest.LaunchAt(rules, 3.5f), 90f);
            Assert.AreEqual(0, log.stillActive);
            Assert.That(log.Seconds, Is.LessThanOrEqualTo(SimTest.ExitBoundSeconds(rules)));
            Assert.That(log.Seconds, Is.LessThan(rules.arena.maxFlightSeconds), "idle wall bounces end the juggle before max flight");
            Assert.That(log.Count(SimEventKind.EnemyHit), Is.LessThan(30), "a juggle is not a damage farm");
        }

        [Test]
        public void PredictPathMatchesTheFirstContactOfStep()
        {
            SimTest.Put(run, ops, EnemyType.Slime, 1, 2);
            SimTest.Put(run, ops, EnemyType.Slime, 4, 5);
            SimTest.Put(run, ops, EnemyType.KingSlime, 2, 7);
            SimTest.Put(run, ops, EnemyType.Slime, 6, 8);
            SimTest.PutObject(run, FieldObjectType.Pillar, 900, 5, 3);
            var path = new Vector2[8];
            var random = new System.Random(11);
            var tolerance = 2f * rules.arena.ballSpeed / rules.arena.substepsPerSecond;
            var checkedShots = 0;
            for (var s = 0; s < 60; s++)
            {
                var angle = (float)(15 + random.NextDouble() * 150);
                var origin = ArenaGeometry.LaunchOrigin(rules.arena, (float)random.NextDouble());
                if (sim.PredictPath(run, origin, SimTest.Direction(angle), 40f, 1, path) < 2) continue;
                var events = new List<SimEvent>();
                sim.Launch(run, SimTest.Ball(BallType.Basic), origin, SimTest.Direction(angle), false, 0, events);
                var found = false;
                for (var frame = 0; frame < SimTest.MaxFrames && sim.ActiveCount > 0 && !found; frame++)
                {
                    events.Clear();
                    sim.Step(run, 1f / SimTest.Fps, events);
                    foreach (var ev in events)
                    {
                        if (ev.kind != SimEventKind.BallWallBounce && ev.kind != SimEventKind.ComboChanged && ev.kind != SimEventKind.BallExited) continue;
                        Assert.That((ev.position - path[1]).magnitude, Is.LessThanOrEqualTo(tolerance), $"angle {angle:F1}: {ev.kind} at {ev.position} vs {path[1]}");
                        found = true;
                        break;
                    }
                }
                Assert.IsTrue(found, $"angle {angle:F1}: no contact observed");
                checkedShots++;
                sim.Clear();
            }
            Assert.That(checkedShots, Is.GreaterThan(40));
        }
    }
}
