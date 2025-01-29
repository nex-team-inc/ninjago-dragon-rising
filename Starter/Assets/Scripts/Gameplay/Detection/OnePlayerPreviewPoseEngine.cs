using System.Collections.Generic;
using Jazz;
using UnityEngine;
using UnityEngine.Events;

#nullable enable

namespace Nex
{
    public class OnePlayerPreviewPoseEngine : MonoBehaviour
    {
        const float autoHideWaitingTime = 0.5f;
        const float ElbowWristLerpRatioForHand = 1.35f;

        [Header("Components")]
        [SerializeField] GameObject originalNodesContainer = null!;
        [SerializeField] GameObject smoothedNodesContainer = null!;
        [SerializeField] bool enableOriginalNodes;
        [SerializeField] bool enableSmoothedNodes = true;

        [Header("Original")]
        [SerializeField] Transform originalLeftHand = null!;
        [SerializeField] Transform originalRightHand = null!;
        [SerializeField] Transform originalChest = null!;
        [SerializeField] Transform originalNose = null!;
        [SerializeField] Transform originalLeftShoulder = null!;
        [SerializeField] Transform originalRightShoulder = null!;
        [SerializeField] Transform originalLeftElbow = null!;
        [SerializeField] Transform originalRightElbow = null!;
        [SerializeField] Transform originalLeftWrist = null!;
        [SerializeField] Transform originalRightWrist = null!;
        [SerializeField] Transform originalLeftHip = null!;
        [SerializeField] Transform originalRightHip = null!;
        [SerializeField] Transform originalLeftKnee = null!;
        [SerializeField] Transform originalRightKnee = null!;

        [Header("Smoothed")]
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

        // ReSharper disable once EventNeverSubscribedTo.Global
        public event UnityAction<BodyPoseDetectionResult>? NewDetectionCapturedAndProcessed;

        readonly Dictionary<Transform, float> lastDetectionTimeByTarget = new();
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        int playerIndex;
        PreviewFrameBase playerPreviewFrame = null!;
        bool isSmoothHelperInitialized;
        readonly FloatHistory ppiHistory = new(3);
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public float DistancePerInch { get; private set; }
        public float RawPpi { get; private set; }
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public BodyPoseDetectionResult LastBodyPoseDetectionResult { get; private set; }

        #region Public Properties

        public Transform OriginalLeftHand => originalLeftHand;
        public Transform OriginalRightHand => originalRightHand;
        public Transform OriginalChest => originalChest;
        public Transform OriginalNose => originalNose;
        public Transform OriginalLeftShoulder => originalLeftShoulder;
        public Transform OriginalRightShoulder => originalRightShoulder;
        public Transform OriginalLeftElbow => originalLeftElbow;
        public Transform OriginalRightElbow => originalRightElbow;
        public Transform OriginalLeftWrist => originalLeftWrist;
        public Transform OriginalRightWrist => originalRightWrist;
        public Transform OriginalLeftHip => originalLeftHip;
        public Transform OriginalRightHip => originalRightHip;
        public Transform OriginalLeftKnee => originalLeftKnee;
        public Transform OriginalRightKnee => originalRightKnee;

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
            int numOfPlayers,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            PreviewFrameBase aPlayerPreviewFrame
        )
        {
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
            playerIndex = aPlayerIndex;
            playerPreviewFrame = aPlayerPreviewFrame;

            bodyPoseDetectionManager.processed.captureAspectNormalizedDetection += ProcessedOnCaptureAspectNormalizedDetection;

            originalNodesContainer.SetActive(false);
            smoothedNodesContainer.SetActive(false);
        }

        #endregion

        #region Life Cycle

        void OnDestroy()
        {
            if (bodyPoseDetectionManager != null)
            {
                bodyPoseDetectionManager.processed.captureAspectNormalizedDetection -=
                    ProcessedOnCaptureAspectNormalizedDetection;
            }
        }

        #endregion

        #region Event

        void ProcessedOnCaptureAspectNormalizedDetection(BodyPoseDetectionResult detectionResult)
        {
            HandleSmoothedPose(detectionResult.processed);
            HandleOriginalPose(detectionResult.original);

            LastBodyPoseDetectionResult = detectionResult;
            NewDetectionCapturedAndProcessed?.Invoke(detectionResult);
        }

        void HandleSmoothedPose(BodyPoseDetection detection)
        {
            smoothedNodesContainer.SetActive(enableSmoothedNodes);
            if (!enableSmoothedNodes)
            {
                return;
            }

            var playerPose = detection.GetPlayerPose(playerIndex);
            var pose = (BodyPose?)playerPose?.bodyPose.Clone();

            if (pose != null)
            {
                ppiHistory.Add(pose.pixelsPerInch, Time.fixedTime);
                ppiHistory.UpdateCurrentFrameTime(Time.fixedTime);

                RawPpi = ppiHistory.Average();
                var aspectNormalFrameSize = DetectionUtils.AspectNormalizedFrameSize;
                var previewRectInNormalizedSpace = playerPreviewFrame.PreviewRectInNormalizedSpace();
                var previewRectHeightInAspectNormalizedSpace = previewRectInNormalizedSpace.height * aspectNormalFrameSize.y;
                var previewRectInWorldSpace = playerPreviewFrame.PreviewRectInWorldSpace();
                DistancePerInch = RawPpi / previewRectHeightInAspectNormalizedSpace * previewRectInWorldSpace.height;
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
        }

        void HandleOriginalPose(BodyPoseDetection detection)
        {
            originalNodesContainer.SetActive(enableOriginalNodes);
            if (!enableOriginalNodes)
            {
                return;
            }

            var playerPose = detection.GetPlayerPose(playerIndex);
            var pose = (BodyPose?)playerPose?.bodyPose.Clone();

            UpdateTargetByLerpNode(originalLeftHand, pose?.LeftElbow(), pose?.LeftWrist(), ElbowWristLerpRatioForHand);
            UpdateTargetByLerpNode(originalRightHand, pose?.RightElbow(), pose?.RightWrist(), ElbowWristLerpRatioForHand);
            UpdateTargetByNode(originalChest, pose?.Chest());
            UpdateTargetByNode(originalNose, pose?.Nose());
            UpdateTargetByNode(originalLeftShoulder, pose?.LeftShoulder());
            UpdateTargetByNode(originalRightShoulder, pose?.RightShoulder());
            UpdateTargetByNode(originalLeftElbow, pose?.LeftElbow());
            UpdateTargetByNode(originalRightElbow, pose?.RightElbow());
            UpdateTargetByNode(originalLeftWrist, pose?.LeftWrist());
            UpdateTargetByNode(originalRightWrist, pose?.RightWrist());
            UpdateTargetByNode(originalLeftHip, pose?.LeftHip());
            UpdateTargetByNode(originalRightHip, pose?.RightHip());
            UpdateTargetByNode(originalLeftKnee, pose?.LeftKnee());
            UpdateTargetByNode(originalRightKnee, pose?.RightKnee());
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
                    targetTransform.localPosition = AspectNormalSpaceToWorldSpace(node.ToVector2());
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
                    var vec1 = AspectNormalSpaceToWorldSpace(node1.ToVector2());
                    var vec2 = AspectNormalSpaceToWorldSpace(node2.ToVector2());
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

        Vector3 AspectNormalSpaceToWorldSpace(Vector2 vec)
        {
            var aspectNormalFrameSize = DetectionUtils.AspectNormalizedFrameSize;
            var previewRectInNormalizedSpace = playerPreviewFrame.PreviewRectInNormalizedSpace();
            var previewRectInAspectNormalizedSpace = new Rect(
                previewRectInNormalizedSpace.x * aspectNormalFrameSize.x,
                previewRectInNormalizedSpace.y * aspectNormalFrameSize.y,
                previewRectInNormalizedSpace.width * aspectNormalFrameSize.x,
                previewRectInNormalizedSpace.height * aspectNormalFrameSize.y
            );
            var previewRectInWorldSpace = playerPreviewFrame.PreviewRectInWorldSpace();

            var newX = (vec.x - previewRectInAspectNormalizedSpace.x) / previewRectInAspectNormalizedSpace.width * previewRectInWorldSpace.width + previewRectInWorldSpace.x;
            var newY = (vec.y - previewRectInAspectNormalizedSpace.y) / previewRectInAspectNormalizedSpace.height * previewRectInWorldSpace.height + previewRectInWorldSpace.y;

            return new Vector2(newX, newY);
        }

        #endregion
    }
}
