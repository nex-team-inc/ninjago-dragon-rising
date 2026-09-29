#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// GDD v2 §5 batch cadence shared by RunFactory (the stage's first batch) and EnemyPhaseResolver (step 4):
    /// batch k spawns when turnInStage reaches k × spawnEveryNTurns, i.e. on turns 1, 1 + N, 1 + 2N (turn = turnInStage + 1).
    /// Skip empty turns: when a phase ends with no enemy on the field (Bone Walls do not count) and a batch is left, the
    /// next batch spawns right away and turnInStage jumps to its turn. A normal stage spawns each planned batch once; a
    /// boss stage cycles its escort batches while the boss lives and spawns nothing once it is dead.
    /// A stage generated before §5 (v1 row waves, StagePlan.batches empty) is converted on its first phase end
    /// (UpgradeLegacyStage) and continues with these rules.
    /// </summary>
    public static class StageSchedule
    {
        #region Public Methods

        /// <summary>True for a v1 plan (row waves, no batches) that has not been converted yet.</summary>
        public static bool IsLegacyPlan(StagePlan stage) => stage.batches.Count == 0 && stage.waves.Count > 0;

        /// <summary>
        /// Whether a batch can still spawn: normal stage = planned batches left; boss stage = escort batches exist and the
        /// boss lives. A legacy plan counts its unspawned v1 waves (boss: escort waves while the boss lives).
        /// </summary>
        public static bool HasBatchesLeft(GameRules rules, RunState run)
        {
            var stage = run.stage;
            if (IsLegacyPlan(stage))
            {
                return stage.isBoss ? stage.waves.Count > 1 && IsBossAlive(rules, run) : run.nextWaveIndex < stage.waves.Count;
            }

            if (stage.batches.Count == 0) return false;
            return stage.isBoss ? IsBossAlive(rules, run) : run.nextBatchIndex < stage.batches.Count;
        }

        /// <summary>Batches a normal stage still has to spawn (0 on a boss stage, whose escorts cycle).</summary>
        public static int BatchesLeft(RunState run)
        {
            var stage = run.stage;
            if (stage.isBoss) return 0;
            if (IsLegacyPlan(stage)) return Mathf.Max(0, stage.waves.Count - run.nextWaveIndex);
            return Mathf.Max(0, stage.batches.Count - run.nextBatchIndex);
        }

        /// <summary>No enemy other than Bone Walls on the field.</summary>
        public static bool IsFieldEmpty(RunState run)
        {
            foreach (var e in run.board.enemies)
            {
                if (e.type != EnemyType.BoneWall) return false;
            }

            return true;
        }

        /// <summary>
        /// Spawns the next batch (a boss stage wraps around its escort batches), advances nextBatchIndex and schedules
        /// the following one spawnEveryNTurns after the current turnInStage.
        /// </summary>
        public static void SpawnNext(GameRules rules, BoardOps ops, RunState run, int skippedTurns, SimRandom rng, List<SimEvent> events)
        {
            var batches = run.stage.batches;
            if (batches.Count == 0) return;
            var index = run.nextBatchIndex;
            var batch = batches[index % batches.Count];
            run.nextBatchIndex = index + 1;
            run.nextBatchTurn = run.turnInStage + Mathf.Max(1, rules.acts[run.actIndex].spawnEveryNTurns);
            ops.SpawnBatch(run, batch, index, skippedTurns, rng, events);
        }

        /// <summary>
        /// Enemy phase step 4, after turnInStage++: spawns the batch that is due, or skips ahead to the next one when the
        /// field is empty. Converts a legacy plan first.
        /// </summary>
        public static void OnPhaseEnd(GameRules rules, BoardOps ops, RunState run, SimRandom rng, List<SimEvent> events)
        {
            UpgradeLegacyStage(rules, run);
            if (!HasBatchesLeft(rules, run)) return;
            if (run.turnInStage >= run.nextBatchTurn)
            {
                SpawnNext(rules, ops, run, 0, rng, events);
                return;
            }

            if (!IsFieldEmpty(run)) return;
            var skipped = run.nextBatchTurn - run.turnInStage;
            run.turnInStage = run.nextBatchTurn;
            SpawnNext(rules, ops, run, skipped, rng, events);
        }

        /// <summary>
        /// v1 save → v2 rules: the unspawned row waves (a boss stage: all escort waves, which v1 cycled) are packed in
        /// order into batches of at least act.minEnemiesPerBatch enemies (the last may be smaller) and the first one is
        /// due at the next phase end. Deterministic, no rng. Returns true when the plan was converted.
        /// </summary>
        public static bool UpgradeLegacyStage(GameRules rules, RunState run)
        {
            var stage = run.stage;
            if (!IsLegacyPlan(stage)) return false;
            var minEnemies = Mathf.Max(1, rules.acts[run.actIndex].minEnemiesPerBatch);
            var first = stage.isBoss ? 1 : Mathf.Max(0, run.nextWaveIndex);
            SpawnBatch? current = null;
            var enemies = 0;
            for (var w = first; w < stage.waves.Count; w++)
            {
                foreach (var cell in stage.waves[w].cells)
                {
                    if (current == null)
                    {
                        current = new SpawnBatch();
                        stage.batches.Add(current);
                        enemies = 0;
                    }

                    current.entries.Add(new BatchEntry { isPickup = cell.isPickup, enemy = cell.enemy, pickup = cell.pickup });
                    if (!cell.isPickup) enemies++;
                    if (enemies >= minEnemies) current = null;
                }
            }

            run.nextWaveIndex = stage.waves.Count;
            run.nextBatchIndex = 0;
            run.nextBatchTurn = run.turnInStage;
            return stage.batches.Count > 0;
        }

        #endregion

        #region Helpers

        static bool IsBossAlive(GameRules rules, RunState run)
        {
            foreach (var e in run.board.enemies)
            {
                if (rules.enemies[(int)e.type].isBoss) return true;
            }

            return false;
        }

        #endregion
    }
}
