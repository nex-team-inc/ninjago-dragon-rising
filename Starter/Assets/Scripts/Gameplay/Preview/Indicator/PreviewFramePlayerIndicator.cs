#nullable enable

using System.Collections.Generic;
using Jazz;
using UnityEngine;
using Nex.Utils;
using UnityEngine.UI;

namespace Nex
{
    public class PreviewFramePlayerIndicator : MonoBehaviour
    {
        [SerializeField] RectTransform indicator = null!;
        [SerializeField] BodyPose.NodeIndex nodeToFollow;
        [SerializeField] Vector2 offsetInInches = Vector2.zero;
        [SerializeField] List<Sprite> spriteByPlayerIndex = null!;
        [SerializeField] Image image = null!;

        int playerIndex = -1;
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        PreviewFrameBase previewFrame = null!;
        RectTransform previewFrameRectTransform = null!;
        bool initialized;
        float sizeRatioToPreviewHeight;

        readonly FloatHistory ppiHistory = new(0.3f);
        readonly ComposedFilter2D<OneEuroFilter> nodeFilter = new(
            new OneEuroFilter(4, 10),
            new OneEuroFilter(4, 10)
        );

        public void Initialize(
            int aPlayerIndex,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            PreviewFrameBase aPreviewFrame,
            float aSizeRatioToPreviewHeight
        )
        {
            playerIndex = aPlayerIndex;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
            previewFrame = aPreviewFrame;
            sizeRatioToPreviewHeight = aSizeRatioToPreviewHeight;

            bodyPoseDetectionManager.captureAspectNormalizedDetection += UpdateDetectionResult;

            image.sprite = spriteByPlayerIndex[playerIndex];

            previewFrameRectTransform = previewFrame.GetComponent<RectTransform>();
            indicator.gameObject.SetActive(false);

            initialized = true;
        }

        void OnDestroy()
        {
            bodyPoseDetectionManager.captureAspectNormalizedDetection -= UpdateDetectionResult;
        }

        void UpdateDetectionResult(BodyPoseDetectionResult detectionResult)
        {
            if (!initialized)
            {
                return;
            }

            var playerPose = detectionResult.processed.GetPlayerPose(playerIndex);
            var pose = (BodyPose?)playerPose?.bodyPose.Clone();

            if (pose == null)
            {
                indicator.gameObject.SetActive(false);
                return;
            }

            ppiHistory.Add(pose.pixelsPerInch, Time.fixedTime);
            ppiHistory.UpdateCurrentFrameTime(Time.fixedTime);

            var ppi = ppiHistory.Average();
            var node = pose.GetNode(nodeToFollow);
            if (!node.isDetected || ppi <= 0)
            {
                indicator.gameObject.SetActive(false);
                return;
            }

            var nodePosition = node.ToVector2();
            nodePosition += offsetInInches * ppi;
            nodePosition = nodeFilter.Filter(nodePosition.x, nodePosition.y);

            var playAreaRect = previewFrame.PreviewRectInAspectNormalizedSpace();
            var previewRectTransformSize = previewFrameRectTransform.rect.size;

            var indicatorSize = previewRectTransformSize.y * sizeRatioToPreviewHeight;

            // Why top & bottom have different margin? Because the indicator is centered
            // at the bottom center (not center center)
            var leftRightMargin = indicatorSize * 0.5f;
            var topMargin = indicatorSize;
            var bottomMargin = 0;

            var nodePositionInPreviewFrameRect = new Vector2(
                RemapUtils.RemapAndClamp(nodePosition.x, playAreaRect.x, playAreaRect.x + playAreaRect.width, -previewRectTransformSize.x / 2, previewRectTransformSize.x / 2, leftRightMargin, leftRightMargin),
                RemapUtils.RemapAndClamp(nodePosition.y, playAreaRect.y, playAreaRect.y + playAreaRect.height, -previewRectTransformSize.y / 2, previewRectTransformSize.y / 2, bottomMargin, topMargin)
            );

            indicator.anchoredPosition = nodePositionInPreviewFrameRect;
            indicator.gameObject.SetActive(true);

            image.rectTransform.sizeDelta = new Vector2(indicatorSize, indicatorSize);
        }
    }
}
