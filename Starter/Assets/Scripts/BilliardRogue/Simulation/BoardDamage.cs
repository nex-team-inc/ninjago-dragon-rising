#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Enemy damage, status and death resolution behind BoardOps: hit accounting, the boss half-HP phase, deaths
    /// with chained bomber explosions and burn spreading. Deaths resolve after the triggering hit, so one
    /// DamageEnemy call removes every victim of an explosion chain.
    /// </summary>
    internal sealed class BoardDamage
    {
        readonly GameRules rules;
        readonly List<EnemyState> dying = new();
        readonly List<EnemyState> neighbours = new();
        bool resolvingDeaths;

        #region Life Cycle

        public BoardDamage(GameRules rules)
        {
            this.rules = rules;
        }

        #endregion

        #region Public Methods

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

        public void ApplyStatus(EnemyState e, StatusType status, int stacks, bool burnSpreads, List<SimEvent> events)
        {
            if (e.hp <= 0 || stacks <= 0)
            {
                return;
            }
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
            var ev = EnemyEvents.Make(rules.arena, SimEventKind.StatusApplied, e, result);
            ev.status = status;
            events.Add(ev);
        }

        public void HealEnemy(EnemyState e, int amount, List<SimEvent> events)
        {
            if (e.hp <= 0 || amount <= 0)
            {
                return;
            }
            var healed = Mathf.Min(amount, e.maxHp - e.hp);
            if (healed <= 0) return;
            e.hp += healed;
            events.Add(EnemyEvents.Make(rules.arena, SimEventKind.EnemyHealed, e, healed));
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
            // Hype's share of what was actually dealt (the part an overkill would have wasted does not count).
            var hypeBonus = src.hypeBonus > 0 ? dealt - Mathf.Min(Mathf.Max(0, total - src.hypeBonus), e.hp) : 0;
            e.hp -= dealt;
            run.stats.damageDealt += dealt;
            if (!src.isStatusTick && !src.isExplosion && !src.isChain)
            {
                run.stats.hits++;
            }
            var hit = EnemyEvents.Make(rules.arena, SimEventKind.EnemyHit, e, dealt);
            hit.ballId = src.ballId;
            hit.ballType = src.ballType;
            hit.flag = src.isCrit;
            hit.value2 = e.hp;
            hit.hypeBonus = hypeBonus;
            events.Add(hit);
            if (rules.enemies[(int)e.type].isBoss && !e.bossHalfTriggered && e.hp > 0 && e.hp * 2 <= e.maxHp)
            {
                e.bossHalfTriggered = true;
                e.halfHpSummonPending = true;
                events.Add(EnemyEvents.Make(rules.arena, SimEventKind.BossPhaseChanged, e, e.hp));
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
            var killed = EnemyEvents.Make(rules.arena, SimEventKind.EnemyKilled, e, 0);
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
            events.Add(new SimEvent
            {
                kind = SimEventKind.Explosion, sourceId = e.id, enemyType = e.type, position = EnemyEvents.Center(rules.arena, e), value = 1, value2 = damage,
            });
            BoardOccupancy.CollectNeighbours(run.board, e, neighbours);
            var src = new DamageSource { isExplosion = true };
            foreach (var n in neighbours)
            {
                if (n.hp <= 0) continue;
                ApplyHit(run, n, damage, src, events);
            }
        }

        void SpreadBurn(RunState run, EnemyState e, List<SimEvent> events)
        {
            BoardOccupancy.CollectNeighbours(run.board, e, neighbours);
            foreach (var n in neighbours)
            {
                if (n.hp <= 0) continue;
                ApplyStatus(n, StatusType.Burn, e.status.burn, false, events);
                return;
            }
        }

        #endregion
    }
}
