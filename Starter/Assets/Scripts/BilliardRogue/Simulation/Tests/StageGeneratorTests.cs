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
        public void EveryStagePlansItsBatchesWithTenEnemiesAndAFewPickups()
        {
            var rules = TestRules.Create();
            for (var seed = 0; seed < SeedCount; seed++)
            {
                foreach (var (act, stage) in AllStages())
                {
                    var plan = Generate(rules, act, stage, seed);
                    var rulesAct = rules.acts[act];
                    Assert.AreEqual(0, plan.waves.Count, "no v1 row waves");
                    Assert.AreEqual(rulesAct.batchesPerStage, plan.batches.Count, $"act {act} stage {stage}");
                    foreach (var batch in plan.batches)
                    {
                        var enemies = 0;
                        var pickups = 0;
                        var seenPickup = false;
                        foreach (var entry in batch.entries)
                        {
                            if (entry.isPickup)
                            {
                                pickups++;
                                seenPickup = true;
                                continue;
                            }

                            Assert.IsFalse(seenPickup, "pickups come after the enemies (dropped first when cells run out)");
                            Assert.IsFalse(rules.enemies[(int)entry.enemy].isBoss, "no boss in a batch");
                            Assert.IsTrue(System.Array.Exists(rulesAct.enemyPool, e => e.type == entry.enemy && e.weight > 0f), $"{entry.enemy} not in the act {act} pool");
                            enemies++;
                        }

                        Assert.AreEqual(rulesAct.minEnemiesPerBatch, enemies);
                        Assert.AreEqual(rulesAct.pickupsPerBatch, pickups);
                    }
                }
            }
        }

        [Test]
        public void BatchCostStaysWithinTheBudgetAndGrowsWithTheStage()
        {
            var rules = TestRules.Create();
            for (var act = 0; act < SimConstants.ActCount; act++)
            {
                var rulesAct = rules.acts[act];
                var cheapest = int.MaxValue;
                foreach (var entry in rulesAct.enemyPool) cheapest = Mathf.Min(cheapest, Mathf.Max(1, entry.cost));
                var firstStageCost = 0f;
                var lastStageCost = 0f;
                for (var seed = 0; seed < SeedCount; seed++)
                {
                    for (var stage = 0; stage < rulesAct.normalStages; stage++)
                    {
                        var stageNumber = act * SimConstants.StagesPerAct + stage;
                        var budget = rulesAct.batchBudgetRows * (rulesAct.baseBudgetPerRow + rulesAct.budgetGrowthPerStage * stageNumber);
                        foreach (var batch in Generate(rules, act, stage, seed).batches)
                        {
                            var cost = 0;
                            foreach (var entry in batch.entries)
                            {
                                if (entry.isPickup) continue;
                                cost += Mathf.Max(1, System.Array.Find(rulesAct.enemyPool, e => e.type == entry.enemy).cost);
                            }

                            Assert.That(cost, Is.LessThanOrEqualTo(Mathf.Max(budget, rulesAct.minEnemiesPerBatch * cheapest)), $"act {act} stage {stage} seed {seed}");
                            if (stage == 0) firstStageCost += cost;
                            if (stage == rulesAct.normalStages - 1) lastStageCost += cost;
                        }
                    }
                }

                Assert.That(lastStageCost, Is.GreaterThanOrEqualTo(firstStageCost), $"act {act}: later stages are not cheaper");
            }
        }

        [Test]
        public void BossStagePlansEscortBatchesAndKeepsTheBossColumnsFreeOfObjects()
        {
            var rules = TestRules.Create();
            for (var act = 0; act < SimConstants.ActCount; act++)
            {
                var rulesAct = rules.acts[act];
                var boss = rules.enemies[(int)rulesAct.bossType];
                var bossCol = (rules.arena.columns - boss.width) / 2;
                for (var seed = 0; seed < SeedCount; seed++)
                {
                    var plan = Generate(rules, act, rulesAct.normalStages, seed);
                    Assert.IsTrue(plan.isBoss);
                    Assert.AreEqual(rulesAct.batchesPerStage, plan.batches.Count);
                    foreach (var obj in plan.fieldObjects)
                    {
                        Assert.IsFalse(obj.col >= bossCol && obj.col < bossCol + boss.width, $"object in boss columns (act {act}, seed {seed})");
                    }
                }
            }
        }

        [Test]
        public void FieldObjectsAvoidSpawnAndDangerRowsAndNeverShareACell()
        {
            var rules = TestRules.Create();
            var dangerRow = ArenaGeometry.DangerRow(rules.arena);
            var lastSpawnRow = rules.arena.rows - 1 - rules.acts[0].spawnForbiddenNearRows;
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
                        Assert.That(obj.row, Is.LessThanOrEqualTo(lastSpawnRow), "objects follow the spawn rule: not in the nearest rows");
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
