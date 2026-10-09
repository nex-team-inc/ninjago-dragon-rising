#nullable enable

using Jazz;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One tracked player: reads that player's DetectionManager engine nodes and holds the chest origin stored at
    /// setup. Lean and hand values are body inches (the engine's ppi), so player height and distance cancel out.
    /// </summary>
    public sealed class PlayerBody
    {
        readonly OnePlayerDetectionEngine engine;

        public int PlayerIndex { get; }
        public Color Color { get; }
        /// <summary>Calibrated chest position in aspect-normalized frame space (x right, y up).</summary>
        public Vector2 Origin { get; private set; }
        public bool IsCalibrated { get; private set; }
        /// <summary>Frame heights per inch for this player, averaged by the engine; 0 before the first detection.</summary>
        public float RawPpi => engine.RawPpi;

        public PlayerBody(int playerIndex, Color color, OnePlayerDetectionEngine engine)
        {
            PlayerIndex = playerIndex;
            Color = color;
            this.engine = engine;
        }

        #region Calibration

        public void SetOrigin(Vector2 chest)
        {
            Origin = chest;
            IsCalibrated = true;
        }

        public void ClearOrigin()
        {
            IsCalibrated = false;
        }

        #endregion

        #region Nodes

        /// <summary>Smoothed chest in aspect-normalized space; false while this player's chest is not tracked.</summary>
        public bool TryGetChest(out Vector2 chest)
        {
            chest = default;
            if (engine.RawPpi <= 0f) return false;
            var pose = engine.LastBodyPoseDetectionResult.processed?.GetPlayerPose(PlayerIndex)?.bodyPose;
            if (pose == null) return false;
            var node = pose.Chest();
            if (!node.isDetected) return false;
            chest = node.ToVector2();
            return true;
        }

        /// <summary>Chest offset from the calibrated origin in body inches (x right = the player's right, y up).</summary>
        public bool TryGetLeanInches(out Vector2 lean)
        {
            if (SimulatedBody.IsEnabled)
            {
                lean = SimulatedBody.GetLeanInches(PlayerIndex);
                return true;
            }

            lean = default;
            if (!IsCalibrated || !TryGetChest(out var chest)) return false;
            lean = (chest - Origin) / engine.RawPpi;
            return true;
        }

        /// <summary>Body hand node relative to the chest, in body inches; false while either node is hidden.</summary>
        public bool TryGetHandInches(bool leftHand, out Vector2 inches)
        {
            inches = default;
            if (engine.RawPpi <= 0f) return false;
            var hand = engine.GetNodePosition(leftHand ? PoseNodeIndex.LeftHand : PoseNodeIndex.RightHand, true);
            var chest = engine.GetNodePosition(PoseNodeIndex.Chest, true);
            if (hand == null || chest == null) return false;
            inches = (Vector2)(hand.Value - chest.Value) / engine.DistancePerInch;
            return true;
        }

        /// <summary>Camera timestamp (seconds) of the body detection behind the current node values.</summary>
        public double BodyFrameTime => engine.LastBodyPoseDetectionResult.original?.frameTime ?? 0d;

        #endregion
    }
}
