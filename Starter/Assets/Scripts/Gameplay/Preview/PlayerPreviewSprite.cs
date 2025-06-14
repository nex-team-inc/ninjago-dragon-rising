#nullable enable

using System;
using Jazz;
using UnityEngine;

namespace Nex
{
    public class PlayerPreviewSprite : MonoBehaviour
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

        enum CenterNodeType
        {
            Chest = 0,
            Nose = 1,
        }

        [SerializeField] Margins marginsInInches = new(24, 34, 32, 32);
        [SerializeField] SpriteRenderer spriteRenderer = null!;
        [SerializeField] float playerSpriteAspectRatio = 1;
        [SerializeField] CenterNodeType centerNodeType = CenterNodeType.Chest;
        [SerializeField] float oneEuroFilterMinCutoff = 4;
        [SerializeField] float oneEuroFilterBeta = 10;

        readonly FloatHistory ppiHistory = new(0.5f);
        Sprite? baseSprite;

        CvDetectionManager cvDetectionManager = null!;
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        int playerIndex;

        ComposedFilter2D<OneEuroFilter> centerFilter = null!;

        Rect previewRectInNormalizedSpace;

        void OnDestroy()
        {
            if (baseSprite != null)
            {
                Destroy(baseSprite);
            }
        }

        public void Initialize(
            int aPlayerIndex,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager)
        {
            playerIndex = aPlayerIndex;
            cvDetectionManager = aCvDetectionManager;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;

            cvDetectionManager.captureCameraFrame += CvDetectionManagerOnCaptureCameraFrame;
            bodyPoseDetectionManager.captureAspectNormalizedDetection += BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection;

            // ReSharper disable once RedundantArgumentDefaultValue
            centerFilter = new ComposedFilter2D<OneEuroFilter>(
                new OneEuroFilter(oneEuroFilterMinCutoff, oneEuroFilterBeta),
                new OneEuroFilter(oneEuroFilterMinCutoff, oneEuroFilterBeta)
            );
        }

        void UpdateSprite(Texture sourceTexture)
        {
            if (previewRectInNormalizedSpace == Rect.zero)
            {
                return;
            }

            if (sourceTexture is Texture2D texture2D)
            {
                // Clean up old sprite
                if (baseSprite != null)
                {
                    Destroy(baseSprite);
                }

                // Create new sprite directly from the source texture
                var rect = ComputeTextureRect(texture2D, previewRectInNormalizedSpace);
                baseSprite = Sprite.Create(
                    texture2D,
                    rect,
                    new Vector2(0.5f, 0.5f),
                    rect.height
                );
                spriteRenderer.sprite = baseSprite;
            }
            else
            {
                Debug.LogError("sourceTexture is not Texture2D");
            }
        }

        Rect ComputeTextureRect(Texture2D texture2D, Rect normalizedRect)
        {
            var x = normalizedRect.x * texture2D.width;
            var y = normalizedRect.y * texture2D.height;

            return new Rect(
                x,
                y,
                Mathf.Min(normalizedRect.width * texture2D.width, texture2D.width - x),
                Mathf.Min(normalizedRect.height * texture2D.height, texture2D.height - y)
            );
        }

        void CvDetectionManagerOnCaptureCameraFrame(FrameInformation frameInformation)
        {
            UpdateSprite(frameInformation.texture);
        }

        void BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection(BodyPoseDetectionResult bodyPoseDetectionResult)
        {
            var poseDetection = bodyPoseDetectionResult.original;
            var playerPose = poseDetection.GetPlayerPose(playerIndex);
            var pose = playerPose?.bodyPose;
            if (pose == null)
            {
                previewRectInNormalizedSpace = Rect.zero;
                return;
            }

            ppiHistory.Add(pose.pixelsPerInch, Time.fixedTime);
            ppiHistory.UpdateCurrentFrameTime(Time.fixedTime);
            var ppi = ppiHistory.Average();

            var fullFrameRect = DetectionUtils.AspectNormalizedFrameRect;
            var centerNode = centerNodeType switch
            {
                CenterNodeType.Chest => pose.Chest(),
                CenterNodeType.Nose => pose.Nose(),
                _ => throw new ArgumentOutOfRangeException()
            };

            var rawCenterPt = centerNode.ToVector2();
            var centerPt = centerFilter.Filter(rawCenterPt.x, rawCenterPt.y);

            var centerX = centerPt.x + ppi * 0.5f * (marginsInInches.right - marginsInInches.left);
            var centerY = centerPt.y + ppi * 0.5f * (marginsInInches.top - marginsInInches.bottom);

            var rectWidth = ppi * (marginsInInches.right + marginsInInches.left);
            var rectHeight = ppi * (marginsInInches.top + marginsInInches.bottom);

            rectWidth = Math.Max(rectWidth, rectHeight * playerSpriteAspectRatio);
            rectHeight = Math.Max(rectHeight, rectWidth / playerSpriteAspectRatio);

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
    }
}
