#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jazz;
using NaughtyAttributes;
using UnityEngine;

namespace Nex
{
    public class WeightedAveragePlayAreaController : BasePlayAreaController
    {
        [Serializable]
        public class Config
        {
            public float smoothTimeWindow = 1f;
            public float topMarginInInches = 20;
            public float bottomMarginInInches = 24;
            public float minPlayAreaToRawFrameRatio = 0.3f;
            public float maxPlayAreaToRawFrameRatio = 1f;

            public float poseScaleSigma = 0.3f;
            public float chestYPositionSigma = 0.3f;

            public float firstRefreshInterval = 1f;
            public float refreshInterval = 5f;

            // Copy from PlayerTrackingUtils
            public float refInches = 44;

            public float deadZone = 0.1f;
            public float aspectRatio = 16f / 9f;

            public float minValidPoseScaleAverage = 0.1f;
            public float maxValidPoseScaleAverage = 1f;
            public float minValidChestYPositionAverage = 0.1f;
            public float maxValidChestYPositionAverage = 1f;
        }

        // Controllers
        [SerializeField] Config config = null!;

        // States
        readonly Vector2 poseSpaceSize = DetectionUtils.AspectNormalizedFrameSize;
        FloatHistory poseScaleHistory = null!;
        FloatHistory chestYPositionHistory = null!;
        Vector2 frameSize = Vector2.one;

        [Jazz.ReadOnly] public float poseScaleWeightedAverage;
        [Jazz.ReadOnly] public float chestYPositionWeightedAverage;
        [ShowNativeProperty] Rect PlayAreaInAspectNormalizedSpace { get; set; } = new(0, 0, 1, 1);

        #region Life Cycle

        public override void Initialize(
            int aNumOfPlayers,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager
        )
        {
            base.Initialize(aNumOfPlayers, aCvDetectionManager, aBodyPoseDetectionManager);
            poseScaleHistory = new FloatHistory(config.smoothTimeWindow);
            chestYPositionHistory = new FloatHistory(config.smoothTimeWindow);
            bodyPoseDetectionManager.captureAspectNormalizedDetection += HandleNewDetection;

            // Set default value (no zooming at the beginning)
            PlayAreaInAspectNormalizedSpace = DetectionUtils.AspectNormalizedFrameRect;

            StartRefreshPlayAreaLoop(destroyCancellationToken).SuppressCancellationThrow().Forget();
        }

        void HandleNewDetection(BodyPoseDetectionResult result)
        {
            var poseDetection = result.original;
            frameSize = poseDetection.frameSize;
            UpdatePoseDataHistory(poseDetection);
        }

        async UniTask StartRefreshPlayAreaLoop(CancellationToken token)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(config.firstRefreshInterval), cancellationToken: token);
            RefreshPlayArea();

            await foreach (var _ in UniTaskAsyncEnumerable.Interval(TimeSpan.FromSeconds(config.refreshInterval)).WithCancellation(token))
            {
                RefreshPlayArea();
            }
        }

        #endregion

        #region Play Area Logic

        void UpdatePoseDataHistory(BodyPoseDetection poseDetection)
        {
            // Need to update time for history to trigger history clean up.
            poseScaleHistory.UpdateCurrentFrameTime(Time.unscaledTime);
            chestYPositionHistory.UpdateCurrentFrameTime(Time.unscaledTime);

            var refPixelPerInch = poseSpaceSize.y / config.refInches;
            var poseScaleWeightedSum = 0f;
            var poseScaleTotalWeight = 0f;
            var chestYPositionWeightedSum = 0f;
            var chestYPositionTotalWeight = 0f;


            for (var poseIndex = 0; poseIndex < numOfPlayers; poseIndex++)
            {
                var playerPose = poseDetection.GetPlayerPose(poseIndex);
                if (playerPose?.bodyPose == null)
                {
                    continue;
                }

                var pose = playerPose.bodyPose;
                var ppi = pose.pixelsPerInch;
                var chest = pose.Chest();
                if (ppi == 0 || !chest.isDetected)
                {
                    continue;
                }

                // Update weighted sum
                var poseScale = pose.pixelsPerInch / refPixelPerInch;
                var chestDiffFromCentre = chest.x - poseSpaceSize.x / 2;
                var poseScaleWeight = (float)BasicUtils.Gaussian(chestDiffFromCentre, config.poseScaleSigma);
                poseScaleWeightedSum += poseScale * poseScaleWeight;

                var chestYPositionWeight = (float)BasicUtils.Gaussian(chestDiffFromCentre, config.chestYPositionSigma);
                chestYPositionWeightedSum += chest.y * chestYPositionWeight;

                poseScaleTotalWeight += poseScaleWeight;
                chestYPositionTotalWeight += chestYPositionWeight;
            }

            if (poseScaleTotalWeight > 0 && chestYPositionTotalWeight > 0)
            {
                poseScaleHistory.Add(poseScaleWeightedSum / poseScaleTotalWeight, Time.unscaledTime);
                chestYPositionHistory.Add(chestYPositionWeightedSum / chestYPositionTotalWeight, Time.unscaledTime);
            }
        }

        Rect ComputePlayArea()
        {
            poseScaleWeightedAverage = poseScaleHistory.Average();
            chestYPositionWeightedAverage = chestYPositionHistory.Average();

            if (poseScaleWeightedAverage < config.minValidPoseScaleAverage ||
                poseScaleWeightedAverage > config.maxValidPoseScaleAverage ||
                chestYPositionWeightedAverage < config.minValidChestYPositionAverage ||
                chestYPositionWeightedAverage > config.maxValidChestYPositionAverage)
            {
                // History data is not good enough to compute a new PlayArea, return the existing one.
                return PlayAreaInAspectNormalizedSpace;
            }

            // We want to find a Rect such that, starting from the chestYPositionWeightedAverage
            // it will be topMarginInInches from top and bottomMarginInInches from bottom.
            var ppi = poseScaleWeightedAverage * config.refInches;
            var expectedRatio = config.aspectRatio;
            var fullArea = new Rect(Vector2.zero, poseSpaceSize);
            var w = poseSpaceSize.x;
            var h = poseSpaceSize.y;

            var y1 = chestYPositionWeightedAverage - ppi * config.bottomMarginInInches / frameSize.y;
            var y2 = chestYPositionWeightedAverage + ppi * config.topMarginInInches / frameSize.y;
            var marginTop = Math.Max(0, y1);
            var marginBottom = Math.Max(0, h - y2);

            var finalMinMarginTop = Math.Max(fullArea.yMin, marginTop);
            var finalMinMarginBottom = Math.Max(h - fullArea.yMax, marginBottom);

            // Assuming we always keep the x-coordinate to be at the center,
            // so we only calculate the y-coordinate and height, then infer the width by aspect ratio.
            var idealCropHeight = h - finalMinMarginTop - finalMinMarginBottom;
            var centerX = w / 2;
            var centerY = (finalMinMarginTop + h - finalMinMarginBottom) / 2;

            idealCropHeight = Math.Clamp(
                idealCropHeight,
                h * config.minPlayAreaToRawFrameRatio,
                h * config.maxPlayAreaToRawFrameRatio
            );
            var idealCropWidth = idealCropHeight * expectedRatio;

            // Convert the size into even number for better processing.
            idealCropHeight = idealCropHeight / 2 * 2;
            idealCropWidth = idealCropWidth / 2 * 2;

            // Shift center point if the bounding box is too left/right/top/bottom
            var leftOffset = centerX - idealCropWidth / 2;
            if (leftOffset < 0)
            {
                centerX += -leftOffset; // Move right.
            }

            var rightOffset = w - (centerX + idealCropWidth / 2);
            if (rightOffset < 0)
            {
                centerX += rightOffset; // Move left
            }

            var topOffset = centerY - idealCropHeight / 2;
            if (topOffset < 0)
            {
                centerY += -topOffset; // Move down
            }

            var bottomOffset = h - (centerY + idealCropHeight / 2);
            if (bottomOffset < 0)
            {
                centerY += bottomOffset; // Move up
            }

            return new Rect(
                centerX - idealCropWidth / 2,
                centerY - idealCropHeight / 2,
                idealCropWidth,
                idealCropHeight
            );
        }

        #endregion

        #region API

        public override Rect GetPlayAreaInNormalizedSpace()
        {
            return new Rect(
                PlayAreaInAspectNormalizedSpace.x / poseSpaceSize.x,
                PlayAreaInAspectNormalizedSpace.y / poseSpaceSize.y,
                PlayAreaInAspectNormalizedSpace.width / poseSpaceSize.x,
                PlayAreaInAspectNormalizedSpace.height / poseSpaceSize.y
            );
        }

        public override Rect GetPlayAreaInAspectNormalizedSpace()
        {
            return PlayAreaInAspectNormalizedSpace;
        }

        public override void RefreshPlayArea()
        {
            if (locked) return;

            var newPlayArea = ComputePlayArea();

            if (Math.Abs(newPlayArea.x - PlayAreaInAspectNormalizedSpace.x) > config.deadZone ||
                Math.Abs(newPlayArea.y - PlayAreaInAspectNormalizedSpace.y) > config.deadZone ||
                Math.Abs(newPlayArea.width - PlayAreaInAspectNormalizedSpace.width) > config.deadZone ||
                Math.Abs(newPlayArea.height - PlayAreaInAspectNormalizedSpace.height) > config.deadZone)
            {
                Debug.Log($"[WeightedAveragePlayAreaController] New Play Area = {newPlayArea}. poseScaleWeightedAverage = {poseScaleWeightedAverage}. chestYPositionWeightedAverage = {chestYPositionWeightedAverage}");
                PlayAreaInAspectNormalizedSpace = newPlayArea;
            }

            // Player tracking position may be polluted. Update it anyway.
            UpdateTrackingPosition();
        }

        public override void ForceSetPlayArea(Rect rect)
        {
            PlayAreaInAspectNormalizedSpace = rect;
            UpdateTrackingPosition();
        }

        #endregion

        [Button]
        public void DebugRefreshPlayArea()
        {
            RefreshPlayArea();
        }
    }
}
