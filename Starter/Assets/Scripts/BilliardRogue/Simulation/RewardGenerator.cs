#nullable enable

using System.Collections.Generic;

// Implemented by Simulation module.
namespace Nex.BilliardRogue.Simulation
{
    /// <summary>Rolls and applies the three between-stage reward cards (GDD §7).</summary>
    public sealed class RewardGenerator
    {
        /// <summary>
        /// Fills output with exactly 3 distinct options: NewBall (ball type drawn by the act's rarityWeights among
        /// balls whose unlockTier ≤ highestUnlockTier and not already at bagCap), UpgradeBall (a bag entry below
        /// levelCap, amount = new level), Heal (amount = balance.healRewardAmount) and MaxHp
        /// (amount = balance.maxHpRewardAmount). Never offers the same ball type twice, never offers Heal at full HP
        /// when another option is possible. Deterministic for a given rng state.
        /// </summary>
        public void Roll(GameRules rules, RunState run, int highestUnlockTier, SimRandom rng, List<RewardOption> output)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>
        /// Applies one option to the run: appends the ball to the bag (respecting bagCap), levels up the bag entry,
        /// heals (clamped to playerMaxHp) or raises playerMaxHp and playerHp by amount. Clears awaitingReward and
        /// pendingRewards.
        /// </summary>
        public void Apply(GameRules rules, RunState run, RewardOption option)
        {
            throw new System.NotImplementedException("Simulation module");
        }
    }
}
