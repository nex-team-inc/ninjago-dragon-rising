using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Jazz;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class PlayerFocusPreviewFrame : PreviewFrameBase
    {
        [Serializable]
        public struct Margins
        {
            public float top;
            public float bottom;
            public float left;
            public float right;

            public Margins(float top, float bottom, float left, float right)
            {
                this.top = top;
                this.bottom = bottom;
                this.left = left;
                this.right = right;
            }
        }

        [SerializeField] Margins marginsInInches = new(24, 34, 32, 32);
        [SerializeField] float oneEuroFilterMinCutoff = 1;
        [SerializeField] float oneEuroFilterBeta = 2;

        [Header("X Logic")]
        [SerializeField] bool followX = true;

        [Header("Y Logic")]
        [SerializeField] bool followY = true;
        [SerializeField] bool useStableChestY = true;
        [SerializeField] float yChangeSigmaInInches = 1;

        CvDetectionManager cvDetectionManager = null!;
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;

        Rect previewRectInNormalizedSpace;

        bool isFirstFrameReceived;
        int playerIndex;

        // ReSharper disable once NotAccessedField.Local
        int numOfPlayers;

        readonly FloatHistory ppiHistory = new(2);

        // Chest Smoothing
        ComposedFilter2D<OneEuroFilter> chestFilter = null!;
        readonly WeightedFloatHistory chestYHistory = new(2);
        float lastRawChestY;

        #region Public

        public void Initialize(
            int aPlayerIndex,
            int aNumOfPlayers,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager
        )
        {
            playerIndex = aPlayerIndex;
            numOfPlayers = aNumOfPlayers;

            cvDetectionManager = aCvDetectionManager;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;

            cvDetectionManager.captureCameraFrame += CvDetectionManagerOnCaptureCameraFrame;
            bodyPoseDetectionManager.captureAspectNormalizedDetection += BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection;
            previewRectInNormalizedSpace = new Rect(0, 0, 1, 1);

            canvasGroup.alpha = 0;

            // ReSharper disable once RedundantArgumentDefaultValue
            chestFilter = new ComposedFilter2D<OneEuroFilter>(
                new OneEuroFilter(oneEuroFilterMinCutoff, oneEuroFilterBeta),
                new OneEuroFilter(oneEuroFilterMinCutoff, oneEuroFilterBeta)
                );
        }

        public override Rect PreviewRectInNormalizedSpace()
        {
            return previewRectInNormalizedSpace;
        }

        #endregion

        #region Life Cycle

        void OnDestroy()
        {
            cvDetectionManager.captureCameraFrame -= CvDetectionManagerOnCaptureCameraFrame;
            bodyPoseDetectionManager.captureAspectNormalizedDetection -= BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection;
        }

        #endregion

        #region Event

        void CvDetectionManagerOnCaptureCameraFrame(FrameInformation frameInformation)
        {
            if (!isFirstFrameReceived)
            {
                isFirstFrameReceived = true;

                canvasGroup.DOFade(1f, 0.5f).WithCancellation(this.GetCancellationTokenOnDestroy());
            }

            rawImage.texture = frameInformation.texture;
            var isMirrored = frameInformation.shouldMirror;

            UpdatePreviewRectInWorldSpaceInfoIfNeeded();

            rawImage.uvRect = FlipRectIfNeeded(previewRectInNormalizedSpace, isMirrored);
        }

        #endregion

        #region Player Chasing

        void BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection(BodyPoseDetectionResult bodyPoseDetectionResult)
        {
            var poseDetection = bodyPoseDetectionResult.original;
            var playerPose = poseDetection.GetPlayerPose(playerIndex);
            var pose = playerPose?.bodyPose;
            if (pose == null)
            {
                return;
            }

            ppiHistory.Add(pose.pixelsPerInch, Time.fixedTime);
            ppiHistory.UpdateCurrentFrameTime(Time.fixedTime);
            var ppi = ppiHistory.Average();

            UpdatePreviewRectInWorldSpaceInfoIfNeeded();

            var fullFrameRect = DetectionUtils.AspectNormalizedFrameRect;
            var chest = pose.Chest().ToVector2();

            chest = GetSmoothedChest(chest, ppi);

            var centerX = chest.x + ppi * 0.5f * (marginsInInches.right - marginsInInches.left);
            var centerY = chest.y + ppi * 0.5f * (marginsInInches.top - marginsInInches.bottom);

            if (!followX)
            {
                centerX = fullFrameRect.width * 0.5f;
            }

            if (!followY)
            {
                centerY = fullFrameRect.height * 0.5f;
            }

            var rectWidth = ppi * (marginsInInches.right + marginsInInches.left);
            var rectHeight = ppi * (marginsInInches.top + marginsInInches.bottom);

            rectWidth = Math.Max(rectWidth, rectHeight * previewRectInWorldSpaceAspectRatio);
            rectHeight = Math.Max(rectHeight, rectWidth / previewRectInWorldSpaceAspectRatio);

            // Scale them to smaller than fullFrameRect, proportionally.
            var scaleRatio = Math.Min(1, Math.Min(fullFrameRect.width / rectWidth, fullFrameRect.height / rectHeight));
            rectWidth *= scaleRatio;
            rectHeight *= scaleRatio;

            // Shift center point in order to make the rect inside the fullFrameRect.
            var leftOffset = centerX - rectWidth * 0.5f;
            if (leftOffset < 0)
            {
                centerX += -leftOffset; // Move right
            }

            var rightOffset = fullFrameRect.width - (centerX + rectWidth * 0.5f);
            if (rightOffset < 0)
            {
                centerX += rightOffset; // Move left
            }

            var bottomOffset = centerY - rectHeight * 0.5f;
            if (bottomOffset < 0)
            {
                centerY += -bottomOffset; // Move up
            }

            var topOffset = fullFrameRect.height - (centerY + rectHeight * 0.5f);
            if (topOffset < 0)
            {
                centerY += topOffset; // Move down
            }

            var playerPreviewRect = new Rect(
                centerX - rectWidth * 0.5f,
                centerY - rectHeight * 0.5f,
                rectWidth,
                rectHeight
            );

            previewRectInNormalizedSpace = new Rect
            {
                x = playerPreviewRect.x / fullFrameRect.width,
                y = playerPreviewRect.y / fullFrameRect.height,
                width = playerPreviewRect.width / fullFrameRect.width,
                height = playerPreviewRect.height / fullFrameRect.height
            };
        }

        #endregion

        #region Smooth Chest

        Vector2 GetSmoothedChest(Vector2 rawChest, float ppi)
        {
            var ret = chestFilter.Filter(rawChest.x, rawChest.y);

            if (useStableChestY)
            {
                // Use weighted average to reduce the influence of a jumping Y value.
                var deltaY = rawChest.y - lastRawChestY;
                lastRawChestY = rawChest.y;

                var weight = (float)BasicUtils.Gaussian(Math.Abs(deltaY), yChangeSigmaInInches * ppi);

                chestYHistory.Add(rawChest.y, weight, Time.fixedTime);
                chestYHistory.UpdateCurrentFrameTime(Time.fixedTime);

                ret.y = chestYHistory.WeightedAverage();
            }

            return ret;
        }

        #endregion
    }
}
