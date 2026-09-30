#nullable enable

using System;
using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Shots within a turn (playtest 4): every player has BalanceRules.shotsPerTurn shots (at least 1) and in 2P they
    /// shoot together. A shot is a volley of the whole bag in order (PlayerTurnLoop streams it), so every ball the
    /// player wins adds to every shot. +1 Ball pickups add shared bonus shots (run.extraBalls, reset here at turn end
    /// per HANDOFF §4). Each player waits the PacingConfig shot cooldown after they shoot. The debug infinite-balls cheat
    /// never runs out of shots.
    /// </summary>
    public sealed class ShotSequencer
    {
        readonly RunState run;
        readonly int shotsPerTurn;
        readonly float cooldown;
        readonly int[] firedBy;
        readonly float[] cooldownRemaining;
        int bonusFired;

        public ShotSequencer(RunState aRun, int aShotsPerTurn, float shotCooldown)
        {
            run = aRun;
            shotsPerTurn = Math.Max(1, aShotsPerTurn);
            cooldown = Math.Max(0f, shotCooldown);
            var players = Math.Max(1, run.numPlayers);
            firedBy = new int[players];
            cooldownRemaining = new float[players];
        }

        public bool Infinite { get; private set; }

        public int Players => firedBy.Length;

        /// <summary>Shots taken so far this turn, every player together.</summary>
        public int Fired { get; private set; }

        /// <summary>What every shot launches: the whole bag, in order.</summary>
        public IReadOnlyList<BallInstance> Volley => run.bag;

        /// <summary>Shots available this turn: every player's own plus the bonus shots collected so far.</summary>
        public int Total => shotsPerTurn * firedBy.Length + run.extraBalls;

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

        public bool HasShot(int player) => Infinite || firedBy[player] < shotsPerTurn || bonusFired < run.extraBalls;

        public bool CanFire(int player) => HasShot(player) && cooldownRemaining[player] <= 0f;

        public void BeginTurn(bool infiniteBalls)
        {
            Clear();
            Infinite = infiniteBalls;
        }

        /// <summary>
        /// Counts the cooldowns down in real time (PlayerTurnLoop passes unscaled time, so slow-mo and fast-forward do not
        /// stretch them; the loop is not ticked while paused).
        /// </summary>
        public void Tick(float unscaledDeltaTime)
        {
            for (var p = 0; p < cooldownRemaining.Length; p++)
            {
                if (cooldownRemaining[p] > 0f) cooldownRemaining[p] -= unscaledDeltaTime;
            }
        }

        /// <summary>Spends player's next shot (their own first, then a shared bonus one) and starts their cooldown.</summary>
        public void Fire(int player)
        {
            cooldownRemaining[player] = cooldown;
            Fired++;
            if (Infinite) return;
            if (firedBy[player] < shotsPerTurn) firedBy[player]++;
            else bonusFired++;
        }

        /// <summary>Bonus shots never carry over to the next turn.</summary>
        public void EndTurn()
        {
            run.extraBalls = 0;
            Clear();
            Infinite = false;
        }

        void Clear()
        {
            Array.Clear(firedBy, 0, firedBy.Length);
            Array.Clear(cooldownRemaining, 0, cooldownRemaining.Length);
            bonusFired = 0;
            Fired = 0;
        }
    }
}
