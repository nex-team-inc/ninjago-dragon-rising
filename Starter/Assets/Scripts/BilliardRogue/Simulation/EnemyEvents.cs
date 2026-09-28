#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>Enemy-centric event construction shared by BoardOps, BoardDamage and the enemy phase classes.</summary>
    internal static class EnemyEvents
    {
        public static Vector2 Center(ArenaRules a, EnemyState e)
        {
            return ArenaGeometry.FootprintCenter(a, e.col, e.row, e.width, e.height);
        }

        /// <summary>targetId = enemy id, enemyType, position = footprint centre.</summary>
        public static SimEvent Make(ArenaRules a, SimEventKind kind, EnemyState e, int value)
        {
            return new SimEvent { kind = kind, targetId = e.id, enemyType = e.type, position = Center(a, e), value = value };
        }
    }
}
