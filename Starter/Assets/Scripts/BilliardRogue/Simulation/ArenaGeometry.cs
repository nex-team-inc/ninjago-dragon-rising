#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// Single owner of sim-space math. Sim space: x right in [0, columns], y up. The launch line y = 0 is the
    /// open bottom exit, the launch zone spans y in [0, launchZoneHeight], and grid row r (0 = top,
    /// rows - 1 = danger row) spans y in [launchZoneHeight + rows - 1 - r, launchZoneHeight + rows - r].
    /// </summary>
    public static class ArenaGeometry
    {
        public static float Width(ArenaRules a) => a.columns;

        /// <summary>y of the reflecting top wall.</summary>
        public static float TopWallY(ArenaRules a) => a.launchZoneHeight + a.rows;

        /// <summary>Bottom y of a grid row (row 0 is the top row).</summary>
        public static float RowBottomY(ArenaRules a, int row) => a.launchZoneHeight + a.rows - 1 - row;

        public static Rect CellRect(ArenaRules a, int col, int row)
        {
            return new Rect(col, RowBottomY(a, row), 1f, 1f);
        }

        /// <summary>Rect of a w×h footprint whose top-left cell is (col, row), shrunk by inset on every side.</summary>
        public static Rect FootprintRect(ArenaRules a, int col, int row, int w, int h, float inset)
        {
            var bottom = RowBottomY(a, row + h - 1);
            return new Rect(col + inset, bottom + inset, w - 2f * inset, h - 2f * inset);
        }

        public static Vector2 CellCenter(ArenaRules a, int col, int row)
        {
            return new Vector2(col + 0.5f, RowBottomY(a, row) + 0.5f);
        }

        public static Vector2 FootprintCenter(ArenaRules a, int col, int row, int w, int h)
        {
            return new Vector2(col + w * 0.5f, RowBottomY(a, row + h - 1) + h * 0.5f);
        }

        /// <summary>The bottom grid row; enemies standing here attack the player.</summary>
        public static int DangerRow(ArenaRules a) => a.rows - 1;

        public static bool IsInsideGrid(ArenaRules a, int col, int row)
        {
            return col >= 0 && col < a.columns && row >= 0 && row < a.rows;
        }

        /// <summary>Grid row containing sim y, or -1 when y lies in the launch zone / above the top wall.</summary>
        public static int RowAtY(ArenaRules a, float y)
        {
            var fromTop = TopWallY(a) - y;
            if (fromTop < 0f || fromTop >= a.rows) return -1;
            return (int)fromTop;
        }

        public static int ColAtX(ArenaRules a, float x)
        {
            if (x < 0f || x >= a.columns) return -1;
            return (int)x;
        }

        /// <summary>Launch origin on the launch line for a normalized cat position (0 = left wall, 1 = right wall).</summary>
        public static Vector2 LaunchOrigin(ArenaRules a, float launchX01) => LaunchOrigin(a, launchX01, 0.5f);

        /// <summary>
        /// Launch origin for a normalized cat position and a small vertical nudge (launchY01: 0.5 = the launch line,
        /// 0/1 = launchYRange cells below/above it, kept inside the launch zone). GDD v2 §21: the ball paw moves the
        /// ball a little on y as well as x.
        /// </summary>
        public static Vector2 LaunchOrigin(ArenaRules a, float launchX01, float launchY01)
        {
            var x = Mathf.Lerp(a.ballRadius, a.columns - a.ballRadius, Mathf.Clamp01(launchX01));
            var y = a.launchY + (Mathf.Clamp01(launchY01) - 0.5f) * 2f * a.launchYRange;
            return new Vector2(x, Mathf.Clamp(y, a.ballRadius, Mathf.Max(a.launchY, a.launchZoneHeight - a.ballRadius)));
        }

        /// <summary>Clamps an aim direction to [minAimAngleDeg, 180 - minAimAngleDeg] measured from +x, pointing up.</summary>
        public static Vector2 ClampAim(ArenaRules a, Vector2 direction)
        {
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle = direction.x >= 0f ? a.minAimAngleDeg : 180f - a.minAimAngleDeg;
            angle = Mathf.Clamp(angle, a.minAimAngleDeg, 180f - a.minAimAngleDeg);
            var rad = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
