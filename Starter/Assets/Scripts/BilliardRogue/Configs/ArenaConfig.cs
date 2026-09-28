#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    [CreateAssetMenu(fileName = "ArenaConfig", menuName = "Nex/Billiard Rogue/Arena Config", order = 50)]
    public sealed class ArenaConfig : ScriptableObject
    {
        /// <summary>Emissive pulse of the danger-row inlays (GDD §4: tinted red, pulsing when occupied).</summary>
        [Serializable]
        public sealed class DangerRowSettings
        {
            [Tooltip("HDR emission colour of the inlays at strength 1 (multiplied by the strengths below). Orange-red so it stays red under the cool Act 2 / violet Act 3 grades.")]
            [ColorUsage(false, true)] public Color emissionColor = new(1.6f, 0.26f, 0.06f);
            [Tooltip("Steady emission strength while the danger row is empty.")]
            [Range(0f, 4f)] public float idleStrength = 0.55f;
            [Tooltip("Emission strength at the bottom of a pulse at full danger level.")]
            [Range(0f, 4f)] public float pulseMinStrength = 0.8f;
            [Tooltip("Emission strength at the top of a pulse at full danger level.")]
            [Range(0f, 6f)] public float pulseMaxStrength = 2f;
            [Tooltip("Pulses per second at full danger level.")]
            [Range(0.1f, 6f)] public float pulseHz = 1.8f;
            [Tooltip("Pulses per second at the lowest non-zero danger level.")]
            [Range(0.1f, 6f)] public float calmPulseHz = 0.9f;
            [Tooltip("How fast the displayed level follows SetDangerLevel (1/s, exponential).")]
            [Range(0.5f, 30f)] public float levelResponse = 8f;
        }

        [Header("Rules")]
        [SerializeField] ArenaRules rules = new();

        [Header("World")]
        [Tooltip("World units per sim cell. Models are authored at 1 unit = 1 cell.")]
        [SerializeField, Range(0.25f, 4f)] float worldScale = 1f;
        [Tooltip("Model for one grid cell (EnvironmentBuilder fills Env_FloorTile.fbx when empty).")]
        [SerializeField] GameObject? floorTilePrefab;
        [Tooltip("Model for one danger-row cell with an emissive inlay part named DangerInlay_Emissive (Env_FloorTile_Danger.fbx).")]
        [SerializeField] GameObject? dangerTilePrefab;
        [Tooltip("1 m rim wall segment, inner face along local +Z (Env_WallSegment.fbx).")]
        [SerializeField] GameObject? wallPrefab;
        [Tooltip("Corner / end post (Env_WallCorner.fbx).")]
        [SerializeField] GameObject? cornerPostPrefab;
        [Tooltip("Launch-zone platform, authored 7 × 1.6 m (Env_LaunchPad.fbx); scaled to columns × launchZoneHeight.")]
        [SerializeField] GameObject? launchPadPrefab;

        [Header("Danger row")]
        [SerializeField] DangerRowSettings dangerRow = new();

        [Header("Camera")]
        [Tooltip("World camera position relative to the arena root (the launch-line centre, TDD §14.1). The camera sits south of the arena (negative z), yaw 0, looking +z; the default aims at the arena centre (z = (launchZoneHeight + rows) / 2 = 5.8) at the default pitch, so the launch pad and the cat stay in frame (the grid spans screen height 0.17-0.86).")]
        [SerializeField] Vector3 cameraPosition = new(0f, 20.4f, -6.947f);
        [Tooltip("Downward pitch of the world camera; its yaw is always 0 (looking +z, north).")]
        [SerializeField, Range(10f, 89f)] float cameraPitchDeg = 58f;
        [SerializeField, Range(10f, 60f)] float cameraFov = 28f;

        public ArenaRules Rules => rules;
        public float WorldScale => worldScale;
        public GameObject? FloorTilePrefab => floorTilePrefab;
        public GameObject? DangerTilePrefab => dangerTilePrefab;
        public GameObject? WallPrefab => wallPrefab;
        public GameObject? CornerPostPrefab => cornerPostPrefab;
        public GameObject? LaunchPadPrefab => launchPadPrefab;
        public DangerRowSettings DangerRow => dangerRow;
        public Vector3 CameraPosition => cameraPosition;
        public float CameraPitchDeg => cameraPitchDeg;
        public float CameraFov => cameraFov;
    }
}
