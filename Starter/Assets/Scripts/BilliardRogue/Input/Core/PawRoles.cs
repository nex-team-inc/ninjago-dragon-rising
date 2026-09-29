#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Which paw holds the ball (GDD v2 §16): the upper paw is the ball, the lower paw is the cue. A tracking segment
    /// starts with the higher paw on the ball; after that the roles swap only once the cue paw has stayed at least
    /// swapMarginInches above the ball paw for swapSeconds, and never while locked (a strike in progress), so a thrust
    /// that ends above the ball paw does not swap them mid-shot. Positions are chest-relative inches, y = up. Pure;
    /// allocation-free.
    /// </summary>
    public sealed class PawRoles
    {
        bool pending;
        double pendingSince;

        public PawRoles(float aSwapMarginInches, float aSwapSeconds)
        {
            SwapMarginInches = aSwapMarginInches;
            SwapSeconds = aSwapSeconds;
        }

        public float SwapMarginInches { get; set; }
        public float SwapSeconds { get; set; }

        /// <summary>The right paw holds the ball (the left paw is the cue).</summary>
        public bool BallIsRight { get; private set; }

        public Vector2 Ball(Vector2 left, Vector2 right) => BallIsRight ? right : left;

        public Vector2 Cue(Vector2 left, Vector2 right) => BallIsRight ? left : right;

        /// <summary>First sample of a tracking segment: the higher paw takes the ball at once.</summary>
        public void Assign(Vector2 left, Vector2 right)
        {
            BallIsRight = right.y > left.y;
            pending = false;
        }

        /// <summary>One sample at a non-decreasing time; true when the roles swapped.</summary>
        public bool Update(double time, Vector2 left, Vector2 right, bool locked)
        {
            if (locked || Cue(left, right).y - Ball(left, right).y < SwapMarginInches)
            {
                pending = false;
                return false;
            }

            if (!pending)
            {
                pending = true;
                pendingSince = time;
            }

            if (time - pendingSince < SwapSeconds) return false;
            BallIsRight = !BallIsRight;
            pending = false;
            return true;
        }
    }
}
