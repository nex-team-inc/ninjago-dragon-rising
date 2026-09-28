#nullable enable

using System;
using System.Collections.Generic;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>Rolls and applies the three between-stage reward cards (GDD §7).</summary>
    public sealed class RewardGenerator
    {
        const int OptionCount = 3;
        const int RarityCount = 3;
        const int RandomKindCount = 4;

        readonly List<BallType> ballTypes = new();
        readonly List<int> bagIndices = new();

        #region Public Methods

        /// <summary>
        /// Fills output with exactly 3 distinct options (no two cards share a kind or a ball type): card 1 is a
        /// NewBall (type drawn by the act's rarityWeights among balls whose unlockTier ≤ highestUnlockTier; Basic is
        /// never offered while an ability ball is available; not offered at bagCap), card 2 an UpgradeBall (a bag
        /// entry below levelCap, amount = new level), card 3 a Heal when HP ≤ 50%, otherwise a random kind among
        /// NewBall / UpgradeBall / Heal (only when hurt) / MaxHp. Heal amount = balance.healRewardAmount, MaxHp amount
        /// = balance.maxHpRewardAmount. Missing kinds fall back in that order; Heal at full HP is the last resort.
        /// Deterministic for a given rng state.
        /// </summary>
        public void Roll(GameRules rules, RunState run, int highestUnlockTier, SimRandom rng, List<RewardOption> output)
        {
            output.Clear();
            var act = rules.acts[Math.Clamp(run.actIndex, 0, rules.acts.Length - 1)];
            var hurt = run.playerHp < run.playerMaxHp;

            TryAddNewBall(rules, run, act, highestUnlockTier, false, rng, output);
            TryAddUpgrade(rules, run, rng, output);
            if (hurt && run.playerHp * 2 <= run.playerMaxHp)
            {
                TryAddHeal(rules.balance, output);
            }

            var first = rng.Range(0, RandomKindCount);
            for (var i = 0; i < RandomKindCount && output.Count < OptionCount; i++)
            {
                switch ((first + i) % RandomKindCount)
                {
                    case 0:
                        TryAddNewBall(rules, run, act, highestUnlockTier, false, rng, output);
                        break;
                    case 1:
                        TryAddUpgrade(rules, run, rng, output);
                        break;
                    case 2:
                        if (hurt)
                        {
                            TryAddHeal(rules.balance, output);
                        }

                        break;
                    default:
                        TryAddMaxHp(rules.balance, output);
                        break;
                }
            }

            while (output.Count < OptionCount && TryAddNewBall(rules, run, act, highestUnlockTier, true, rng, output)) { }
            while (output.Count < OptionCount && TryAddUpgrade(rules, run, rng, output)) { }
            if (output.Count < OptionCount)
            {
                TryAddMaxHp(rules.balance, output);
            }

            if (output.Count < OptionCount)
            {
                TryAddHeal(rules.balance, output);
            }
        }

        /// <summary>
        /// Applies one option to the run: appends the ball to the bag (respecting bagCap), levels up the bag entry
        /// (never above levelCap, never down), heals (clamped to playerMaxHp) or raises playerMaxHp and playerHp by
        /// amount. Clears awaitingReward and pendingRewards.
        /// </summary>
        public void Apply(GameRules rules, RunState run, RewardOption option)
        {
            var balance = rules.balance;
            switch (option.kind)
            {
                case RewardKind.NewBall:
                    if (run.bag.Count < balance.bagCap)
                    {
                        run.bag.Add(new BallInstance { type = option.ballType, level = 1 });
                    }

                    break;
                case RewardKind.UpgradeBall:
                    if (option.bagIndex >= 0 && option.bagIndex < run.bag.Count && run.bag[option.bagIndex].type == option.ballType)
                    {
                        var ball = run.bag[option.bagIndex];
                        ball.level = Math.Min(balance.levelCap, Math.Max(ball.level, option.amount));
                    }

                    break;
                case RewardKind.Heal:
                    run.playerHp = Math.Min(run.playerMaxHp, run.playerHp + option.amount);
                    break;
                case RewardKind.MaxHp:
                    run.playerMaxHp += option.amount;
                    run.playerHp = Math.Min(run.playerMaxHp, run.playerHp + option.amount);
                    break;
            }

            run.awaitingReward = false;
            run.pendingRewards.Clear();
        }

        #endregion

        #region Options

        bool TryAddNewBall(GameRules rules, RunState run, ActRules act, int highestUnlockTier, bool allowBasic, SimRandom rng, List<RewardOption> output)
        {
            if (run.bag.Count >= rules.balance.bagCap) return false;

            var total = 0f;
            for (var r = 0; r < RarityCount; r++)
            {
                if (CollectBallTypes(rules, highestUnlockTier, allowBasic, (BallRarity)r, output) > 0)
                {
                    total += RarityWeight(act, r);
                }
            }

            int count;
            if (total > 0f)
            {
                var pick = rng.Value01() * total;
                var rarity = RarityCount - 1;
                for (var r = 0; r < RarityCount; r++)
                {
                    if (CollectBallTypes(rules, highestUnlockTier, allowBasic, (BallRarity)r, output) == 0) continue;
                    pick -= RarityWeight(act, r);
                    if (pick >= 0f) continue;
                    rarity = r;
                    break;
                }

                count = CollectBallTypes(rules, highestUnlockTier, allowBasic, (BallRarity)rarity, output);
            }
            else
            {
                count = CollectBallTypes(rules, highestUnlockTier, allowBasic, null, output);
            }

            if (count == 0) return false;
            output.Add(new RewardOption { kind = RewardKind.NewBall, ballType = ballTypes[rng.Range(0, count)] });
            return true;
        }

        bool TryAddUpgrade(GameRules rules, RunState run, SimRandom rng, List<RewardOption> output)
        {
            bagIndices.Clear();
            for (var i = 0; i < run.bag.Count; i++)
            {
                var ball = run.bag[i];
                if (ball.level >= rules.balance.levelCap || IsTypeOffered(output, ball.type))
                {
                    continue;
                }

                bagIndices.Add(i);
            }

            if (bagIndices.Count == 0) return false;
            var index = bagIndices[rng.Range(0, bagIndices.Count)];
            var chosen = run.bag[index];
            output.Add(new RewardOption { kind = RewardKind.UpgradeBall, ballType = chosen.type, bagIndex = index, amount = chosen.level + 1 });
            return true;
        }

        static void TryAddHeal(BalanceRules balance, List<RewardOption> output)
        {
            if (IsKindOffered(output, RewardKind.Heal)) return;
            output.Add(new RewardOption { kind = RewardKind.Heal, amount = balance.healRewardAmount });
        }

        static void TryAddMaxHp(BalanceRules balance, List<RewardOption> output)
        {
            if (IsKindOffered(output, RewardKind.MaxHp)) return;
            output.Add(new RewardOption { kind = RewardKind.MaxHp, amount = balance.maxHpRewardAmount });
        }

        #endregion

        #region Helpers

        /// <summary>Fills ballTypes with unlocked, not yet offered types of the rarity (null = any rarity).</summary>
        int CollectBallTypes(GameRules rules, int highestUnlockTier, bool allowBasic, BallRarity? rarity, List<RewardOption> output)
        {
            ballTypes.Clear();
            for (var i = 0; i < rules.balls.Length; i++)
            {
                var ball = rules.balls[i];
                if (ball.unlockTier > highestUnlockTier) continue;
                if (rarity.HasValue && ball.rarity != rarity.Value)
                {
                    continue;
                }

                var type = (BallType)i;
                if ((!allowBasic && type == BallType.Basic) || IsTypeOffered(output, type))
                {
                    continue;
                }

                ballTypes.Add(type);
            }

            return ballTypes.Count;
        }

        static float RarityWeight(ActRules act, int rarity)
        {
            return rarity < act.rarityWeights.Length ? Math.Max(0f, act.rarityWeights[rarity]) : 0f;
        }

        static bool IsTypeOffered(List<RewardOption> output, BallType type)
        {
            for (var i = 0; i < output.Count; i++)
            {
                var option = output[i];
                if ((option.kind == RewardKind.NewBall || option.kind == RewardKind.UpgradeBall) && option.ballType == type)
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsKindOffered(List<RewardOption> output, RewardKind kind)
        {
            for (var i = 0; i < output.Count; i++)
            {
                if (output[i].kind == kind) return true;
            }

            return false;
        }

        #endregion
    }
}
