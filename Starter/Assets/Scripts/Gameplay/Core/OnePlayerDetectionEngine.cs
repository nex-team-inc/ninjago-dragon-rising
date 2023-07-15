using System.Collections.Generic;
using Jazz;
using NaughtyAttributes;
using UnityEngine;

#nullable enable

namespace Starter
{
    public class OnePlayerDetectionEngine : MonoBehaviour
    {
        const float ElbowWristLerpRatioForHand = 1.3f;

        [Header("Targets")]
        [SerializeField] Transform leftHand = null!;
        [SerializeField] Transform rightHand = null!;
        [SerializeField] Transform chest = null!;

        [Header("Auto Hide")]
        [SerializeField] bool autoHideIfNoDetection = true;

        [ShowIf(nameof(autoHideIfNoDetection))]
        [SerializeField] float autoHideWaitingTime = 0.5f;

        [Header("Smoothing")]
        [SerializeField] BodyPoseSmoothHelper? smoothHelper;

        readonly Dictionary<Transform, float> noDetectionStartTimeByTarget = new();
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        int playerIndex;
        int numOfPlayers;
        RectTransform referenceTransform = null!;
        Vector2 rawFrameSize;
        Vector2 playerFrameSize;
        Vector2 playerFrameCenter;

        #region Public

        public void Initialize(
            int aPlayerIndex,
            int aNumOfPlayers,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            RectTransform aReferenceTransform
            )
        {
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
            playerIndex = aPlayerIndex;
            numOfPlayers = aNumOfPlayers;
            referenceTransform = aReferenceTransform;

            bodyPoseDetectionManager.captureBodyPoseDetection += BodyPoseDetectionManagerOnCaptureBodyPoseDetection;

            if (smoothHelper != null)
            {
                smoothHelper.Initialize();
            }
        }

        #endregion

        #region Event

        void BodyPoseDetectionManagerOnCaptureBodyPoseDetection(BodyPoseDetection detection)
        {

            rawFrameSize = detection.frameSize;
            playerFrameSize = new Vector2(rawFrameSize.x / numOfPlayers, rawFrameSize.y);
            playerFrameCenter = new Vector2((0.5f + playerIndex) * playerFrameSize.x, 0.5f * rawFrameSize.y);

            var playerPose = detection.GetPlayerPose(playerIndex);
            if (smoothHelper != null)
            {
                playerPose = smoothHelper.Smooth(playerPose);
            }

            var pose = playerPose?.bodyPose;

            UpdateTargetByLerpNode(leftHand, pose?.LeftElbow(), pose?.LeftWrist(), ElbowWristLerpRatioForHand);
            UpdateTargetByLerpNode(rightHand, pose?.RightElbow(), pose?.RightWrist(), ElbowWristLerpRatioForHand);
            UpdateTargetByNode(chest, pose?.Chest());
        }

        #endregion

        #region Helper

        void UpdateTargetByNode(Transform targetTransform, PoseNode? optNode)
        {
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
            var updated = false;
            if (optNode1 != null && optNode2 != null)
            {
                var node1 = (PoseNode)optNode1;
                var node2 = (PoseNode)optNode2;

                if (node1.isDetected && node2.isDetected)
                {
                    var vec1 = DetectionSpaceToReferenceSpace(node1.ToVector2());
                    var vec2 = DetectionSpaceToReferenceSpace(node2.ToVector2());
                    targetTransform.localPosition = Vector2.Lerp(vec1, vec2, lerpRatio);
                    updated = true;
                }
            }

            UpdateTargetVisibility(targetTransform, updated);
        }

        void UpdateTargetVisibility(Transform target, bool hasDetection)
        {
            bool shouldShow;

            // If we don't need to hide it, always show it.
            if (!autoHideIfNoDetection)
            {
                shouldShow = true;
            }
            else
            {
                // If want to auto hide it, we initialize its "no detection start time" first.
                if (!noDetectionStartTimeByTarget.ContainsKey(target))
                {
                    noDetectionStartTimeByTarget[target] = 0;
                }

                if (hasDetection)
                {
                    noDetectionStartTimeByTarget[target] = 0;
                    shouldShow = true;
                }
                else
                {
                    var curTime = Time.fixedTime;
                    var startTime = noDetectionStartTimeByTarget[target];
                    if (startTime == 0)
                    {
                        noDetectionStartTimeByTarget[target] = startTime = curTime;
                    }

                    shouldShow = curTime - startTime < autoHideWaitingTime;
                }
            }

            target.gameObject.SetActive(shouldShow);
        }

        Vector2 DetectionSpaceToReferenceSpace(Vector2 vec)
        {
            var xRate = (vec.x - playerFrameCenter.x) / playerFrameSize.x;
            var yRate = -((vec.y - playerFrameCenter.y) / playerFrameSize.y);
            var newX = xRate * referenceTransform.rect.width + referenceTransform.position.x;
            var newY = yRate * referenceTransform.rect.height + referenceTransform.position.y;
            return new Vector2(newX, newY);
        }

        #endregion
    }
}
