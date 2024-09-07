using System.Collections.Generic;
using Jazz;
using UnityEngine;
using UnityEngine.Events;

#nullable enable

namespace Nex
{
    public class OnePlayerDetectionEngine : MonoBehaviour
    {
        const float autoHideWaitingTime = 0.5f;
        const float totalHeightInInches = 58;
        const float ElbowWristLerpRatioForHand = 1.3f;

        [Header("Targets")]
        [SerializeField] Transform leftHand = null!;
        [SerializeField] Transform rightHand = null!;
        [SerializeField] Transform chest = null!;
        [SerializeField] Transform nose = null!;
        [SerializeField] Transform leftShoulder = null!;
        [SerializeField] Transform rightShoulder = null!;
        [SerializeField] Transform leftElbow = null!;
        [SerializeField] Transform rightElbow = null!;
        [SerializeField] Transform leftWrist = null!;
        [SerializeField] Transform rightWrist = null!;
        [SerializeField] Transform leftHip = null!;
        [SerializeField] Transform rightHip = null!;
        [SerializeField] Transform leftKnee = null!;
        [SerializeField] Transform rightKnee = null!;

        [Header("Reference Frame")]
        [SerializeField] RectTransform referenceTransform = null!;

        // ReSharper disable once EventNeverSubscribedTo.Global
        public event UnityAction<BodyPoseDetection>? NewDetectionCapturedAndProcessed;

        readonly Dictionary<Transform, float> lastDetectionTimeByTarget = new();
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        int playerIndex;
        bool isSmoothHelperInitialized;
        readonly FloatHistory ppiHistory = new(3);

        // ReSharper disable once UnusedMember.Global
        public float DistancePerInch => referenceTransform.rect.height / totalHeightInInches;

        #region Public Properties

        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;
        public Transform Chest => chest;
        public Transform Nose => nose;
        public Transform LeftShoulder => leftShoulder;
        public Transform RightShoulder => rightShoulder;
        public Transform LeftElbow => leftElbow;
        public Transform RightElbow => rightElbow;
        public Transform LeftWrist => leftWrist;
        public Transform RightWrist => rightWrist;
        public Transform LeftHip => leftHip;
        public Transform RightHip => rightHip;
        public Transform LeftKnee => leftKnee;
        public Transform RightKnee => rightKnee;

        #endregion

        #region Public

        public void Initialize(
            int aPlayerIndex,
            BodyPoseDetectionManager aBodyPoseDetectionManager
        )
        {
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
            playerIndex = aPlayerIndex;

            bodyPoseDetectionManager.processed.captureAspectNormalizedDetection += ProcessedOnCaptureAspectNormalizedDetection;
        }

        void OnDestroy()
        {
            bodyPoseDetectionManager.processed.captureAspectNormalizedDetection -= ProcessedOnCaptureAspectNormalizedDetection;
        }

        #endregion

        #region Event

        void ProcessedOnCaptureAspectNormalizedDetection(BodyPoseDetectionResult detectionResult)
        {
            var detection = detectionResult.processed;
            var playerPose = detection.GetPlayerPose(playerIndex);
            var pose = (BodyPose?)playerPose?.bodyPose.Clone();

            if (pose != null)
            {
                ppiHistory.Add(pose.pixelsPerInch, Time.fixedTime);
                ppiHistory.UpdateCurrentFrameTime(Time.fixedTime);

                var ppi = ppiHistory.Average();

                if (ppi > 0)
                {
                    NormalizePose(pose, ppi);
                }
            }

            UpdateTargetByLerpNode(leftHand, pose?.LeftElbow(), pose?.LeftWrist(), ElbowWristLerpRatioForHand);
            UpdateTargetByLerpNode(rightHand, pose?.RightElbow(), pose?.RightWrist(), ElbowWristLerpRatioForHand);
            UpdateTargetByNode(chest, pose?.Chest());
            UpdateTargetByNode(nose, pose?.Nose());
            UpdateTargetByNode(leftShoulder, pose?.LeftShoulder());
            UpdateTargetByNode(rightShoulder, pose?.RightShoulder());
            UpdateTargetByNode(leftElbow, pose?.LeftElbow());
            UpdateTargetByNode(rightElbow, pose?.RightElbow());
            UpdateTargetByNode(leftWrist, pose?.LeftWrist());
            UpdateTargetByNode(rightWrist, pose?.RightWrist());
            UpdateTargetByNode(leftHip, pose?.LeftHip());
            UpdateTargetByNode(rightHip, pose?.RightHip());
            UpdateTargetByNode(leftKnee, pose?.LeftKnee());
            UpdateTargetByNode(rightKnee, pose?.RightKnee());

            NewDetectionCapturedAndProcessed?.Invoke(detection);
        }

        #endregion

        #region Normalization

        void NormalizePose(BodyPose pose, float ppi)
        {
            // Add more normalization logic if needed.
            var chestPt = pose.Chest().ToVector2();
            var scale = 1 / (ppi * totalHeightInInches);
            ScalePose(pose, scale, chestPt);
            pose.InvalidatePpi();
        }

        void ScalePose(BodyPose pose, float scale, Vector2 pivot)
        {
            for (var i = 0; i < BodyPose.nodeNumber; i++)
            {
                var node = pose.nodes[i];
                var dx = node.x - pivot.x;
                var dy = node.y - pivot.y;
                node.x =  dx * scale + pivot.x;
                node.y = dy * scale + pivot.y;
                pose.nodes[i] = node;
            }
        }

        #endregion

        #region Node

        void UpdateTargetByNode(Transform targetTransform, PoseNode? optNode)
        {
            if (targetTransform == null)
            {
                return;
            }

            var updated = false;
            if (optNode != null)
            {
                var node = (PoseNode)optNode;
                if (node.isDetected)
                {
                    targetTransform.localPosition = DetectionSpaceToReferenceSpace(node.ToVector2());
                    updated = true;
                }
            }

            UpdateTargetVisibility(targetTransform, updated);
        }

        void UpdateTargetByLerpNode(Transform targetTransform, PoseNode? optNode1, PoseNode? optNode2, float lerpRatio)
        {
            if (targetTransform == null)
            {
                return;
            }

            var updated = false;
            if (optNode1 != null && optNode2 != null)
            {
                var node1 = (PoseNode)optNode1;
                var node2 = (PoseNode)optNode2;

                if (node1.isDetected && node2.isDetected)
                {
                    var vec1 = DetectionSpaceToReferenceSpace(node1.ToVector2());
                    var vec2 = DetectionSpaceToReferenceSpace(node2.ToVector2());
                    targetTransform.localPosition = Vector2.LerpUnclamped(vec1, vec2, lerpRatio);
                    updated = true;
                }
            }

            UpdateTargetVisibility(targetTransform, updated);
        }

        void UpdateTargetVisibility(Transform target, bool hasDetection)
        {
            bool shouldShow;

            if (!lastDetectionTimeByTarget.ContainsKey(target))
            {
                lastDetectionTimeByTarget[target] = -1e9f;
            }

            if (hasDetection)
            {
                lastDetectionTimeByTarget[target] = Time.fixedTime;
                shouldShow = true;
            }
            else
            {
                var curTime = Time.fixedTime;
                var lastDetectionTime = lastDetectionTimeByTarget[target];

                shouldShow = curTime - lastDetectionTime < autoHideWaitingTime;
            }

            target.gameObject.SetActive(shouldShow);
        }

        Vector3 DetectionSpaceToReferenceSpace(Vector2 vec)
        {
            var aspectNormalizedFrameSize = DetectionUtils.AspectNormalizedFrameSize;
            var xRate = (vec.x - aspectNormalizedFrameSize.x * 0.5f) / aspectNormalizedFrameSize.x;
            var yRate = (vec.y - aspectNormalizedFrameSize.y * 0.5f) / aspectNormalizedFrameSize.y;
            var newX = xRate * referenceTransform.rect.width + referenceTransform.localPosition.x;
            var newY = yRate * referenceTransform.rect.height + referenceTransform.localPosition.y;
            return new Vector3(newX, newY, 0f) + referenceTransform.localPosition;
        }

        #endregion
    }
}
