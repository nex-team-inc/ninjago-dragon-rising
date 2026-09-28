#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Resolves one enemy phase (GDD §2.2) as five staged steps (SimEvent.step) so presentation can animate them in order.
    /// Cadence: every enemy's turnCounter increments at the start of the phase; a cadence-N ability or movement fires
    /// when turnCounter % N == 0 and is telegraphed (EnemyAbilityTelegraph, flag = false) the phase before, when
    /// (turnCounter + 1) % N == 0 and N &gt; 1. Enemies spawned during a phase (turnCounter 0) do not act until the next.
    /// Freeze: an enemy frozen when the phase starts skips its abilities, advance and attacks; its frozenTurns then
    /// decrements at step 4 of the same phase (FreezeExpired at 0), so "Freeze 1" skips exactly one enemy phase.
    /// Ranged enemies do not also melee in the danger row on a phase where they cast.
    /// </summary>
    public sealed class EnemyPhaseResolver
    {
        const int StepStatus = 0;
        const int StepAbilities = 1;
        const int StepAdvance = 2;
        const int StepAttacks = 3;
        const int StepSpawn = 4;

        /// <summary>EnemyAbilityTelegraph.value codes, matching the Telegraph_* icons.</summary>
        public const int TelegraphSpawn = 0;
        public const int TelegraphCast = 1;
        public const int TelegraphHeal = 2;
        public const int TelegraphQuake = 3;

        readonly GameRules rules;
        readonly BoardOps ops;
        readonly List<EnemyState> order = new();
        readonly List<EnemyState> frozen = new();
        readonly List<EnemyState> cast = new();
        readonly List<EnemyState> neighbours = new();
        readonly List<GridPos> cells = new();
        int quakeRows;
        int quakeSourceId;

        #region Life Cycle

        public EnemyPhaseResolver(GameRules rules, BoardOps ops)
        {
            this.rules = rules;
            this.ops = ops;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// step 0: status ticks — burn deals N then decays to N-1, poison deals N and never decays (StatusTick, then the
        /// EnemyHit/EnemyKilled of the damage).
        /// step 1: abilities — golem shield rotation (EnemyShieldRotated, value = Face), lich one-time half-HP summon,
        /// ranged bolts (EnemyAttack flag = true, abilityValue + act attack bonus), healer heals adjacent enemies
        /// (EnemyHealed), quake (EnemyAbilityTelegraph flag = true, value = TelegraphQuake: every other enemy advances
        /// abilityValue extra rows at step 2), spawns into random free cells around the caster outside the danger row
        /// (EnemySpawned with sourceId = caster; spawnCountBelowHalf once at or below 50% HP).
        /// step 2: advance — pickups first (EnemyMoved with flag = true and targetId = pickup id; PickupExpired when
        /// entering the danger row), then enemies front row first: each row of movement goes straight down, else
        /// diagonally toward the centre column, else stops (EnemyMoved position → position2, value = rows moved).
        /// Enemies never move past the danger row.
        /// step 3: danger-row attacks (EnemyAttack flag = false, PlayerDamaged, PlayerDied).
        /// step 4: freeze expiry, turnInStage++ and stats.turns++, then the next planned wave into row 0 (WaveSpawned);
        /// on a boss stage with the plan exhausted, escort wave waves[1 + k % (waves.Count - 1)] every
        /// bossEscortEveryNTurns turns (k = turnInStage / bossEscortEveryNTurns). A boss stage spawns nothing once the
        /// boss is dead.
        /// Stops after the step in which the player dies. Stores rng.State into run.rngState.
        /// </summary>
        public void Resolve(RunState run, SimRandom rng, List<SimEvent> events)
        {
            if (run.outcome != RunOutcome.None) return;
            BeginPhase(run);
            var mark = events.Count;
            TickStatuses(run, events);
            Stamp(events, mark, StepStatus);
            mark = events.Count;
            ResolveAbilities(run, rng, events);
            Stamp(events, mark, StepAbilities);
            if (run.outcome == RunOutcome.None)
            {
                mark = events.Count;
                Advance(run, events);
                Stamp(events, mark, StepAdvance);
                mark = events.Count;
                Attack(run, events);
                Stamp(events, mark, StepAttacks);
            }
            if (run.outcome == RunOutcome.None)
            {
                mark = events.Count;
                EndPhase(run, events);
                Stamp(events, mark, StepSpawn);
            }
            run.rngState = rng.State;
        }

        #endregion

        #region Steps

        void BeginPhase(RunState run)
        {
            frozen.Clear();
            cast.Clear();
            quakeRows = 0;
            quakeSourceId = -1;
            foreach (var e in run.board.enemies)
            {
                e.turnCounter++;
                if (e.status.frozenTurns > 0)
                {
                    frozen.Add(e);
                }
            }
        }

        void TickStatuses(RunState run, List<SimEvent> events)
        {
            Snapshot(run.board.enemies);
            foreach (var e in order)
            {
                if (e.hp <= 0) continue;
                if (e.status.burn > 0)
                {
                    var burn = e.status.burn;
                    events.Add(StatusTick(e, StatusType.Burn, burn));
                    ops.DamageEnemy(run, e, burn, new DamageSource { ballType = BallType.Flame, isStatusTick = true }, events);
                    e.status.burn = burn - 1;
                    if (e.status.burn == 0)
                    {
                        e.status.burnSpreads = false;
                    }
                }
                if (e.hp > 0 && e.status.poison > 0)
                {
                    var poison = e.status.poison;
                    events.Add(StatusTick(e, StatusType.Poison, poison));
                    ops.DamageEnemy(run, e, poison, new DamageSource { ballType = BallType.Venom, isStatusTick = true }, events);
                }
            }
        }

        void ResolveAbilities(RunState run, SimRandom rng, List<SimEvent> events)
        {
            Snapshot(run.board.enemies);
            foreach (var e in order)
            {
                if (e.hp <= 0 || e.turnCounter <= 0 || frozen.Contains(e)) continue;
                var r = rules.enemies[(int)e.type];
                var tc = e.turnCounter;
                if (r.rotatingShield)
                {
                    RotateShield(e, events);
                }
                if (r.halfHpSummonCount > 0 && ops.ConsumeHalfPhase(e.id))
                {
                    SpawnAround(run, e, r.halfHpSummonType, r.halfHpSummonCount, rng, events);
                }
                if (r.ranged && r.abilityEveryNTurns > 0)
                {
                    if (IsDue(tc, r.abilityEveryNTurns))
                    {
                        Bolt(run, e, r, events);
                    }
                    else
                    {
                        Telegraph(e, tc, r.abilityEveryNTurns, TelegraphCast, events);
                    }
                }
                else if (r.healAmount > 0)
                {
                    var every = Mathf.Max(1, r.abilityEveryNTurns);
                    if (IsDue(tc, every))
                    {
                        HealNeighbours(run, e, r.healAmount, events);
                    }
                    else
                    {
                        Telegraph(e, tc, every, TelegraphHeal, events);
                    }
                }
                else if (r.abilityEveryNTurns > 0 && r.abilityValue > 0)
                {
                    if (IsDue(tc, r.abilityEveryNTurns))
                    {
                        Quake(e, r.abilityValue, events);
                    }
                    else
                    {
                        Telegraph(e, tc, r.abilityEveryNTurns, TelegraphQuake, events);
                    }
                }
                if (r.spawnEveryNTurns > 0 && r.spawnCount > 0)
                {
                    if (IsDue(tc, r.spawnEveryNTurns))
                    {
                        var count = r.spawnCountBelowHalf > 0 && e.hp * 2 <= e.maxHp ? r.spawnCountBelowHalf : r.spawnCount;
                        SpawnAround(run, e, r.spawnType, count, rng, events);
                    }
                    else
                    {
                        Telegraph(e, tc, r.spawnEveryNTurns, TelegraphSpawn, events);
                    }
                }
            }
            ops.ClearPendingHalfPhases();
        }

        void Advance(RunState run, List<SimEvent> events)
        {
            MovePickups(run, events);
            SortFrontFirst(run.board.enemies);
            var dangerRow = ArenaGeometry.DangerRow(rules.arena);
            var centerX = rules.arena.columns * 0.5f;
            foreach (var e in order)
            {
                if (e.hp <= 0 || e.turnCounter <= 0 || frozen.Contains(e)) continue;
                var r = rules.enemies[(int)e.type];
                var rows = r.moveRows > 0 && IsDue(e.turnCounter, Mathf.Max(1, r.moveEveryNTurns)) ? r.moveRows : 0;
                if (e.id != quakeSourceId)
                {
                    rows += quakeRows;
                }
                if (rows <= 0) continue;
                var from = ops.Center(e);
                var moved = 0;
                for (var i = 0; i < rows; i++)
                {
                    if (e.row + e.height - 1 >= dangerRow) break;
                    if (ops.IsFootprintFree(run.board, e.col, e.row + 1, e.width, e.height, e))
                    {
                        e.row++;
                        moved++;
                        continue;
                    }
                    var footprintCenter = e.col + e.width * 0.5f;
                    var dir = footprintCenter < centerX - 0.01f ? 1 : footprintCenter > centerX + 0.01f ? -1 : 0;
                    if (dir == 0 || !ops.IsFootprintFree(run.board, e.col + dir, e.row + 1, e.width, e.height, e)) break;
                    e.col += dir;
                    e.row++;
                    moved++;
                }
                if (moved == 0) continue;
                var ev = ops.EnemyEvent(SimEventKind.EnemyMoved, e, moved);
                ev.position = from;
                ev.position2 = ops.Center(e);
                events.Add(ev);
            }
        }

        void Attack(RunState run, List<SimEvent> events)
        {
            var dangerRow = ArenaGeometry.DangerRow(rules.arena);
            foreach (var e in run.board.enemies)
            {
                if (run.outcome != RunOutcome.None) break;
                if (e.attack <= 0 || e.turnCounter <= 0 || e.row + e.height - 1 != dangerRow) continue;
                if (frozen.Contains(e) || cast.Contains(e)) continue;
                var ev = ops.EnemyEvent(SimEventKind.EnemyAttack, e, e.attack);
                ev.sourceId = e.id;
                events.Add(ev);
                ops.DamagePlayer(run, e.attack, e.id, events);
            }
        }

        void EndPhase(RunState run, List<SimEvent> events)
        {
            foreach (var e in frozen)
            {
                if (e.hp <= 0 || e.status.frozenTurns <= 0) continue;
                e.status.frozenTurns--;
                if (e.status.frozenTurns == 0)
                {
                    events.Add(ops.EnemyEvent(SimEventKind.FreezeExpired, e, 0));
                }
            }
            run.turnInStage++;
            run.stats.turns++;
            if (run.stage.isBoss && !IsBossAlive(run)) return;
            var waves = run.stage.waves;
            if (run.nextWaveIndex < waves.Count)
            {
                ops.SpawnWaveRow(run, waves[run.nextWaveIndex++], 0, events);
                return;
            }
            var every = rules.acts[run.actIndex].bossEscortEveryNTurns;
            if (!run.stage.isBoss || every <= 0 || waves.Count < 2 || run.turnInStage % every != 0) return;
            var k = run.turnInStage / every;
            ops.SpawnWaveRow(run, waves[1 + k % (waves.Count - 1)], 0, events);
        }

        #endregion

        #region Helpers

        static bool IsDue(int turnCounter, int every) => every > 0 && turnCounter % every == 0;

        void Telegraph(EnemyState e, int turnCounter, int every, int kind, List<SimEvent> events)
        {
            if (every <= 1 || (turnCounter + 1) % every != 0) return;
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
            quakeRows += rows;
            quakeSourceId = e.id;
            var ev = ops.EnemyEvent(SimEventKind.EnemyAbilityTelegraph, e, TelegraphQuake);
            ev.flag = true;
            events.Add(ev);
        }

        void SpawnAround(RunState run, EnemyState e, EnemyType type, int count, SimRandom rng, List<SimEvent> events)
        {
            var spawnRules = rules.enemies[(int)type];
            var w = Mathf.Max(1, spawnRules.width);
            var h = Mathf.Max(1, spawnRules.height);
            var dangerRow = ArenaGeometry.DangerRow(rules.arena);
            cells.Clear();
            for (var row = e.row - 1; row <= e.row + e.height; row++)
            {
                for (var col = e.col - 1; col <= e.col + e.width; col++)
                {
                    var inside = col >= e.col && col < e.col + e.width && row >= e.row && row < e.row + e.height;
                    if (inside || row + h - 1 >= dangerRow) continue;
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

        bool IsBossAlive(RunState run)
        {
            foreach (var e in run.board.enemies)
            {
                if (rules.enemies[(int)e.type].isBoss)
                {
                    return true;
                }
            }
            return false;
        }

        SimEvent StatusTick(EnemyState e, StatusType status, int damage)
        {
            var ev = ops.EnemyEvent(SimEventKind.StatusTick, e, damage);
            ev.status = status;
            return ev;
        }

        void Snapshot(List<EnemyState> enemies)
        {
            order.Clear();
            for (var i = 0; i < enemies.Count; i++)
            {
                order.Add(enemies[i]);
            }
        }

        void SortFrontFirst(List<EnemyState> enemies)
        {
            Snapshot(enemies);
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

        static void Stamp(List<SimEvent> events, int from, int step)
        {
            for (var i = from; i < events.Count; i++)
            {
                var ev = events[i];
                ev.step = step;
                events[i] = ev;
            }
        }

        #endregion
    }
}
