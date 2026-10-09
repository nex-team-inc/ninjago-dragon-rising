#nullable enable

using Jazz;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One tracked player: reads that player's DetectionManager engine nodes and holds the body origin stored at
    /// setup. Lean and hand values are body inches (the engine's ppi), so player height and distance cancel out.
    /// </summary>
    public sealed class PlayerBody
    {
        // Same hand point as BaseOnePlayerDetectionEngine: past the wrist along the forearm.
        const float elbowToWristHandRatio = 1.35f;

        readonly OnePlayerDetectionEngine engine;

        public int PlayerIndex { get; }
        public Color Color { get; }
        /// <summary>Calibrated chest position in aspect-normalized frame space (x right, y up).</summary>
        public Vector2 Origin { get; private set; }
        /// <summary>Hip-to-knee drop of each leg (x left, y right) in body inches while standing at setup; null when the knees were hidden.</summary>
        public Vector2? RestingKneeDrops { get; private set; }
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

        public void SetOrigin(Vector2 chest, Vector2? restingKneeDrops)
        {
            Origin = chest;
            RestingKneeDrops = restingKneeDrops;
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

        /// <summary>Smoothed body hand node in aspect-normalized space; false while its elbow or wrist is hidden.</summary>
        public bool TryGetHand(bool leftHand, out Vector2 hand)
        {
            hand = default;
            var pose = Pose;
            if (pose == null) return false;
            var elbow = leftHand ? pose.LeftElbow() : pose.RightElbow();
            var wrist = leftHand ? pose.LeftWrist() : pose.RightWrist();
            if (!elbow.isDetected || !wrist.isDetected) return false;
            hand = Vector2.LerpUnclamped(elbow.ToVector2(), wrist.ToVector2(), elbowToWristHandRatio);
            return true;
        }

        /// <summary>Vertical hip-to-knee drop of each leg (x left, y right) in body inches; a lifted knee shrinks it.</summary>
        public bool TryGetKneeDrops(out Vector2 drops)
        {
            drops = default;
            var pose = Pose;
            if (pose == null) return false;
            var leftHip = pose.LeftHip();
            var leftKnee = pose.LeftKnee();
            var rightHip = pose.RightHip();
            var rightKnee = pose.RightKnee();
            if (!leftHip.isDetected || !leftKnee.isDetected || !rightHip.isDetected || !rightKnee.isDetected) return false;
            drops = new Vector2(leftHip.ToVector2().y - leftKnee.ToVector2().y, rightHip.ToVector2().y - rightKnee.ToVector2().y) / engine.RawPpi;
            return true;
        }

        /// <summary>Camera timestamp (seconds) of the body detection behind the current node values.</summary>
        public double BodyFrameTime => engine.LastBodyPoseDetectionResult.original?.frameTime ?? 0d;

        BodyPose? Pose => engine.RawPpi > 0f ? engine.LastBodyPoseDetectionResult.processed?.GetPlayerPose(PlayerIndex)?.bodyPose : null;

        #endregion
    }
}
