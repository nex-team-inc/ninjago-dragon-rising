#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Dual-paw hold logic of the reward motion pick (GDD v2 §4), free of Unity objects so EditMode tests can drive it.
    /// Each frame: the balls' centres/radii and the two paw positions (same space); a paw hovers the nearest ball
    /// within its radius; while both paws hover the same ball its fill grows to 1 in holdSeconds and picks it; every
    /// other fill drains (drainRate x the fill speed).
    /// </summary>
    public sealed class RewardPawPicker
    {
        public const int MaxBalls = 3;

        readonly Vector2[] centers = new Vector2[MaxBalls];
        readonly float[] radii = new float[MaxBalls];
        readonly float[] fills = new float[MaxBalls];
        int count;

        /// <summary>Ball under the left / right paw, -1 for none.</summary>
        public int LeftHover { get; private set; } = -1;
        public int RightHover { get; private set; } = -1;

        /// <summary>Ball both paws are on, -1 for none.</summary>
        public int BothHover { get; private set; } = -1;

        /// <summary>Emphasised ball: both paws, else any paw (left first), -1 for none.</summary>
        public int Hovered => BothHover >= 0 ? BothHover : LeftHover >= 0 ? LeftHover : RightHover;

        public float Fill(int index) => index >= 0 && index < count ? fills[index] : 0f;

        public void Reset(int ballCount)
        {
            count = Mathf.Clamp(ballCount, 0, MaxBalls);
            for (var i = 0; i < MaxBalls; i++)
            {
                fills[i] = 0f;
            }

            LeftHover = RightHover = BothHover = -1;
        }

        public void SetBall(int index, Vector2 center, float radius)
        {
            if (index < 0 || index >= count) return;
            centers[index] = center;
            radii[index] = radius;
        }

        /// <summary>
        /// Advances one frame. tracked false = no paws (every fill drains). Returns the picked ball index (its fill
        /// reached 1 this step) or -1.
        /// </summary>
        public int Step(bool tracked, Vector2 left, Vector2 right, float dt, float holdSeconds, float drainRate)
        {
            LeftHover = tracked ? HitTest(left) : -1;
            RightHover = tracked ? HitTest(right) : -1;
            BothHover = LeftHover >= 0 && LeftHover == RightHover ? LeftHover : -1;
            var rate = dt / Mathf.Max(0.05f, holdSeconds);
            var picked = -1;
            for (var i = 0; i < count; i++)
            {
                if (i == BothHover)
                {
                    fills[i] = Mathf.Min(1f, fills[i] + rate);
                    if (fills[i] >= 1f) picked = i;
                }
                else
                {
                    fills[i] = Mathf.Max(0f, fills[i] - rate * drainRate);
                }
            }

            return picked;
        }

        int HitTest(Vector2 paw)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var distance = (paw - centers[i]).sqrMagnitude;
                if (distance > radii[i] * radii[i] || distance >= bestDistance) continue;
                best = i;
                bestDistance = distance;
            }

            return best;
        }
    }
}
