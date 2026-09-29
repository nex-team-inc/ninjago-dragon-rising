#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation.Tests
{
    /// <summary>
    /// GDD v2 §5 batch spawning: batch size, the forbidden rows nearest the player, cadence, skip-empty-turn, stage clear,
    /// boss stages, determinism, save round-trip and the v1 save upgrade.
    /// </summary>
    public class BatchSpawnTests
    {
        const int SeedCount = 40;

        GameRules rules = null!;
        RunFactory factory = null!;
        BoardOps ops = null!;
        EnemyPhaseResolver resolver = null!;

        [SetUp]
        public void SetUp()
        {
            rules = SimTest.Rules();
            factory = new RunFactory();
            ops = new BoardOps(rules);
            resolver = new EnemyPhaseResolver(rules, ops);
        }

        #region Helpers

        int LastSpawnRow => rules.arena.rows - 1 - rules.acts[0].spawnForbiddenNearRows;

        /// <summary>A run at stageNumber (act / stage derived like the flow does) with its stage begun; immortal player.</summary>
        (RunState run, SimRandom rng, List<SimEvent> events) Begin(int seed, int stageNumber)
        {
            var run = factory.NewRun(rules, seed, 1);
            run.actIndex = stageNumber / SimConstants.StagesPerAct;
            run.stageInAct = stageNumber % SimConstants.StagesPerAct;
            run.stageNumber = stageNumber;
            run.playerHp = run.playerMaxHp = SimTest.ImmortalHp;
            var rng = new SimRandom(run.rngState);
            var events = new List<SimEvent>();
            factory.BeginStage(rules, run, rng, events);
            return (run, rng, events);
        }

        static List<SimEvent> OfKind(List<SimEvent> events, SimEventKind kind) => events.FindAll(e => e.kind == kind);

        static int Enemies(SpawnBatch batch)
        {
            var n = 0;
            foreach (var entry in batch.entries)
            {
                if (!entry.isPickup) n++;
            }

            return n;
        }

        #endregion

        [Test]
        public void TheFirstBatchPopsInWithAtLeastTenEnemiesAndNeverInTheNearestThreeRows()
        {
            Assert.AreEqual(6, LastSpawnRow, "10 rows, 3 forbidden: rows 0..6");
            for (var seed = 0; seed < SeedCount; seed++)
            {
                for (var stage = 0; stage < SimConstants.StageCount; stage++)
                {
                    var (run, _, events) = Begin(seed, stage);
                    var spawned = OfKind(events, SimEventKind.EnemySpawned);
                    var popIns = spawned.FindAll(e => e.flag);
                    var batch = OfKind(events, SimEventKind.BatchSpawned);
                    Assert.AreEqual(1, batch.Count, $"seed {seed} stage {stage}");
                    Assert.AreEqual(0, batch[0].value, "batch index");
                    Assert.IsFalse(batch[0].flag, "a fresh board has room for the whole batch");
                    Assert.AreEqual(0, batch[0].sourceId, "not a skip");
                    Assert.That(popIns.Count, Is.GreaterThanOrEqualTo(rules.acts[run.actIndex].minEnemiesPerBatch));
                    Assert.AreEqual(run.stage.isBoss ? 1 : 0, spawned.Count - popIns.Count, "only the boss appears without the pop-in flag");
                    for (var i = 0; i < popIns.Count; i++)
                    {
                        Assert.AreEqual(i, popIns[i].value2, "stagger index counts up in spawn order");
                    }

                    foreach (var e in run.board.enemies)
                    {
                        if (rules.enemies[(int)e.type].isBoss) continue;
                        Assert.That(e.row + e.height - 1, Is.LessThanOrEqualTo(LastSpawnRow), $"seed {seed} stage {stage}: {e.type} at row {e.row}");
                    }

                    foreach (var p in run.board.pickups)
                    {
                        Assert.That(p.row, Is.LessThanOrEqualTo(LastSpawnRow));
                    }

                    Assert.AreEqual(run.board.pickups.Count, OfKind(events, SimEventKind.PickupSpawned).Count);
                    Assert.AreEqual(1, run.nextBatchIndex);
                    Assert.AreEqual(rules.acts[run.actIndex].spawnEveryNTurns, run.nextBatchTurn);
                }
            }
        }

        [Test]
        public void EveryRowKeepsAnOpenColumnWhenABatchSpawns()
        {
            var minOpen = rules.acts[0].minOpenColumnsPerRow;
            for (var seed = 0; seed < SeedCount; seed++)
            {
                var (run, _, _) = Begin(seed, 2);
                for (var row = 0; row <= LastSpawnRow; row++)
                {
                    var free = 0;
                    for (var col = 0; col < rules.arena.columns; col++)
                    {
                        if (ops.IsCellFree(run.board, col, row)) free++;
                    }

                    Assert.That(free, Is.GreaterThanOrEqualTo(minOpen), $"seed {seed} row {row}");
                }
            }
        }

        [Test]
        public void BatchesSpawnEveryThreeTurnsStartingOnTheFirstTurnThenStop()
        {
            var (run, rng, _) = Begin(7, 0);
            var spawnTurns = new List<int>();
            for (var phase = 0; phase < 10; phase++)
            {
                var events = SimTest.Resolve(resolver, run, rng);
                var batch = events.FindAll(e => e.kind == SimEventKind.BatchSpawned);
                if (batch.Count == 0) continue;
                Assert.AreEqual(1, batch.Count);
                Assert.AreEqual(4, batch[0].step, "spawns at the end of the phase");
                Assert.AreEqual(0, batch[0].sourceId, "the field was never empty");
                spawnTurns.Add(run.turnInStage + 1);
            }

            CollectionAssert.AreEqual(new[] { 4, 7 }, spawnTurns, "batch 1 on turn 1 (stage start), then turns 4 and 7");
            Assert.AreEqual(3, run.nextBatchIndex);
            Assert.AreEqual(0, StageSchedule.BatchesLeft(run));
            Assert.AreEqual(10, run.stats.turns);
        }

        [Test]
        public void AnEmptyFieldSkipsToTheNextBatchAndJumpsTheTurn()
        {
            var (run, rng, _) = Begin(11, 1);
            run.board.enemies.Clear();
            var events = SimTest.Resolve(resolver, run, rng);
            var batch = events.Find(e => e.kind == SimEventKind.BatchSpawned);
            Assert.AreEqual(SimEventKind.BatchSpawned, batch.kind, "the empty turn spawned the next batch");
            Assert.AreEqual(1, batch.value);
            Assert.AreEqual(2, batch.sourceId, "turns 2 and 3 skipped");
            Assert.AreEqual(3, run.turnInStage, "the next turn shown is 4");
            Assert.AreEqual(1, run.stats.turns, "only the played turn counts");
            Assert.AreEqual(6, run.nextBatchTurn);
            Assert.That(run.board.enemies.Count, Is.GreaterThanOrEqualTo(rules.acts[0].minEnemiesPerBatch));

            // Bone Walls do not keep a turn alive.
            run.board.enemies.Clear();
            SimTest.Put(run, ops, EnemyType.BoneWall, 0, 3);
            events = SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(1, SimTest.Count(events, SimEventKind.BatchSpawned));
            Assert.AreEqual(6, run.turnInStage);

            // No batch left: an empty field is a cleared stage, nothing spawns and the turn does not jump.
            run.board.enemies.Clear();
            events = SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(0, SimTest.Count(events, SimEventKind.BatchSpawned));
            Assert.AreEqual(7, run.turnInStage);
            Assert.IsTrue(factory.IsStageCleared(run));
        }

        [Test]
        public void StageClearsOnlyWhenNoBatchIsLeftAndOnlyBoneWallsRemain()
        {
            var (run, rng, _) = Begin(5, 0);
            run.board.enemies.Clear();
            Assert.IsFalse(factory.IsStageCleared(run), "two batches left");
            SimTest.Resolve(resolver, run, rng);
            run.board.enemies.Clear();
            SimTest.Resolve(resolver, run, rng);
            Assert.AreEqual(0, StageSchedule.BatchesLeft(run));
            Assert.IsFalse(factory.IsStageCleared(run), "the last batch is on the field");
            run.board.enemies.Clear();
            SimTest.Put(run, ops, EnemyType.BoneWall, 1, 2);
            Assert.IsTrue(factory.IsStageCleared(run), "bone walls do not block the clear");
        }

        [Test]
        public void FullRowsDropEntriesInsteadOfSpawningNearThePlayer()
        {
            var run = SimTest.NewRun(rules);
            // Fill rows 0..6 except one column per row (minOpenColumnsPerRow keeps it free) and leave rows 7..9 empty.
            for (var row = 0; row <= LastSpawnRow; row++)
            {
                for (var col = 1; col < rules.arena.columns; col++)
                {
                    SimTest.Put(run, ops, EnemyType.Totem, col, row);
                }
            }

            var batch = new SpawnBatch();
            for (var i = 0; i < 10; i++)
            {
                batch.entries.Add(new BatchEntry { enemy = EnemyType.Slime });
            }

            var before = run.board.enemies.Count;
            var events = new List<SimEvent>();
            ops.SpawnBatch(run, batch, 0, 0, new SimRandom(99), events);
            var summary = events.Find(e => e.kind == SimEventKind.BatchSpawned);
            Assert.AreEqual(0, summary.value2, "no row may lose its last open column");
            Assert.IsTrue(summary.flag);
            Assert.AreEqual(before, run.board.enemies.Count);

            // Free cols 1 and 2 of row 3: two of its three free cells fill, one stays open.
            run.board.enemies.RemoveAll(e => e.row == 3 && e.col <= 2);
            events.Clear();
            ops.SpawnBatch(run, batch, 0, 0, new SimRandom(5), events);
            summary = events.Find(e => e.kind == SimEventKind.BatchSpawned);
            Assert.AreEqual(2, summary.value2, "three free cells in row 3, one must stay open");
            foreach (var e in run.board.enemies)
            {
                Assert.That(e.row, Is.LessThanOrEqualTo(LastSpawnRow));
            }
        }

        [Test]
        public void BossStagePlacesTheBossTopCentreWithAnEscortBatchAndCyclesEscortsWhileItLives()
        {
            for (var act = 0; act < SimConstants.ActCount; act++)
            {
                var stageNumber = act * SimConstants.StagesPerAct + rules.acts[act].normalStages;
                var (run, rng, events) = Begin(21 + act, stageNumber);
                Assert.IsTrue(run.stage.isBoss);
                var bossType = rules.acts[act].bossType;
                var boss = run.board.enemies.Find(e => e.type == bossType);
                Assert.IsNotNull(boss, $"act {act}: boss spawned");
                Assert.AreEqual((rules.arena.columns - boss!.width) / 2, boss.col);
                Assert.AreEqual(0, boss.row);
                Assert.AreEqual(2, boss.height, "rows 0-1");
                var escorts = run.board.enemies.Count - 1;
                Assert.That(escorts, Is.GreaterThanOrEqualTo(rules.acts[act].minEnemiesPerBatch), $"act {act}: first escort batch");
                foreach (var batch in run.stage.batches)
                {
                    foreach (var entry in batch.entries)
                    {
                        Assert.IsFalse(!entry.isPickup && rules.enemies[(int)entry.enemy].isBoss, "no boss in an escort batch");
                    }
                }

                // Escorts keep coming past the planned batches while the boss lives.
                var spawnedBatches = 1;
                for (var phase = 0; phase < 3 * rules.acts[act].batchesPerStage + 1; phase++)
                {
                    spawnedBatches += SimTest.Count(SimTest.Resolve(resolver, run, rng), SimEventKind.BatchSpawned);
                }

                Assert.That(spawnedBatches, Is.GreaterThan(rules.acts[act].batchesPerStage), $"act {act}: escorts cycle");
                Assert.IsTrue(StageSchedule.HasBatchesLeft(rules, run));

                // Boss dead: nothing spawns any more, not even on an empty field.
                run.board.enemies.Clear();
                Assert.IsFalse(StageSchedule.HasBatchesLeft(rules, run));
                Assert.IsTrue(factory.IsStageCleared(run));
                for (var phase = 0; phase < 4; phase++)
                {
                    Assert.AreEqual(0, SimTest.Count(SimTest.Resolve(resolver, run, rng), SimEventKind.BatchSpawned));
                }
            }
        }

        [Test]
        public void TheSameSeedSpawnsTheSameBoard()
        {
            string Play(int seed)
            {
                var (run, rng, _) = Begin(seed, 5);
                for (var phase = 0; phase < 4; phase++)
                {
                    if (phase == 1) run.board.enemies.Clear();
                    SimTest.Resolve(resolver, run, rng);
                }

                return JsonUtility.ToJson(run.board) + run.turnInStage + "/" + run.nextBatchIndex + "/" + run.rngState;
            }

            Assert.AreEqual(Play(31), Play(31));
            Assert.AreNotEqual(Play(31), Play(32));
        }

        [Test]
        public void ASavedRunContinuesWithTheSameBatches()
        {
            var (run, rng, _) = Begin(41, 1);
            SimTest.Resolve(resolver, run, rng);
            var json = JsonUtility.ToJson(run);
            var loaded = JsonUtility.FromJson<RunState>(json);
            Assert.AreEqual(json, JsonUtility.ToJson(loaded));
            Assert.AreEqual(run.stage.batches.Count, loaded.stage.batches.Count);
            Assert.AreEqual(run.nextBatchIndex, loaded.nextBatchIndex);
            Assert.AreEqual(run.nextBatchTurn, loaded.nextBatchTurn);
            Assert.AreEqual(Enemies(run.stage.batches[2]), Enemies(loaded.stage.batches[2]));

            var original = new List<SimEvent>();
            var resumed = new List<SimEvent>();
            var loadedRng = new SimRandom(loaded.rngState);
            for (var phase = 0; phase < 3; phase++)
            {
                original.AddRange(SimTest.Resolve(resolver, run, rng));
                resumed.AddRange(SimTest.Resolve(resolver, loaded, loadedRng));
            }

            Assert.AreEqual(1, SimTest.Count(original, SimEventKind.BatchSpawned), "batch 2 spawned on turn 4");
            Assert.AreEqual(original.Count, resumed.Count);
            Assert.AreEqual(JsonUtility.ToJson(run), JsonUtility.ToJson(loaded), "the continued run matches");
        }

        [Test]
        public void AV1SaveMidStageContinuesWithBatches()
        {
            // A stage generated before GDD v2 §5: row waves, no batches, two waves already on the board.
            var run = SimTest.NewRun(rules);
            run.playerHp = run.playerMaxHp = SimTest.ImmortalHp;
            for (var w = 0; w < 6; w++)
            {
                var wave = new WaveRow();
                for (var c = 0; c < 4; c++)
                {
                    wave.cells.Add(new WaveCell { col = c, enemy = EnemyType.Slime });
                }

                wave.cells.Add(new WaveCell { col = 5, isPickup = true, pickup = PickupType.Heal });
                run.stage.waves.Add(wave);
            }

            run.nextWaveIndex = 2;
            run.turnInStage = 1;
            SimTest.Put(run, ops, EnemyType.Bat, 3, 4);
            Assert.IsTrue(StageSchedule.IsLegacyPlan(run.stage));
            Assert.AreEqual(4, StageSchedule.BatchesLeft(run), "unspawned v1 waves count as left");
            Assert.IsFalse(factory.IsStageCleared(run));

            var rng = new SimRandom(run.rngState);
            var events = SimTest.Resolve(resolver, run, rng);
            Assert.IsFalse(StageSchedule.IsLegacyPlan(run.stage));
            Assert.AreEqual(2, run.stage.batches.Count, "16 remaining slimes pack into 10 + 6");
            Assert.AreEqual(10, Enemies(run.stage.batches[0]));
            Assert.AreEqual(6, Enemies(run.stage.batches[1]));
            Assert.AreEqual(run.stage.waves.Count, run.nextWaveIndex, "v1 waves are consumed");
            Assert.AreEqual(1, SimTest.Count(events, SimEventKind.BatchSpawned), "the first converted batch spawns at once");
            Assert.AreEqual(0, SimTest.Count(events, SimEventKind.WaveSpawned));
            Assert.AreEqual(1, run.nextBatchIndex);
            foreach (var e in run.board.enemies)
            {
                Assert.That(e.row, Is.LessThanOrEqualTo(LastSpawnRow + 1), "only the moved bat can be below the spawn rows");
            }

            var json = JsonUtility.ToJson(run);
            Assert.AreEqual(json, JsonUtility.ToJson(JsonUtility.FromJson<RunState>(json)), "the converted plan saves");
        }
    }
}
