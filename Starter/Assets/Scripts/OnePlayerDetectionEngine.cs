using Jazz;
using UnityEngine;

#nullable enable

namespace Starter
{
    public class OnePlayerDetectionEngine : MonoBehaviour
    {
        const float ElbowWristLerpRatioForHand = 1.3f;

        [SerializeField] Transform leftHand = null!;
        [SerializeField] Transform rightHand = null!;
        [SerializeField] Transform chest = null!;
        [SerializeField] bool autoHideIfNoDetection = true;

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
        }

        #endregion

        #region Event

        void BodyPoseDetectionManagerOnCaptureBodyPoseDetection(BodyPoseDetection detection)
        {

            rawFrameSize = detection.frameSize;
            playerFrameSize = new Vector2(rawFrameSize.x / numOfPlayers, rawFrameSize.y);
            playerFrameCenter = new Vector2((0.5f + playerIndex) * playerFrameSize.x, 0.5f * rawFrameSize.y);

            var pose = detection.GetPlayerPose(playerIndex)?.bodyPose;

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

            targetTransform.gameObject.SetActive(!autoHideIfNoDetection || updated);
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

            targetTransform.gameObject.SetActive(!autoHideIfNoDetection || updated);
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
