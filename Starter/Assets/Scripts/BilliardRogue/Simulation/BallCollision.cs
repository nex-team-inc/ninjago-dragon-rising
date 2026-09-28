#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    public enum BallContactKind
    {
        None = 0,
        Enemy = 1,
        Pillar = 2,
        Crate = 3,
    }

    /// <summary>The deepest solid a ball is moving into during one substep.</summary>
    public struct BallContact
    {
        public BallContactKind kind;
        public EnemyState? enemy;
        public FieldObjectState? fieldObject;
        /// <summary>Unit normal pointing from the solid toward the ball centre.</summary>
        public Vector2 normal;
        public float depth;
    }

    /// <summary>
    /// Collision math shared by BallSimulator.Step and BallSimulator.PredictPath so the aim guide traces exactly the
    /// geometry real balls bounce off.
    /// </summary>
    public static class BallCollision
    {
        #region Public Methods

        /// <summary>Circle vs axis-aligned rect overlap; the normal points from the rect toward the circle centre.</summary>
        public static bool CircleRect(Rect rect, Vector2 center, float radius, out Vector2 normal, out float depth)
        {
            var closestX = Mathf.Clamp(center.x, rect.xMin, rect.xMax);
            var closestY = Mathf.Clamp(center.y, rect.yMin, rect.yMax);
            var dx = center.x - closestX;
            var dy = center.y - closestY;
            var distanceSq = dx * dx + dy * dy;
            if (distanceSq > radius * radius)
            {
                normal = Vector2.zero;
                depth = 0f;
                return false;
            }
            if (distanceSq > 1e-10f)
            {
                var distance = Mathf.Sqrt(distanceSq);
                normal = new Vector2(dx / distance, dy / distance);
                depth = radius - distance;
                return true;
            }
            PushOutOfInside(rect, center, radius, out normal, out depth);
            return true;
        }

        /// <summary>Reflects off the left, right and top walls; the bottom stays open.</summary>
        public static bool CollideWalls(ArenaRules a, ref Vector2 position, ref Vector2 direction, float radius)
        {
            var bounced = false;
            if (position.x < radius && direction.x < 0f)
            {
                position.x = radius;
                direction.x = -direction.x;
                bounced = true;
            }
            var right = a.columns - radius;
            if (position.x > right && direction.x > 0f)
            {
                position.x = right;
                direction.x = -direction.x;
                bounced = true;
            }
            var top = ArenaGeometry.TopWallY(a) - radius;
            if (position.y > top && direction.y > 0f)
            {
                position.y = top;
                direction.y = -direction.y;
                bounced = true;
            }
            return bounced;
        }

        public static Vector2 Reflect(Vector2 direction, Vector2 normal)
        {
            var d = Vector2.Dot(direction, normal);
            return d < 0f ? direction - 2f * d * normal : direction;
        }

        /// <summary>Face of the solid the ball touched, from the contact normal (dominant axis wins on corners).</summary>
        public static Face FaceFromNormal(Vector2 normal)
        {
            if (Mathf.Abs(normal.y) >= Mathf.Abs(normal.x)) return normal.y < 0f ? Face.Bottom : Face.Top;
            return normal.x < 0f ? Face.Left : Face.Right;
        }

        public static Rect EnemyRect(ArenaRules a, EnemyState e)
        {
            var inset = e.width > 1 || e.height > 1 ? a.bossInset : a.enemyInset;
            return ArenaGeometry.FootprintRect(a, e.col, e.row, e.width, e.height, inset);
        }

        /// <summary>
        /// Finds the deepest enemy, pillar or crate the ball overlaps while moving into it. Enemies whose id is in
        /// ignoreIds[0..ignoreCount) (already pierced) are skipped.
        /// </summary>
        public static bool FindSolidContact(ArenaRules a, BoardState board, Vector2 position, Vector2 direction, float radius,
            int[]? ignoreIds, int ignoreCount, out BallContact contact)
        {
            contact = default;
            var found = false;
            var enemies = board.enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (Contains(ignoreIds, ignoreCount, e.id)) continue;
                if (!CircleRect(EnemyRect(a, e), position, radius, out var normal, out var depth)) continue;
                if (Vector2.Dot(direction, normal) >= 0f) continue;
                if (found && depth <= contact.depth) continue;
                contact = new BallContact { kind = BallContactKind.Enemy, enemy = e, normal = normal, depth = depth };
                found = true;
            }
            var objects = board.fieldObjects;
            for (var i = 0; i < objects.Count; i++)
            {
                var o = objects[i];
                if (o.type != FieldObjectType.Pillar && o.type != FieldObjectType.Crate) continue;
                if (!CircleRect(ArenaGeometry.CellRect(a, o.col, o.row), position, radius, out var normal, out var depth)) continue;
                if (Vector2.Dot(direction, normal) >= 0f) continue;
                if (found && depth <= contact.depth) continue;
                var kind = o.type == FieldObjectType.Pillar ? BallContactKind.Pillar : BallContactKind.Crate;
                contact = new BallContact { kind = kind, fieldObject = o, normal = normal, depth = depth };
                found = true;
            }
            return found;
        }

        /// <summary>Trigger overlap against a single cell shrunk by inset (pickups, portals, mud).</summary>
        public static bool OverlapsCell(ArenaRules a, int col, int row, float inset, Vector2 position, float radius)
        {
            return CircleRect(ArenaGeometry.FootprintRect(a, col, row, 1, 1, inset), position, radius, out _, out _);
        }

        /// <summary>
        /// Deterministic 0..1 roll for crit/freeze procs keyed by (seed, shot, hit, salt): no RNG state to save and
        /// no allocation, and a continued run replays the same procs for the same shots.
        /// </summary>
        public static float Hash01(int seed, int shot, int hit, int salt)
        {
            unchecked
            {
                var h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)shot * 0x85EBCA77u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)hit * 0xC2B2AE3Du;
                h = (h << 11) | (h >> 21);
                h ^= (uint)salt * 0x27D4EB2Fu;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h >> 8) * (1f / 16777216f);
            }
        }

        #endregion

        #region Helpers

        static void PushOutOfInside(Rect rect, Vector2 center, float radius, out Vector2 normal, out float depth)
        {
            var left = center.x - rect.xMin;
            var right = rect.xMax - center.x;
            var bottom = center.y - rect.yMin;
            var top = rect.yMax - center.y;
            normal = Vector2.left;
            depth = left;
            if (right < depth)
            {
                normal = Vector2.right;
                depth = right;
            }
            if (bottom < depth)
            {
                normal = Vector2.down;
                depth = bottom;
            }
            if (top < depth)
            {
                normal = Vector2.up;
                depth = top;
            }
            depth += radius;
        }

        static bool Contains(int[]? ids, int count, int id)
        {
            if (ids == null) return false;
            for (var i = 0; i < count; i++)
            {
                if (ids[i] == id) return true;
            }
            return false;
        }

        #endregion
    }
}
