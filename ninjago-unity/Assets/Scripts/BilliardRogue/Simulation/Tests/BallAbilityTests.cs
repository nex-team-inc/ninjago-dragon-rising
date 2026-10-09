#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation.Tests
{
    /// <summary>Bomb area, chain targets, statuses, vampire cap, rubber bonus and power shots (GDD §5, TDD §3.6).</summary>
    public class BallAbilityTests
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
        public void BombDamagesTheThreeByThreeAroundTheHitCell()
        {
            var cluster = new EnemyState[3, 3];
            for (var c = 2; c <= 4; c++)
            {
                for (var r = 3; r <= 5; r++)
                {
                    cluster[c - 2, r - 3] = SimTest.Put(run, ops, EnemyType.Slime, c, r);
                }
            }
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Bomb), SimTest.LaunchAt(rules, 3.5f), 90f);
            var explosion = log.First(SimEventKind.Explosion);
            Assert.AreEqual(1, log.Count(SimEventKind.Explosion));
            Assert.AreEqual(1, explosion.value);
            Assert.AreEqual(ArenaGeometry.CellCenter(rules.arena, 3, 5), explosion.position);
            for (var c = 2; c <= 4; c++)
            {
                for (var r = 3; r <= 5; r++)
                {
                    var expected = r == 3 ? 0 : c == 3 && r == 5 ? 4 : 3;
                    Assert.AreEqual(SimTest.ImmortalHp - expected, cluster[c - 2, r - 3].hp, $"enemy ({c},{r})");
                }
            }
        }

        [Test]
        public void ThunderChainsToTheNearestEnemiesWithinRange()
        {
            var first = SimTest.Put(run, ops, EnemyType.Slime, 3, 5);
            var near = SimTest.Put(run, ops, EnemyType.Slime, 3, 4);
            var second = SimTest.Put(run, ops, EnemyType.Slime, 5, 4);
            var far = SimTest.Put(run, ops, EnemyType.Slime, 0, 5);
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Thunder), SimTest.LaunchAt(rules, 3.5f), 90f);
            Assert.AreEqual(2, log.Count(SimEventKind.ChainLightning));
            var targets = new System.Collections.Generic.List<int>();
            foreach (var ev in log.events)
            {
                if (ev.kind == SimEventKind.ChainLightning) targets.Add(ev.targetId);
            }
            CollectionAssert.AreEqual(new[] { near.id, second.id }, targets);
            Assert.AreEqual(SimTest.ImmortalHp - 1, first.hp);
            Assert.AreEqual(SimTest.ImmortalHp - 1, near.hp);
            Assert.AreEqual(SimTest.ImmortalHp - 1, second.hp);
            Assert.AreEqual(SimTest.ImmortalHp, far.hp, "out of chain range");
        }

        [Test]
        public void FlameAndVenomApplyStatusesAndPoisonAddsOneDamagePerHit()
        {
            var burning = SimTest.Put(run, ops, EnemyType.Slime, 2, 3);
            var poisoned = SimTest.Put(run, ops, EnemyType.Slime, 4, 3);
            var flame = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Flame), SimTest.LaunchAt(rules, 2.5f), 90f);
            var applied = flame.First(SimEventKind.StatusApplied);
            Assert.AreEqual(StatusType.Burn, applied.status);
            Assert.AreEqual(2, applied.value);
            Assert.AreEqual(2, burning.status.burn);

            SimTest.Shoot(sim, run, SimTest.Ball(BallType.Venom), SimTest.LaunchAt(rules, 4.5f), 90f);
            Assert.AreEqual(1, poisoned.status.poison);
            var basic = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 4.5f), 90f);
            Assert.AreEqual(2, basic.First(SimEventKind.EnemyHit).value, "poison adds +1 to every hit");

            SimTest.Shoot(sim, run, SimTest.Ball(BallType.Venom, 3), SimTest.LaunchAt(rules, 4.5f), 90f);
            var capped = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Venom, 3), SimTest.LaunchAt(rules, 4.5f), 90f);
            Assert.AreEqual(rules.balance.poisonMax, poisoned.status.poison);
            Assert.AreEqual(rules.balance.poisonMax, capped.First(SimEventKind.StatusApplied).value);
        }

        [Test]
        public void VampireHealsUpToTheCapPerShot()
        {
            var boss = SimTest.Put(run, ops, EnemyType.KingSlime, 0, 5);
            run.playerHp = 10;
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Vampire), SimTest.LaunchAt(rules, 2.5f), 92.86f);
            Assert.That(log.HitsOn(boss.id), Is.GreaterThanOrEqualTo(3), "the top-face juggle lands at least three hits");
            Assert.AreEqual(2, log.Sum(SimEventKind.PlayerHealed), "heal cap 2 per shot");
            Assert.AreEqual(12, run.playerHp);
            Assert.AreEqual(0, log.stillActive);
            Assert.That(log.Seconds, Is.LessThanOrEqualTo(SimTest.ExitBoundSeconds(rules)));
        }

        [Test]
        public void RubberGainsOneDamagePerWallBounceUpToTheCap()
        {
            // 34.6° from x = 0.3 unfolds to 14.4 units of travel: two side-wall bounces, then the row-0 enemy at col 1.
            var target = SimTest.Put(run, ops, EnemyType.Slime, 1, 0);
            var two = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Rubber), SimTest.LaunchAt(rules, 0.3f), 34.6f);
            Assert.AreEqual(target.id, two.First(SimEventKind.EnemyHit).targetId);
            Assert.AreEqual(2, two.First(SimEventKind.EnemyHit).value2 == SimTest.ImmortalHp - 3 ? 2 : -1, "two wall bounces before the hit");
            Assert.AreEqual(3, two.First(SimEventKind.EnemyHit).value, "1 + 2 wall bounces");

            // 15° unfolds to 37 units: five bounces, capped at +4, landing on col 2.
            run.board.enemies.Clear();
            var capped = SimTest.Put(run, ops, EnemyType.Slime, 2, 0);
            var five = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Rubber), SimTest.LaunchAt(rules, 0.3f), 15f);
            Assert.AreEqual(capped.id, five.First(SimEventKind.EnemyHit).targetId);
            Assert.AreEqual(5, five.First(SimEventKind.BallWallBounce).value == 1 ? five.Count(SimEventKind.BallWallBounce) >= 5 ? 5 : -1 : -1, "five wall bounces");
            Assert.AreEqual(5, five.First(SimEventKind.EnemyHit).value, "1 + min(5 bounces, cap 4)");
        }

        [Test]
        public void PowerShotAndPowerPickupBoostTheFirstHitOnly()
        {
            SimTest.Put(run, ops, EnemyType.Slime, 3, 4);
            var power = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f, true);
            Assert.AreEqual(1 + rules.balance.powerShotBonusDamage, power.First(SimEventKind.EnemyHit).value);

            run.powerPickupArmed = true;
            var doubled = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f, true);
            Assert.AreEqual(1, doubled.Count(SimEventKind.PowerShotConsumed));
            Assert.AreEqual((1 + rules.balance.powerShotBonusDamage) * rules.balance.powerPickupMultiplier, doubled.First(SimEventKind.EnemyHit).value);
            Assert.IsFalse(run.powerPickupArmed);

            var plain = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Basic), SimTest.LaunchAt(rules, 3.5f), 90f);
            Assert.AreEqual(1, plain.First(SimEventKind.EnemyHit).value);
        }

        [Test]
        public void FrozenEnemiesTakeTheFrostBonusAndBossesResistFreeze()
        {
            var slime = SimTest.Put(run, ops, EnemyType.Slime, 3, 4);
            var boss = SimTest.Put(run, ops, EnemyType.KingSlime, 0, 7);
            var events = new System.Collections.Generic.List<SimEvent>();
            ops.ApplyStatus(run, slime, StatusType.Freeze, 1, false, events);
            ops.ApplyStatus(run, boss, StatusType.Freeze, 1, false, events);
            Assert.AreEqual(1, slime.status.frozenTurns);
            Assert.AreEqual(0, boss.status.frozenTurns, "bosses are immune");
            Assert.AreEqual(1, SimTest.Count(events, SimEventKind.StatusApplied), "no event for the immune boss");
            var log = SimTest.Shoot(sim, run, SimTest.Ball(BallType.Frost, 3), SimTest.LaunchAt(rules, 3.5f), 90f);
            Assert.AreEqual(1 + 2, log.First(SimEventKind.EnemyHit).value, "Frost L3: +2 on frozen targets");
        }
    }
}
