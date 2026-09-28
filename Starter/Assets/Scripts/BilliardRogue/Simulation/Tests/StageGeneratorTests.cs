#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation.Tests
{
    public class StageGeneratorTests
    {
        const int SeedCount = 60;

        static StagePlan Generate(GameRules rules, int actIndex, int stageInAct, int seed)
        {
            var stageNumber = actIndex * SimConstants.StagesPerAct + stageInAct;
            return new StageGenerator().Generate(rules, actIndex, stageInAct, stageNumber, new SimRandom(SimRandom.SeedToState(seed)));
        }

        static IEnumerable<(int act, int stage)> AllStages()
        {
            for (var act = 0; act < SimConstants.ActCount; act++)
            {
                for (var stage = 0; stage < SimConstants.StagesPerAct; stage++)
                {
                    yield return (act, stage);
                }
            }
        }

        [Test]
        public void SameSeedProducesAnIdenticalPlan()
        {
            var rules = TestRules.Create();
            foreach (var (act, stage) in AllStages())
            {
                var a = JsonUtility.ToJson(Generate(rules, act, stage, 1234));
                var b = JsonUtility.ToJson(Generate(rules, act, stage, 1234));
                Assert.AreEqual(a, b, $"act {act} stage {stage}");
            }
        }

        [Test]
        public void NormalStageWaveCountStaysWithinTheActRange()
        {
            var rules = TestRules.Create();
            for (var seed = 0; seed < SeedCount; seed++)
            {
                foreach (var (act, stage) in AllStages())
                {
                    var plan = Generate(rules, act, stage, seed);
                    if (plan.isBoss) continue;
                    var rulesAct = rules.acts[act];
                    Assert.That(plan.waves.Count, Is.InRange(rulesAct.minWaves, rulesAct.maxWaves));
                }
            }
        }

        [Test]
        public void EveryRowHasAnEnemyAndCellsUseDistinctColumnsInsideTheGrid()
        {
            var rules = TestRules.Create();
            var columns = rules.arena.columns;
            for (var seed = 0; seed < SeedCount; seed++)
            {
                foreach (var (act, stage) in AllStages())
                {
                    foreach (var wave in Generate(rules, act, stage, seed).waves)
                    {
                        var used = new bool[columns];
                        var enemies = 0;
                        foreach (var cell in wave.cells)
                        {
                            var width = cell.isPickup ? 1 : rules.enemies[(int)cell.enemy].width;
                            Assert.That(cell.col, Is.GreaterThanOrEqualTo(0));
                            Assert.That(cell.col + width, Is.LessThanOrEqualTo(columns));
                            for (var c = cell.col; c < cell.col + width; c++)
                            {
                                Assert.IsFalse(used[c], $"column {c} used twice (seed {seed}, act {act}, stage {stage})");
                                used[c] = true;
                            }

                            if (!cell.isPickup)
                            {
                                enemies++;
                            }
                        }

                        Assert.That(enemies, Is.GreaterThanOrEqualTo(1));
                    }
                }
            }
        }

        [Test]
        public void BossStageWaveZeroHoldsTheActBossAtTheTopCentre()
        {
            var rules = TestRules.Create();
            for (var act = 0; act < SimConstants.ActCount; act++)
            {
                var rulesAct = rules.acts[act];
                var plan = Generate(rules, act, rulesAct.normalStages, 77);
                Assert.IsTrue(plan.isBoss);
                Assert.AreEqual(1 + rulesAct.bossEscortWaves, plan.waves.Count);
                Assert.AreEqual(1, plan.waves[0].cells.Count);
                var bossCell = plan.waves[0].cells[0];
                var boss = rules.enemies[(int)rulesAct.bossType];
                Assert.AreEqual(rulesAct.bossType, bossCell.enemy);
                Assert.IsFalse(bossCell.isPickup);
                Assert.AreEqual((rules.arena.columns - boss.width) / 2, bossCell.col);

                for (var w = 1; w < plan.waves.Count; w++)
                {
                    foreach (var cell in plan.waves[w].cells)
                    {
                        var width = cell.isPickup ? 1 : rules.enemies[(int)cell.enemy].width;
                        var overlapsBoss = cell.col < bossCell.col + boss.width && cell.col + width > bossCell.col;
                        Assert.IsFalse(overlapsBoss, $"escort in boss columns (act {act}, wave {w})");
                        Assert.IsFalse(!cell.isPickup && rules.enemies[(int)cell.enemy].isBoss);
                    }
                }
            }
        }

        [Test]
        public void FieldObjectsAvoidSpawnAndDangerRowsAndNeverShareACell()
        {
            var rules = TestRules.Create();
            var dangerRow = ArenaGeometry.DangerRow(rules.arena);
            for (var seed = 0; seed < SeedCount; seed++)
            {
                foreach (var (act, stage) in AllStages())
                {
                    var plan = Generate(rules, act, stage, seed);
                    Assert.That(plan.fieldObjects.Count, Is.LessThanOrEqualTo(rules.acts[act].maxFieldObjects));
                    var ids = new HashSet<int>();
                    var columnsUsed = new HashSet<int>();
                    foreach (var obj in plan.fieldObjects)
                    {
                        Assert.That(obj.row, Is.GreaterThanOrEqualTo(2));
                        Assert.That(obj.row, Is.LessThan(dangerRow));
                        Assert.IsTrue(ArenaGeometry.IsInsideGrid(rules.arena, obj.col, obj.row));
                        Assert.IsTrue(ids.Add(obj.id), "duplicate field object id");
                        Assert.IsTrue(columnsUsed.Add(obj.col), "two field objects in one column");
                        Assert.That(obj.id, Is.GreaterThan(0));
                        if (obj.type == FieldObjectType.Crate)
                        {
                            Assert.That(obj.hp, Is.GreaterThan(0));
                        }
                    }
                }
            }
        }

        [Test]
        public void PortalPairsReferenceEachOther()
        {
            var rules = TestRules.Create();
            var portalsSeen = 0;
            for (var seed = 0; seed < 200; seed++)
            {
                foreach (var (act, stage) in AllStages())
                {
                    var plan = Generate(rules, act, stage, seed);
                    foreach (var obj in plan.fieldObjects)
                    {
                        if (obj.type != FieldObjectType.Portal)
                        {
                            Assert.AreEqual(-1, obj.pairId);
                            continue;
                        }

                        portalsSeen++;
                        var partner = plan.fieldObjects.Find(o => o.id == obj.pairId);
                        Assert.IsNotNull(partner, "portal without partner");
                        Assert.AreEqual(FieldObjectType.Portal, partner!.type);
                        Assert.AreEqual(obj.id, partner.pairId);
                        Assert.AreNotEqual(obj.id, partner.id);
                    }
                }
            }

            Assert.That(portalsSeen, Is.GreaterThan(0), "no portal generated across 200 seeds");
        }
    }
}
