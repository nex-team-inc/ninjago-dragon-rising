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
        [Tooltip("World position of the world camera relative to the arena root.")]
        [SerializeField] Vector3 cameraPosition = new(0f, 20.4f, 12.7f);
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
