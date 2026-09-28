#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Builds the deterministic wave schedule and static field objects for one stage (GDD §7).
    /// </summary>
    public sealed class StageGenerator
    {
        // Rows 0-1 receive spawning waves and the danger row must stay reachable, so objects live in between.
        const int FirstObjectRow = 2;
        // Every wave row keeps at least one open column so a ball can always slip past it.
        const int MinOpenColumnsPerRow = 1;

        readonly List<int> startColumns = new();
        readonly List<WaveEntryWeight> candidates = new();
        bool[] takenColumns = new bool[8];

        #region Public Methods

        /// <summary>
        /// Normal stage: minWaves..maxWaves rows drawn from the act's weighted enemy pool with a difficulty budget
        /// of baseBudgetPerRow + budgetGrowthPerStage × stageNumber per row (enemy cost subtracts from it), 20%
        /// (balance.pickupChancePerRow) of rows carry one pickup cell, and 0..maxFieldObjects static objects
        /// (pillar, crate, portal pair, mud) are placed on free grid cells outside rows 0-1 and the danger row.
        /// Boss stage: wave 0 holds the act boss at the top-centre footprint (col = (columns − width) / 2) plus
        /// bossEscortWaves escort rows with half the budget that avoid the boss columns; EnemyPhaseResolver spawns
        /// further escorts every bossEscortEveryNTurns while the boss lives.
        /// Field object ids are 1..N (portal pairs reference each other through pairId); crates get balance.crateHp
        /// scaled like enemy HP. Every draw comes from rng so a seed reproduces the plan.
        /// </summary>
        public StagePlan Generate(GameRules rules, int actIndex, int stageInAct, int stageNumber, SimRandom rng)
        {
            var act = rules.acts[actIndex];
            var plan = new StagePlan { actIndex = actIndex, stageInAct = stageInAct, isBoss = stageInAct >= act.normalStages };
            var budget = act.baseBudgetPerRow + act.budgetGrowthPerStage * stageNumber;
            var blockedStart = 0;
            var blockedWidth = 0;
            if (plan.isBoss)
            {
                var boss = rules.enemies[(int)act.bossType];
                blockedWidth = Math.Min(boss.width, rules.arena.columns);
                blockedStart = (rules.arena.columns - blockedWidth) / 2;
                var bossRow = new WaveRow();
                bossRow.cells.Add(new WaveCell { col = blockedStart, enemy = act.bossType });
                plan.waves.Add(bossRow);
                var escortBudget = Math.Max(1, budget / 2);
                for (var i = 0; i < act.bossEscortWaves; i++)
                {
                    plan.waves.Add(BuildRow(rules, act, escortBudget, blockedStart, blockedWidth, rng));
                }
            }
            else
            {
                var waveCount = rng.Range(act.minWaves, act.maxWaves + 1);
                for (var i = 0; i < waveCount; i++)
                {
                    plan.waves.Add(BuildRow(rules, act, budget, 0, 0, rng));
                }
            }

            PlaceFieldObjects(rules, act, plan, stageNumber, blockedStart, blockedWidth, rng);
            return plan;
        }

        #endregion

        #region Waves

        WaveRow BuildRow(GameRules rules, ActRules act, int budget, int blockedStart, int blockedWidth, SimRandom rng)
        {
            var columnCount = rules.arena.columns;
            ResetTaken(columnCount, blockedStart, blockedWidth);
            var row = new WaveRow();
            var free = CollectStarts(columnCount, 1);

            if (free > MinOpenColumnsPerRow + 1 && rng.Chance(rules.balance.pickupChancePerRow))
            {
                var col = startColumns[rng.Range(0, free)];
                row.cells.Add(new WaveCell { col = col, isPickup = true, pickup = (PickupType)rng.Range(0, SimConstants.PickupTypeCount) });
                Mark(col, 1);
                free--;
            }

            var maxEnemies = Math.Max(1, free - MinOpenColumnsPerRow);
            var remaining = budget;
            for (var placed = 0; placed < maxEnemies; placed++)
            {
                var entry = DrawEntry(rules, act, remaining, columnCount, rng);
                if (entry == null && placed == 0)
                {
                    entry = DrawEntry(rules, act, int.MaxValue, columnCount, rng);
                }

                if (entry == null) break;

                var width = rules.enemies[(int)entry.type].width;
                var starts = CollectStarts(columnCount, width);
                var col = startColumns[rng.Range(0, starts)];
                row.cells.Add(new WaveCell { col = col, enemy = entry.type });
                Mark(col, width);
                remaining -= Cost(entry);
            }

            return row;
        }

        /// <summary>Weighted draw among non-boss pool entries that fit the budget and still have room in the row.</summary>
        WaveEntryWeight? DrawEntry(GameRules rules, ActRules act, int budgetCap, int columnCount, SimRandom rng)
        {
            candidates.Clear();
            var total = 0f;
            foreach (var entry in act.enemyPool)
            {
                if (entry.weight <= 0f || Cost(entry) > budgetCap)
                {
                    continue;
                }

                var enemy = rules.enemies[(int)entry.type];
                if (enemy.isBoss || CollectStarts(columnCount, enemy.width) == 0)
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

        static int Cost(WaveEntryWeight entry) => Math.Max(1, entry.cost);

        #endregion

        #region Field Objects

        void PlaceFieldObjects(GameRules rules, ActRules act, StagePlan plan, int stageNumber, int blockedStart, int blockedWidth, SimRandom rng)
        {
            var arena = rules.arena;
            var lastRow = ArenaGeometry.DangerRow(arena) - 1;
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
