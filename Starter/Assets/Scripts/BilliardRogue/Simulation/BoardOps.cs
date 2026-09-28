#nullable enable

using System.Collections.Generic;

// Implemented by Simulation module.
namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Spawning, occupancy, damage and status application shared by BallSimulator and EnemyPhaseResolver so both
    /// resolve deaths, explosions and burn spreading identically.
    /// </summary>
    public sealed class BoardOps
    {
        public BoardOps(GameRules rules)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>True when no enemy footprint, field object or pickup occupies the cell (cell must be inside the grid).</summary>
        public bool IsCellFree(BoardState b, int col, int row)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>
        /// Creates an enemy of type t at (col, row) with stats scaled for the run's stage: hp × (1 + hpScalePerStage ×
        /// stageNumber) rounded, attack + attackBonusPerAct × actIndex, boss footprint from rules. Assigns
        /// board.nextId, appends EnemySpawned and returns the state.
        /// </summary>
        public EnemyState SpawnEnemy(RunState run, EnemyType t, int col, int row, List<SimEvent> events)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>
        /// Deals amount (+1 while poisoned, ×2 first hit of a power-pickup ball, crit handled by the caller) and
        /// returns the damage actually dealt. On death: EnemyKilled, bomber explosion (deathExplosionDamage to the
        /// 8 neighbours, chaining), Flame level-3 burn spread to one neighbour, stats.kills, boss bookkeeping
        /// (bossesDefeated, BossPhaseChanged at 50%). Emits EnemyHit with value = dealt.
        /// </summary>
        public int DamageEnemy(RunState run, EnemyState e, int amount, DamageSource src, List<SimEvent> events)
        {
            throw new System.NotImplementedException("Simulation module");
        }
    }
}
