#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    internal enum HitOutcome
    {
        Reflect = 0,
        PassThrough = 1,
        Split = 2,
    }

    /// <summary>Applies one ball's abilities (GDD §5) when it strikes an enemy; every board mutation goes through BoardOps.</summary>
    internal sealed class BallHitResolver
    {
        const int CritSalt = 1;
        const int FreezeSalt = 2;
        const int MaxChainTargets = 16;

        readonly GameRules rules;
        readonly BoardOps ops;
        readonly List<EnemyState> areaTargets = new(32);
        readonly int[] chainVisited = new int[MaxChainTargets];

        #region Life Cycle

        public BallHitResolver(GameRules rules, BoardOps ops)
        {
            this.rules = rules;
            this.ops = ops;
        }

        #endregion

        #region Public Methods

        public BallLevelStats LevelStats(BallType type, int level)
        {
            var levels = rules.balls[(int)type].levels;
            return levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];
        }

        public HitOutcome Resolve(RunState run, BallSlot b, EnemyState e, Vector2 normal, List<SimEvent> events)
        {
            if (e.shieldFace != Face.None && BallCollision.FaceFromNormal(normal) == e.shieldFace)
            {
                events.Add(new SimEvent { kind = SimEventKind.EnemyBlocked, ballId = b.id, targetId = e.id, ballType = b.type, enemyType = e.type, position = b.position });
                return HitOutcome.Reflect;
            }
            var stats = LevelStats(b.type, b.level);
            var damage = ComputeDamage(run, b, e, stats, out var isCrit);
            var center = ArenaGeometry.FootprintCenter(rules.arena, e.col, e.row, e.width, e.height);
            var src = new DamageSource { ballType = b.type, ballId = b.id, isCrit = isCrit };
            ops.DamageEnemy(run, e, damage, src, events);
            b.combo++;
            if (b.combo > run.stats.bestCombo) run.stats.bestCombo = b.combo;
            events.Add(new SimEvent { kind = SimEventKind.ComboChanged, ballId = b.id, ballType = b.type, value = b.combo, position = b.position });
            if (e.hp > 0) ApplyStatuses(run, b, e, stats, events);
            if (stats.healPerHit > 0) HealOnHit(run, b, stats, events);
            if (stats.chainCount > 0) Chain(run, b, e, center, stats, events);
            if (stats.areaDamage > 0 && !b.areaUsed)
            {
                b.areaUsed = true;
                Explode(run, b, e, stats, events);
            }
            if (stats.splitCount > 0 && !b.isMini && !b.splitUsed)
            {
                b.splitUsed = true;
                return HitOutcome.Split;
            }
            if (b.piercesUsed < stats.pierceCount && b.piercedCount < BallSlot.MaxPierced)
            {
                b.piercesUsed++;
                b.piercedIds[b.piercedCount++] = e.id;
                return HitOutcome.PassThrough;
            }
            return HitOutcome.Reflect;
        }

        #endregion

        #region Helpers

        int ComputeDamage(RunState run, BallSlot b, EnemyState e, BallLevelStats stats, out bool isCrit)
        {
            var damage = b.isMini ? stats.splitDamage : stats.damage;
            if (stats.bonusPerWallBounce > 0) damage += Mathf.Min(b.wallBounces * stats.bonusPerWallBounce, stats.bonusCap);
            if (e.status.frozenTurns > 0) damage += stats.frozenBonusDamage;
            if (b.firstHitPending)
            {
                b.firstHitPending = false;
                if (b.powerShot) damage += rules.balance.powerShotBonusDamage;
                if (b.powerPickup) damage *= rules.balance.powerPickupMultiplier;
            }
            isCrit = stats.critChance > 0f && Roll(run, b, CritSalt) < stats.critChance;
            if (isCrit) damage *= stats.critMultiplier;
            b.hitIndex++;
            return damage;
        }

        void ApplyStatuses(RunState run, BallSlot b, EnemyState e, BallLevelStats stats, List<SimEvent> events)
        {
            if (stats.statusStacks > 0)
            {
                if (b.type == BallType.Venom)
                {
                    ops.ApplyStatus(run, e, StatusType.Poison, stats.statusStacks, false, events);
                }
                else
                {
                    ops.ApplyStatus(run, e, StatusType.Burn, stats.statusStacks, stats.spreadBurnOnDeath, events);
                }
            }
            if (stats.procChance > 0f && Roll(run, b, FreezeSalt) < stats.procChance)
            {
                ops.ApplyStatus(run, e, StatusType.Freeze, 1, false, events);
            }
        }

        void HealOnHit(RunState run, BallSlot b, BallLevelStats stats, List<SimEvent> events)
        {
            var room = stats.healCapPerShot - b.healedThisShot;
            if (room <= 0) return;
            var amount = Mathf.Min(stats.healPerHit, room);
            b.healedThisShot += amount;
            ops.HealPlayer(run, amount, events);
        }

        void Chain(RunState run, BallSlot b, EnemyState first, Vector2 from, BallLevelStats stats, List<SimEvent> events)
        {
            var visitedCount = 0;
            chainVisited[visitedCount++] = first.id;
            var rangeSq = stats.chainRange * stats.chainRange;
            var src = new DamageSource { ballType = b.type, ballId = b.id, isChain = true };
            for (var link = 0; link < stats.chainCount && visitedCount < MaxChainTargets; link++)
            {
                EnemyState? best = null;
                var bestSq = rangeSq;
                var bestCenter = Vector2.zero;
                var enemies = run.board.enemies;
                for (var i = 0; i < enemies.Count; i++)
                {
                    var candidate = enemies[i];
                    if (IsVisited(candidate.id, visitedCount)) continue;
                    var c = ArenaGeometry.FootprintCenter(rules.arena, candidate.col, candidate.row, candidate.width, candidate.height);
                    var sq = (c - from).sqrMagnitude;
                    if (sq > bestSq) continue;
                    best = candidate;
                    bestSq = sq;
                    bestCenter = c;
                }
                if (best == null) break;
                chainVisited[visitedCount++] = best.id;
                events.Add(new SimEvent { kind = SimEventKind.ChainLightning, ballId = b.id, targetId = best.id, ballType = b.type, position = from, position2 = bestCenter });
                ops.DamageEnemy(run, best, stats.chainDamage, src, events);
                from = bestCenter;
            }
        }

        void Explode(RunState run, BallSlot b, EnemyState hit, BallLevelStats stats, List<SimEvent> events)
        {
            var a = rules.arena;
            var col = Mathf.Clamp(Mathf.FloorToInt(b.position.x), hit.col, hit.col + hit.width - 1);
            var row = Mathf.Clamp(Mathf.FloorToInt(ArenaGeometry.TopWallY(a) - b.position.y), hit.row, hit.row + hit.height - 1);
            var radius = stats.areaRadius;
            events.Add(new SimEvent { kind = SimEventKind.Explosion, ballId = b.id, ballType = b.type, value = radius, flag = stats.areaCross, position = ArenaGeometry.CellCenter(a, col, row) });
            areaTargets.Clear();
            var enemies = run.board.enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                if (InArea(enemies[i], col, row, radius, stats.areaCross)) areaTargets.Add(enemies[i]);
            }
            var src = new DamageSource { ballType = b.type, ballId = b.id, isExplosion = true };
            for (var i = 0; i < areaTargets.Count; i++)
            {
                var target = areaTargets[i];
                if (target.hp <= 0) continue;
                ops.DamageEnemy(run, target, stats.areaDamage, src, events);
            }
            areaTargets.Clear();
        }

        static bool InArea(EnemyState t, int col, int row, int radius, bool cross)
        {
            var minCol = t.col;
            var maxCol = t.col + t.width - 1;
            var minRow = t.row;
            var maxRow = t.row + t.height - 1;
            if (!cross) return maxCol >= col - radius && minCol <= col + radius && maxRow >= row - radius && minRow <= row + radius;
            var horizontal = maxCol >= col - radius && minCol <= col + radius && minRow <= row && maxRow >= row;
            var vertical = minCol <= col && maxCol >= col && maxRow >= row - radius && minRow <= row + radius;
            return horizontal || vertical;
        }

        bool IsVisited(int id, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (chainVisited[i] == id) return true;
            }
            return false;
        }

        static float Roll(RunState run, BallSlot b, int salt)
        {
            return BallCollision.Hash01(run.seed, b.shotNumber, b.hitIndex, salt * 32 + b.hashSalt);
        }

        #endregion
    }
}
