#nullable enable

using System;
using Nex.Util;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Chest Chase tuning. Setters exist for the Debug Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "ChaseConfig", menuName = "Nex/Ninjago/Chase Config")]
    public class ChaseConfig : ScriptableObject
    {
        [Serializable]
        public class VehicleSettings
        {
            [Tooltip("Seconds this segment lasts, obstacles hit or not.")]
            public float segmentSeconds = 20f;
            [Tooltip("Cruise speed in world units per second.")]
            public float speed = 14f;
            [Tooltip("The car ignores vertical lean on purpose; the skycraft uses it on purpose.")]
            public bool usesVerticalLean;
            [Tooltip("Lateral travel from the center line at a full lean (world units).")]
            public float halfWidth = 2.2f;
            [Tooltip("Height of the vehicle when the chest is at its calibrated height.")]
            public float centerHeight = 0.35f;
            [Tooltip("Vertical travel above and below the center height at a full rise or drop (skycraft only).")]
            public float halfHeight;
            [Tooltip("Seconds the vehicle takes to settle on the steer target; higher feels heavier.")]
            public float steerSmoothingSeconds = 0.35f;
            [Tooltip("Cap on how fast the vehicle can move across the lane (world units per second).")]
            public float maxSteerSpeed = 7f;
            [Tooltip("Seconds between two obstacles reaching the vehicle.")]
            public float obstacleSpacingSeconds = 1.7f;
            [Tooltip("Seconds an obstacle is visible before it reaches the vehicle.")]
            public float obstacleLeadSeconds = 1.5f;
            [Tooltip("Seconds without obstacles at the start of the segment.")]
            public float openingSeconds = 2f;
            [Tooltip("Width of one lane or column of obstacle cells (world units).")]
            public float cellWidth = 2f;
            [Tooltip("Height of one row of obstacle cells (skycraft only).")]
            public float cellHeight = 1.6f;
            [Tooltip("Vehicle half extents used for the hit test (x = half width, y = half height).")]
            public Vector2 vehicleHalfExtents = new(0.7f, 0.3f);
        }

        [Header("Vehicles")]
        [SerializeField] EnumDictionary<VehicleType, VehicleSettings> vehicles = new();

        [Header("Steering")]
        [Tooltip("Chest movement from the calibrated origin (body inches) ignored so breathing does not swerve.")]
        [SerializeField, Range(0f, 6f)] float deadzoneInches = 1.5f;
        [Tooltip("Sideways chest lean (body inches) that steers to the edge of the lane.")]
        [SerializeField, Range(2f, 20f)] float fullLeanInches = 7f;
        [Tooltip("Chest rise or drop (body inches) that climbs or dives to the edge of the sky lane.")]
        [SerializeField, Range(1f, 20f)] float fullRiseInches = 4f;

        [Header("Two Players")]
        [Tooltip("Share of the steer that comes from player 1 when both chests are tracked; player 2 gets the rest. 0.5 = equal.")]
        [SerializeField, Range(0f, 1f)] float playerOneSteerShare = 0.5f;

        [Header("Bump")]
        [Tooltip("Fraction of speed lost when the vehicle hits an obstacle.")]
        [SerializeField, Range(0f, 0.9f)] float bumpSpeedDrop = 0.35f;
        [Tooltip("Seconds to get back to cruise speed after a bump.")]
        [SerializeField, Range(0.1f, 4f)] float bumpRecoverSeconds = 1.2f;

        [Header("Pacing")]
        [Tooltip("Seconds of 'Lean to steer' before the first segment.")]
        [SerializeField, Range(0f, 6f)] float introSeconds = 2f;
        [Tooltip("Seconds between the car segment and the skycraft segment.")]
        [SerializeField, Range(0f, 4f)] float transitionSeconds = 1.2f;
        [Tooltip("Seconds the 'Use your whole chest' line stays up when the skycraft appears.")]
        [SerializeField, Range(0.5f, 6f)] float wholeChestHintSeconds = 3f;

        #region Public API

        public VehicleSettings GetVehicle(VehicleType type) => vehicles[type];
        public float DeadzoneInches { get => deadzoneInches; set => deadzoneInches = Mathf.Max(0f, value); }
        public float FullLeanInches { get => fullLeanInches; set => fullLeanInches = value; }
        public float FullRiseInches { get => fullRiseInches; set => fullRiseInches = value; }
        public float PlayerOneSteerShare { get => playerOneSteerShare; set => playerOneSteerShare = Mathf.Clamp01(value); }
        public float BumpSpeedDrop { get => bumpSpeedDrop; set => bumpSpeedDrop = Mathf.Clamp(value, 0f, 0.9f); }
        public float BumpRecoverSeconds => bumpRecoverSeconds;
        public float IntroSeconds => introSeconds;
        public float TransitionSeconds => transitionSeconds;
        public float WholeChestHintSeconds => wholeChestHintSeconds;

        #endregion
    }
}
