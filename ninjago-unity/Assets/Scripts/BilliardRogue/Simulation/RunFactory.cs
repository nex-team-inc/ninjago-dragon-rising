#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>Creates runs and drives the stage progression (3 acts × 4 stages, GDD §7).</summary>
    public sealed class RunFactory
    {
        readonly StageGenerator stageGenerator = new();
        BoardOps? cachedOps;
        GameRules? cachedOpsRules;

        #region Public Methods

        /// <summary>
        /// New run at act 0 / stage 0 with playerHp = playerMaxHp = balance.playerMaxHp, bag = balance.startingBag at
        /// level 1, rngState = SimRandom.SeedToState(seed), a fresh runId, numPlayers and empty board/plan.
        /// Does not generate the stage: call BeginStage next.
        /// </summary>
        public RunState NewRun(GameRules rules, int seed, int numPlayers)
        {
            var balance = rules.balance;
            var run = new RunState
            {
                runId = Guid.NewGuid().ToString("N"),
                seed = seed,
                rngState = SimRandom.SeedToState(seed),
                numPlayers = Math.Max(1, numPlayers),
                playerHp = balance.playerMaxHp,
                playerMaxHp = balance.playerMaxHp,
                energy = Math.Min(balance.startingEnergy, balance.energyMax),
            };
            foreach (var type in balance.startingBag)
            {
                run.bag.Add(new BallInstance { type = type, level = 1 });
            }
            return run;
        }

        /// <summary>
        /// A run saved under a larger BalanceRules.playerMaxHp (GDD v2 §10 cut it to 3) continues with the current
        /// maximum, its HP keeping its share of it (at least 1). Returns true when the run changed.
        /// </summary>
        public static bool FitHpToRules(GameRules rules, RunState run)
        {
            var max = Math.Max(1, rules.balance.playerMaxHp);
            if (run.playerMaxHp <= max) return false;
            var share = (float)run.playerHp / run.playerMaxHp;
            run.playerMaxHp = max;
            run.playerHp = Mathf.Clamp(Mathf.RoundToInt(share * max), 1, max);
            return true;
        }

        /// <summary>
        /// Generates run.stage for the current act/stage (StageGenerator), clears the board, copies the plan's field
        /// objects (same ids; crates without hp get balance.crateHp; board.nextId = max id + 1), resets
        /// turnInStage/nextWaveIndex/nextBatchIndex/extraBalls and spawns the stage's first turn (GDD v2 §5): a boss stage
        /// first places the act boss at col (columns − width) / 2, row 0 (EnemySpawned without the pop-in flag), then the
        /// first batch pops in (StageSchedule.SpawnNext; the next one is due spawnEveryNTurns later). Stores rng.State into
        /// run.rngState. The post-boss heal happens in CompleteStage, before the reward.
        /// </summary>
        public void BeginStage(GameRules rules, RunState run, SimRandom rng, List<SimEvent> events)
        {
            var ops = OpsFor(rules);
            var plan = stageGenerator.Generate(rules, run.actIndex, run.stageInAct, run.stageNumber, rng);
            run.stage = plan;
            var board = run.board;
            board.enemies.Clear();
            board.fieldObjects.Clear();
            board.pickups.Clear();
            var maxId = 0;
            foreach (var planned in plan.fieldObjects)
            {
                var hp = planned.hp;
                if (planned.type == FieldObjectType.Crate && hp <= 0)
                {
                    hp = rules.balance.crateHp;
                }
                board.fieldObjects.Add(new FieldObjectState
                {
                    id = planned.id,
                    type = planned.type,
                    col = planned.col,
                    row = planned.row,
                    hp = hp,
                    pairId = planned.pairId,
                });
                maxId = Math.Max(maxId, planned.id);
            }
            board.nextId = maxId + 1;
            run.turnInStage = 0;
            run.nextWaveIndex = 0;
            run.nextBatchIndex = 0;
            run.nextBatchTurn = 0;
            run.extraBalls = 0;
            if (plan.isBoss)
            {
                var bossType = rules.acts[run.actIndex].bossType;
                var width = Math.Max(1, rules.enemies[(int)bossType].width);
                ops.SpawnEnemy(run, bossType, Math.Max(0, (rules.arena.columns - width) / 2), 0, events);
            }
            StageSchedule.SpawnNext(rules, ops, run, 0, rng, events);
            run.rngState = rng.State;
        }

        /// <summary>
        /// True when no batch can spawn any more (normal stage: all planned batches spawned; boss stage: the boss is dead)
        /// and no enemy other than BoneWall remains on the board.
        /// </summary>
        public bool IsStageCleared(RunState run)
        {
            if (!run.stage.isBoss && StageSchedule.BatchesLeft(run) > 0)
            {
                return false;
            }
            return StageSchedule.IsFieldEmpty(run);
        }

        /// <summary>
        /// Call once when IsStageCleared turns true: leftover pickups vanish (PickupExpired), the player heals
        /// balance.stageClearHeal plus, on a boss stage, balance.bossHealFraction of max HP (one PlayerHealed, clamped to
        /// max HP; none at full HP), emits StageCleared (value = stageNumber, flag = boss)
        /// and sets awaitingReward unless this was the final stage.
        /// </summary>
        public void CompleteStage(GameRules rules, RunState run, List<SimEvent> events)
        {
            var pickups = run.board.pickups;
            foreach (var p in pickups)
            {
                events.Add(new SimEvent
                {
                    kind = SimEventKind.PickupExpired,
                    targetId = p.id,
                    pickup = p.type,
                    position = ArenaGeometry.CellCenter(rules.arena, p.col, p.row),
                });
            }
            pickups.Clear();
            var heal = Mathf.Max(0, rules.balance.stageClearHeal);
            if (run.stage.isBoss) heal += Mathf.RoundToInt(run.playerMaxHp * rules.balance.bossHealFraction);
            if (heal > 0) OpsFor(rules).HealPlayer(run, heal, events);
            events.Add(new SimEvent { kind = SimEventKind.StageCleared, value = run.stageNumber, flag = run.stage.isBoss });
            run.awaitingReward = !IsFinalStage(rules, run);
        }

        /// <summary>
        /// Moves to the next stage (stageInAct++, wrapping into the next act after its boss stage; stageNumber++).
        /// Returns false and sets outcome = Victory when the act-3 boss stage was the last one.
        /// </summary>
        public bool AdvanceToNextStage(GameRules rules, RunState run)
        {
            if (IsFinalStage(rules, run))
            {
                run.outcome = RunOutcome.Victory;
                return false;
            }
            if (run.stageInAct < rules.acts[run.actIndex].normalStages)
            {
                run.stageInAct++;
            }
            else
            {
                run.actIndex++;
                run.stageInAct = 0;
            }
            run.stageNumber++;
            run.turnInStage = 0;
            return true;
        }

        #endregion

        #region Helpers

        static bool IsFinalStage(GameRules rules, RunState run)
        {
            return run.actIndex >= rules.acts.Length - 1 && run.stageInAct >= rules.acts[run.actIndex].normalStages;
        }

        BoardOps OpsFor(GameRules rules)
        {
            if (cachedOps == null || cachedOpsRules != rules)
            {
                cachedOps = new BoardOps(rules);
                cachedOpsRules = rules;
            }
            return cachedOps;
        }

        #endregion
    }
}
