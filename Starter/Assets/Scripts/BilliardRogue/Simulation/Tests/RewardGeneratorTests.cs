#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Simulation.Tests
{
    public class RewardGeneratorTests
    {
        const int SeedCount = 200;

        static RunState MakeRun(GameRules rules, int hp)
        {
            var run = new RunState { playerMaxHp = rules.balance.playerMaxHp, playerHp = hp };
            foreach (var type in rules.balance.startingBag)
            {
                run.bag.Add(new BallInstance { type = type, level = 1 });
            }

            return run;
        }

        static List<RewardOption> Roll(GameRules rules, RunState run, int tier, int seed)
        {
            var output = new List<RewardOption>();
            new RewardGenerator().Roll(rules, run, tier, new SimRandom(SimRandom.SeedToState(seed)), output);
            return output;
        }

        [Test]
        public void RollOffersExactlyThreeDistinctOptions()
        {
            var rules = TestRules.Create();
            for (var seed = 0; seed < SeedCount; seed++)
            {
                for (var act = 0; act < SimConstants.ActCount; act++)
                {
                    var run = MakeRun(rules, 14);
                    run.actIndex = act;
                    var options = Roll(rules, run, 2, seed);
                    Assert.AreEqual(3, options.Count);
                    var kinds = new HashSet<RewardKind>();
                    var types = new HashSet<BallType>();
                    foreach (var option in options)
                    {
                        if (option.kind == RewardKind.NewBall || option.kind == RewardKind.UpgradeBall)
                        {
                            Assert.IsTrue(types.Add(option.ballType), $"ball type {option.ballType} offered twice");
                        }
                        else
                        {
                            Assert.IsTrue(kinds.Add(option.kind), $"{option.kind} offered twice");
                        }
                    }
                }
            }
        }

        [Test]
        public void RollIsDeterministicForASeed()
        {
            var rules = TestRules.Create();
            for (var seed = 0; seed < 20; seed++)
            {
                var a = Roll(rules, MakeRun(rules, 20), 1, seed);
                var b = Roll(rules, MakeRun(rules, 20), 1, seed);
                Assert.AreEqual(a.Count, b.Count);
                for (var i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(JsonUtility.ToJson(a[i]), JsonUtility.ToJson(b[i]));
                }
            }
        }

        [Test]
        public void NoHealAtFullHpWhenAlternativesExist()
        {
            var rules = TestRules.Create();
            for (var seed = 0; seed < SeedCount; seed++)
            {
                var options = Roll(rules, MakeRun(rules, rules.balance.playerMaxHp), 0, seed);
                foreach (var option in options)
                {
                    Assert.AreNotEqual(RewardKind.Heal, option.kind);
                }
            }
        }

        [Test]
        public void LockedBallsAreNeverOffered()
        {
            var rules = TestRules.Create();
            for (var tier = 0; tier <= 2; tier++)
            {
                for (var seed = 0; seed < SeedCount; seed++)
                {
                    foreach (var option in Roll(rules, MakeRun(rules, 10), tier, seed))
                    {
                        if (option.kind != RewardKind.NewBall) continue;
                        Assert.That(rules.balls[(int)option.ballType].unlockTier, Is.LessThanOrEqualTo(tier));
                    }
                }
            }
        }

        [Test]
        public void UpgradeTargetsABagEntryBelowTheLevelCap()
        {
            var rules = TestRules.Create();
            for (var seed = 0; seed < SeedCount; seed++)
            {
                var run = MakeRun(rules, 20);
                run.bag[0].level = rules.balance.levelCap;
                foreach (var option in Roll(rules, run, 2, seed))
                {
                    if (option.kind != RewardKind.UpgradeBall) continue;
                    Assert.AreNotEqual(0, option.bagIndex);
                    Assert.AreEqual(run.bag[option.bagIndex].type, option.ballType);
                    Assert.AreEqual(run.bag[option.bagIndex].level + 1, option.amount);
                }
            }
        }

        [Test]
        public void ApplyChangesTheRunAndClearsThePendingReward()
        {
            var rules = TestRules.Create();
            var generator = new RewardGenerator();
            var run = MakeRun(rules, 10);

            void Pending()
            {
                run.awaitingReward = true;
                run.pendingRewards.Add(new RewardOption { kind = RewardKind.MaxHp, amount = 1 });
            }

            Pending();
            var bagBefore = run.bag.Count;
            generator.Apply(rules, run, new RewardOption { kind = RewardKind.NewBall, ballType = BallType.Flame });
            Assert.AreEqual(bagBefore + 1, run.bag.Count);
            Assert.AreEqual(BallType.Flame, run.bag[run.bag.Count - 1].type);
            Assert.AreEqual(1, run.bag[run.bag.Count - 1].level);
            Assert.IsFalse(run.awaitingReward);
            Assert.AreEqual(0, run.pendingRewards.Count);

            Pending();
            generator.Apply(rules, run, new RewardOption { kind = RewardKind.UpgradeBall, ballType = run.bag[1].type, bagIndex = 1, amount = 2 });
            Assert.AreEqual(2, run.bag[1].level);
            Assert.IsFalse(run.awaitingReward);
            Assert.AreEqual(0, run.pendingRewards.Count);

            Pending();
            generator.Apply(rules, run, new RewardOption { kind = RewardKind.Heal, amount = 100 });
            Assert.AreEqual(run.playerMaxHp, run.playerHp);

            var maxBefore = run.playerMaxHp;
            run.playerHp = 5;
            Pending();
            generator.Apply(rules, run, new RewardOption { kind = RewardKind.MaxHp, amount = rules.balance.maxHpRewardAmount });
            Assert.AreEqual(maxBefore + rules.balance.maxHpRewardAmount, run.playerMaxHp);
            Assert.AreEqual(5 + rules.balance.maxHpRewardAmount, run.playerHp);
            Assert.IsFalse(run.awaitingReward);
            Assert.AreEqual(0, run.pendingRewards.Count);
        }

        [Test]
        public void ApplyNewBallRespectsTheBagCap()
        {
            var rules = TestRules.Create();
            var run = MakeRun(rules, 10);
            while (run.bag.Count < rules.balance.bagCap)
            {
                run.bag.Add(new BallInstance { type = BallType.Basic });
            }

            new RewardGenerator().Apply(rules, run, new RewardOption { kind = RewardKind.NewBall, ballType = BallType.Iron });
            Assert.AreEqual(rules.balance.bagCap, run.bag.Count);
        }
    }
}
