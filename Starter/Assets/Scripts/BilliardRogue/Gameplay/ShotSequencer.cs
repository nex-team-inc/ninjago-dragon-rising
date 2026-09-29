#nullable enable

using System;
using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Firing order within a turn: every player has BalanceRules.shotsPerTurn shots (0 = the whole bag once, shared, the
    /// v1 rule). They come from the bag in order, starting at run.nextBagIndex and wrapping around, so every ball keeps
    /// its place in the rotation across turns; in 2P the players shoot together and whoever fires takes the next ball.
    /// Then the extra Basic shots granted by +1 Ball pickups (run.extraBalls, shared, reset here at turn end per
    /// HANDOFF §4). Each player has the PacingConfig shot cooldown between their shots. The debug infinite-balls cheat
    /// cycles the bag instead of ending the turn.
    /// </summary>
    public sealed class ShotSequencer
    {
        readonly RunState run;
        readonly int shotsPerTurn;
        readonly float cooldown;
        readonly BallInstance extraBall = new() { type = BallType.Basic, level = 1 };
        readonly List<BallInstance> turnBalls = new();
        readonly int[] firedBy;
        readonly float[] cooldownRemaining;
        int turnStart;
        int bagFired;
        int bonusFired;

        public ShotSequencer(RunState aRun, int aShotsPerTurn, float shotCooldown)
        {
            run = aRun;
            shotsPerTurn = Math.Max(0, aShotsPerTurn);
            cooldown = Math.Max(0f, shotCooldown);
            var players = Math.Max(1, run.numPlayers);
            firedBy = new int[players];
            cooldownRemaining = new float[players];
            FillTurnBalls();
        }

        public bool Infinite { get; private set; }

        public int Players => firedBy.Length;

        /// <summary>Shots fired so far this turn, every player together.</summary>
        public int Fired => bagFired + bonusFired;

        /// <summary>This turn's bag balls in firing order (the bonus shots follow them); the coming turn's between turns.</summary>
        public IReadOnlyList<BallInstance> TurnBalls => turnBalls;

        /// <summary>Changes whenever TurnBalls is refilled, so the HUD knows to redraw the queue.</summary>
        public int TurnBallsVersion { get; private set; }

        /// <summary>Position of the next shot in TurnBalls (TurnBalls.Count.. = bonus shots).</summary>
        public int NextShot => Infinite && turnBalls.Count > 0 ? bagFired % turnBalls.Count
            : bagFired < turnBalls.Count ? bagFired : turnBalls.Count + bonusFired;

        /// <summary>Shots available this turn: the turn's bag balls plus the extra balls collected so far.</summary>
        public int Total => turnBalls.Count + run.extraBalls;

        public int Remaining => Infinite ? Total : Math.Max(0, Total - Fired);

        /// <summary>Some player still has a shot this turn.</summary>
        public bool HasBallToFire
        {
            get
            {
                for (var p = 0; p < firedBy.Length; p++)
                {
                    if (HasShot(p)) return true;
                }

                return false;
            }
        }

        public bool HasShot(int player) => Infinite ? turnBalls.Count > 0 : HasBagShot(player) || bonusFired < run.extraBalls;

        public bool CanFire(int player) => HasShot(player) && cooldownRemaining[player] <= 0f;

        public void BeginTurn(bool infiniteBalls)
        {
            Array.Clear(firedBy, 0, firedBy.Length);
            Array.Clear(cooldownRemaining, 0, cooldownRemaining.Length);
            bagFired = bonusFired = 0;
            Infinite = infiniteBalls;
            FillTurnBalls();
        }

        /// <summary>
        /// Counts the cooldowns down in real time (PlayerTurnLoop passes unscaled time, so hit-stop and slow-mo do not
        /// stretch them; the loop is not ticked while paused).
        /// </summary>
        public void Tick(float unscaledDeltaTime)
        {
            for (var p = 0; p < cooldownRemaining.Length; p++)
            {
                if (cooldownRemaining[p] > 0f) cooldownRemaining[p] -= unscaledDeltaTime;
            }
        }

        /// <summary>Returns the ball player fires next and starts their cooldown: the next bag ball while they have bag shots, then a bonus Basic.</summary>
        public BallInstance Fire(int player)
        {
            cooldownRemaining[player] = cooldown;
            if (Infinite)
            {
                return turnBalls[bagFired++ % turnBalls.Count];
            }

            if (HasBagShot(player))
            {
                firedBy[player]++;
                return turnBalls[bagFired++];
            }

            bonusFired++;
            return extraBall;
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
                run.nextBagIndex = (turnStart + bagFired) % bagCount;
            }

            run.extraBalls = 0;
            Array.Clear(firedBy, 0, firedBy.Length);
            Array.Clear(cooldownRemaining, 0, cooldownRemaining.Length);
            bagFired = bonusFired = 0;
            Infinite = false;
            FillTurnBalls();
        }

        bool HasBagShot(int player) => bagFired < turnBalls.Count && (shotsPerTurn == 0 || firedBy[player] < shotsPerTurn);

        void FillTurnBalls()
        {
            turnBalls.Clear();
            TurnBallsVersion++;
            var bag = run.bag;
            if (bag.Count == 0) return;
            turnStart = run.nextBagIndex % bag.Count;
            var count = Infinite || shotsPerTurn == 0 ? bag.Count : shotsPerTurn * firedBy.Length;
            for (var i = 0; i < count; i++)
            {
                turnBalls.Add(bag[(turnStart + i) % bag.Count]);
            }
        }
    }
}
