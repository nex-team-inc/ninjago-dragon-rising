#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;
using NUnit.Framework;

namespace Nex.BilliardRogue.Editor.Tests
{
    /// <summary>
    /// ShotSequencer turn rules: BalanceRules.shotsPerTurn balls per turn in bag order, the rotation carried across turns
    /// (run.nextBagIndex), bonus shots from pickups, the whole-bag rule at 0, the infinite-balls cheat and the 2P rotation.
    /// </summary>
    public class ShotSequencerTests
    {
        static readonly BallType[] FourBalls = { BallType.Basic, BallType.Flame, BallType.Frost, BallType.Bomb };

        static RunState NewRun(BallType[] bag, int numPlayers = 1)
        {
            var run = new RunState { numPlayers = numPlayers };
            foreach (var type in bag)
            {
                run.bag.Add(new BallInstance { type = type, level = 1 });
            }

            return run;
        }

        static List<BallType> PlayTurn(ShotSequencer sequencer, bool infinite = false, int maxShots = 100)
        {
            var fired = new List<BallType>();
            sequencer.BeginTurn(infinite);
            while (sequencer.HasBallToFire && fired.Count < maxShots)
            {
                fired.Add(sequencer.Fire(0).type);
            }

            sequencer.EndTurn();
            return fired;
        }

        [Test]
        public void ATurnFiresShotsPerTurnBallsInBagOrder()
        {
            var sequencer = new ShotSequencer(NewRun(FourBalls), 3, 0f);
            sequencer.BeginTurn(false);
            Assert.AreEqual(3, sequencer.Total);
            Assert.AreEqual(3, sequencer.Remaining);
            Assert.AreEqual(BallType.Basic, sequencer.Fire(0).type);
            Assert.AreEqual(BallType.Flame, sequencer.Fire(0).type);
            Assert.AreEqual(BallType.Frost, sequencer.Fire(0).type);
            Assert.IsFalse(sequencer.HasBallToFire, "the fourth ball waits for the next turn");
            Assert.AreEqual(0, sequencer.Remaining);
        }

        [Test]
        public void TheNextTurnContinuesTheRotation()
        {
            var run = NewRun(FourBalls);
            var sequencer = new ShotSequencer(run, 3, 0f);
            PlayTurn(sequencer);
            Assert.AreEqual(3, run.nextBagIndex);
            CollectionAssert.AreEqual(new[] { BallType.Bomb, BallType.Basic, BallType.Flame }, PlayTurn(sequencer));
            CollectionAssert.AreEqual(new[] { BallType.Frost, BallType.Bomb, BallType.Basic }, PlayTurn(sequencer));
            Assert.AreEqual(1, run.nextBagIndex);
        }

        [Test]
        public void ATurnCutShortKeepsItsUnfiredBallsFirstInLine()
        {
            var run = NewRun(FourBalls);
            var sequencer = new ShotSequencer(run, 3, 0f);
            sequencer.BeginTurn(false);
            sequencer.Fire(0);
            sequencer.EndTurn();
            Assert.AreEqual(1, run.nextBagIndex);
            CollectionAssert.AreEqual(new[] { BallType.Flame, BallType.Frost, BallType.Bomb }, PlayTurn(sequencer));
        }

        [Test]
        public void PickupsAddBonusBasicShotsThatDoNotCarryOver()
        {
            var run = NewRun(new[] { BallType.Flame, BallType.Frost, BallType.Bomb, BallType.Iron });
            var sequencer = new ShotSequencer(run, 3, 0f);
            sequencer.BeginTurn(false);
            sequencer.Fire(0);
            run.extraBalls = 1;
            Assert.AreEqual(4, sequencer.Total);
            sequencer.Fire(0);
            sequencer.Fire(0);
            Assert.IsTrue(sequencer.HasBallToFire, "the pickup reopened the turn");
            Assert.AreEqual(BallType.Basic, sequencer.Fire(0).type, "bonus shots are Basic balls");
            Assert.IsFalse(sequencer.HasBallToFire);
            sequencer.EndTurn();
            Assert.AreEqual(0, run.extraBalls);
            Assert.AreEqual(3, run.nextBagIndex, "bonus shots do not move the bag rotation");
        }

        [Test]
        public void ZeroShotsPerTurnFiresTheWholeBag()
        {
            var sequencer = new ShotSequencer(NewRun(FourBalls), 0, 0f);
            CollectionAssert.AreEqual(FourBalls, PlayTurn(sequencer));
        }

        [Test]
        public void ASmallBagWrapsWithinTheTurn()
        {
            var sequencer = new ShotSequencer(NewRun(new[] { BallType.Flame, BallType.Frost }), 3, 0f);
            CollectionAssert.AreEqual(new[] { BallType.Flame, BallType.Frost, BallType.Flame }, PlayTurn(sequencer));
            CollectionAssert.AreEqual(new[] { BallType.Frost, BallType.Flame, BallType.Frost }, PlayTurn(sequencer));
        }

        [Test]
        public void TheInfiniteCheatCyclesTheBagWithoutEndingTheTurn()
        {
            var sequencer = new ShotSequencer(NewRun(FourBalls), 3, 0f);
            var fired = PlayTurn(sequencer, infinite: true, maxShots: 6);
            CollectionAssert.AreEqual(new[] { BallType.Basic, BallType.Flame, BallType.Frost, BallType.Bomb, BallType.Basic, BallType.Flame }, fired);
            Assert.IsFalse(sequencer.Infinite, "the cheat is chosen again at every turn start");
        }

        [Test]
        public void TwoPlayersEachHaveShotsPerTurnAndShootTogether()
        {
            var run = NewRun(FourBalls, numPlayers: 2);
            var sequencer = new ShotSequencer(run, 3, 0.35f);
            sequencer.BeginTurn(false);
            Assert.AreEqual(6, sequencer.Total, "3 shots each");
            Assert.AreEqual(BallType.Basic, sequencer.Fire(0).type);
            Assert.IsFalse(sequencer.CanFire(0), "P1 waits out their own cooldown");
            Assert.IsTrue(sequencer.CanFire(1), "while P2 can shoot at the same moment");
            Assert.AreEqual(BallType.Flame, sequencer.Fire(1).type, "whoever fires takes the next ball in the bag");
            sequencer.Tick(1f);
            sequencer.Fire(0);
            sequencer.Tick(1f);
            sequencer.Fire(0);
            sequencer.Tick(1f);
            Assert.IsFalse(sequencer.HasShot(0), "P1 used their 3 shots");
            Assert.IsTrue(sequencer.HasShot(1), "P2 still has 2");
            Assert.IsTrue(sequencer.HasBallToFire);
            sequencer.Fire(1);
            sequencer.Tick(1f);
            sequencer.Fire(1);
            Assert.IsFalse(sequencer.HasBallToFire);
            sequencer.EndTurn();
            Assert.AreEqual(6 % FourBalls.Length, run.nextBagIndex);
        }

        [Test]
        public void ABonusShotGoesToWhicheverPlayerFiresIt()
        {
            var run = NewRun(FourBalls, numPlayers: 2);
            var sequencer = new ShotSequencer(run, 1, 0f);
            sequencer.BeginTurn(false);
            sequencer.Fire(0);
            run.extraBalls = 1;
            Assert.IsTrue(sequencer.HasShot(0), "P1's own shot is gone, the shared bonus shot is left");
            Assert.AreEqual(BallType.Basic, sequencer.Fire(0).type);
            Assert.IsFalse(sequencer.HasShot(0));
            Assert.IsTrue(sequencer.HasShot(1), "P2 keeps their own shot");
        }

        [Test]
        public void BetweenTurnsTheQueueShowsTheComingTurn()
        {
            var sequencer = new ShotSequencer(NewRun(FourBalls), 3, 0f);
            CollectionAssert.AreEqual(new[] { BallType.Basic, BallType.Flame, BallType.Frost }, Types(sequencer.TurnBalls));
            PlayTurn(sequencer);
            CollectionAssert.AreEqual(new[] { BallType.Bomb, BallType.Basic, BallType.Flame }, Types(sequencer.TurnBalls));
        }

        static List<BallType> Types(IReadOnlyList<BallInstance> balls)
        {
            var types = new List<BallType>();
            foreach (var ball in balls)
            {
                types.Add(ball.type);
            }

            return types;
        }
    }
}
