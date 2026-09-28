#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Spawning, occupancy, damage and status application shared by BallSimulator, EnemyPhaseResolver and RunFactory
    /// so all of them resolve deaths, explosions, burn spreading, pickups and player damage identically.
    /// Enemy-centric events carry the enemy id in targetId, its type in enemyType and its footprint centre in position.
    /// DamageEnemy can remove several enemies at once (chained bomber explosions): callers iterating board.enemies
    /// must iterate a snapshot.
    /// </summary>
    public sealed class BoardOps
    {
        readonly GameRules rules;
        readonly List<EnemyState> dying = new();
        readonly List<EnemyState> neighbours = new();
        // Bosses that crossed 50% HP since the last enemy phase; lives only within one turn (saves happen at turn boundaries).
        readonly List<int> pendingHalfPhaseBossIds = new();
        bool resolvingDeaths;

        #region Life Cycle

        public BoardOps(GameRules rules)
        {
            this.rules = rules;
        }

        #endregion

        #region Public Methods

        /// <summary>True when the cell is inside the grid and no enemy footprint, field object or pickup occupies it.</summary>
        public bool IsCellFree(BoardState b, int col, int row)
        {
            return IsFootprintFree(b, col, row, 1, 1, null);
        }

        /// <summary>
        /// True when every cell of the w×h footprint with top-left (col, row) is inside the grid and free, ignoring the
        /// cells taken by <paramref name="ignore"/> (the enemy being moved).
        /// </summary>
        public bool IsFootprintFree(BoardState b, int col, int row, int width, int height, EnemyState? ignore)
        {
            var a = rules.arena;
            if (col < 0 || row < 0 || col + width > a.columns || row + height > a.rows)
            {
                return false;
            }
            foreach (var e in b.enemies)
            {
                if (e == ignore) continue;
                if (Overlaps(e.col, e.row, e.width, e.height, col, row, width, height))
                {
                    return false;
                }
            }
            foreach (var o in b.fieldObjects)
            {
                if (Overlaps(o.col, o.row, 1, 1, col, row, width, height))
                {
                    return false;
                }
            }
            foreach (var p in b.pickups)
            {
                if (Overlaps(p.col, p.row, 1, 1, col, row, width, height))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The enemy whose footprint covers the cell, or null.</summary>
        public EnemyState? EnemyAt(BoardState b, int col, int row)
        {
            foreach (var e in b.enemies)
            {
                if (Overlaps(e.col, e.row, e.width, e.height, col, row, 1, 1))
                {
                    return e;
                }
            }
            return null;
        }

        /// <summary>
        /// Creates an enemy of type t at (col, row) with stats scaled for the run's stage: hp × (1 + hpScalePerStage ×
        /// stageNumber) rounded, attack + attackBonusPerAct × actIndex (attack 0 stays 0), footprint and shield face
        /// from rules. Assigns board.nextId, appends EnemySpawned (value = hp) and returns the state. Does not check
        /// occupancy.
        /// </summary>
        public EnemyState SpawnEnemy(RunState run, EnemyType t, int col, int row, List<SimEvent> events)
        {
            var r = rules.enemies[(int)t];
            var balance = rules.balance;
            var hp = Mathf.Max(1, Mathf.RoundToInt(r.hp * (1f + balance.hpScalePerStage * run.stageNumber)));
            var attack = r.attack > 0 ? r.attack + balance.attackBonusPerAct * run.actIndex : 0;
            var e = new EnemyState
            {
                id = run.board.nextId++,
                type = t,
                col = col,
                row = row,
                width = Mathf.Max(1, r.width),
                height = Mathf.Max(1, r.height),
                hp = hp,
                maxHp = hp,
                attack = attack,
                shieldFace = r.shieldFace,
            };
            run.board.enemies.Add(e);
            events.Add(EnemyEvent(SimEventKind.EnemySpawned, e, hp));
            return e;
        }

        /// <summary>
        /// Spawns one planned wave row into grid row <paramref name="row"/>: enemies via SpawnEnemy and pickups as
        /// PickupState, skipping cells that are not free. Appends WaveSpawned (value = row, value2 = cells spawned).
        /// Pickups have no spawn event: presentation syncs them from board.pickups on WaveSpawned.
        /// </summary>
        public void SpawnWaveRow(RunState run, WaveRow wave, int row, List<SimEvent> events)
        {
            var b = run.board;
            var spawned = 0;
            foreach (var cell in wave.cells)
            {
                if (cell.isPickup)
                {
                    if (!IsCellFree(b, cell.col, row)) continue;
                    b.pickups.Add(new PickupState { id = b.nextId++, type = cell.pickup, col = cell.col, row = row });
                    spawned++;
                    continue;
                }
                var r = rules.enemies[(int)cell.enemy];
                if (!IsFootprintFree(b, cell.col, row, Mathf.Max(1, r.width), Mathf.Max(1, r.height), null)) continue;
                SpawnEnemy(run, cell.enemy, cell.col, row, events);
                spawned++;
            }
            events.Add(new SimEvent { kind = SimEventKind.WaveSpawned, value = row, value2 = spawned });
        }

        /// <summary>
        /// Deals amount (+1 while poisoned unless src.isStatusTick; every other multiplier — crit, power shot, power
        /// pickup, rubber bonus, frozen bonus — is already applied by the caller) and returns the damage actually
        /// dealt (clamped to remaining hp). Emits EnemyHit (value = dealt, value2 = hp left, flag = src.isCrit, ballId)
        /// and updates stats.damageDealt, plus stats.hits for direct ball hits. On death: removes the enemy,
        /// EnemyKilled (flag = boss), stats.kills, bomber explosion (Explosion value = radius 1, value2 = damage;
        /// deathExplosionDamage to the 8 neighbours, chaining), burn spread (current burn stacks, non-spreading) to the
        /// first living neighbour when status.burnSpreads, stats.bossesDefeated. Crossing 50% HP on a living boss sets
        /// bossHalfTriggered and emits BossPhaseChanged. Damage to an already dead enemy returns 0.
        /// </summary>
        public int DamageEnemy(RunState run, EnemyState e, int amount, DamageSource src, List<SimEvent> events)
        {
            if (e.hp <= 0 || amount <= 0)
            {
                return 0;
            }
            var dealt = ApplyHit(run, e, amount, src, events);
            if (!resolvingDeaths)
            {
                ResolveDeaths(run, events);
            }
            return dealt;
        }

        /// <summary>
        /// Burn adds stacks, poison adds stacks up to balance.poisonMax, freeze sets frozenTurns to max(current, stacks).
        /// burnSpreads marks a Flame level-3 burn. Emits StatusApplied (status, value = resulting stacks). Bosses are
        /// immune to freeze (no event).
        /// </summary>
        public void ApplyStatus(RunState run, EnemyState e, StatusType status, int stacks, bool burnSpreads, List<SimEvent> events)
        {
            if (e.hp <= 0 || stacks <= 0) return;
            int result;
            switch (status)
            {
                case StatusType.Burn:
                    e.status.burn += stacks;
                    e.status.burnSpreads |= burnSpreads;
                    result = e.status.burn;
                    break;
                case StatusType.Poison:
                    e.status.poison = Mathf.Min(rules.balance.poisonMax, e.status.poison + stacks);
                    result = e.status.poison;
                    break;
                default:
                    if (rules.enemies[(int)e.type].isBoss) return;
                    e.status.frozenTurns = Mathf.Max(e.status.frozenTurns, stacks);
                    result = e.status.frozenTurns;
                    break;
            }
            var ev = EnemyEvent(SimEventKind.StatusApplied, e, result);
            ev.status = status;
            events.Add(ev);
        }

        /// <summary>Heals up to maxHp; emits EnemyHealed (value = healed) only when something was healed.</summary>
        public void HealEnemy(RunState run, EnemyState e, int amount, List<SimEvent> events)
        {
            if (e.hp <= 0 || amount <= 0) return;
            var healed = Mathf.Min(amount, e.maxHp - e.hp);
            if (healed <= 0) return;
            e.hp += healed;
            events.Add(EnemyEvent(SimEventKind.EnemyHealed, e, healed));
        }

        /// <summary>
        /// Crate takes damage (CrateHit value = dealt, value2 = hp left); at 0 HP it is removed (CrateBroken). Crates
        /// with an odd id leave a pickup in their cell, type cycling by id: CrateBroken then has flag = true,
        /// pickup = type and sourceId = the new pickup id.
        /// </summary>
        public void DamageCrate(RunState run, FieldObjectState crate, int amount, List<SimEvent> events)
        {
            if (crate.type != FieldObjectType.Crate || crate.hp <= 0 || amount <= 0) return;
            var dealt = Mathf.Min(amount, crate.hp);
            crate.hp -= dealt;
            var center = ArenaGeometry.CellCenter(rules.arena, crate.col, crate.row);
            events.Add(new SimEvent { kind = SimEventKind.CrateHit, targetId = crate.id, value = dealt, value2 = crate.hp, position = center });
            if (crate.hp > 0) return;
            var b = run.board;
            b.fieldObjects.Remove(crate);
            var broken = new SimEvent { kind = SimEventKind.CrateBroken, targetId = crate.id, position = center };
            if (crate.id % 2 == 1 && IsCellFree(b, crate.col, crate.row))
            {
                var pickup = new PickupState
                {
                    id = b.nextId++,
                    type = (PickupType)(crate.id / 2 % SimConstants.PickupTypeCount),
                    col = crate.col,
                    row = crate.row,
                };
                b.pickups.Add(pickup);
                broken.flag = true;
                broken.pickup = pickup.type;
                broken.sourceId = pickup.id;
            }
            events.Add(broken);
        }

        /// <summary>
        /// Removes the pickup and applies it: ExtraBall → run.extraBalls++, Heal → HealPlayer(balance.pickupHealAmount),
        /// Power → run.powerPickupArmed = true. Emits PickupCollected (ballId, targetId = pickup id, pickup, position).
        /// </summary>
        public void CollectPickup(RunState run, PickupState pickup, int ballId, List<SimEvent> events)
        {
            if (!run.board.pickups.Remove(pickup)) return;
            events.Add(new SimEvent
            {
                kind = SimEventKind.PickupCollected,
                ballId = ballId,
                targetId = pickup.id,
                pickup = pickup.type,
                position = ArenaGeometry.CellCenter(rules.arena, pickup.col, pickup.row),
            });
            switch (pickup.type)
            {
                case PickupType.ExtraBall:
                    run.extraBalls++;
                    break;
                case PickupType.Heal:
                    HealPlayer(run, rules.balance.pickupHealAmount, events);
                    break;
                default:
                    run.powerPickupArmed = true;
                    break;
            }
        }

        /// <summary>
        /// Lowers playerHp (never below 0), stats.damageTaken, emits PlayerDamaged (value = dealt, value2 = hp left,
        /// sourceId). When hp reaches 0 emits PlayerDied once and sets run.outcome = Defeat.
        /// </summary>
        public void DamagePlayer(RunState run, int amount, int sourceId, List<SimEvent> events)
        {
            if (amount <= 0 || run.playerHp <= 0) return;
            var dealt = Mathf.Min(amount, run.playerHp);
            run.playerHp -= dealt;
            run.stats.damageTaken += dealt;
            events.Add(new SimEvent { kind = SimEventKind.PlayerDamaged, sourceId = sourceId, value = dealt, value2 = run.playerHp });
            if (run.playerHp > 0) return;
            run.outcome = RunOutcome.Defeat;
            events.Add(new SimEvent { kind = SimEventKind.PlayerDied, sourceId = sourceId });
        }

        /// <summary>Heals up to playerMaxHp; emits PlayerHealed (value = healed) only when something was healed.</summary>
        public void HealPlayer(RunState run, int amount, List<SimEvent> events)
        {
            if (amount <= 0 || run.playerHp <= 0) return;
            var healed = Mathf.Min(amount, run.playerMaxHp - run.playerHp);
            if (healed <= 0) return;
            run.playerHp += healed;
            events.Add(new SimEvent { kind = SimEventKind.PlayerHealed, value = healed, value2 = run.playerHp });
        }

        #endregion

        #region Internal

        /// <summary>Living or dying enemies whose footprint touches the 8-neighbourhood ring of center (center excluded).</summary>
        internal void CollectNeighbours(BoardState b, EnemyState center, List<EnemyState> output)
        {
            output.Clear();
            foreach (var n in b.enemies)
            {
                if (n == center) continue;
                if (Overlaps(n.col, n.row, n.width, n.height, center.col - 1, center.row - 1, center.width + 2, center.height + 2))
                {
                    output.Add(n);
                }
            }
        }

        /// <summary>True once per boss that crossed 50% HP since the last call to ClearPendingHalfPhases.</summary>
        internal bool ConsumeHalfPhase(int enemyId) => pendingHalfPhaseBossIds.Remove(enemyId);

        internal void ClearPendingHalfPhases() => pendingHalfPhaseBossIds.Clear();

        internal Vector2 Center(EnemyState e) => ArenaGeometry.FootprintCenter(rules.arena, e.col, e.row, e.width, e.height);

        internal SimEvent EnemyEvent(SimEventKind kind, EnemyState e, int value)
        {
            return new SimEvent { kind = kind, targetId = e.id, enemyType = e.type, position = Center(e), value = value };
        }

        #endregion

        #region Helpers

        int ApplyHit(RunState run, EnemyState e, int amount, DamageSource src, List<SimEvent> events)
        {
            var total = amount;
            if (e.status.poison > 0 && !src.isStatusTick)
            {
                total++;
            }
            var dealt = Mathf.Min(total, e.hp);
            e.hp -= dealt;
            run.stats.damageDealt += dealt;
            if (!src.isStatusTick && !src.isExplosion && !src.isChain)
            {
                run.stats.hits++;
            }
            var hit = EnemyEvent(SimEventKind.EnemyHit, e, dealt);
            hit.ballId = src.ballId;
            hit.ballType = src.ballType;
            hit.flag = src.isCrit;
            hit.value2 = e.hp;
            events.Add(hit);
            var r = rules.enemies[(int)e.type];
            if (r.isBoss && !e.bossHalfTriggered && e.hp > 0 && e.hp * 2 <= e.maxHp)
            {
                e.bossHalfTriggered = true;
                pendingHalfPhaseBossIds.Add(e.id);
                events.Add(EnemyEvent(SimEventKind.BossPhaseChanged, e, e.hp));
            }
            if (e.hp <= 0)
            {
                dying.Add(e);
            }
            return dealt;
        }

        void ResolveDeaths(RunState run, List<SimEvent> events)
        {
            resolvingDeaths = true;
            try
            {
                for (var i = 0; i < dying.Count; i++)
                {
                    Kill(run, dying[i], events);
                }
            }
            finally
            {
                dying.Clear();
                resolvingDeaths = false;
            }
        }

        void Kill(RunState run, EnemyState e, List<SimEvent> events)
        {
            run.board.enemies.Remove(e);
            var r = rules.enemies[(int)e.type];
            run.stats.kills++;
            if (r.isBoss)
            {
                run.stats.bossesDefeated++;
            }
            var killed = EnemyEvent(SimEventKind.EnemyKilled, e, 0);
            killed.flag = r.isBoss;
            events.Add(killed);
            if (r.deathExplosionDamage > 0)
            {
                Explode(run, e, r.deathExplosionDamage, events);
            }
            if (e.status.burnSpreads && e.status.burn > 0)
            {
                SpreadBurn(run, e, events);
            }
        }

        void Explode(RunState run, EnemyState e, int damage, List<SimEvent> events)
        {
            events.Add(new SimEvent { kind = SimEventKind.Explosion, sourceId = e.id, enemyType = e.type, position = Center(e), value = 1, value2 = damage });
            CollectNeighbours(run.board, e, neighbours);
            var src = new DamageSource { isExplosion = true };
            foreach (var n in neighbours)
            {
                if (n.hp <= 0) continue;
                ApplyHit(run, n, damage, src, events);
            }
        }

        void SpreadBurn(RunState run, EnemyState e, List<SimEvent> events)
        {
            CollectNeighbours(run.board, e, neighbours);
            foreach (var n in neighbours)
            {
                if (n.hp <= 0) continue;
                ApplyStatus(run, n, StatusType.Burn, e.status.burn, false, events);
                return;
            }
        }

        static bool Overlaps(int c0, int r0, int w0, int h0, int c1, int r1, int w1, int h1)
        {
            return c0 < c1 + w1 && c1 < c0 + w0 && r0 < r1 + h1 && r1 < r0 + h0;
        }

        #endregion
    }
}
