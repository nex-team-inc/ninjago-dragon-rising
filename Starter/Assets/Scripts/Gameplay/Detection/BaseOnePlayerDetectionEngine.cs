#nullable enable

using System.Collections.Generic;
using Jazz;
using UnityEngine;
using UnityEngine.Events;

namespace Nex
{
    public abstract class BaseOnePlayerDetectionEngine : MonoBehaviour
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

        readonly Dictionary<Transform, float> lastDetectionTimeByTarget = new();
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        int playerIndex;

        readonly FloatHistory ppiHistory = new(3);

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public float DistancePerInch { get; private set; }

        // ReSharper disable once MemberCanBeProtected.Global
        public float RawPpi { get; private set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public BodyPoseDetectionResult LastBodyPoseDetectionResult { get; private set; }

        public event UnityAction<BodyPoseDetectionResult>? NewDetectionCapturedAndProcessed;

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

        #region Initialization

        protected void Initialize(
            int aPlayerIndex,
            BodyPoseDetectionManager aBodyPoseDetectionManager
        )
        {
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
            playerIndex = aPlayerIndex;

            bodyPoseDetectionManager.processed.captureAspectNormalizedDetection += ProcessedOnCaptureAspectNormalizedDetection;

            originalNodesContainer.SetActive(false);
            smoothedNodesContainer.SetActive(false);
        }

        #endregion

        #region Life Cycle

        void OnDestroy()
        {
            bodyPoseDetectionManager.processed.captureAspectNormalizedDetection -=
                ProcessedOnCaptureAspectNormalizedDetection;
        }

        #endregion

        #region Transform

        protected abstract Vector3 AspectNormalSpaceToWorldSpace(Vector2 vec);
        protected abstract float ConvertRawPpiToDpi(float rawPpi);

        #endregion

        #region Process Pose

        void ProcessedOnCaptureAspectNormalizedDetection(BodyPoseDetectionResult detectionResult)
        {
            UpdatePpi(detectionResult.original);

            originalNodesContainer.SetActive(enableOriginalNodes);
            smoothedNodesContainer.SetActive(enableSmoothedNodes);

            if (enableOriginalNodes)
            {
                HandleOriginalPose(detectionResult.original);
            }

            if (enableSmoothedNodes)
            {
                HandleSmoothedPose(detectionResult.processed);
            }

            LastBodyPoseDetectionResult = detectionResult;
            NewDetectionCapturedAndProcessed?.Invoke(detectionResult);
        }

        void UpdatePpi(BodyPoseDetection detection)
        {
            var playerPose = detection.GetPlayerPose(playerIndex);
            var pose = playerPose?.bodyPose;

            // ReSharper disable once MergeIntoPattern
            if (pose != null && pose.pixelsPerInch > 0)
            {
                ppiHistory.Add(pose.pixelsPerInch, Time.fixedTime);
                ppiHistory.UpdateCurrentFrameTime(Time.fixedTime);

                RawPpi = ppiHistory.Average();

                DistancePerInch = ConvertRawPpiToDpi(RawPpi);
            }
        }

        void HandleSmoothedPose(BodyPoseDetection detection)
        {
            var playerPose = detection.GetPlayerPose(playerIndex);
            var pose = (BodyPose?)playerPose?.bodyPose.Clone();

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

        #endregion
    }
}
