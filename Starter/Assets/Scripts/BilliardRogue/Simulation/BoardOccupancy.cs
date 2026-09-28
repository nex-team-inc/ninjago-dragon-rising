#nullable enable

using System.Collections.Generic;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>Footprint occupancy queries over a BoardState (grid bounds from ArenaRules); never mutates.</summary>
    internal static class BoardOccupancy
    {
        public static bool IsFootprintFree(ArenaRules a, BoardState b, int col, int row, int width, int height, EnemyState? ignore)
        {
            if (col < 0 || row < 0 || col + width > a.columns || row + height > a.rows)
            {
                return false;
            }
            foreach (var e in b.enemies)
            {
                if (e == ignore) continue;
                if (Overlaps(e.col, e.row, e.width, e.height, col, row, width, height))
                {
                    return false;
                }
            }
            foreach (var o in b.fieldObjects)
            {
                if (Overlaps(o.col, o.row, 1, 1, col, row, width, height))
                {
                    return false;
                }
            }
            foreach (var p in b.pickups)
            {
                if (Overlaps(p.col, p.row, 1, 1, col, row, width, height))
                {
                    return false;
                }
            }
            return true;
        }

        public static EnemyState? EnemyAt(BoardState b, int col, int row)
        {
            foreach (var e in b.enemies)
            {
                if (Overlaps(e.col, e.row, e.width, e.height, col, row, 1, 1))
                {
                    return e;
                }
            }
            return null;
        }

        /// <summary>Living or dying enemies whose footprint touches the 8-neighbourhood ring of center (center excluded).</summary>
        public static void CollectNeighbours(BoardState b, EnemyState center, List<EnemyState> output)
        {
            output.Clear();
            foreach (var n in b.enemies)
            {
                if (n == center) continue;
                if (Overlaps(n.col, n.row, n.width, n.height, center.col - 1, center.row - 1, center.width + 2, center.height + 2))
                {
                    output.Add(n);
                }
            }
        }

        public static bool Overlaps(int c0, int r0, int w0, int h0, int c1, int r1, int w1, int h1)
        {
            return c0 < c1 + w1 && c1 < c0 + w0 && r0 < r1 + h1 && r1 < r0 + h0;
        }
    }
}
