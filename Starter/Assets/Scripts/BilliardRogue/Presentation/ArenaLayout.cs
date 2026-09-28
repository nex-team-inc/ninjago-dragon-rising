#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Single owner of the sim → world mapping (TDD §14.1). Sim x maps to local +x, sim y (up the arena, away from
    /// the player) maps to local +z and height to local +y, so the camera sits south (low z) looking north and
    /// models authored facing +z face away from the player. The local origin is the centre of the launch line
    /// (sim (columns/2, 0)): the arena spans x ∈ [-columns/2, columns/2] and z ∈ [0, launchZoneHeight + rows],
    /// which is where the environment layouts place it. One sim cell is worldScale world units.
    /// </summary>
    public sealed class ArenaLayout : MonoBehaviour
    {
        ArenaRules rules = new();
        float worldScale = 1f;
        float halfColumns;

        public ArenaRules Rules => rules;
        public float WorldScale => worldScale;
        /// <summary>World size of one sim cell.</summary>
        public float CellSize => worldScale;
        /// <summary>Sim-space size of the whole arena (columns × (launchZoneHeight + rows)).</summary>
        public Vector2 SimSize => new(rules.columns, rules.launchZoneHeight + rules.rows);

        public void Initialize(ArenaRules arenaRules, float scale)
        {
            rules = arenaRules;
            worldScale = scale;
            halfColumns = rules.columns * 0.5f;
        }

        public Vector3 ToWorld(Vector2 sim, float height = 0f)
        {
            var local = new Vector3((sim.x - halfColumns) * worldScale, height * worldScale, sim.y * worldScale);
            return transform.TransformPoint(local);
        }

        public Vector2 ToSim(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            return new Vector2(local.x / worldScale + halfColumns, local.z / worldScale);
        }

        /// <summary>Height above the arena floor in sim units for a world point.</summary>
        public float HeightOf(Vector3 world) => transform.InverseTransformPoint(world).y / worldScale;

        /// <summary>Sim direction (x right, y up the arena) expressed as a world direction on the floor plane.</summary>
        public Vector3 DirectionToWorld(Vector2 simDirection)
        {
            return transform.TransformDirection(new Vector3(simDirection.x, 0f, simDirection.y));
        }

        public Vector3 CellCenterWorld(int col, int row, float height = 0f)
        {
            return ToWorld(ArenaGeometry.CellCenter(rules, col, row), height);
        }

        public Vector3 FootprintCenterWorld(int col, int row, int w, int h, float height = 0f)
        {
            return ToWorld(ArenaGeometry.FootprintCenter(rules, col, row, w, h), height);
        }

        public Vector3 LaunchOriginWorld(float launchX01, float height = 0f)
        {
            return ToWorld(ArenaGeometry.LaunchOrigin(rules, launchX01), height);
        }

        /// <summary>World centre of the launch line (the local origin).</summary>
        public Vector3 LaunchLineCenterWorld => transform.position;

        /// <summary>World centre of the arena floor (grid + launch zone).</summary>
        public Vector3 CenterWorld => ToWorld(SimSize * 0.5f);

        /// <summary>World centre of the grid only (camera focus point).</summary>
        public Vector3 GridCenterWorld => ToWorld(new Vector2(halfColumns, rules.launchZoneHeight + rules.rows * 0.5f));
    }
}
