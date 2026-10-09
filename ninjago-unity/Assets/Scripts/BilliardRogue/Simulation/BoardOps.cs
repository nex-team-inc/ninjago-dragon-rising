#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Spawning, occupancy, damage and status application shared by BallSimulator, EnemyPhaseResolver and RunFactory
    /// so all of them resolve deaths, explosions, burn spreading, pickups and player damage identically (occupancy
    /// queries live in BoardOccupancy, enemy damage and deaths in BoardDamage).
    /// Enemy-centric events carry the enemy id in targetId, its type in enemyType and its footprint centre in position.
    /// DamageEnemy can remove several enemies at once (chained bomber explosions): callers iterating board.enemies
    /// must iterate a snapshot.
    /// </summary>
    public sealed class BoardOps
    {
        readonly GameRules rules;
        readonly BoardDamage damage;
        readonly BoardSpawning spawning;

        #region Life Cycle

        public BoardOps(GameRules rules)
        {
            this.rules = rules;
            damage = new BoardDamage(rules);
            spawning = new BoardSpawning(rules);
        }

        #endregion

        #region Public Methods

        /// <summary>True when the cell is inside the grid and no enemy footprint, field object or pickup occupies it.</summary>
        public bool IsCellFree(BoardState b, int col, int row)
        {
            return BoardOccupancy.IsFootprintFree(rules.arena, b, col, row, 1, 1, null);
        }

        /// <summary>
        /// True when every cell of the w×h footprint with top-left (col, row) is inside the grid and free, ignoring the
        /// cells taken by <paramref name="ignore"/> (the enemy being moved).
        /// </summary>
        public bool IsFootprintFree(BoardState b, int col, int row, int width, int height, EnemyState? ignore)
        {
            return BoardOccupancy.IsFootprintFree(rules.arena, b, col, row, width, height, ignore);
        }

        /// <summary>The enemy whose footprint covers the cell, or null.</summary>
        public EnemyState? EnemyAt(BoardState b, int col, int row)
        {
            return BoardOccupancy.EnemyAt(b, col, row);
        }

        /// <summary>
        /// Creates an enemy of type t at (col, row) with stats scaled for the run's stage: hp × (1 + hpScalePerStage ×
        /// stageNumber) × coopEnemyHpScale with two players, rounded; attack + attackBonusPerAct × actIndex (attack 0
        /// stays 0), footprint and shield face from rules. Assigns board.nextId, appends EnemySpawned (value = hp) and
        /// returns the state. Does not check occupancy.
        /// </summary>
        public EnemyState SpawnEnemy(RunState run, EnemyType t, int col, int row, List<SimEvent> events)
        {
            var r = rules.enemies[(int)t];
            var balance = rules.balance;
            var coop = run.numPlayers > 1 ? balance.coopEnemyHpScale : 1f;
            var hp = Mathf.Max(1, Mathf.RoundToInt(r.hp * (1f + balance.hpScalePerStage * run.stageNumber) * coop));
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
        /// GDD v2 §5: pops a batch in at random free cells of rows 0..rows − 1 − act.spawnForbiddenNearRows (one rng draw per
        /// placed entry, each row keeping act.minOpenColumnsPerRow free cells), entries in plan order: enemies through
        /// SpawnEnemy with EnemySpawned.flag = true and value2 = stagger index, pickups with PickupSpawned (same fields).
        /// Entries without a free cell are dropped. Ends with BatchSpawned (value = batchIndex, value2 = entries spawned,
        /// flag = something dropped, sourceId = skippedTurns). Returns the entries spawned.
        /// </summary>
        public int SpawnBatch(RunState run, SpawnBatch batch, int batchIndex, int skippedTurns, SimRandom rng, List<SimEvent> events)
        {
            return spawning.Spawn(this, run, batch, batchIndex, skippedTurns, rng, events);
        }

        /// <summary>Last grid row a batch may spawn into for the run's act.</summary>
        public int SpawnLastRow(RunState run) => BoardSpawning.SpawnLastRow(rules.arena, rules.acts[run.actIndex]);

        /// <summary>
        /// v1 (legacy saves): spawns one planned wave row into grid row <paramref name="row"/>: enemies via SpawnEnemy and pickups as
        /// PickupState. A cell whose planned column is occupied moves to the nearest free column of the row (closer
        /// wins, left on ties) so the planned enemy count survives congestion; only a row without room drops cells.
        /// Appends WaveSpawned (value = row, value2 = cells spawned, flag = a cell was dropped). Pickups have no spawn
        /// event: presentation syncs them from board.pickups on WaveSpawned.
        /// </summary>
        public void SpawnWaveRow(RunState run, WaveRow wave, int row, List<SimEvent> events)
        {
            var b = run.board;
            var spawned = 0;
            foreach (var cell in wave.cells)
            {
                if (cell.isPickup)
                {
                    var col = FindFreeStart(b, cell.col, row, 1, 1);
                    if (col < 0) continue;
                    b.pickups.Add(new PickupState { id = b.nextId++, type = cell.pickup, col = col, row = row });
                    spawned++;
                    continue;
                }
                var r = rules.enemies[(int)cell.enemy];
                var start = FindFreeStart(b, cell.col, row, Mathf.Max(1, r.width), Mathf.Max(1, r.height));
                if (start < 0) continue;
                SpawnEnemy(run, cell.enemy, start, row, events);
                spawned++;
            }
            events.Add(new SimEvent { kind = SimEventKind.WaveSpawned, value = row, value2 = spawned, flag = spawned < wave.cells.Count });
        }

        /// <summary>
        /// Deals amount (+1 while poisoned unless src.isStatusTick; every other multiplier — crit, power shot, power
        /// pickup, rubber bonus, frozen bonus — is already applied by the caller) and returns the damage actually
        /// dealt (clamped to remaining hp). Emits EnemyHit (value = dealt, value2 = hp left, flag = src.isCrit, ballId)
        /// and updates stats.damageDealt, plus stats.hits for direct ball hits. On death: removes the enemy,
        /// EnemyKilled (flag = boss), stats.kills, bomber explosion (Explosion value = radius 1, value2 = damage;
        /// deathExplosionDamage to the 8 neighbours, chaining), burn spread (current burn stacks, non-spreading) to the
        /// first living neighbour when status.burnSpreads, stats.bossesDefeated. Crossing 50% HP on a living boss sets
        /// bossHalfTriggered and halfHpSummonPending and emits BossPhaseChanged. Damage to a dead enemy returns 0.
        /// </summary>
        public int DamageEnemy(RunState run, EnemyState e, int amount, DamageSource src, List<SimEvent> events)
        {
            return damage.DamageEnemy(run, e, amount, src, events);
        }

        /// <summary>
        /// Burn adds stacks, poison adds stacks up to balance.poisonMax, freeze sets frozenTurns to max(current, stacks).
        /// burnSpreads marks a Flame level-3 burn. Emits StatusApplied (status, value = resulting stacks). Bosses are
        /// immune to freeze (no event).
        /// </summary>
        public void ApplyStatus(RunState run, EnemyState e, StatusType status, int stacks, bool burnSpreads, List<SimEvent> events)
        {
            damage.ApplyStatus(e, status, stacks, burnSpreads, events);
        }

        /// <summary>Heals up to maxHp; emits EnemyHealed (value = healed) only when something was healed.</summary>
        public void HealEnemy(RunState run, EnemyState e, int amount, List<SimEvent> events)
        {
            damage.HealEnemy(e, amount, events);
        }

        /// <summary>
        /// Crate takes damage (CrateHit value = dealt, value2 = hp left); at 0 HP it is removed (CrateBroken). Crates
        /// with an odd id leave a pickup in their cell, type cycling by id: CrateBroken then has flag = true,
        /// pickup = type and sourceId = the new pickup id.
        /// </summary>
        public void DamageCrate(RunState run, FieldObjectState crate, int amount, List<SimEvent> events)
        {
            if (crate.type != FieldObjectType.Crate || crate.hp <= 0 || amount <= 0)
            {
                return;
            }
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
        /// sourceId). When hp reaches 0 emits PlayerDied once and sets run.outcome = Defeat. With
        /// balance.damagePerAttack set, every attack takes exactly that much instead of its amount.
        /// </summary>
        public void DamagePlayer(RunState run, int amount, int sourceId, List<SimEvent> events)
        {
            if (amount <= 0 || run.playerHp <= 0)
            {
                return;
            }
            if (rules.balance.damagePerAttack > 0) amount = rules.balance.damagePerAttack;
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
            if (amount <= 0 || run.playerHp <= 0)
            {
                return;
            }
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
            BoardOccupancy.CollectNeighbours(b, center, output);
        }

        internal Vector2 Center(EnemyState e) => EnemyEvents.Center(rules.arena, e);

        internal SimEvent EnemyEvent(SimEventKind kind, EnemyState e, int value) => EnemyEvents.Make(rules.arena, kind, e, value);

        #endregion

        #region Helpers

        /// <summary>The planned start column when its footprint is free, else the nearest free one (closer wins, left on ties), else -1.</summary>
        int FindFreeStart(BoardState b, int planned, int row, int width, int height)
        {
            var a = rules.arena;
            for (var offset = 0; offset < a.columns; offset++)
            {
                if (BoardOccupancy.IsFootprintFree(a, b, planned - offset, row, width, height, null))
                {
                    return planned - offset;
                }
                if (offset > 0 && BoardOccupancy.IsFootprintFree(a, b, planned + offset, row, width, height, null))
                {
                    return planned + offset;
                }
            }
            return -1;
        }

        #endregion
    }
}
