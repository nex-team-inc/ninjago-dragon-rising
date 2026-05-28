#nullable enable

using System;
using System.Collections.Generic;
using Jazz;
using Nex.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Nex
{
    // OnePlayerPhotoTracker needs tp be a prefab because IPreviewTextureHandler needs to be a unity object as of MDK 3.1.0
    public class OnePlayerPhotoTracker : MonoBehaviour, IPreviewTextureHandler
    {
        [Serializable]
        public class TrackedBodyNode
        {
            public BodyPose.NodeIndex trackedNodeIndex;
            public float zoomInFactor; // = 0.8f;
            public float topMarginInInches; // = 3.9f;
            public float bottomMarginInInches; // = 3.5f;
            public float leftMarginInInches; // = 3.7f;
            public float rightMarginInInches; // = 3.7f;


            public Rect curFaceCrop = new(0, 0, 0, 0);
            public Rect nativeZoomFaceCrop = new(0, 0, 0, 0);
            public readonly FloatHistory ppiHistory = new(3);
        }

        int playerIndex;
        readonly FloatHistory ppiHistory = new(3);
        readonly Vector2 normalizedFrameSize = new(16f / 9f, 1f);
        readonly List<TrackedBodyNode> trackedBodyNodes = new()
        {
            new()
            {
                trackedNodeIndex = BodyPose.NodeIndex.Nose,
                zoomInFactor = 0.8f,
                topMarginInInches = 3.9f,
                bottomMarginInInches = 3.5f,
                leftMarginInInches = 3.7f,
                rightMarginInInches = 3.7f,
            }
        };

        Texture2D? latestPhoto;
        Texture2D? previewImageTexture;

        BodyPoseDetectionManager bodyPoseDetectionManager;

        public event UnityAction<OnePlayerPhotoTracker>? PhotoUpdated;

        public void Initialize(
            int aPlayerIndex,
            BodyPoseDetectionManager aBodyPoseDetectionManager)
        {
            playerIndex = aPlayerIndex;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;

            aBodyPoseDetectionManager.processed.captureAspectNormalizedDetection +=
                BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection;
        }

        public int AddTrackedBodyNode(TrackedBodyNode trackedBodyNode)
        {
            trackedBodyNodes.Add(trackedBodyNode);
            return trackedBodyNodes.Count - 1;
        }

        public void CleanUp()
        {
            bodyPoseDetectionManager.processed.captureAspectNormalizedDetection -= BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection;
        }

        public void SetPreviewImageTexture(Texture2D? aPreviewImageTexture)
        {
            previewImageTexture = aPreviewImageTexture;
        }

        public PlayerPhotoData GetPlayerPhotoData(int trackedBodyNodeIndex)
        {
            var trackedBodyNode = trackedBodyNodes[trackedBodyNodeIndex];
            return new PlayerPhotoData(
                latestPhoto ? latestPhoto : previewImageTexture,
                latestPhoto != null ? new Rect(0, 0, 1, 1) : trackedBodyNode.curFaceCrop
            );
        }

        public void TakePhoto(int trackedBodyNodeIndex)
        {
            if (previewImageTexture == null)
            {
                return;
            }

            var trackedBodyNode = trackedBodyNodes[trackedBodyNodeIndex];
            var area = trackedBodyNode.curFaceCrop;
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
            foreach (var margins in trackedBodyNodes)
            {
                BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection(detectionResult, margins);
            }

            PhotoUpdated?.Invoke(this);
        }

        void BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection(BodyPoseDetectionResult detectionResult,
            TrackedBodyNode trackedBodyNode)
        {
            var detection = detectionResult.processed;
            var playerPose = detection.GetPlayerPose(playerIndex);
            var pose = playerPose?.bodyPose;

            if (pose != null)
            {
                ppiHistory.Add(pose.pixelsPerInch, Time.fixedTime);
                ppiHistory.UpdateCurrentFrameTime(Time.fixedTime);
                var ppi = ppiHistory.Average();

                var node = pose.GetNode(trackedBodyNode.trackedNodeIndex);

                if (pose.Nose().isDetected && ppi > 0)
                {
                    var nosePoint = node.ToVector2();
                    var curFaceCropInAspectNormFrameSpace = new Rect(
                        nosePoint.x - trackedBodyNode.leftMarginInInches * ppi,
                        nosePoint.y - trackedBodyNode.bottomMarginInInches * ppi,
                        (trackedBodyNode.leftMarginInInches + trackedBodyNode.rightMarginInInches) * ppi,
                        (trackedBodyNode.bottomMarginInInches + trackedBodyNode.topMarginInInches) * ppi);

                    trackedBodyNode.curFaceCrop =
                        RectUtils.FromFrameSpaceToNormalizedSpace(curFaceCropInAspectNormFrameSpace,
                            normalizedFrameSize);
                    trackedBodyNode.curFaceCrop =
                        RectUtils.GetIntersection(trackedBodyNode.curFaceCrop, new Rect(0, 0, 1, 1));
                }
            }

            PhotoUpdated?.Invoke(this);
        }

        public Rect GetPreviewRegion()
        {
            return trackedBodyNodes[0].curFaceCrop;
        }

        public void OnTextureUpdated(Texture2D newTexture, Rect newUV)
        {
            SetPreviewImageTexture(newTexture);

            trackedBodyNodes[0].nativeZoomFaceCrop = newUV;
        }
    }
}
