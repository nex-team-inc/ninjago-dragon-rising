#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Blends every tracked player's steer into the one shared vehicle target: a weighted average where player 1
    /// carries playerOneShare and player 2 the rest, renormalized over the chests tracked this frame (so a lone or
    /// briefly lost player still drives at full strength). Also accumulates each player's steering effort.
    /// </summary>
    public sealed class SharedSteer
    {
        readonly float[] effort = new float[SimulatedBody.MaxPlayers];
        readonly float[] lastContribution = new float[SimulatedBody.MaxPlayers];

        #region Public API

        /// <param name="steers">Per player index; null when that player is inactive or not tracked this frame.</param>
        public Vector2 Blend(Vector2?[] steers, float playerOneShare, float deltaTime)
        {
            var totalWeight = 0f;
            for (var playerIndex = 0; playerIndex < steers.Length; playerIndex++)
            {
                if (steers[playerIndex].HasValue) totalWeight += Weight(playerIndex, playerOneShare);
            }

            var blended = Vector2.zero;
            for (var playerIndex = 0; playerIndex < steers.Length; playerIndex++)
            {
                lastContribution[playerIndex] = 0f;
                var steer = steers[playerIndex];
                if (!steer.HasValue || totalWeight <= 0f) continue;
                var weight = Weight(playerIndex, playerOneShare) / totalWeight;
                blended += steer.Value * weight;
                lastContribution[playerIndex] = weight * steer.Value.magnitude;
                effort[playerIndex] += lastContribution[playerIndex] * deltaTime;
            }

            return blended;
        }

        /// <summary>Share (0..1) of all steering effort so far that came from this player.</summary>
        public float EffortShare(int playerIndex)
        {
            var total = 0f;
            foreach (var value in effort) total += value;
            return total <= 0f ? 0f : effort[playerIndex] / total;
        }

        /// <summary>The player who steered harder on the last Blend, or -1 when nobody steered.</summary>
        public int DominantPlayer
        {
            get
            {
                var best = -1;
                var bestValue = 0f;
                for (var playerIndex = 0; playerIndex < lastContribution.Length; playerIndex++)
                {
                    if (lastContribution[playerIndex] <= bestValue) continue;
                    best = playerIndex;
                    bestValue = lastContribution[playerIndex];
                }

                return best;
            }
        }

        #endregion

        #region Helpers

        // The share is clamped away from 0 and 1 so a tracked player is never ignored entirely.
        static float Weight(int playerIndex, float playerOneShare)
        {
            var share = Mathf.Clamp(playerOneShare, 0.001f, 0.999f);
            return playerIndex == 0 ? share : 1f - share;
        }

        #endregion
    }
}
