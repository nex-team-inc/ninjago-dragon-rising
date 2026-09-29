#nullable enable

using System;
using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Firing order within a turn: BalanceRules.shotsPerTurn balls from the bag in order, starting at run.nextBagIndex
    /// and wrapping around, so every ball keeps its place in the rotation across turns (0 = the whole bag, the v1 rule);
    /// then the extra Basic shots granted by +1 Ball pickups (run.extraBalls, reset here at turn end per HANDOFF §4).
    /// Shooters alternate P1, P2, ... per shot (run.activePlayerIndex persists so a continued run keeps the rotation),
    /// with the PacingConfig shot cooldown in between. The debug infinite-balls cheat cycles the bag instead of ending
    /// the turn.
    /// </summary>
    public sealed class ShotSequencer
    {
        readonly RunState run;
        readonly int shotsPerTurn;
        readonly float cooldown;
        readonly BallInstance extraBall = new() { type = BallType.Basic, level = 1 };
        readonly List<BallInstance> turnBalls = new();
        float cooldownRemaining;
        int turnStart;
        int fired;

        public ShotSequencer(RunState aRun, int aShotsPerTurn, float shotCooldown)
        {
            run = aRun;
            shotsPerTurn = Math.Max(0, aShotsPerTurn);
            cooldown = Math.Max(0f, shotCooldown);
            FillTurnBalls();
        }

        public bool Infinite { get; private set; }

        /// <summary>Shots fired so far this turn.</summary>
        public int Fired => fired;

        /// <summary>This turn's bag balls in firing order (the bonus shots follow them); the coming turn's between turns.</summary>
        public IReadOnlyList<BallInstance> TurnBalls => turnBalls;

        /// <summary>Changes whenever TurnBalls is refilled, so the HUD knows to redraw the queue.</summary>
        public int TurnBallsVersion { get; private set; }

        /// <summary>Position of the next shot in TurnBalls (TurnBalls.Count.. = bonus shots).</summary>
        public int NextShot => Infinite && turnBalls.Count > 0 ? fired % turnBalls.Count : fired;

        /// <summary>Shots available this turn: the turn's bag balls plus the extra balls collected so far.</summary>
        public int Total => turnBalls.Count + run.extraBalls;

        public int Remaining => Infinite ? Total : Math.Max(0, Total - fired);

        public bool HasBallToFire => Infinite ? turnBalls.Count > 0 : fired < Total;

        public bool CanFire => HasBallToFire && cooldownRemaining <= 0f;

        public void BeginTurn(bool infiniteBalls)
        {
            fired = 0;
            cooldownRemaining = 0f;
            Infinite = infiniteBalls;
            FillTurnBalls();
        }

        /// <summary>
        /// Counts the cooldown down in real time (PlayerTurnLoop passes unscaled time, so hit-stop and slow-mo do not
        /// stretch it; the loop is not ticked while paused).
        /// </summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (cooldownRemaining > 0f) cooldownRemaining -= unscaledDeltaTime;
        }

        /// <summary>Returns the ball to fire, advances the queue, starts the cooldown and passes the cue to the next player.</summary>
        public BallInstance Fire(out int shooter)
        {
            shooter = run.activePlayerIndex;
            var shot = NextShot;
            var ball = shot < turnBalls.Count ? turnBalls[shot] : extraBall;
            fired++;
            cooldownRemaining = cooldown;
            run.activePlayerIndex = (run.activePlayerIndex + 1) % Math.Max(1, run.numPlayers);
            return ball;
        }

        /// <summary>
        /// The next turn starts with the first bag ball this one did not fire (a turn cut short by an empty field or a
        /// cleared stage keeps its unfired balls first in line). Extra balls never carry over.
        /// </summary>
        public void EndTurn()
        {
            var bagCount = run.bag.Count;
            if (bagCount > 0)
            {
                var bagShotsFired = Infinite ? fired : Math.Min(fired, turnBalls.Count);
                run.nextBagIndex = (turnStart + bagShotsFired) % bagCount;
            }

            run.extraBalls = 0;
            fired = 0;
            cooldownRemaining = 0f;
            Infinite = false;
            FillTurnBalls();
        }

        void FillTurnBalls()
        {
            turnBalls.Clear();
            TurnBallsVersion++;
            var bag = run.bag;
            if (bag.Count == 0) return;
            turnStart = run.nextBagIndex % bag.Count;
            var count = Infinite || shotsPerTurn == 0 ? bag.Count : shotsPerTurn;
            for (var i = 0; i < count; i++)
            {
                turnBalls.Add(bag[(turnStart + i) % bag.Count]);
            }
        }
    }
}
