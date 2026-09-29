#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation.Tests
{
    /// <summary>Status ticks, freeze skip, advance with blocking and diagonal slide, danger attacks, boss spawns (TDD §3.6).</summary>
    public class EnemyPhaseTests
    {
        GameRules rules = null!;
        RunState run = null!;
        BoardOps ops = null!;
        EnemyPhaseResolver resolver = null!;
        SimRandom rng = null!;

        [SetUp]
        public void SetUp()
        {
            rules = SimTest.Rules();
            run = SimTest.NewRun(rules);
            ops = new BoardOps(rules);
            resolver = new EnemyPhaseResolver(rules, ops);
            rng = new SimRandom(SimRandom.SeedToState(1));
        }

        [Test]
        public void BurnDecaysEachTickAndPoisonPersists()
        {
            var burning = SimTest.Put(run, ops, EnemyType.Slime, 1, 3);
            var poisoned = SimTest.Put(run, ops, EnemyType.Slime, 4, 3);
            var scratch = new List<SimEvent>();
            ops.ApplyStatus(run, burning, StatusType.Burn, 2, false, scratch);
            ops.ApplyStatus(run, poisoned, StatusType.Poison, 2, false, scratch);

            var first = SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(2, SimTest.Count(first, SimEventKind.StatusTick));
            foreach (var ev in first)
            {
                if (ev.kind == SimEventKind.StatusTick) Assert.AreEqual(2, ev.value);
                if (ev.kind == SimEventKind.StatusTick) Assert.AreEqual(0, ev.step);
            }
            Assert.AreEqual(SimTest.ImmortalHp - 2, burning.hp);
            Assert.AreEqual(1, burning.status.burn);
            Assert.AreEqual(SimTest.ImmortalHp - 2, poisoned.hp);
            Assert.AreEqual(2, poisoned.status.poison);

            SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(SimTest.ImmortalHp - 3, burning.hp);
            Assert.AreEqual(0, burning.status.burn);
            Assert.AreEqual(SimTest.ImmortalHp - 4, poisoned.hp);
            Assert.AreEqual(2, poisoned.status.poison);
        }

        [Test]
        public void FrozenEnemySkipsExactlyOnePhase()
        {
            var frozen = SimTest.Put(run, ops, EnemyType.Slime, 0, 5);
            var free = SimTest.Put(run, ops, EnemyType.Slime, 6, 5);
            ops.ApplyStatus(run, frozen, StatusType.Freeze, 1, false, new List<SimEvent>());

            var events = SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(5, frozen.row, "frozen: no advance");
            Assert.AreEqual(6, free.row);
            Assert.AreEqual(0, frozen.status.frozenTurns);
            Assert.AreEqual(1, SimTest.Count(events, SimEventKind.FreezeExpired));
            Assert.AreEqual(4, events[events.Count - 1].step, "freeze expires in the spawn step");

            SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(6, frozen.row, "moves again the phase after");
        }

        [Test]
        public void BlockedEnemiesSlideDiagonallyTowardTheCentreOrStay()
        {
            var leftSlider = SimTest.Put(run, ops, EnemyType.Slime, 1, 5);
            SimTest.Put(run, ops, EnemyType.Totem, 1, 6);
            var rightSlider = SimTest.Put(run, ops, EnemyType.Slime, 5, 5);
            SimTest.Put(run, ops, EnemyType.Totem, 5, 6);
            var stuck = SimTest.Put(run, ops, EnemyType.Slime, 3, 5);
            SimTest.Put(run, ops, EnemyType.Totem, 3, 6);
            var walled = SimTest.Put(run, ops, EnemyType.Bat, 0, 2);
            SimTest.Put(run, ops, EnemyType.Totem, 0, 3);
            SimTest.Put(run, ops, EnemyType.Totem, 1, 3);

            var events = SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual((2, 6), (leftSlider.col, leftSlider.row), "slides down-right toward the centre");
            Assert.AreEqual((4, 6), (rightSlider.col, rightSlider.row), "slides down-left toward the centre");
            Assert.AreEqual((3, 5), (stuck.col, stuck.row), "centred enemy with a blocked cell below stays");
            Assert.AreEqual((0, 2), (walled.col, walled.row), "both cells below blocked: stays");
            Assert.AreEqual(2, SimTest.Count(events, SimEventKind.EnemyMoved));
        }

        [Test]
        public void EnemiesStopAtTheDangerRowAndAttackFromIt()
        {
            var attacker = SimTest.Put(run, ops, EnemyType.Slime, 6, 9);
            var arriving = SimTest.Put(run, ops, EnemyType.Bat, 2, 8);
            var events = SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(9, attacker.row);
            Assert.AreEqual(9, arriving.row, "a 2-row mover stops at the danger row");
            Assert.AreEqual(2, SimTest.Count(events, SimEventKind.EnemyAttack), "everyone standing in the danger row after the advance attacks");
            var attack = events.Find(e => e.kind == SimEventKind.EnemyAttack && e.targetId == attacker.id);
            Assert.AreEqual(attacker.attack, attack.value);
            Assert.IsFalse(attack.flag);
            Assert.AreEqual(3, attack.step);
            Assert.AreEqual(rules.balance.playerMaxHp - attacker.attack - arriving.attack, run.playerHp);

            SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(rules.balance.playerMaxHp - 2 * (attacker.attack + arriving.attack), run.playerHp);
        }

        [Test]
        public void DamagePerAttackMakesEveryAttackTakeTheSameHp()
        {
            rules.balance.damagePerAttack = 1;
            run.playerHp = run.playerMaxHp = 3;
            var slime = SimTest.Put(run, ops, EnemyType.Slime, 1, 9);
            var skeleton = SimTest.Put(run, ops, EnemyType.Skeleton, 5, 9);
            Assert.Greater(slime.attack + skeleton.attack, 2, "the attack stats alone would deal more");

            var events = SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(2, SimTest.Count(events, SimEventKind.PlayerDamaged));
            foreach (var ev in events)
            {
                if (ev.kind == SimEventKind.PlayerDamaged) Assert.AreEqual(1, ev.value);
            }

            Assert.AreEqual(1, run.playerHp);
            SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(0, run.playerHp, "the third attack takes the last HP");
            Assert.AreEqual(RunOutcome.Defeat, run.outcome);
        }

        [Test]
        public void RangedEnemiesTelegraphThenCastAndDoNotAlsoMelee()
        {
            var mage = SimTest.Put(run, ops, EnemyType.Mage, 5, 9);
            var first = SimTest.Resolve(resolver, run, rng);
            var telegraph = first.Find(e => e.kind == SimEventKind.EnemyAbilityTelegraph);
            Assert.AreEqual(EnemyPhaseResolver.TelegraphCast, telegraph.value);
            Assert.AreEqual(1, SimTest.Count(first, SimEventKind.EnemyAttack), "melee from the danger row on the off turn");

            var second = SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(1, SimTest.Count(second, SimEventKind.EnemyAttack), "the cast replaces the melee");
            var bolt = second.Find(e => e.kind == SimEventKind.EnemyAttack);
            Assert.IsTrue(bolt.flag);
            Assert.AreEqual(rules.enemies[(int)EnemyType.Mage].abilityValue, bolt.value);
            Assert.AreEqual(rules.balance.playerMaxHp - mage.attack - bolt.value, run.playerHp);
        }

        [Test]
        public void BossSpawnsBesideItselfAndStillAdvancesOnItsDueTurn()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                run = SimTest.NewRun(rules);
                var boss = SimTest.Put(run, ops, EnemyType.KingSlime, 2, 0);
                var phaseRng = new SimRandom(SimRandom.SeedToState(seed));
                SimTest.Resolve(resolver, run, phaseRng);
                Assert.AreEqual(0, boss.row, "cadence 2: no move on turn 1");
                var events = SimTest.Resolve(resolver, run, phaseRng);
                Assert.AreEqual(2, SimTest.Count(events, SimEventKind.EnemySpawned), $"seed {seed}: two slimes");
                Assert.AreEqual(1, boss.row, $"seed {seed}: the boss moved");
                foreach (var e in run.board.enemies)
                {
                    if (e == boss) continue;
                    Assert.IsFalse(e.row == 2 && e.col >= 2 && e.col <= 3, $"seed {seed}: spawn straight in front at ({e.col},{e.row})");
                }
            }
        }

        [Test]
        public void HalfHpSummonFiresOnceAndSurvivesASaveRoundTrip()
        {
            var lich = SimTest.Put(run, ops, EnemyType.BoneLich, 2, 0, 130);
            var events = new List<SimEvent>();
            ops.DamageEnemy(run, lich, 66, new DamageSource { ballType = BallType.Basic, ballId = 1 }, events);
            Assert.IsTrue(lich.bossHalfTriggered);
            Assert.IsTrue(lich.halfHpSummonPending);
            Assert.AreEqual(1, SimTest.Count(events, SimEventKind.BossPhaseChanged));

            var restored = JsonUtility.FromJson<RunState>(JsonUtility.ToJson(run));
            var restoredOps = new BoardOps(rules);
            var restoredResolver = new EnemyPhaseResolver(rules, restoredOps);
            var phase = SimTest.Resolve(restoredResolver, restored, rng);
            var skeletons = 0;
            foreach (var ev in phase)
            {
                if (ev.kind != SimEventKind.EnemySpawned) continue;
                Assert.AreEqual(EnemyType.Skeleton, ev.enemyType);
                Assert.AreEqual(lich.id, ev.sourceId);
                skeletons++;
            }
            Assert.AreEqual(2, skeletons);
            Assert.IsFalse(restored.board.enemies[0].halfHpSummonPending);

            var next = SimTest.Resolve(restoredResolver, restored, rng);
            Assert.AreEqual(0, SimTest.Count(next, SimEventKind.EnemySpawned), "the summon fires once");
        }

        [Test]
        public void PickupsDriftWithTheWavesAndExpireAtTheDangerRow()
        {
            run.board.pickups.Add(new PickupState { id = run.board.nextId++, type = PickupType.Heal, col = 3, row = 7 });
            var first = SimTest.Resolve(resolver, run, rng);
            var moved = first.Find(e => e.kind == SimEventKind.EnemyMoved);
            Assert.IsTrue(moved.flag);
            Assert.AreEqual(PickupType.Heal, moved.pickup);
            Assert.AreEqual(8, run.board.pickups[0].row);
            var second = SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(1, SimTest.Count(second, SimEventKind.PickupExpired));
            Assert.AreEqual(0, run.board.pickups.Count);
        }
    }
}
