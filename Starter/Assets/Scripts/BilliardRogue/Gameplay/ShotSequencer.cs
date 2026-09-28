#nullable enable

using System;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Firing order within a turn: the bag in order, then the extra Basic shots granted by +1 Ball pickups
    /// (run.extraBalls, reset here at turn end per HANDOFF §4), shooters alternating P1, P2, ... across the turn
    /// (run.activePlayerIndex persists so a continued run keeps the rotation), and the PacingConfig shot cooldown.
    /// The debug infinite-balls cheat cycles the bag instead of ending the turn.
    /// </summary>
    public sealed class ShotSequencer
    {
        readonly RunState run;
        readonly BallInstance extraBall = new() { type = BallType.Basic, level = 1 };
        readonly float cooldown;
        float cooldownRemaining;
        int nextIndex;

        public ShotSequencer(RunState aRun, float shotCooldown)
        {
            run = aRun;
            cooldown = Math.Max(0f, shotCooldown);
        }

        /// <summary>Index of the next ball in the bag (bag.Count.. = extra shots).</summary>
        public int NextIndex => nextIndex;

        public bool Infinite { get; private set; }

        /// <summary>Shots available this turn: bag plus extra balls collected so far.</summary>
        public int Total => run.bag.Count + run.extraBalls;

        public int Remaining => Infinite ? Total : Math.Max(0, Total - nextIndex);

        public bool HasBallToFire => Infinite || nextIndex < Total;

        public bool CanFire => HasBallToFire && cooldownRemaining <= 0f;

        #region Public Methods

        public void BeginTurn(bool infiniteBalls)
        {
            nextIndex = 0;
            cooldownRemaining = 0f;
            Infinite = infiniteBalls;
        }

        /// <summary>Counts the cooldown down in gameplay time (frozen by hit-stop and pause).</summary>
        public void Tick(float scaledDeltaTime)
        {
            if (cooldownRemaining > 0f) cooldownRemaining -= scaledDeltaTime;
        }

        /// <summary>Returns the ball to fire, advances the queue, starts the cooldown and passes the cue to the next player.</summary>
        public BallInstance Fire(out int shooter)
        {
            shooter = run.activePlayerIndex;
            var bag = run.bag;
            var ball = nextIndex < bag.Count ? bag[nextIndex] : extraBall;
            nextIndex = Infinite && bag.Count > 0 ? (nextIndex + 1) % bag.Count : nextIndex + 1;
            cooldownRemaining = cooldown;
            run.activePlayerIndex = (run.activePlayerIndex + 1) % Math.Max(1, run.numPlayers);
            return ball;
        }

        /// <summary>Extra balls never carry over to the next turn.</summary>
        public void EndTurn()
        {
            run.extraBalls = 0;
            nextIndex = 0;
            cooldownRemaining = 0f;
        }

        #endregion
    }
}
