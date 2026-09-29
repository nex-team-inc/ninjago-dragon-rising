#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// GDD v2 §5 batch placement behind BoardOps.SpawnBatch: every entry takes a random free footprint in rows
    /// 0..SpawnLastRow (never the ActRules.spawnForbiddenNearRows rows nearest the player) that still leaves each row it
    /// covers with ActRules.minOpenColumnsPerRow free cells; one SimRandom draw per placed entry. Entries that find no
    /// cell are dropped (BatchSpawned.flag). Allocation-free apart from the spawned states.
    /// </summary>
    internal sealed class BoardSpawning
    {
        readonly GameRules rules;
        readonly List<int> cells = new();
        int[] rowFree = new int[16];

        public BoardSpawning(GameRules rules)
        {
            this.rules = rules;
        }

        /// <summary>Last grid row a batch may spawn into: rows − 1 − spawnForbiddenNearRows (at least 0).</summary>
        public static int SpawnLastRow(ArenaRules a, ActRules act)
        {
            return Mathf.Max(0, a.rows - 1 - Mathf.Max(0, act.spawnForbiddenNearRows));
        }

        public int Spawn(BoardOps ops, RunState run, SpawnBatch batch, int batchIndex, int skippedTurns, SimRandom rng, List<SimEvent> events)
        {
            var a = rules.arena;
            var act = rules.acts[run.actIndex];
            var lastRow = SpawnLastRow(a, act);
            var minOpen = Mathf.Max(0, act.minOpenColumnsPerRow);
            var b = run.board;
            var placed = 0;
            foreach (var entry in batch.entries)
            {
                var width = 1;
                var height = 1;
                if (!entry.isPickup)
                {
                    var r = rules.enemies[(int)entry.enemy];
                    width = Mathf.Max(1, r.width);
                    height = Mathf.Max(1, r.height);
                }

                if (!PickCell(b, width, height, lastRow, minOpen, rng, out var col, out var row)) continue;
                if (entry.isPickup)
                {
                    var pickup = new PickupState { id = b.nextId++, type = entry.pickup, col = col, row = row };
                    b.pickups.Add(pickup);
                    events.Add(new SimEvent
                    {
                        kind = SimEventKind.PickupSpawned,
                        targetId = pickup.id,
                        pickup = pickup.type,
                        position = ArenaGeometry.CellCenter(a, col, row),
                        flag = true,
                        value = batchIndex,
                        value2 = placed,
                    });
                }
                else
                {
                    ops.SpawnEnemy(run, entry.enemy, col, row, events);
                    var last = events.Count - 1;
                    var spawned = events[last];
                    spawned.flag = true;
                    spawned.value2 = placed;
                    events[last] = spawned;
                }

                placed++;
            }

            events.Add(new SimEvent
            {
                kind = SimEventKind.BatchSpawned,
                value = batchIndex,
                value2 = placed,
                flag = placed < batch.entries.Count,
                sourceId = skippedTurns,
            });
            return placed;
        }

        /// <summary>A uniformly random free width×height footprint with its bottom row at most lastRow, or false.</summary>
        bool PickCell(BoardState b, int width, int height, int lastRow, int minOpen, SimRandom rng, out int col, out int row)
        {
            var a = rules.arena;
            CountFreeCells(b, lastRow);
            cells.Clear();
            for (var r = 0; r + height - 1 <= lastRow; r++)
            {
                if (!RowsStayOpen(r, height, width, minOpen)) continue;
                for (var c = 0; c + width <= a.columns; c++)
                {
                    if (BoardOccupancy.IsFootprintFree(a, b, c, r, width, height, null))
                    {
                        cells.Add(r * a.columns + c);
                    }
                }
            }

            if (cells.Count == 0)
            {
                col = row = -1;
                return false;
            }

            var pick = cells[rng.Range(0, cells.Count)];
            col = pick % a.columns;
            row = pick / a.columns;
            return true;
        }

        void CountFreeCells(BoardState b, int lastRow)
        {
            var a = rules.arena;
            if (rowFree.Length <= lastRow)
            {
                rowFree = new int[lastRow + 1];
            }

            for (var r = 0; r <= lastRow; r++)
            {
                var free = 0;
                for (var c = 0; c < a.columns; c++)
                {
                    if (BoardOccupancy.IsFootprintFree(a, b, c, r, 1, 1, null)) free++;
                }

                rowFree[r] = free;
            }
        }

        bool RowsStayOpen(int row, int height, int width, int minOpen)
        {
            for (var r = row; r < row + height; r++)
            {
                if (rowFree[r] - width < minOpen) return false;
            }

            return true;
        }
    }
}
