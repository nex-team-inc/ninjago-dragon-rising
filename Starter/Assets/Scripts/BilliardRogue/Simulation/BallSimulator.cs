#nullable enable

using System.Collections.Generic;
using UnityEngine;

// Implemented by Simulation module.
namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Fixed-substep 2D ball physics in sim space: elastic reflections off the left/right/top walls, enemy
    /// footprints (inset per rules) and static objects; open bottom exit; per-ball abilities (GDD §5); pickups,
    /// portals, mud, anti-stall. Allocation-free per step; balls are pooled internally.
    /// </summary>
    public sealed class BallSimulator
    {
        public BallSimulator(GameRules rules, BoardOps ops)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>Number of balls (including minis) still in flight.</summary>
        public int ActiveCount => throw new System.NotImplementedException("Simulation module");

        /// <summary>
        /// Fires one ball from origin (on the launch line, see ArenaGeometry.LaunchOrigin) along direction (already
        /// clamped) at ballSpeed × speedMultiplier. powerShot adds balance.powerShotBonusDamage to the first enemy
        /// hit; a Power pickup doubles the first hit and is consumed (PowerShotConsumed). Emits BallLaunched and
        /// increments stats.shots.
        /// </summary>
        public void Launch(RunState run, BallInstance ball, Vector2 origin, Vector2 direction, bool powerShot, int shooterIndex, List<SimEvent> events)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>
        /// Advances every ball by dt using substepsPerSecond internally. Handles wall bounces (BallWallBounce, Rubber
        /// bonus, maxBounces), enemy hits via BoardOps (shield BLOCK from the shielded face, pierce, split, bomb area,
        /// chain lightning, freeze/burn/poison procs, crit, vampire heal cap, combo per ball → ComboChanged), crates,
        /// pickups (PickupCollected → extraBalls / heal / power), portal teleports with re-entry lock, mud slow, the
        /// anti-stall pull after maxFlightSeconds or maxIdleWallBounces, and BallExited when y &lt; 0.
        /// </summary>
        public void Step(RunState run, float dt, List<SimEvent> events)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>Visits every active ball (id, type, level, position, velocity, isMini, radius) for presentation.</summary>
        public void ForEachBall(BallVisitor visitor)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>
        /// Traces the aim guide from origin along dir against walls, enemies and objects without mutating state,
        /// stopping after maxLength sim units or maxBounces reflections. Writes the polyline (origin first) into
        /// pointsOut and returns the number of points written (≤ pointsOut.Length). Must match Step's trajectory.
        /// </summary>
        public int PredictPath(RunState run, Vector2 origin, Vector2 dir, float maxLength, int maxBounces, Vector2[] pointsOut)
        {
            throw new System.NotImplementedException("Simulation module");
        }

        /// <summary>Removes every ball without emitting events (stage transitions, abandon).</summary>
        public void Clear()
        {
            throw new System.NotImplementedException("Simulation module");
        }
    }
}
