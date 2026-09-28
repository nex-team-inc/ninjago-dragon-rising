#nullable enable

using System.Collections.Generic;

// Implemented by Simulation module.
namespace Nex.BilliardRogue.Simulation
{
    /// <summary>Resolves one enemy phase (GDD §2.2) as five staged steps so presentation can animate them in order.</summary>
    public sealed class EnemyPhaseResolver
    {
        public EnemyPhaseResolver(GameRules rules, BoardOps ops)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>
        /// step 0: status ticks — burn deals N then decays to N-1 (StatusTick), poison deals N and never decays
        /// (max balance.poisonMax), freeze decrements (FreezeExpired at 0).
        /// step 1: abilities on their cadence with a telegraph the turn before (EnemyAbilityTelegraph): mage/lich bolts
        /// (EnemyAttack flag = ranged), healer heals adjacent enemies (EnemyHealed), totem/king slime/lich spawns into
        /// free adjacent cells, lich one-time half-HP summon, golem shield rotation (EnemyShieldRotated) and quake
        /// (every enemy advances abilityValue extra rows).
        /// step 2: advance — every non-frozen enemy moves down moveRows on its moveEveryNTurns cadence; when blocked
        /// it tries the diagonal toward the centre column, else stays (EnemyMoved position → position2). Pickups
        /// move with the waves and expire in the danger row (PickupExpired).
        /// step 3: danger-row attacks — every enemy in DangerRow deals its attack (EnemyAttack, PlayerDamaged,
        /// PlayerDied when hp ≤ 0; stats.damageTaken).
        /// step 4: spawn the next wave row at the top if the plan has one (WaveSpawned, EnemySpawned), or boss escort
        /// waves on bossEscortEveryNTurns; increments turnInStage and stats.turns.
        /// </summary>
        public void Resolve(RunState run, SimRandom rng, List<SimEvent> events)
        {
            throw new System.NotImplementedException("Simulation module");
        }
    }
}
