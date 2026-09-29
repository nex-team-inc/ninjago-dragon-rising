#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation.Tests
{
    /// <summary>BallSimulator.SetHype (GDD v2 §3): speed with re-derived substeps, damage multiplier, bonus floor, determinism.</summary>
    public class HypeTests
    {
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
        public void HypeSpeedsBallsUpAlongTheSamePath()
        {
            var calm = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 0.5f), 150f);
            sim.SetHype(1.8f, 1f);
            var hyped = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 0.5f), 150f);
            Assert.That(hyped.Seconds, Is.LessThan(calm.Seconds * 0.62f), "1.8× speed flies the path in ~1/1.8 of the time");
            var a = calm.First(SimEventKind.BallWallBounce);
            var b = hyped.First(SimEventKind.BallWallBounce);
            Assert.AreEqual(a.position.x, b.position.x, 0.05f);
            Assert.AreEqual(a.position.y, b.position.y, 0.1f, "same bounce point: only the speed changed");
            Assert.AreEqual(calm.Count(SimEventKind.BallWallBounce), hyped.Count(SimEventKind.BallWallBounce));
        }

        [Test]
        public void FastHypedBallsStillHitThinTargetsWithoutTunnelling()
        {
            sim.SetHype(3f, 1f);
            var e = SimTest.Put(run, ops, EnemyType.Slime, 3, 4);
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f);
            Assert.AreEqual(1, log.HitsOn(e.id), "bottom face: one hit, then straight back out");
            Assert.AreEqual(1, log.Count(SimEventKind.BallExited));
            Assert.AreEqual(3f, sim.HypeSpeedMultiplier);
            sim.SetHype(99f, 1f);
            Assert.AreEqual(rules.balance.maxHypeSpeedMultiplier, sim.HypeSpeedMultiplier, "clamped");
            sim.SetHype(0.2f, 0.5f);
            Assert.AreEqual(1f, sim.HypeSpeedMultiplier, "Hype never slows a ball");
            Assert.AreEqual(1f, sim.HypeDamageMultiplier, "Hype never weakens a ball");
        }

        [Test]
        public void HypeMultipliesHitDamageRoundingHalfUpAndReportsTheBonus()
        {
            var e = SimTest.Put(run, ops, EnemyType.Slime, 3, 4);
            sim.SetHype(1f, 2.5f);
            var hit = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f).First(SimEventKind.EnemyHit);
            Assert.AreEqual(e.id, hit.targetId);
            Assert.AreEqual(3, hit.value, "1 × 2.5 = 2.5 rounds up to 3");
            Assert.AreEqual(2, hit.hypeBonus);

            sim.SetHype(1f, 1.2f);
            hit = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f).First(SimEventKind.EnemyHit);
            Assert.AreEqual(1, hit.value, "1 × 1.2 rounds to 1");
            Assert.AreEqual(0, hit.hypeBonus);

            sim.SetHype(1f, 1.2f, 1);
            hit = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f).First(SimEventKind.EnemyHit);
            Assert.AreEqual(2, hit.value, "the tier-2 floor adds at least +1");
            Assert.AreEqual(1, hit.hypeBonus);

            sim.SetHype(1f, 1f);
            hit = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f).First(SimEventKind.EnemyHit);
            Assert.AreEqual(1, hit.value);
            Assert.AreEqual(0, hit.hypeBonus);
        }

        [Test]
        public void HypeBonusCountsOnlyTheDamageActuallyDealt()
        {
            SimTest.Put(run, ops, EnemyType.Slime, 3, 4, 2);
            sim.SetHype(1f, 2.5f);
            var hit = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f).First(SimEventKind.EnemyHit);
            Assert.AreEqual(2, hit.value, "3 damage on a 2 HP enemy deals 2");
            Assert.AreEqual(1, hit.hypeBonus, "1 of the 2 came from Hype");
        }

        [Test]
        public void HypeScalesExplosionsToo()
        {
            var e = SimTest.Put(run, ops, EnemyType.Slime, 3, 4);
            sim.SetHype(1f, 2.5f);
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Bomb), SimTest.LaunchAt(rules, 3.5f), 90f);
            Assert.AreEqual(1, log.Count(SimEventKind.Explosion));
            var areaDamage = rules.balls[(int)BallType.Bomb].levels[0].areaDamage;
            var expected = Mathf.FloorToInt(1 * 2.5f + 0.5f) + Mathf.FloorToInt(areaDamage * 2.5f + 0.5f);
            Assert.AreEqual(expected, SumDamageOn(log, e.id), "direct round(1 × 2.5) + area round(3 × 2.5)");
        }

        [Test]
        public void TheSameHypeSequenceGivesTheSameEvents()
        {
            var a = Replay(1);
            var b = Replay(1);
            Assert.AreEqual(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(Describe(a[i]), Describe(b[i]), $"event {i}");
            }

            Assert.That(a.Count, Is.GreaterThan(3));
        }

        [Test]
        public void ClearResetsHype()
        {
            sim.SetHype(2f, 2f, 1);
            sim.Clear();
            Assert.AreEqual(1f, sim.HypeSpeedMultiplier);
            Assert.AreEqual(1f, sim.HypeDamageMultiplier);
        }

        List<SimEvent> Replay(int seed)
        {
            var r = SimTest.NewRun(rules, seed);
            var o = new BoardOps(rules);
            var s = new BallSimulator(rules, o);
            SimTest.Put(r, o, EnemyType.Slime, 2, 3);
            SimTest.Put(r, o, EnemyType.Slime, 4, 5);
            SimTest.Put(r, o, EnemyType.Slime, 1, 6);
            var events = new List<SimEvent>();
            var all = new List<SimEvent>();
            s.Launch(r, SimTest.Ball(BallType.Thunder), SimTest.LaunchAt(rules, 1.2f), SimTest.Direction(70f), false, 0, events);
            for (var frame = 0; frame < SimTest.MaxFrames && s.ActiveCount > 0; frame++)
            {
                var hype = (frame % 30) / 30f;
                s.SetHype(1f + 0.8f * hype, 1f + 1.5f * hype, hype >= 0.55f ? 1 : 0);
                s.Step(r, 1f / SimTest.Fps, events);
            }

            all.AddRange(events);
            return all;
        }

        static string Describe(in SimEvent ev)
        {
            return $"{ev.kind} {ev.ballId} {ev.targetId} {ev.sourceId} {ev.value} {ev.value2} {ev.hypeBonus} {ev.flag} {ev.position.x:R} {ev.position.y:R}";
        }

        static int SumDamageOn(ShotLog log, int enemyId)
        {
            var sum = 0;
            foreach (var ev in log.events)
            {
                if (ev.kind == SimEventKind.EnemyHit && ev.targetId == enemyId) sum += ev.value;
            }

            return sum;
        }
    }
}
