#nullable enable

using System;
using System.Collections.Generic;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Rolls and applies the between-stage reward: three balls of distinct types (GDD v2 §1). Below bagCap every
    /// option is a NewBall (adds one level-1 ball); at bagCap only owned types are offered and each option is an
    /// UpgradeBall that levels up the lowest-level copy of that type. The v1 Heal / MaxHp kinds are never rolled any
    /// more but still apply, so a v1 save holding a pending Heal card stays valid.
    /// </summary>
    public sealed class RewardGenerator
    {
        public const int OptionCount = 3;
        const int RarityCount = 3;

        readonly List<BallType> ballTypes = new();

        #region Public Methods

        /// <summary>
        /// Fills output with up to 3 options of distinct ball types, each drawn by the act's rarityWeights (a rarity
        /// with no candidate left is skipped, then uniform within the rarity). Candidates below bagCap: every type
        /// whose unlockTier ≤ highestUnlockTier. At bagCap: owned types with a copy below levelCap (unlock tier
        /// ignored: they are already in the bag). Basic is a candidate only once no ability ball is left to offer
        /// (unless balance.offerBasicBall). Fewer than 3 options only at bagCap with fewer than 3 upgradable owned
        /// types (0 when the whole bag is maxed). Deterministic for a given rng state; stores rng.State into
        /// run.rngState so a save after the reward resumes the same run (callers keep the rolled options in
        /// run.pendingRewards for re-display).
        /// </summary>
        public void Roll(GameRules rules, RunState run, int highestUnlockTier, SimRandom rng, List<RewardOption> output)
        {
            output.Clear();
            var act = rules.acts[Math.Clamp(run.actIndex, 0, rules.acts.Length - 1)];
            var atCap = run.bag.Count >= rules.balance.bagCap;
            var offerBasic = rules.balance.offerBasicBall;
            while (output.Count < OptionCount && TryAddBall(rules, run, act, highestUnlockTier, atCap, offerBasic, rng, output)) { }
            if (!offerBasic)
            {
                while (output.Count < OptionCount && TryAddBall(rules, run, act, highestUnlockTier, atCap, true, rng, output)) { }
            }

            run.rngState = rng.State;
        }

        /// <summary>
        /// Applies one option: NewBall adds a level-1 ball below bagCap and, at bagCap, levels up the lowest-level copy
        /// of that type instead; UpgradeBall levels up its bag entry (never above levelCap, never down; a stale index
        /// falls back to the lowest-level copy of the type); legacy Heal heals (clamped to playerMaxHp) and legacy
        /// MaxHp raises playerMaxHp and playerHp by amount. Clears awaitingReward and pendingRewards.
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
                    else
                    {
                        LevelUpLowest(rules, run, option.ballType);
                    }

                    break;
                case RewardKind.UpgradeBall:
                    var index = option.bagIndex;
                    if (index >= 0 && index < run.bag.Count && run.bag[index].type == option.ballType)
                    {
                        var ball = run.bag[index];
                        ball.level = Math.Min(balance.levelCap, Math.Max(ball.level, option.amount));
                    }
                    else
                    {
                        LevelUpLowest(rules, run, option.ballType);
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

        /// <summary>
        /// The option picking a ball of this type gives right now: a NewBall below bagCap, else an UpgradeBall of the
        /// lowest-level copy (bagIndex, amount = new level). False at bagCap when no copy is below levelCap.
        /// </summary>
        public static bool TryMakeBallOption(GameRules rules, RunState run, BallType type, out RewardOption option)
        {
            if (run.bag.Count < rules.balance.bagCap)
            {
                option = new RewardOption { kind = RewardKind.NewBall, ballType = type };
                return true;
            }

            var index = LowestLevelIndex(rules, run, type);
            if (index < 0)
            {
                option = new RewardOption();
                return false;
            }

            option = new RewardOption { kind = RewardKind.UpgradeBall, ballType = type, bagIndex = index, amount = run.bag[index].level + 1 };
            return true;
        }

        /// <summary>Bag index of the lowest-level copy of type below levelCap (first one on ties), -1 if none.</summary>
        public static int LowestLevelIndex(GameRules rules, RunState run, BallType type)
        {
            var best = -1;
            for (var i = 0; i < run.bag.Count; i++)
            {
                var ball = run.bag[i];
                if (ball.type != type || ball.level >= rules.balance.levelCap) continue;
                if (best < 0 || ball.level < run.bag[best].level) best = i;
            }

            return best;
        }

        #endregion

        #region Helpers

        bool TryAddBall(GameRules rules, RunState run, ActRules act, int highestUnlockTier, bool atCap, bool allowBasic, SimRandom rng, List<RewardOption> output)
        {
            var total = 0f;
            for (var r = 0; r < RarityCount; r++)
            {
                if (Collect(rules, run, highestUnlockTier, atCap, allowBasic, (BallRarity)r, output) > 0)
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
                    if (Collect(rules, run, highestUnlockTier, atCap, allowBasic, (BallRarity)r, output) == 0) continue;
                    pick -= RarityWeight(act, r);
                    if (pick >= 0f) continue;
                    rarity = r;
                    break;
                }

                count = Collect(rules, run, highestUnlockTier, atCap, allowBasic, (BallRarity)rarity, output);
            }
            else
            {
                count = Collect(rules, run, highestUnlockTier, atCap, allowBasic, null, output);
            }

            if (count == 0) return false;
            if (!TryMakeBallOption(rules, run, ballTypes[rng.Range(0, count)], out var option)) return false;
            output.Add(option);
            return true;
        }

        /// <summary>Fills ballTypes with the not yet offered candidates of the rarity (null = any rarity).</summary>
        int Collect(GameRules rules, RunState run, int highestUnlockTier, bool atCap, bool allowBasic, BallRarity? rarity, List<RewardOption> output)
        {
            ballTypes.Clear();
            for (var i = 0; i < rules.balls.Length; i++)
            {
                var ball = rules.balls[i];
                if (rarity.HasValue && ball.rarity != rarity.Value) continue;
                var type = (BallType)i;
                if (!allowBasic && type == BallType.Basic) continue;
                if (IsTypeOffered(output, type)) continue;
                var eligible = atCap ? LowestLevelIndex(rules, run, type) >= 0 : ball.unlockTier <= highestUnlockTier;
                if (eligible) ballTypes.Add(type);
            }

            return ballTypes.Count;
        }

        static void LevelUpLowest(GameRules rules, RunState run, BallType type)
        {
            var index = LowestLevelIndex(rules, run, type);
            if (index >= 0) run.bag[index].level++;
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

        #endregion
    }
}
