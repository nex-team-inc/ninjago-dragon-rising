#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Fixed-size ring buffer of timestamped aim directions, so a strike can use the aim from shortly before it
    /// started (the thrust itself wobbles the paw vector). Times must be added in increasing order. Allocation-free
    /// after construction.
    /// </summary>
    public sealed class AimHistory
    {
        readonly double[] times;
        readonly Vector2[] aims;
        int newest = -1;
        int count;

        public AimHistory(int capacity)
        {
            times = new double[capacity];
            aims = new Vector2[capacity];
        }

        public int Count => count;

        public void Add(double time, Vector2 aim)
        {
            newest = (newest + 1) % times.Length;
            times[newest] = time;
            aims[newest] = aim;
            if (count < times.Length) count++;
        }

        /// <summary>
        /// The newest aim recorded at or before time. When every sample is newer, the oldest one is returned (the
        /// history is shorter than the look-back). False only when the history is empty.
        /// </summary>
        public bool TryGetAt(double time, out Vector2 aim)
        {
            aim = default;
            if (count == 0) return false;

            var index = newest;
            for (var i = 0; i < count; i++)
            {
                aim = aims[index];
                if (times[index] <= time) return true;
                index = index == 0 ? times.Length - 1 : index - 1;
            }

            return true;
        }

        public void Clear()
        {
            newest = -1;
            count = 0;
        }
    }
}
