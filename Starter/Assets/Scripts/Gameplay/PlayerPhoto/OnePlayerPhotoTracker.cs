#nullable enable

using System;
using Jazz;
using Nex.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Nex
{
    public class OnePlayerPhotoTracker
    {
        const float zoomInFactor = 0.8f;
        const float faceTopMarginInInches = 3.9f / zoomInFactor;
        const float faceBottomMarginInInches = 3.5f / zoomInFactor;
        const float faceLeftMarginInInches = 3.7f / zoomInFactor;
        const float faceRightMarginInInches = 3.7f / zoomInFactor;

        readonly int playerIndex;
        readonly FloatHistory ppiHistory = new(3);
        readonly Vector2 normalizedFrameSize = new(16f / 9f, 1f);
        Texture2D? latestPhoto;
        Texture2D? previewImageTexture;
        Rect curFaceCrop = new(0, 0, 0, 0 );

        readonly BodyPoseDetectionManager bodyPoseDetectionManager;

        public event UnityAction<OnePlayerPhotoTracker>? PhotoUpdated;

        public OnePlayerPhotoTracker(
            int playerIndex,
            BodyPoseDetectionManager bodyPoseDetectionManager)
        {
            this.playerIndex = playerIndex;
            this.bodyPoseDetectionManager = bodyPoseDetectionManager;

            bodyPoseDetectionManager.processed.captureAspectNormalizedDetection += BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection;
        }

        public void CleanUp()
        {
            bodyPoseDetectionManager.processed.captureAspectNormalizedDetection -= BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection;
        }

        public void SetPreviewImageTexture(Texture2D? aPreviewImageTexture)
        {
            previewImageTexture = aPreviewImageTexture;
        }

        public PlayerPhotoData GetPlayerPhotoData()
        {
            return new PlayerPhotoData(
                latestPhoto ? latestPhoto : previewImageTexture,
                latestPhoto != null ? new Rect(0, 0, 1, 1) : curFaceCrop
            );
        }

        public void TakePhoto()
        {
            if (previewImageTexture == null)
            {
                return;
            }

            var area = curFaceCrop;
            var x = Mathf.RoundToInt(area.x * previewImageTexture.width);
            var y = Mathf.RoundToInt(area.y * previewImageTexture.height);
            var width = Mathf.RoundToInt(area.width * previewImageTexture.width);
            var height = Mathf.RoundToInt(area.height * previewImageTexture.height);
            x = Math.Clamp(x, 0, previewImageTexture.width - 1);
            y = Math.Clamp(y, 0, previewImageTexture.height - 1);
            width = Math.Min(previewImageTexture.width - x, width);
            height = Math.Min(previewImageTexture.height - y, height);
            if (width <= 0 || height <= 0) return;
            latestPhoto = new Texture2D(width, height, previewImageTexture.format, false, false);
            Graphics.CopyTexture(previewImageTexture, 0, 0, x, y, latestPhoto.width, latestPhoto.height, latestPhoto, 0, 0, 0,
                0);

            PhotoUpdated?.Invoke(this);
        }

        public void ClearPhoto()
        {
            latestPhoto = null;

            PhotoUpdated?.Invoke(this);
        }

        void BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection(BodyPoseDetectionResult detectionResult)
        {
            var detection = detectionResult.processed;
            var playerPose = detection.GetPlayerPose(playerIndex);
            var pose = playerPose?.bodyPose;

            if (pose != null)
            {
                ppiHistory.Add(pose.pixelsPerInch, Time.fixedTime);
                ppiHistory.UpdateCurrentFrameTime(Time.fixedTime);
                var ppi = ppiHistory.Average();

                if (pose.Nose().isDetected && ppi > 0)
                {
                    var nosePoint = pose.Nose().ToVector2();
                    var curFaceCropInAspectNormFrameSpace = new Rect(
                        nosePoint.x - faceLeftMarginInInches * ppi,
                        nosePoint.y - faceBottomMarginInInches * ppi,
                        (faceLeftMarginInInches + faceRightMarginInInches) * ppi,
                        (faceBottomMarginInInches + faceTopMarginInInches) * ppi);

                    curFaceCrop = RectUtils.FromFrameSpaceToNormalizedSpace(curFaceCropInAspectNormFrameSpace, normalizedFrameSize);
                    curFaceCrop = RectUtils.GetIntersection(curFaceCrop, new Rect(0, 0, 1, 1));
                }
            }

            PhotoUpdated?.Invoke(this);
        }
    }
}
