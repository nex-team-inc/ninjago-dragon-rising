#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Step 2 of the enemy phase: pickups drift one row (expiring at the danger row), then enemies advance front row
    /// first — straight down, else diagonally toward the centre column, else they stop — never past the danger row.
    /// </summary>
    internal sealed class EnemyAdvance
    {
        readonly GameRules rules;
        readonly BoardOps ops;
        readonly List<EnemyState> order = new();

        #region Life Cycle

        public EnemyAdvance(GameRules rules, BoardOps ops)
        {
            this.rules = rules;
            this.ops = ops;
        }

        #endregion

        #region Public Methods

        /// <summary>quakeRows extra rows apply to every enemy except quakeSourceId; frozen enemies stay put.</summary>
        public void Resolve(RunState run, List<EnemyState> frozen, int quakeRows, int quakeSourceId, List<SimEvent> events)
        {
            MovePickups(run, events);
            SortFrontFirst(run.board.enemies);
            var lastRow = ArenaGeometry.DangerRow(rules.arena);
            var centerX = rules.arena.columns * 0.5f;
            foreach (var e in order)
            {
                if (e.hp <= 0 || e.turnCounter <= 0 || frozen.Contains(e))
                {
                    continue;
                }
                var r = rules.enemies[(int)e.type];
                var rows = r.moveRows > 0 && EnemyPhaseResolver.IsDue(e.turnCounter, Mathf.Max(1, r.moveEveryNTurns)) ? r.moveRows : 0;
                if (e.id != quakeSourceId)
                {
                    rows += quakeRows;
                }
                if (rows <= 0) continue;
                var from = ops.Center(e);
                var moved = Move(run, e, rows, lastRow, centerX);
                if (moved == 0) continue;
                var ev = ops.EnemyEvent(SimEventKind.EnemyMoved, e, moved);
                ev.position = from;
                ev.position2 = ops.Center(e);
                events.Add(ev);
            }
        }

        #endregion

        #region Helpers

        int Move(RunState run, EnemyState e, int rows, int lastRow, float centerX)
        {
            var moved = 0;
            for (var i = 0; i < rows; i++)
            {
                if (e.row + e.height - 1 >= lastRow) break;
                if (ops.IsFootprintFree(run.board, e.col, e.row + 1, e.width, e.height, e))
                {
                    e.row++;
                    moved++;
                    continue;
                }
                var footprintCenter = e.col + e.width * 0.5f;
                var dir = footprintCenter < centerX - 0.01f ? 1 : footprintCenter > centerX + 0.01f ? -1 : 0;
                if (dir == 0 || !ops.IsFootprintFree(run.board, e.col + dir, e.row + 1, e.width, e.height, e))
                {
                    break;
                }
                e.col += dir;
                e.row++;
                moved++;
            }
            return moved;
        }

        void MovePickups(RunState run, List<SimEvent> events)
        {
            var pickups = run.board.pickups;
            var dangerRow = ArenaGeometry.DangerRow(rules.arena);
            for (var i = pickups.Count - 1; i >= 0; i--)
            {
                var p = pickups[i];
                var from = ArenaGeometry.CellCenter(rules.arena, p.col, p.row);
                if (p.row + 1 >= dangerRow)
                {
                    pickups.RemoveAt(i);
                    events.Add(new SimEvent { kind = SimEventKind.PickupExpired, targetId = p.id, pickup = p.type, position = from });
                    continue;
                }
                if (!ops.IsCellFree(run.board, p.col, p.row + 1)) continue;
                p.row++;
                events.Add(new SimEvent
                {
                    kind = SimEventKind.EnemyMoved,
                    targetId = p.id,
                    pickup = p.type,
                    flag = true,
                    value = 1,
                    position = from,
                    position2 = ArenaGeometry.CellCenter(rules.arena, p.col, p.row),
                });
            }
        }

        /// <summary>Insertion sort of a snapshot: lowest footprint bottom first, then left to right.</summary>
        void SortFrontFirst(List<EnemyState> enemies)
        {
            order.Clear();
            for (var i = 0; i < enemies.Count; i++)
            {
                order.Add(enemies[i]);
            }
            for (var i = 1; i < order.Count; i++)
            {
                var item = order[i];
                var j = i - 1;
                while (j >= 0 && IsBehind(order[j], item))
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = item;
            }
        }

        static bool IsBehind(EnemyState a, EnemyState b)
        {
            var aBottom = a.row + a.height;
            var bBottom = b.row + b.height;
            return aBottom < bBottom || (aBottom == bBottom && a.col > b.col);
        }

        #endregion
    }
}
