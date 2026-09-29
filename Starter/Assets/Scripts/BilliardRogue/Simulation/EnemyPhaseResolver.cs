#nullable enable

using System.Collections.Generic;

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
    /// Step 1 is EnemyAbilities, step 2 EnemyAdvance; this class owns the phase order, statuses, attacks and spawns.
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
        readonly EnemyAbilities abilities;
        readonly EnemyAdvance advance;
        readonly List<EnemyState> order = new();
        readonly List<EnemyState> frozen = new();

        #region Life Cycle

        public EnemyPhaseResolver(GameRules rules, BoardOps ops)
        {
            this.rules = rules;
            this.ops = ops;
            abilities = new EnemyAbilities(rules, ops);
            advance = new EnemyAdvance(rules, ops);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// step 0: status ticks — burn deals N then decays to N-1, poison deals N and never decays (StatusTick, then the
        /// EnemyHit/EnemyKilled of the damage).
        /// step 1: abilities — golem shield rotation (EnemyShieldRotated, value = Face), lich one-time half-HP summon
        /// (EnemyState.halfHpSummonPending), ranged bolts (EnemyAttack flag = true, abilityValue + act attack bonus),
        /// healer heals adjacent enemies (EnemyHealed), quake (EnemyAbilityTelegraph flag = true, value = TelegraphQuake:
        /// every other enemy advances abilityValue extra rows at step 2), spawns into random free cells around the
        /// caster outside the danger row and never straight in front of a caster that moves (EnemySpawned with
        /// sourceId = caster; spawnCountBelowHalf once at or below 50% HP).
        /// step 2: advance — pickups first (EnemyMoved with flag = true and targetId = pickup id; PickupExpired when
        /// entering the danger row), then enemies front row first: each row of movement goes straight down, else
        /// diagonally toward the centre column, else stops (EnemyMoved position → position2, value = rows moved).
        /// Enemies never move past the danger row.
        /// step 3: danger-row attacks (EnemyAttack flag = false, PlayerDamaged, PlayerDied).
        /// step 4: freeze expiry, turnInStage++ and stats.turns++, then StageSchedule.OnPhaseEnd (GDD v2 §5): the batch due
        /// on the coming turn pops in at random free cells outside the rows nearest the player (EnemySpawned / PickupSpawned
        /// with flag = true, then BatchSpawned); with the field empty and a batch left, the next batch spawns now and
        /// turnInStage jumps to its turn (BatchSpawned.sourceId = turns skipped; stats.turns counts played turns only).
        /// A boss stage cycles its escort batches while the boss lives and spawns nothing once it is dead.
        /// Stops after the step in which the player dies. Stores rng.State into run.rngState.
        /// holdBeforeDangerRow (debug practice mode) stops every advance one row short of the danger row.
        /// </summary>
        public void Resolve(RunState run, SimRandom rng, List<SimEvent> events, bool holdBeforeDangerRow = false)
        {
            if (run.outcome != RunOutcome.None) return;
            BeginPhase(run);
            var mark = events.Count;
            TickStatuses(run, events);
            Stamp(events, mark, StepStatus);
            mark = events.Count;
            abilities.Resolve(run, frozen, rng, events);
            Stamp(events, mark, StepAbilities);
            if (run.outcome == RunOutcome.None)
            {
                mark = events.Count;
                advance.Resolve(run, frozen, abilities.QuakeRows, abilities.QuakeSourceId, holdBeforeDangerRow, events);
                Stamp(events, mark, StepAdvance);
                mark = events.Count;
                Attack(run, events);
                Stamp(events, mark, StepAttacks);
            }
            if (run.outcome == RunOutcome.None)
            {
                mark = events.Count;
                EndPhase(run, rng, events);
                Stamp(events, mark, StepSpawn);
            }
            run.rngState = rng.State;
        }

        #endregion

        #region Telegraphs

        /// <summary>
        /// The warning an enemy carries between phases (EnemyAbilityTelegraph.value code, -1 = none): the cadence-N
        /// action that fires in its next phase, by the rules Resolve telegraphs with, so a continued run rebuilds
        /// the icons its events had set. An enemy frozen for the next phase skips its actions and shows none.
        /// </summary>
        public static int PendingTelegraph(GameRules rules, EnemyState e)
        {
            if (e.hp <= 0 || e.status.frozenTurns > 0)
            {
                return -1;
            }
            var r = rules.enemies[(int)e.type];
            var next = e.turnCounter + 1;
            // Resolve emits the spawn telegraph after the ability one, so it is the icon left showing when both fire.
            if (r.spawnCount > 0 && Telegraphs(next, r.spawnEveryNTurns))
            {
                return TelegraphSpawn;
            }
            if (r.ranged)
            {
                return Telegraphs(next, r.abilityEveryNTurns) ? TelegraphCast : -1;
            }
            if (r.healAmount > 0)
            {
                return Telegraphs(next, r.abilityEveryNTurns) ? TelegraphHeal : -1;
            }
            return r.abilityValue > 0 && Telegraphs(next, r.abilityEveryNTurns) ? TelegraphQuake : -1;
        }

        // Every-phase actions (N <= 1) are never telegraphed.
        static bool Telegraphs(int nextCounter, int every) => every > 1 && nextCounter % every == 0;

        #endregion

        #region Internal

        internal static bool IsDue(int turnCounter, int every) => every > 0 && turnCounter % every == 0;

        #endregion

        #region Steps

        void BeginPhase(RunState run)
        {
            frozen.Clear();
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

        void Attack(RunState run, List<SimEvent> events)
        {
            var dangerRow = ArenaGeometry.DangerRow(rules.arena);
            foreach (var e in run.board.enemies)
            {
                if (run.outcome != RunOutcome.None) break;
                if (e.attack <= 0 || e.turnCounter <= 0 || e.row + e.height - 1 != dangerRow)
                {
                    continue;
                }
                if (frozen.Contains(e) || abilities.HasCast(e))
                {
                    continue;
                }
                var ev = ops.EnemyEvent(SimEventKind.EnemyAttack, e, e.attack);
                ev.sourceId = e.id;
                events.Add(ev);
                ops.DamagePlayer(run, e.attack, e.id, events);
            }
        }

        void EndPhase(RunState run, SimRandom rng, List<SimEvent> events)
        {
            foreach (var e in frozen)
            {
                if (e.hp <= 0 || e.status.frozenTurns <= 0)
                {
                    continue;
                }
                e.status.frozenTurns--;
                if (e.status.frozenTurns == 0)
                {
                    events.Add(ops.EnemyEvent(SimEventKind.FreezeExpired, e, 0));
                }
            }
            run.turnInStage++;
            run.stats.turns++;
            StageSchedule.OnPhaseEnd(rules, ops, run, rng, events);
        }

        #endregion

        #region Helpers

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
