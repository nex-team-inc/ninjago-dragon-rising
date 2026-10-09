#nullable enable

using System;
using Jazz;
using NaughtyAttributes;
using UnityEngine;
using Rect = UnityEngine.Rect;

namespace Nex
{
     public class PlayAreaController : BasePlayAreaController
     {
         [Serializable]
         public class Config
         {
             public float smoothTimeWindow = 1f;
             public float leftMarginInInches = 32;
             public float rightMarginInInches = 32;
             public float topMarginInInches = 24;
             public float bottomMarginInInches = 34;
             public float minPlayAreaToRawFrameRatio = 0.3f;
             public float maxPlayAreaToRawFrameRatio = 1f;
             public float deadZone;
             public float aspectRatio = 16f / 9f;
         }

         // Controllers
         [SerializeField] Config config = null!;

         // States
         readonly Vector2 poseSpaceSize = DetectionUtils.AspectNormalizedFrameSize;
         FloatHistory minMarginLeftHistory = null!;
         FloatHistory minMarginRightHistory = null!;
         FloatHistory minMarginTopHistory = null!;
         FloatHistory minMarginBottomHistory = null!;

         [ShowNativeProperty] Rect PlayAreaInAspectNormalizedSpace { get; set; } = new(0, 0, 1, 1);

         [ShowNativeProperty] int totalStableFrameCount { get; set; }

         #region Public

         public override void Initialize(
             int aNumOfPlayers,
             CvDetectionManager aCvDetectionManager,
             BodyPoseDetectionManager aBodyPoseDetectionManager
             )
         {
             base.Initialize(aNumOfPlayers, aCvDetectionManager, aBodyPoseDetectionManager);
             minMarginLeftHistory = new FloatHistory(config.smoothTimeWindow);
             minMarginRightHistory = new FloatHistory(config.smoothTimeWindow);
             minMarginTopHistory = new FloatHistory(config.smoothTimeWindow);
             minMarginBottomHistory = new FloatHistory(config.smoothTimeWindow);
             bodyPoseDetectionManager.captureAspectNormalizedDetection += HandleNewDetection;
         }

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

         public Rect GetPlayAreaInFrameSpace()
         {
             var rawFrameSize = cvDetectionManager.GetFrameProvider().GetRawFrameSize();
             var aspectNormalizedFactor = (float)(rawFrameSize.height > rawFrameSize.width ? rawFrameSize.width : rawFrameSize.height);
             return new Rect(
                 (PlayAreaInAspectNormalizedSpace.x + 0.5f) * aspectNormalizedFactor,
                 (PlayAreaInAspectNormalizedSpace.y + 0.5f) * aspectNormalizedFactor,
                 PlayAreaInAspectNormalizedSpace.width * aspectNormalizedFactor,
                 PlayAreaInAspectNormalizedSpace.height * aspectNormalizedFactor);
         }

         void OnDestroy()
         {
             if (bodyPoseDetectionManager) bodyPoseDetectionManager.captureAspectNormalizedDetection -= HandleNewDetection;
         }

         #endregion

         #region Helper

         public void ClearTargetMarginHistory()
         {
             minMarginLeftHistory.Clear();
             minMarginRightHistory.Clear();
             minMarginTopHistory.Clear();
             minMarginBottomHistory.Clear();
         }

         void UpdateTargetMarginHistory(BodyPoseDetection poseDetection)
         {
             var w = poseSpaceSize.x;
             var h = poseSpaceSize.y;

             // Need to update time for history to trigger history clean up.
             minMarginLeftHistory.UpdateCurrentFrameTime(Time.unscaledTime);
             minMarginRightHistory.UpdateCurrentFrameTime(Time.unscaledTime);
             minMarginTopHistory.UpdateCurrentFrameTime(Time.unscaledTime);
             minMarginBottomHistory.UpdateCurrentFrameTime(Time.unscaledTime);

             var minMarginLeft = w;
             var minMarginRight = w;
             var minMarginTop = h;
             var minMarginBottom = h;

             var hasValidMargins = false;

             // For every player (single player or 2-player), find its min bounding boxes.
             for (var poseIndex = 0; poseIndex < numOfPlayers; poseIndex++)
             {
                 var playerPose = poseDetection.GetPlayerPose(poseIndex);
                 if (playerPose?.bodyPose == null)
                 {
                     continue;
                 }

                 var pose = playerPose.bodyPose;

                 var chest = pose.Chest();
                 var ppi = pose.pixelsPerInch;

                 if (ppi == 0)
                 {
                     continue;
                 }

                 // Define a range
                 var x1 = chest.x - ppi * config.leftMarginInInches;
                 var x2 = chest.x + ppi * config.rightMarginInInches;
                 var y1 = chest.y - ppi * config.bottomMarginInInches;
                 var y2 = chest.y + ppi * config.topMarginInInches;

                 // The margin to 4 edges.
                 var marginLeft = Math.Max(0, x1);
                 var marginRight = Math.Max(0, w - x2);
                 var marginTop = Math.Max(0, y1);
                 var marginBottom = Math.Max(0, h - y2);

                 if (ShouldAnchorXAtCenter())
                 {
                     marginRight = marginLeft = Math.Min(marginRight, marginLeft);
                 }

                 // Find the min margins (which defines the max bounding boxes)
                 // For understanding: if all margins are 0, it means we want to use the full frame as ROI.
                 minMarginLeft = Math.Min(minMarginLeft, marginLeft);
                 minMarginRight = Math.Min(minMarginRight, marginRight);
                 minMarginTop = Math.Min(minMarginTop, marginTop);
                 minMarginBottom = Math.Min(minMarginBottom, marginBottom);

                 hasValidMargins = true;
             }

             if (hasValidMargins)
             {
                 // The histories are for smoothing.
                 // PPI is always jumping and making the margins unstable.
                 // Smoothing the margins can help making the cropped process frame and detections more stable.
                 minMarginLeftHistory.Add(minMarginLeft, Time.unscaledTime);
                 minMarginRightHistory.Add(minMarginRight, Time.unscaledTime);
                 minMarginTopHistory.Add(minMarginTop, Time.unscaledTime);
                 minMarginBottomHistory.Add(minMarginBottom, Time.unscaledTime);
             }
         }

         Rect ComputePlayArea()
         {
             var expectedRatio = config.aspectRatio;
             var fullArea = new Rect(Vector2.zero, poseSpaceSize);

             // NOTE: what we want to do below is:
             // - Based on a smoothed left/right/top/bottom margins (define the tracking target's range)
             // - Find a ROI of 16:9 ratio, inside the raw frame range (no black padding)
             // - The ROI should include the tracking target's range, in other words, has smaller left/right/top/bottom margins.

             var w = poseSpaceSize.x;
             var h = poseSpaceSize.y;

             var finalMinMarginLeft = Math.Max(fullArea.xMin, minMarginLeftHistory.Average());
             var finalMinMarginRight = Math.Max(w - fullArea.xMax, minMarginRightHistory.Average());
             var finalMinMarginTop = Math.Max(fullArea.yMin, minMarginTopHistory.Average());
             var finalMinMarginBottom = Math.Max(h - fullArea.yMax, minMarginBottomHistory.Average());

             // Now, determine the crop size and center.
             // We depends on the bigger edge to determine a bigger size.
             var idealCropWidth = w - finalMinMarginLeft - finalMinMarginRight;
             var idealCropHeight = h - finalMinMarginTop - finalMinMarginBottom;
             var centerX = (finalMinMarginLeft + w - finalMinMarginRight) / 2;
             var centerY = (finalMinMarginTop + h - finalMinMarginBottom) / 2;

             idealCropHeight = Math.Clamp(idealCropHeight, (h * config.minPlayAreaToRawFrameRatio), (h * config.maxPlayAreaToRawFrameRatio));
             idealCropWidth = Math.Clamp(idealCropWidth, (w * config.minPlayAreaToRawFrameRatio), (w * config.maxPlayAreaToRawFrameRatio));

             idealCropHeight = Math.Max(idealCropHeight, (idealCropWidth / expectedRatio));
             idealCropWidth = Math.Max(idealCropWidth, (idealCropHeight * expectedRatio));

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

         void HandleNewDetection(BodyPoseDetectionResult result)
         {
             var poseDetection = result.original;

             UpdateTargetMarginHistory(poseDetection);

             if (!locked)
             {
                 var newPlayArea = ComputePlayArea();

                 // Update final rect only if the differences are significant.
                 if (Math.Abs(newPlayArea.x - PlayAreaInAspectNormalizedSpace.x) > config.deadZone ||
                     Math.Abs(newPlayArea.y - PlayAreaInAspectNormalizedSpace.y) > config.deadZone ||
                     Math.Abs(newPlayArea.width - PlayAreaInAspectNormalizedSpace.width) > config.deadZone ||
                     Math.Abs(newPlayArea.height - PlayAreaInAspectNormalizedSpace.height) > config.deadZone)
                 {
                     PlayAreaInAspectNormalizedSpace = newPlayArea;
                     UpdateTrackingPosition();
                 }
                 else
                 {
                     totalStableFrameCount++;
                 }
             }
         }

         bool ShouldAnchorXAtCenter()
         {
             return true;
         }

         public override void ForceSetPlayArea(Rect rect)
         {
             PlayAreaInAspectNormalizedSpace = rect;
             UpdateTrackingPosition();
         }

         public override void RefreshPlayArea()
         {
             if (locked) return;

             var newPlayArea = ComputePlayArea();
             PlayAreaInAspectNormalizedSpace = newPlayArea;
             UpdateTrackingPosition();
         }

         #endregion
     }
}
