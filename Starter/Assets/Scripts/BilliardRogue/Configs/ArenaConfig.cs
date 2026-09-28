#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    [CreateAssetMenu(fileName = "ArenaConfig", menuName = "Nex/Billiard Rogue/Arena Config", order = 50)]
    public sealed class ArenaConfig : ScriptableObject
    {
        [Header("Rules")]
        [SerializeField] ArenaRules rules = new();

        [Header("World")]
        [Tooltip("World units per sim cell. Models are authored at 1 unit = 1 cell.")]
        [SerializeField, Range(0.25f, 4f)] float worldScale = 1f;
        [SerializeField] GameObject? floorTilePrefab;
        [SerializeField] GameObject? dangerTilePrefab;
        [SerializeField] GameObject? wallPrefab;
        [SerializeField] GameObject? cornerPostPrefab;

        [Header("Camera")]
        [Tooltip("World camera position relative to the arena root (the launch-line centre, TDD §14.1). The camera sits south of the arena (negative z), yaw 0, looking +z; the default aims at the grid centre (z = launchZoneHeight + rows / 2) at the default pitch.")]
        [SerializeField] Vector3 cameraPosition = new(0f, 20.4f, -6.15f);
        [Tooltip("Downward pitch of the world camera; its yaw is always 0 (looking +z, north).")]
        [SerializeField, Range(10f, 89f)] float cameraPitchDeg = 58f;
        [SerializeField, Range(10f, 60f)] float cameraFov = 28f;

        public ArenaRules Rules => rules;
        public float WorldScale => worldScale;
        public GameObject? FloorTilePrefab => floorTilePrefab;
        public GameObject? DangerTilePrefab => dangerTilePrefab;
        public GameObject? WallPrefab => wallPrefab;
        public GameObject? CornerPostPrefab => cornerPostPrefab;
        public Vector3 CameraPosition => cameraPosition;
        public float CameraPitchDeg => cameraPitchDeg;
        public float CameraFov => cameraFov;
    }
}
