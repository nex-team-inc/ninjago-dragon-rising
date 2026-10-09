#nullable enable

using System.Collections.Generic;
using DG.Tweening;
using Jazz;
using UnityEngine;
using Nex.Utils;
using UnityEngine.UI;

namespace Nex
{
    public class PreviewFramePlayerIndicator : MonoBehaviour
    {
        public enum HighlightState
        {
            /// <summary>Prefab look (scale 1, prefab colour): outside gameplay turns.</summary>
            Neutral,
            /// <summary>The active shooter: scaled up, opaque, bouncing.</summary>
            Active,
            /// <summary>Another player is shooting: prefab colour at dimmedAlpha.</summary>
            Dimmed,
        }

        [SerializeField] RectTransform indicator = null!;
        [SerializeField] BodyPose.NodeIndex nodeToFollow;
        [SerializeField] Vector2 offsetInInches = Vector2.zero;
        [SerializeField] List<Sprite> spriteByPlayerIndex = null!;
        [SerializeField] Image image = null!;

        [Header("Active player highlight")]
        [Tooltip("Scale of the indicator while it marks the active shooter.")]
        [SerializeField, Range(1f, 3f)] float highlightedScale = 1.4f;
        [Tooltip("Extra scale of the highlight bounce (yoyo, unscaled time).")]
        [SerializeField, Range(1f, 2f)] float bounceScale = 1.12f;
        [SerializeField, Range(0.1f, 2f)] float bounceSeconds = 0.35f;
        [Tooltip("Alpha of indicators that are not the active shooter.")]
        [SerializeField, Range(0f, 1f)] float dimmedAlpha = 0.45f;

        int playerIndex = -1;
        Tween? bounceTween;
        Color baseColor = Color.white;
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

        #region Public Methods

        public int PlayerIndex => playerIndex;

        /// <summary>Applies the highlight look (see HighlightState). Position stays script-driven.</summary>
        public void SetHighlight(HighlightState state)
        {
            bounceTween?.Kill();
            bounceTween = null;
            var scale = state == HighlightState.Active ? highlightedScale : 1f;
            indicator.localScale = Vector3.one * scale;
            var color = baseColor;
            if (state == HighlightState.Dimmed) color.a *= dimmedAlpha;
            image.color = color;
            if (state != HighlightState.Active) return;
            bounceTween = indicator.DOScale(scale * bounceScale, bounceSeconds)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

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
            baseColor = image.color;

            previewFrameRectTransform = previewFrame.GetComponent<RectTransform>();
            indicator.gameObject.SetActive(false);

            initialized = true;
        }

        #endregion

        #region Detection

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
                RemapUtils.RemapAndClamp(nodePosition.x, playAreaRect.x, playAreaRect.x + playAreaRect.width, -previewRectTransformSize.x / 2 + leftRightMargin, previewRectTransformSize.x / 2 - leftRightMargin),
                RemapUtils.RemapAndClamp(nodePosition.y, playAreaRect.y, playAreaRect.y + playAreaRect.height, -previewRectTransformSize.y / 2 + bottomMargin, previewRectTransformSize.y / 2 - topMargin)
            );

            indicator.anchoredPosition = nodePositionInPreviewFrameRect;
            indicator.gameObject.SetActive(true);

            image.rectTransform.sizeDelta = new Vector2(indicatorSize, indicatorSize);
        }

        #endregion
    }
}
