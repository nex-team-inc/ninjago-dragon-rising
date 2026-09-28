#nullable enable

// Implemented by Simulation module.
namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Builds the deterministic wave schedule and static field objects for one stage (GDD §7).
    /// </summary>
    public sealed class StageGenerator
    {
        /// <summary>
        /// Normal stage: minWaves..maxWaves rows drawn from the act's weighted enemy pool with a difficulty budget
        /// of baseBudgetPerRow + budgetGrowthPerStage × stageNumber per row (enemy cost subtracts from it), 20%
        /// (balance.pickupChancePerRow) of rows carry one pickup cell, and 0..maxFieldObjects static objects
        /// (pillar, crate, portal pair, mud) are placed on free grid cells outside rows 0-1 and the danger row.
        /// Boss stage: wave 0 holds the act boss at the top-centre 2×2 footprint plus bossEscortWaves escort rows;
        /// EnemyPhaseResolver spawns further escorts every bossEscortEveryNTurns while the boss lives.
        /// Every draw comes from rng so a seed reproduces the plan.
        /// </summary>
        public StagePlan Generate(GameRules rules, int actIndex, int stageInAct, int stageNumber, SimRandom rng)
        {
            throw new System.NotImplementedException("Simulation module");
        }
    }
}
