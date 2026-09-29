#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Builds the deterministic batch plan and static field objects for one stage (GDD §7, v2 §5).
    /// </summary>
    public sealed class StageGenerator
    {
        // Rows 0-1 hold the boss footprint on boss stages; objects start below it.
        const int FirstObjectRow = 2;

        readonly List<int> startColumns = new();
        readonly List<WaveEntryWeight> candidates = new();
        bool[] takenColumns = new bool[8];

        #region Public Methods

        /// <summary>
        /// GDD v2 §5: act.batchesPerStage batches, each with act.minEnemiesPerBatch enemies drawn from the act's weighted
        /// pool within a budget of batchBudgetRows × (baseBudgetPerRow + budgetGrowthPerStage × stageNumber) (a boss
        /// stage's escort batches get half; when the budget runs short the cheapest entries fill the batch) followed by
        /// act.pickupsPerBatch pickups. Where they spawn is decided when they spawn (BoardOps.SpawnBatch). The boss of a
        /// boss stage is not in the plan: RunFactory.BeginStage places act.bossType at the top centre.
        /// 0..maxFieldObjects static objects (pillar, crate, portal pair, mud) go on distinct columns in rows
        /// 2..min(dangerRow − 1, last spawn row), outside the boss columns. Field object ids are 1..N (portal pairs
        /// reference each other through pairId); crates get balance.crateHp scaled like enemy HP. Every draw comes from
        /// rng so a seed reproduces the plan.
        /// </summary>
        public StagePlan Generate(GameRules rules, int actIndex, int stageInAct, int stageNumber, SimRandom rng)
        {
            var act = rules.acts[actIndex];
            var plan = new StagePlan { actIndex = actIndex, stageInAct = stageInAct, isBoss = stageInAct >= act.normalStages };
            var budget = Math.Max(1, act.batchBudgetRows) * (act.baseBudgetPerRow + act.budgetGrowthPerStage * stageNumber);
            var blockedStart = 0;
            var blockedWidth = 0;
            if (plan.isBoss)
            {
                var boss = rules.enemies[(int)act.bossType];
                blockedWidth = Math.Min(boss.width, rules.arena.columns);
                blockedStart = (rules.arena.columns - blockedWidth) / 2;
                budget = Math.Max(1, budget / 2);
            }

            var batchCount = Math.Max(1, act.batchesPerStage);
            for (var i = 0; i < batchCount; i++)
            {
                plan.batches.Add(BuildBatch(rules, act, budget, rng));
            }

            PlaceFieldObjects(rules, act, plan, stageNumber, blockedStart, blockedWidth, rng);
            return plan;
        }

        #endregion

        #region Batches

        SpawnBatch BuildBatch(GameRules rules, ActRules act, int budget, SimRandom rng)
        {
            var batch = new SpawnBatch();
            var count = Math.Max(1, act.minEnemiesPerBatch);
            var minCost = CheapestCost(rules, act);
            var remaining = budget;
            for (var slot = 0; slot < count; slot++)
            {
                // Leave enough budget for the cheapest entry in every slot still to fill.
                var cap = remaining - (count - slot - 1) * minCost;
                var entry = DrawEntry(rules, act, cap, rng) ?? DrawEntry(rules, act, minCost, rng);
                if (entry == null) break;
                batch.entries.Add(new BatchEntry { enemy = entry.type });
                remaining -= Cost(entry);
            }

            for (var p = 0; p < act.pickupsPerBatch; p++)
            {
                batch.entries.Add(new BatchEntry { isPickup = true, pickup = (PickupType)rng.Range(0, SimConstants.PickupTypeCount) });
            }

            return batch;
        }

        /// <summary>Weighted draw among non-boss pool entries whose cost fits the cap; null when none does.</summary>
        WaveEntryWeight? DrawEntry(GameRules rules, ActRules act, int budgetCap, SimRandom rng)
        {
            candidates.Clear();
            var total = 0f;
            foreach (var entry in act.enemyPool)
            {
                if (entry.weight <= 0f || Cost(entry) > budgetCap || rules.enemies[(int)entry.type].isBoss)
                {
                    continue;
                }

                candidates.Add(entry);
                total += entry.weight;
            }

            if (candidates.Count == 0) return null;
            var pick = rng.Value01() * total;
            for (var i = 0; i < candidates.Count; i++)
            {
                pick -= candidates[i].weight;
                if (pick < 0f) return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        static int CheapestCost(GameRules rules, ActRules act)
        {
            var cheapest = int.MaxValue;
            foreach (var entry in act.enemyPool)
            {
                if (entry.weight <= 0f || rules.enemies[(int)entry.type].isBoss) continue;
                cheapest = Math.Min(cheapest, Cost(entry));
            }

            return cheapest == int.MaxValue ? 1 : cheapest;
        }

        static int Cost(WaveEntryWeight entry) => Math.Max(1, entry.cost);

        #endregion

        #region Field Objects

        void PlaceFieldObjects(GameRules rules, ActRules act, StagePlan plan, int stageNumber, int blockedStart, int blockedWidth, SimRandom rng)
        {
            var arena = rules.arena;
            // Objects follow the spawn rule too: never in the rows nearest the player.
            var lastRow = Math.Min(ArenaGeometry.DangerRow(arena) - 1, BoardSpawning.SpawnLastRow(arena, act));
            if (lastRow < FirstObjectRow) return;

            ResetTaken(arena.columns, blockedStart, blockedWidth);
            // One object per column (never walls off a column) and at least one column left open.
            var count = Math.Min(rng.Range(0, act.maxFieldObjects + 1), CollectStarts(arena.columns, 1) - 1);
            var nextId = 1;
            var portalPlaced = false;
            while (plan.fieldObjects.Count < count)
            {
                var type = (FieldObjectType)rng.Range(0, SimConstants.FieldObjectTypeCount);
                if (type == FieldObjectType.Portal && (portalPlaced || count - plan.fieldObjects.Count < 2))
                {
                    type = rng.Range(0, 3) switch
                    {
                        0 => FieldObjectType.Pillar,
                        1 => FieldObjectType.Crate,
                        _ => FieldObjectType.Mud,
                    };
                }

                var first = AddObject(rules, plan, type, nextId++, stageNumber, lastRow, rng);
                if (type != FieldObjectType.Portal) continue;

                var second = AddObject(rules, plan, type, nextId++, stageNumber, lastRow, rng);
                first.pairId = second.id;
                second.pairId = first.id;
                portalPlaced = true;
            }
        }

        FieldObjectState AddObject(GameRules rules, StagePlan plan, FieldObjectType type, int id, int stageNumber, int lastRow, SimRandom rng)
        {
            var free = CollectStarts(rules.arena.columns, 1);
            var col = startColumns[rng.Range(0, free)];
            Mark(col, 1);
            var state = new FieldObjectState
            {
                id = id,
                type = type,
                col = col,
                row = rng.Range(FirstObjectRow, lastRow + 1),
                hp = type == FieldObjectType.Crate ? ScaledCrateHp(rules.balance, stageNumber) : 0,
            };
            plan.fieldObjects.Add(state);
            return state;
        }

        static int ScaledCrateHp(BalanceRules balance, int stageNumber)
        {
            return Math.Max(1, Mathf.RoundToInt(balance.crateHp * (1f + balance.hpScalePerStage * stageNumber)));
        }

        #endregion

        #region Column Helpers

        void ResetTaken(int columnCount, int blockedStart, int blockedWidth)
        {
            if (takenColumns.Length < columnCount)
            {
                takenColumns = new bool[columnCount];
            }

            for (var c = 0; c < columnCount; c++)
            {
                takenColumns[c] = c >= blockedStart && c < blockedStart + blockedWidth;
            }
        }

        void Mark(int col, int width)
        {
            for (var c = col; c < col + width; c++)
            {
                takenColumns[c] = true;
            }
        }

        /// <summary>Fills startColumns with every column where a footprint of this width fits on free columns.</summary>
        int CollectStarts(int columnCount, int width)
        {
            startColumns.Clear();
            for (var start = 0; start + width <= columnCount; start++)
            {
                var fits = true;
                for (var c = start; c < start + width; c++)
                {
                    if (!takenColumns[c]) continue;
                    fits = false;
                    break;
                }

                if (fits)
                {
                    startColumns.Add(start);
                }
            }

            return startColumns.Count;
        }

        #endregion
    }
}
