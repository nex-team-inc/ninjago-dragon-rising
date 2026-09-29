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
                fired.Add(sequencer.Fire(out _).type);
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
            Assert.AreEqual(BallType.Basic, sequencer.Fire(out _).type);
            Assert.AreEqual(BallType.Flame, sequencer.Fire(out _).type);
            Assert.AreEqual(BallType.Frost, sequencer.Fire(out _).type);
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
            sequencer.Fire(out _);
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
            sequencer.Fire(out _);
            run.extraBalls = 1;
            Assert.AreEqual(4, sequencer.Total);
            sequencer.Fire(out _);
            sequencer.Fire(out _);
            Assert.IsTrue(sequencer.HasBallToFire, "the pickup reopened the turn");
            Assert.AreEqual(BallType.Basic, sequencer.Fire(out _).type, "bonus shots are Basic balls");
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
        public void TwoPlayersAlternateAcrossTurns()
        {
            var run = NewRun(FourBalls, numPlayers: 2);
            var sequencer = new ShotSequencer(run, 3, 0f);
            var shooters = new List<int>();
            for (var turn = 0; turn < 2; turn++)
            {
                sequencer.BeginTurn(false);
                while (sequencer.HasBallToFire)
                {
                    sequencer.Fire(out var shooter);
                    shooters.Add(shooter);
                }

                sequencer.EndTurn();
            }

            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1, 0, 1 }, shooters, "an odd shot count per turn still gives both players equal shots");
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
