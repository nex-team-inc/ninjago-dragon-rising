#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation.Tests
{
    /// <summary>Wave spawning, stage clear, reward rng persistence and the RunState JSON round-trip (TDD §3.6).</summary>
    public class RunFlowTests
    {
        GameRules rules = null!;
        RunFactory factory = null!;
        BoardOps ops = null!;

        [SetUp]
        public void SetUp()
        {
            rules = SimTest.Rules();
            factory = new RunFactory();
            ops = new BoardOps(rules);
        }

        static int PlannedEnemies(WaveRow wave)
        {
            var n = 0;
            foreach (var cell in wave.cells)
            {
                if (!cell.isPickup) n++;
            }
            return n;
        }

        [Test]
        public void BeginStageSpawnsTwoWavesAndEachPhaseSpawnsTheNext()
        {
            var run = factory.NewRun(rules, 3, 1);
            var rng = new SimRandom(run.rngState);
            var events = new List<SimEvent>();
            factory.BeginStage(rules, run, rng, events);
            Assert.AreEqual(2, run.nextWaveIndex);
            Assert.AreEqual(2, SimTest.Count(events, SimEventKind.WaveSpawned));
            Assert.AreEqual(PlannedEnemies(run.stage.waves[0]) + PlannedEnemies(run.stage.waves[1]), run.board.enemies.Count);
            foreach (var e in run.board.enemies)
            {
                Assert.That(e.row, Is.InRange(0, 1));
            }
            Assert.AreEqual(rng.State, run.rngState);

            var resolver = new EnemyPhaseResolver(rules, ops);
            var phase = SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(3, run.nextWaveIndex);
            var wave = phase.Find(e => e.kind == SimEventKind.WaveSpawned);
            Assert.AreEqual(0, wave.value);
            Assert.AreEqual(run.stage.waves[2].cells.Count, wave.value2, "row 0 was vacated, nothing dropped");
            Assert.IsFalse(wave.flag);
            Assert.AreEqual(4, wave.step);
        }

        [Test]
        public void WaveCellsRelocateToTheNearestFreeColumnInsteadOfVanishing()
        {
            var run = SimTest.NewRun(rules);
            SimTest.Put(run, ops, EnemyType.Totem, 2, 0);
            SimTest.Put(run, ops, EnemyType.Totem, 3, 0);
            var wave = new WaveRow();
            wave.cells.Add(new WaveCell { col = 2, enemy = EnemyType.Slime });
            wave.cells.Add(new WaveCell { col = 3, enemy = EnemyType.Slime });
            wave.cells.Add(new WaveCell { col = 5, isPickup = true, pickup = PickupType.Power });
            var events = new List<SimEvent>();
            ops.SpawnWaveRow(run, wave, 0, events);
            var spawned = events.Find(e => e.kind == SimEventKind.WaveSpawned);
            Assert.AreEqual(3, spawned.value2);
            Assert.IsFalse(spawned.flag);
            Assert.IsNotNull(ops.EnemyAt(run.board, 1, 0), "planned col 2 moved left to 1");
            Assert.IsNotNull(ops.EnemyAt(run.board, 4, 0), "planned col 3 moved right to 4");
            Assert.AreEqual(1, run.board.pickups.Count);
            Assert.AreEqual(5, run.board.pickups[0].col);

            run.board.pickups.Clear();
            for (var c = 0; c < rules.arena.columns; c++)
            {
                if (ops.IsCellFree(run.board, c, 0)) SimTest.Put(run, ops, EnemyType.Totem, c, 0);
            }
            events.Clear();
            ops.SpawnWaveRow(run, wave, 0, events);
            var dropped = events.Find(e => e.kind == SimEventKind.WaveSpawned);
            Assert.AreEqual(0, dropped.value2);
            Assert.IsTrue(dropped.flag, "a full row reports the drop");
        }

        [Test]
        public void StageClearsOnlyAfterEveryWaveSpawnedAndNoEnemyButBoneWallsRemain()
        {
            var run = SimTest.NewRun(rules);
            run.stage.waves.Add(new WaveRow());
            run.stage.waves.Add(new WaveRow());
            run.nextWaveIndex = 1;
            var slime = SimTest.Put(run, ops, EnemyType.Slime, 3, 4, 1);
            Assert.IsFalse(factory.IsStageCleared(run), "waves left");
            run.nextWaveIndex = 2;
            Assert.IsFalse(factory.IsStageCleared(run), "enemy left");
            var events = new List<SimEvent>();
            ops.DamageEnemy(run, slime, 5, new DamageSource { ballType = BallType.Basic }, events);
            Assert.AreEqual(1, SimTest.Count(events, SimEventKind.EnemyKilled));
            Assert.IsTrue(factory.IsStageCleared(run));
            SimTest.Put(run, ops, EnemyType.BoneWall, 0, 4);
            Assert.IsTrue(factory.IsStageCleared(run), "bone walls do not block the clear");

            events.Clear();
            factory.CompleteStage(rules, run, events);
            Assert.AreEqual(1, SimTest.Count(events, SimEventKind.StageCleared));
            Assert.IsTrue(run.awaitingReward);

            var bossRun = SimTest.NewRun(rules);
            bossRun.stage.isBoss = true;
            bossRun.stage.waves.Add(new WaveRow());
            var boss = SimTest.Put(bossRun, ops, EnemyType.KingSlime, 2, 0, 1);
            Assert.IsFalse(factory.IsStageCleared(bossRun));
            ops.DamageEnemy(bossRun, boss, 5, new DamageSource { ballType = BallType.Basic }, events);
            Assert.IsTrue(factory.IsStageCleared(bossRun), "boss dead, plan irrelevant");
        }

        [Test]
        public void RewardRollStoresTheRngStateSoASaveResumesTheSameRun()
        {
            var run = factory.NewRun(rules, 5, 1);
            run.playerHp = 10;
            var before = run.rngState;
            var rng = new SimRandom(before);
            var output = new List<RewardOption>();
            new RewardGenerator().Roll(rules, run, 2, rng, output);
            Assert.AreEqual(3, output.Count);
            Assert.AreNotEqual(before, run.rngState);
            Assert.AreEqual(rng.State, run.rngState);
            Assert.AreEqual(rng.NextUInt(), new SimRandom(run.rngState).NextUInt(), "a continued run draws the same next value");
        }

        [Test]
        public void BasicIsOfferedOnlyWhenTheBalanceAllowsIt()
        {
            var basicSeen = false;
            for (var seed = 0; seed < 100 && !basicSeen; seed++)
            {
                var run = factory.NewRun(rules, seed, 1);
                var output = new List<RewardOption>();
                new RewardGenerator().Roll(rules, run, 2, new SimRandom(SimRandom.SeedToState(seed)), output);
                foreach (var option in output)
                {
                    if (option.kind == RewardKind.NewBall && option.ballType == BallType.Basic) basicSeen = true;
                }
            }
            Assert.IsFalse(basicSeen, "default: no Basic card while ability balls exist");

            rules.balance.offerBasicBall = true;
            for (var seed = 0; seed < 100 && !basicSeen; seed++)
            {
                var run = factory.NewRun(rules, seed, 1);
                var output = new List<RewardOption>();
                new RewardGenerator().Roll(rules, run, 2, new SimRandom(SimRandom.SeedToState(seed)), output);
                foreach (var option in output)
                {
                    if (option.kind == RewardKind.NewBall && option.ballType == BallType.Basic) basicSeen = true;
                }
            }
            Assert.IsTrue(basicSeen, "offerBasicBall puts Basic back into the pool");
        }

        [Test]
        public void RunStateSurvivesAJsonRoundTrip()
        {
            var run = factory.NewRun(rules, 123456789, 2);
            var rng = new SimRandom(run.rngState);
            factory.BeginStage(rules, run, rng, new List<SimEvent>());
            run.rngState = ulong.MaxValue - 12345UL;
            var e = run.board.enemies[0];
            e.status.burn = 3;
            e.status.burnSpreads = true;
            e.halfHpSummonPending = true;
            run.pendingRewards.Add(new RewardOption { kind = RewardKind.UpgradeBall, ballType = BallType.Basic, bagIndex = 1, amount = 2 });
            var json = JsonUtility.ToJson(run);
            var back = JsonUtility.FromJson<RunState>(json);
            Assert.AreEqual(json, JsonUtility.ToJson(back));
            Assert.AreEqual(run.rngState, back.rngState);
            Assert.AreEqual(run.runId, back.runId);
            Assert.AreEqual(run.board.enemies.Count, back.board.enemies.Count);
            Assert.AreEqual(run.stage.waves.Count, back.stage.waves.Count);
            Assert.AreEqual(3, back.board.enemies[0].status.burn);
            Assert.IsTrue(back.board.enemies[0].status.burnSpreads);
            Assert.IsTrue(back.board.enemies[0].halfHpSummonPending);
            Assert.AreEqual(1, back.pendingRewards.Count);
            Assert.AreEqual(2, back.numPlayers);
        }
    }
}
