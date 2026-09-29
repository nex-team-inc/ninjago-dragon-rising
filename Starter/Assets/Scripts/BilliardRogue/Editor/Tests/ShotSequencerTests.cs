#nullable enable

using Nex.BilliardRogue.Simulation;
using NUnit.Framework;

namespace Nex.BilliardRogue.Editor.Tests
{
    /// <summary>
    /// ShotSequencer turn rules (playtest 4): BalanceRules.shotsPerTurn shots per player per turn, every shot a volley of
    /// the whole bag, shared bonus shots from pickups, per-player cooldowns when 2P shoot together, the infinite cheat.
    /// </summary>
    public class ShotSequencerTests
    {
        static RunState NewRun(int balls, int numPlayers = 1)
        {
            var run = new RunState { numPlayers = numPlayers };
            for (var i = 0; i < balls; i++)
            {
                run.bag.Add(new BallInstance { type = BallType.Basic, level = 1 });
            }

            return run;
        }

        [Test]
        public void ATurnHasShotsPerTurnShots()
        {
            var sequencer = new ShotSequencer(NewRun(4), 3, 0f);
            sequencer.BeginTurn(false);
            Assert.AreEqual(3, sequencer.Total);
            for (var shot = 0; shot < 3; shot++)
            {
                Assert.IsTrue(sequencer.CanFire(0), "shot " + (shot + 1));
                sequencer.Fire(0);
            }

            Assert.IsFalse(sequencer.HasBallToFire);
            Assert.AreEqual(0, sequencer.Remaining);
            Assert.AreEqual(3, sequencer.Fired);
        }

        [Test]
        public void EveryShotIsAVolleyOfTheWholeBag()
        {
            var run = NewRun(4);
            var sequencer = new ShotSequencer(run, 3, 0f);
            Assert.AreSame(run.bag, sequencer.Volley);
            run.bag.Add(new BallInstance { type = BallType.Flame, level = 1 });
            Assert.AreEqual(5, sequencer.Volley.Count, "a won ball joins every later shot");
        }

        [Test]
        public void PickupsAddSharedBonusShotsThatDoNotCarryOver()
        {
            var run = NewRun(4);
            var sequencer = new ShotSequencer(run, 3, 0f);
            sequencer.BeginTurn(false);
            sequencer.Fire(0);
            run.extraBalls = 1;
            Assert.AreEqual(4, sequencer.Total);
            sequencer.Fire(0);
            sequencer.Fire(0);
            Assert.IsTrue(sequencer.HasShot(0), "the pickup reopened the turn");
            sequencer.Fire(0);
            Assert.IsFalse(sequencer.HasBallToFire);
            sequencer.EndTurn();
            Assert.AreEqual(0, run.extraBalls);
            sequencer.BeginTurn(false);
            Assert.AreEqual(3, sequencer.Total, "bonus shots never carry over");
        }

        [Test]
        public void TheInfiniteCheatNeverRunsOutOfShots()
        {
            var sequencer = new ShotSequencer(NewRun(4), 3, 0f);
            sequencer.BeginTurn(true);
            for (var shot = 0; shot < 10; shot++)
            {
                sequencer.Fire(0);
            }

            Assert.IsTrue(sequencer.HasBallToFire);
            sequencer.EndTurn();
            Assert.IsFalse(sequencer.Infinite, "the cheat is chosen again at every turn start");
        }

        [Test]
        public void TwoPlayersEachHaveShotsPerTurnAndShootTogether()
        {
            var sequencer = new ShotSequencer(NewRun(4, numPlayers: 2), 3, 0.35f);
            sequencer.BeginTurn(false);
            Assert.AreEqual(6, sequencer.Total, "3 shots each");
            sequencer.Fire(0);
            Assert.IsFalse(sequencer.CanFire(0), "P1 waits out their own cooldown");
            Assert.IsTrue(sequencer.CanFire(1), "while P2 can shoot at the same moment");
            sequencer.Fire(1);
            for (var shot = 0; shot < 2; shot++)
            {
                sequencer.Tick(1f);
                sequencer.Fire(0);
            }

            sequencer.Tick(1f);
            Assert.IsFalse(sequencer.HasShot(0), "P1 used their 3 shots");
            Assert.IsTrue(sequencer.HasShot(1), "P2 still has 2");
            Assert.IsTrue(sequencer.HasBallToFire);
            sequencer.Fire(1);
            sequencer.Tick(1f);
            sequencer.Fire(1);
            Assert.IsFalse(sequencer.HasBallToFire);
        }

        [Test]
        public void ABonusShotGoesToWhicheverPlayerFiresIt()
        {
            var run = NewRun(4, numPlayers: 2);
            var sequencer = new ShotSequencer(run, 1, 0f);
            sequencer.BeginTurn(false);
            sequencer.Fire(0);
            run.extraBalls = 1;
            Assert.IsTrue(sequencer.HasShot(0), "P1's own shot is gone, the shared bonus shot is left");
            sequencer.Fire(0);
            Assert.IsFalse(sequencer.HasShot(0));
            Assert.IsTrue(sequencer.HasShot(1), "P2 keeps their own shot");
        }
    }
}
