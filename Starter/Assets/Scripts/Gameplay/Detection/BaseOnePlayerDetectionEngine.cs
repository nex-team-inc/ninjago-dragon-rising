#nullable enable

using System.Collections.Generic;
using Jazz;
using UnityEngine;
using UnityEngine.Events;

namespace Nex
{
    public abstract class BaseOnePlayerDetectionEngine : MonoBehaviour, PlayerAttachmentDataSource
    {
        const float autoHideWaitingTime = 0.2f;
        const float elbowWristLerpRatioForHand = 1.35f;

        [Header("Components")]
        [SerializeField] GameObject originalNodesContainer = null!;
        [SerializeField] GameObject smoothedNodesContainer = null!;

        bool enableOriginalNodes;
        bool enableSmoothedNodes;

        [Header("Nodes")]
        [SerializeField] GameObject originalNodePrefab = null!;
        [SerializeField] GameObject smoothedNodePrefab = null!;

        readonly Dictionary<PoseNodeIndex, Transform> originalNodeByType = new();
        readonly Dictionary<PoseNodeIndex, Transform> smoothedNodeByType = new();

        readonly List<PoseNodeIndex> supportNodeIndexList = new()
        {
            PoseNodeIndex.LeftHand,
            PoseNodeIndex.RightHand,
            PoseNodeIndex.Chest,
            PoseNodeIndex.Nose,
            PoseNodeIndex.LeftShoulder,
            PoseNodeIndex.RightShoulder,
            PoseNodeIndex.LeftElbow,
            PoseNodeIndex.RightElbow,
            // PoseNodeIndex.LeftWrist,
            // PoseNodeIndex.RightWrist,
            PoseNodeIndex.LeftHip,
            PoseNodeIndex.RightHip,
            PoseNodeIndex.LeftKnee,
            PoseNodeIndex.RightKnee,
            PoseNodeIndex.LeftEye,
            PoseNodeIndex.RightEye,
            // PoseNodeIndex.LeftEar,
            // PoseNodeIndex.RightEar,
            PoseNodeIndex.HipCenter,
            // PoseNodeIndex.LeftAnkle,
            // PoseNodeIndex.RightAnkle,
        };

        readonly Dictionary<Transform, float> lastDetectionTimeByTarget = new();
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        int playerIndex;

        readonly FloatHistory ppiHistory = new(0.5f);

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public float DistancePerInch { get; private set; }

        // ReSharper disable once MemberCanBeProtected.Global
        public float RawPpi { get; private set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public BodyPoseDetectionResult LastBodyPoseDetectionResult { get; private set; }

        public event UnityAction<BodyPoseDetectionResult>? NewDetectionCapturedAndProcessed;

        #region Public Properties for Nodes

        public Transform OriginalLeftHand => originalNodeByType[PoseNodeIndex.LeftHand];
        public Transform OriginalRightHand => originalNodeByType[PoseNodeIndex.RightHand];
        public Transform OriginalChest => originalNodeByType[PoseNodeIndex.Chest];
        public Transform OriginalNose => originalNodeByType[PoseNodeIndex.Nose];
        public Transform OriginalLeftShoulder => originalNodeByType[PoseNodeIndex.LeftShoulder];
        public Transform OriginalRightShoulder => originalNodeByType[PoseNodeIndex.RightShoulder];
        public Transform OriginalLeftElbow => originalNodeByType[PoseNodeIndex.LeftElbow];
        public Transform OriginalRightElbow => originalNodeByType[PoseNodeIndex.RightElbow];
        public Transform OriginalLeftWrist => originalNodeByType[PoseNodeIndex.LeftWrist];
        public Transform OriginalRightWrist => originalNodeByType[PoseNodeIndex.RightWrist];
        public Transform OriginalLeftHip => originalNodeByType[PoseNodeIndex.LeftHip];
        public Transform OriginalRightHip => originalNodeByType[PoseNodeIndex.RightHip];
        public Transform OriginalLeftKnee => originalNodeByType[PoseNodeIndex.LeftKnee];
        public Transform OriginalRightKnee => originalNodeByType[PoseNodeIndex.RightKnee];
        public Transform OriginalLeftEye => originalNodeByType[PoseNodeIndex.LeftEye];
        public Transform OriginalRightEye => originalNodeByType[PoseNodeIndex.RightEye];
        public Transform OriginalLeftEar => originalNodeByType[PoseNodeIndex.LeftEar];
        public Transform OriginalRightEar => originalNodeByType[PoseNodeIndex.RightEar];
        public Transform OriginalHipCenter => originalNodeByType[PoseNodeIndex.HipCenter];
        public Transform OriginalLeftAnkle => originalNodeByType[PoseNodeIndex.LeftAnkle];
        public Transform OriginalRightAnkle => originalNodeByType[PoseNodeIndex.RightAnkle];

        public Transform LeftHand => smoothedNodeByType[PoseNodeIndex.LeftHand];
        public Transform RightHand => smoothedNodeByType[PoseNodeIndex.RightHand];
        public Transform Chest => smoothedNodeByType[PoseNodeIndex.Chest];
        public Transform Nose => smoothedNodeByType[PoseNodeIndex.Nose];
        public Transform LeftShoulder => smoothedNodeByType[PoseNodeIndex.LeftShoulder];
        public Transform RightShoulder => smoothedNodeByType[PoseNodeIndex.RightShoulder];
        public Transform LeftElbow => smoothedNodeByType[PoseNodeIndex.LeftElbow];
        public Transform RightElbow => smoothedNodeByType[PoseNodeIndex.RightElbow];
        public Transform LeftWrist => smoothedNodeByType[PoseNodeIndex.LeftWrist];
        public Transform RightWrist => smoothedNodeByType[PoseNodeIndex.RightWrist];
        public Transform LeftHip => smoothedNodeByType[PoseNodeIndex.LeftHip];
        public Transform RightHip => smoothedNodeByType[PoseNodeIndex.RightHip];
        public Transform LeftKnee => smoothedNodeByType[PoseNodeIndex.LeftKnee];
        public Transform RightKnee => smoothedNodeByType[PoseNodeIndex.RightKnee];
        public Transform LeftEye => smoothedNodeByType[PoseNodeIndex.LeftEye];
        public Transform RightEye => smoothedNodeByType[PoseNodeIndex.RightEye];
        public Transform LeftEar => smoothedNodeByType[PoseNodeIndex.LeftEar];
        public Transform RightEar => smoothedNodeByType[PoseNodeIndex.RightEar];
        public Transform HipCenter => smoothedNodeByType[PoseNodeIndex.HipCenter];
        public Transform LeftAnkle => smoothedNodeByType[PoseNodeIndex.LeftAnkle];
        public Transform RightAnkle => smoothedNodeByType[PoseNodeIndex.RightAnkle];

        #endregion

        #region Initialization

        protected void Initialize(
            int aPlayerIndex,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            bool aEnableOriginalNodes = true,
            bool aEnableSmoothedNodes = true
        )
        {
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
            playerIndex = aPlayerIndex;
            enableOriginalNodes = aEnableOriginalNodes;
            enableSmoothedNodes = aEnableSmoothedNodes;

            foreach (var nodeIndex in supportNodeIndexList)
            {
                var originalNode = Instantiate(originalNodePrefab, originalNodesContainer.transform);
                originalNode.name = $"Original{nodeIndex}";
                originalNodeByType[nodeIndex] = originalNode.transform;

                var smoothedNode = Instantiate(smoothedNodePrefab, smoothedNodesContainer.transform);
                smoothedNode.name = $"{nodeIndex}";
                smoothedNodeByType[nodeIndex] = smoothedNode.transform;
            }

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
        protected abstract BodyPose? CloneAndProcessPoseIfNeeded(BodyPose? pose);

        #endregion

        #region Process Pose

        void ProcessedOnCaptureAspectNormalizedDetection(BodyPoseDetectionResult detectionResult)
        {
            UpdatePpi(detectionResult.original);

            if (RawPpi > 0)
            {
                originalNodesContainer.SetActive(enableOriginalNodes);
                smoothedNodesContainer.SetActive(enableSmoothedNodes);

                if (enableOriginalNodes)
                {
                    UpdateTargetsForDetection(detectionResult.original, originalNodeByType);
                }

                if (enableSmoothedNodes)
                {
                    UpdateTargetsForDetection(detectionResult.processed, smoothedNodeByType);
                }

                LastBodyPoseDetectionResult = detectionResult;
                NewDetectionCapturedAndProcessed?.Invoke(detectionResult);
            }
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

        void UpdateTargetsForDetection(BodyPoseDetection detection, Dictionary<PoseNodeIndex, Transform> nodeByType)
        {
            var playerPose = detection.GetPlayerPose(playerIndex);
            var pose = CloneAndProcessPoseIfNeeded(playerPose?.bodyPose);

            UpdateTargetByLerpNode(nodeByType.GetValueOrDefault(PoseNodeIndex.LeftHand), pose?.LeftElbow(), pose?.LeftWrist(), elbowWristLerpRatioForHand);
            UpdateTargetByLerpNode(nodeByType.GetValueOrDefault(PoseNodeIndex.RightHand), pose?.RightElbow(), pose?.RightWrist(), elbowWristLerpRatioForHand);
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.Chest), pose?.Chest());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.Nose), pose?.Nose());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.LeftShoulder), pose?.LeftShoulder());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.RightShoulder), pose?.RightShoulder());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.LeftElbow), pose?.LeftElbow());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.RightElbow), pose?.RightElbow());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.LeftWrist), pose?.LeftWrist());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.RightWrist), pose?.RightWrist());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.LeftHip), pose?.LeftHip());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.RightHip), pose?.RightHip());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.LeftKnee), pose?.LeftKnee());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.RightKnee), pose?.RightKnee());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.LeftEye), pose?.LeftEye());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.RightEye), pose?.RightEye());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.LeftEar), pose?.LeftEar());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.RightEar), pose?.RightEar());
            UpdateTargetByLerpNode(nodeByType.GetValueOrDefault(PoseNodeIndex.HipCenter), pose?.LeftHip(), pose?.RightHip(), 0.5f);
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.LeftAnkle), pose?.LeftAnkle());
            UpdateTargetByNode(nodeByType.GetValueOrDefault(PoseNodeIndex.RightAnkle), pose?.RightAnkle());
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

        #region PlayerAttachmentDataSource

        public Vector3? GetNodePosition(PoseNodeIndex poseNodeIndex, bool smoothed)
        {
            var nodeTransform = smoothed
                ? smoothedNodeByType.GetValueOrDefault(poseNodeIndex)
                : originalNodeByType.GetValueOrDefault(poseNodeIndex);

            if (nodeTransform != null && nodeTransform.gameObject.activeSelf)
            {
                return nodeTransform.position;
            }

            return null;
        }

        #endregion
    }
}
