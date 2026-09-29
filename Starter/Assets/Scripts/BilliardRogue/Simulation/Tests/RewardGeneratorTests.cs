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
        public void RollOffersExactlyThreeDistinctNewBalls()
        {
            var rules = TestRules.Create();
            for (var seed = 0; seed < SeedCount; seed++)
            {
                for (var act = 0; act < SimConstants.ActCount; act++)
                {
                    var run = MakeRun(rules, 5 + seed % 20);
                    run.actIndex = act;
                    var options = Roll(rules, run, 2, seed);
                    Assert.AreEqual(3, options.Count);
                    var types = new HashSet<BallType>();
                    foreach (var option in options)
                    {
                        Assert.AreEqual(RewardKind.NewBall, option.kind, "below the bag cap every option adds a ball");
                        Assert.IsTrue(types.Add(option.ballType), $"ball type {option.ballType} offered twice");
                        Assert.AreNotEqual(BallType.Basic, option.ballType, "Basic while ability balls are available");
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
        public void BasicIsOnlyTheLastResort()
        {
            var rules = TestRules.Create();
            foreach (var ball in rules.balls)
            {
                ball.unlockTier = ball.type == BallType.Basic || ball.type == BallType.Flame ? 0 : 9;
            }

            for (var seed = 0; seed < 20; seed++)
            {
                var options = Roll(rules, MakeRun(rules, 20), 0, seed);
                Assert.AreEqual(2, options.Count, "only Flame and Basic are unlocked");
                Assert.AreEqual(BallType.Flame, options[0].ballType, "the ability ball comes first");
                Assert.AreEqual(BallType.Basic, options[1].ballType);
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
        public void AtTheBagCapOnlyOwnedTypesLevelUpTheirLowestCopy()
        {
            var rules = TestRules.Create();
            for (var seed = 0; seed < SeedCount; seed++)
            {
                var run = MakeRun(rules, 20);
                run.bag.Clear();
                var owned = new[] { BallType.Basic, BallType.Flame, BallType.Frost, BallType.Iron, BallType.Piercer };
                while (run.bag.Count < rules.balance.bagCap)
                {
                    var type = owned[run.bag.Count % owned.Length];
                    run.bag.Add(new BallInstance { type = type, level = 1 + (run.bag.Count / owned.Length) % rules.balance.levelCap });
                }

                // Iron is maxed everywhere: it can no longer level up.
                foreach (var ball in run.bag)
                {
                    if (ball.type == BallType.Iron) ball.level = rules.balance.levelCap;
                }

                var options = Roll(rules, run, 0, seed);
                Assert.AreEqual(3, options.Count, "Flame, Frost and Piercer can level up (Piercer despite its unlock tier)");
                var types = new HashSet<BallType>();
                foreach (var option in options)
                {
                    Assert.AreEqual(RewardKind.UpgradeBall, option.kind);
                    Assert.IsTrue(types.Add(option.ballType));
                    Assert.AreNotEqual(BallType.Iron, option.ballType);
                    Assert.AreNotEqual(BallType.Basic, option.ballType, "Basic while ability balls can level up");
                    Assert.AreEqual(RewardGenerator.LowestLevelIndex(rules, run, option.ballType), option.bagIndex);
                    Assert.AreEqual(run.bag[option.bagIndex].level + 1, option.amount);
                }
            }
        }

        [Test]
        public void AFullyMaxedBagAtTheCapGetsNoOption()
        {
            var rules = TestRules.Create();
            var run = MakeRun(rules, 20);
            while (run.bag.Count < rules.balance.bagCap)
            {
                run.bag.Add(new BallInstance { type = BallType.Flame });
            }

            foreach (var ball in run.bag)
            {
                ball.level = rules.balance.levelCap;
            }

            Assert.AreEqual(0, Roll(rules, run, 2, 3).Count);
        }

        [Test]
        public void PickingAnOwnedTypeAtTheCapLevelsUpItsLowestCopy()
        {
            var rules = TestRules.Create();
            var generator = new RewardGenerator();
            var run = MakeRun(rules, 10);
            while (run.bag.Count < rules.balance.bagCap)
            {
                run.bag.Add(new BallInstance { type = BallType.Frost, level = 2 });
            }

            run.bag[run.bag.Count - 1].level = 1;
            var lowest = run.bag.Count - 1;
            Assert.IsTrue(RewardGenerator.TryMakeBallOption(rules, run, BallType.Frost, out var option));
            Assert.AreEqual(RewardKind.UpgradeBall, option.kind);
            Assert.AreEqual(lowest, option.bagIndex);
            Assert.AreEqual(2, option.amount, "Lv 1 → Lv 2");
            Assert.IsFalse(RewardGenerator.TryMakeBallOption(rules, run, BallType.Thunder, out _), "not owned at the cap");

            generator.Apply(rules, run, option);
            Assert.AreEqual(rules.balance.bagCap, run.bag.Count);
            Assert.AreEqual(2, run.bag[lowest].level);

            // A NewBall of an owned type at the cap (forced debug ball, stale card) levels up instead of adding.
            generator.Apply(rules, run, new RewardOption { kind = RewardKind.NewBall, ballType = BallType.Frost });
            Assert.AreEqual(rules.balance.bagCap, run.bag.Count);
            Assert.AreEqual(3, run.bag[FirstIndexOf(run, BallType.Frost)].level);
        }

        static int FirstIndexOf(RunState run, BallType type)
        {
            for (var i = 0; i < run.bag.Count; i++)
            {
                if (run.bag[i].type == type) return i;
            }

            return -1;
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
