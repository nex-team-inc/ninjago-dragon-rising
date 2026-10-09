#nullable enable

using Jazz;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Shared paw reads for the paw inputs (shot input, paw pointer). Allocation-free.</summary>
    public static class PawSampling
    {
        /// <summary>Chest, elbows and wrists (the engine derives each paw from elbow + wrist) detected in this camera frame.</summary>
        public static bool ArePawsDetected(BodyPoseDetection detection, int playerIndex)
        {
            var pose = detection.GetPlayerPose(playerIndex)?.bodyPose;
            if (pose == null) return false;
            return pose.Chest().isDetected && pose.LeftElbow().isDetected && pose.LeftWrist().isDetected
                   && pose.RightElbow().isDetected && pose.RightWrist().isDetected;
        }

        /// <summary>
        /// Left and right paw positions in body-normalized inches relative to the chest (x = screen right = the
        /// player's right, y = up), from the smoothed or the raw engine nodes; false while a node is hidden.
        /// </summary>
        public static bool TrySampleHands(OnePlayerDetectionEngine engine, bool smoothed, out Vector2 leftInches, out Vector2 rightInches)
        {
            leftInches = default;
            rightInches = default;
            var chest = engine.GetNodePosition(PoseNodeIndex.Chest, smoothed);
            var left = engine.GetNodePosition(PoseNodeIndex.LeftHand, smoothed);
            var right = engine.GetNodePosition(PoseNodeIndex.RightHand, smoothed);
            if (chest == null || left == null || right == null || engine.DistancePerInch <= 0f)
            {
                return false;
            }

            var root = engine.transform;
            var perInch = 1f / engine.DistancePerInch;
            leftInches = (Vector2)root.InverseTransformVector(left.Value - chest.Value) * perInch;
            rightInches = (Vector2)root.InverseTransformVector(right.Value - chest.Value) * perInch;
            return true;
        }
    }
}
