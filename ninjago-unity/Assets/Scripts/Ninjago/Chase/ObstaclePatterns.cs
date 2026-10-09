#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Obstacle layouts as 9-bit masks over a 3x3 grid: bit = row * 3 + column, column 0 = left, row 0 = low.
    /// The car only has the low row. Every layout leaves at least one full cell open, and a shuffled bag makes
    /// sure left, center and right (and high, middle and low) all come up.
    /// </summary>
    public sealed class ObstaclePatterns
    {
        public const int Columns = 3;
        public const int Rows = 3;

        static readonly int[] carLayouts =
        {
            0b110, // open left
            0b101, // open center
            0b011, // open right
        };

        static readonly int[] skyLayouts =
        {
            0b000_111_111, // low and middle closed: climb
            0b111_111_000, // high and middle closed: dive
            0b011_011_011, // left and center closed: go right
            0b110_110_110, // right and center closed: go left
            0b111_101_111, // ring: hold the middle
            0b001_001_111, // low row and left column closed
            0b111_100_100, // high row and right column closed
        };

        readonly int[] layouts;
        readonly int[] bag;
        int bagIndex;
        int lastLayout = -1;

        public ObstaclePatterns(VehicleType vehicle)
        {
            layouts = vehicle == VehicleType.Car ? carLayouts : skyLayouts;
            bag = new int[layouts.Length];
            bagIndex = bag.Length;
        }

        public static bool IsBlocked(int mask, int column, int row) => (mask & (1 << (row * Columns + column))) != 0;

        public int Next()
        {
            if (bagIndex >= bag.Length) Refill();
            var layout = bag[bagIndex++];
            lastLayout = layout;
            return layout;
        }

        void Refill()
        {
            for (var i = 0; i < bag.Length; i++) bag[i] = layouts[i];
            for (var i = bag.Length - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            // No layout twice in a row across bag refills.
            if (bag.Length > 1 && bag[0] == lastLayout) (bag[0], bag[1]) = (bag[1], bag[0]);
            bagIndex = 0;
        }
    }
}
