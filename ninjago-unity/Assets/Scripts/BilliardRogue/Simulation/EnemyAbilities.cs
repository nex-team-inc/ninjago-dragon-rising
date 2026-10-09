#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Step 1 of the enemy phase: shield rotation, the one-time half-HP summon, ranged bolts, heals, quakes and
    /// recurring spawns, each telegraphed the phase before a cadence-N action fires. Records which enemies cast this
    /// phase (they skip the danger-row melee) and the quake rows the advance step adds.
    /// </summary>
    internal sealed class EnemyAbilities
    {
        readonly GameRules rules;
        readonly BoardOps ops;
        readonly List<EnemyState> order = new();
        readonly List<EnemyState> cast = new();
        readonly List<EnemyState> neighbours = new();
        readonly List<GridPos> cells = new();

        #region Life Cycle

        public EnemyAbilities(GameRules rules, BoardOps ops)
        {
            this.rules = rules;
            this.ops = ops;
        }

        #endregion

        /// <summary>Extra rows every enemy except QuakeSourceId advances this phase.</summary>
        public int QuakeRows { get; private set; }

        public int QuakeSourceId { get; private set; } = -1;

        #region Public Methods

        public bool HasCast(EnemyState e) => cast.Contains(e);

        /// <summary>Frozen enemies and enemies spawned this phase (turnCounter 0) skip the step.</summary>
        public void Resolve(RunState run, List<EnemyState> frozen, SimRandom rng, List<SimEvent> events)
        {
            cast.Clear();
            QuakeRows = 0;
            QuakeSourceId = -1;
            order.Clear();
            foreach (var e in run.board.enemies)
            {
                order.Add(e);
            }
            foreach (var e in order)
            {
                if (e.hp <= 0 || e.turnCounter <= 0 || frozen.Contains(e))
                {
                    continue;
                }
                var r = rules.enemies[(int)e.type];
                var tc = e.turnCounter;
                if (r.rotatingShield)
                {
                    RotateShield(e, events);
                }
                if (e.halfHpSummonPending)
                {
                    e.halfHpSummonPending = false;
                    if (r.halfHpSummonCount > 0)
                    {
                        SpawnAround(run, e, r.halfHpSummonType, r.halfHpSummonCount, rng, events);
                    }
                }
                if (r.ranged && r.abilityEveryNTurns > 0)
                {
                    if (EnemyPhaseResolver.IsDue(tc, r.abilityEveryNTurns))
                    {
                        Bolt(run, e, r, events);
                    }
                    else
                    {
                        Telegraph(e, tc, r.abilityEveryNTurns, EnemyPhaseResolver.TelegraphCast, events);
                    }
                }
                else if (r.healAmount > 0)
                {
                    var every = Mathf.Max(1, r.abilityEveryNTurns);
                    if (EnemyPhaseResolver.IsDue(tc, every))
                    {
                        HealNeighbours(run, e, r.healAmount, events);
                    }
                    else
                    {
                        Telegraph(e, tc, every, EnemyPhaseResolver.TelegraphHeal, events);
                    }
                }
                else if (r.abilityEveryNTurns > 0 && r.abilityValue > 0)
                {
                    if (EnemyPhaseResolver.IsDue(tc, r.abilityEveryNTurns))
                    {
                        Quake(e, r.abilityValue, events);
                    }
                    else
                    {
                        Telegraph(e, tc, r.abilityEveryNTurns, EnemyPhaseResolver.TelegraphQuake, events);
                    }
                }
                if (r.spawnEveryNTurns > 0 && r.spawnCount > 0)
                {
                    if (EnemyPhaseResolver.IsDue(tc, r.spawnEveryNTurns))
                    {
                        var count = r.spawnCountBelowHalf > 0 && e.hp * 2 <= e.maxHp ? r.spawnCountBelowHalf : r.spawnCount;
                        SpawnAround(run, e, r.spawnType, count, rng, events);
                    }
                    else
                    {
                        Telegraph(e, tc, r.spawnEveryNTurns, EnemyPhaseResolver.TelegraphSpawn, events);
                    }
                }
            }
        }

        #endregion

        #region Helpers

        void Telegraph(EnemyState e, int turnCounter, int every, int kind, List<SimEvent> events)
        {
            if (every <= 1 || (turnCounter + 1) % every != 0)
            {
                return;
            }
            events.Add(ops.EnemyEvent(SimEventKind.EnemyAbilityTelegraph, e, kind));
        }

        void RotateShield(EnemyState e, List<SimEvent> events)
        {
            e.shieldFace = e.shieldFace switch
            {
                Face.Bottom => Face.Left,
                Face.Left => Face.Top,
                Face.Top => Face.Right,
                _ => Face.Bottom,
            };
            events.Add(ops.EnemyEvent(SimEventKind.EnemyShieldRotated, e, (int)e.shieldFace));
        }

        void Bolt(RunState run, EnemyState e, EnemyRules r, List<SimEvent> events)
        {
            var damage = (r.abilityValue > 0 ? r.abilityValue : r.attack) + rules.balance.attackBonusPerAct * run.actIndex;
            var ev = ops.EnemyEvent(SimEventKind.EnemyAttack, e, damage);
            ev.flag = true;
            ev.sourceId = e.id;
            events.Add(ev);
            ops.DamagePlayer(run, damage, e.id, events);
            cast.Add(e);
        }

        void HealNeighbours(RunState run, EnemyState e, int amount, List<SimEvent> events)
        {
            ops.CollectNeighbours(run.board, e, neighbours);
            foreach (var n in neighbours)
            {
                ops.HealEnemy(run, n, amount, events);
            }
        }

        void Quake(EnemyState e, int rows, List<SimEvent> events)
        {
            QuakeRows += rows;
            QuakeSourceId = e.id;
            var ev = ops.EnemyEvent(SimEventKind.EnemyAbilityTelegraph, e, EnemyPhaseResolver.TelegraphQuake);
            ev.flag = true;
            events.Add(ev);
        }

        /// <summary>
        /// Spawns count enemies into random free cells of the caster's 8-neighbourhood ring, outside the danger row and
        /// never straight in front of a caster that advances (its own spawns would otherwise wall it in for good).
        /// EnemySpawned gets sourceId = caster.
        /// </summary>
        void SpawnAround(RunState run, EnemyState e, EnemyType type, int count, SimRandom rng, List<SimEvent> events)
        {
            var spawnRules = rules.enemies[(int)type];
            var w = Mathf.Max(1, spawnRules.width);
            var h = Mathf.Max(1, spawnRules.height);
            var dangerRow = ArenaGeometry.DangerRow(rules.arena);
            var keepsFrontFree = rules.enemies[(int)e.type].moveRows > 0;
            cells.Clear();
            for (var row = e.row - 1; row <= e.row + e.height; row++)
            {
                for (var col = e.col - 1; col <= e.col + e.width; col++)
                {
                    var inside = col >= e.col && col < e.col + e.width && row >= e.row && row < e.row + e.height;
                    var front = keepsFrontFree && BoardOccupancy.Overlaps(col, row, w, h, e.col, e.row + e.height, e.width, 1);
                    if (inside || front || row + h - 1 >= dangerRow)
                    {
                        continue;
                    }
                    if (ops.IsFootprintFree(run.board, col, row, w, h, null))
                    {
                        cells.Add(new GridPos(col, row));
                    }
                }
            }
            for (var i = 0; i < count && cells.Count > 0; i++)
            {
                var pick = rng.Range(0, cells.Count);
                var cell = cells[pick];
                cells[pick] = cells[cells.Count - 1];
                cells.RemoveAt(cells.Count - 1);
                if (!ops.IsFootprintFree(run.board, cell.col, cell.row, w, h, null)) continue;
                ops.SpawnEnemy(run, type, cell.col, cell.row, events);
                var last = events.Count - 1;
                var spawned = events[last];
                spawned.sourceId = e.id;
                events[last] = spawned;
            }
        }

        #endregion
    }
}
