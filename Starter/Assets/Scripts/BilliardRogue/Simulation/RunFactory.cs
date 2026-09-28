#nullable enable

using System.Collections.Generic;

// Implemented by Simulation module.
namespace Nex.BilliardRogue.Simulation
{
    /// <summary>Creates runs and drives the stage progression (3 acts × 4 stages, GDD §7).</summary>
    public sealed class RunFactory
    {
        /// <summary>
        /// New run at act 0 / stage 0 with playerHp = playerMaxHp = balance.playerMaxHp, bag = balance.startingBag at
        /// level 1, rngState = SimRandom.SeedToState(seed), a fresh runId, numPlayers and empty board/plan.
        /// Does not generate the stage: call BeginStage next.
        /// </summary>
        public RunState NewRun(GameRules rules, int seed, int numPlayers)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>
        /// Generates run.stage for the current act/stage (StageGenerator), clears the board, places the plan's field
        /// objects, spawns wave rows 0-1 (WaveSpawned/EnemySpawned), resets turnInStage/nextWaveIndex/extraBalls and
        /// stores rng.State back into run.rngState. Boss stages heal balance.bossHealFraction of max HP first
        /// (PlayerHealed) — GDD: "After a boss: full heal 50% of max HP first" applies when entering the reward.
        /// </summary>
        public void BeginStage(GameRules rules, RunState run, SimRandom rng, List<SimEvent> events)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>True when every scheduled wave has spawned and no enemy remains on the board.</summary>
        public bool IsStageCleared(RunState run)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>
        /// Moves to the next stage (stageInAct++, wrapping into the next act; stageNumber++). Returns false when the
        /// act-3 boss stage was the last one: the run is complete and outcome should become Victory.
        /// </summary>
        public bool AdvanceToNextStage(GameRules rules, RunState run)
        {
            throw new System.NotImplementedException("Simulation module");
        }
    }
}
