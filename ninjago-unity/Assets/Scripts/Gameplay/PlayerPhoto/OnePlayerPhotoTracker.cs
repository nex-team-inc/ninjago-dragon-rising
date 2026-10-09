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
            public NodeRotation? nodeRotation;
            public float zoomInFactor; // = 0.8f;
            public float topMarginInInches; // = 3.9f;
            public float bottomMarginInInches; // = 3.5f;
            public float leftMarginInInches; // = 3.7f;
            public float rightMarginInInches; // = 3.7f;

            public Vector3 rotationVector = Vector3.zero;
            public Rect curFaceCrop = new(0, 0, 0, 0);
            public Rect nativeZoomFaceCrop = new(0, 0, 0, 0);
            public readonly FloatHistory PpiHistory = new(3);
        }

        [Serializable]
        public class NodeRotation
        {
            public BodyPose.NodeIndex nodeIndex1;
            public BodyPose.NodeIndex nodeIndex2;

            public NodeRotation(BodyPose.NodeIndex aNodeIndex1, BodyPose.NodeIndex aNodeIndex2)
            {
                nodeIndex1 = aNodeIndex1;
                nodeIndex2 = aNodeIndex2;
            }
        }

        int playerIndex;
        readonly FloatHistory ppiHistory = new(3);
        readonly Vector2 normalizedFrameSize = new(16f / 9f, 1f);
        readonly List<TrackedBodyNode> trackedBodyNodes = new()
        {
            new()
            {
                trackedNodeIndex = BodyPose.NodeIndex.Nose,
                nodeRotation = new NodeRotation(BodyPose.NodeIndex.Nose, BodyPose.NodeIndex.Nose),
                zoomInFactor = 0.8f,
                topMarginInInches = 3.9f,
                bottomMarginInInches = 3.5f,
                leftMarginInInches = 3.7f,
                rightMarginInInches = 3.7f,
            }
        };

        Texture2D? latestPhoto;
        Texture2D? previewImageTexture;

        BodyPoseDetectionManager? bodyPoseDetectionManager;

        public event UnityAction<OnePlayerPhotoTracker>? PhotoUpdated;

        public void Initialize(
            int aPlayerIndex,
            BodyPoseDetectionManager aBodyPoseDetectionManager)
        {
            playerIndex = aPlayerIndex;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;

            bodyPoseDetectionManager.processed.captureAspectNormalizedDetection +=
                BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection;
        }

        public int AddTrackedBodyNode(TrackedBodyNode trackedBodyNode)
        {
            trackedBodyNodes.Add(trackedBodyNode);
            return trackedBodyNodes.Count - 1;
        }

        public void CleanUp()
        {
           if(bodyPoseDetectionManager) bodyPoseDetectionManager.processed.captureAspectNormalizedDetection -= BodyPoseDetectionManagerOnCaptureAspectNormalizedDetection;
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
            var degree = trackedBodyNode.rotationVector.z;
            var newXY = GetRotatedPoint(new Vector2(x, y) + new Vector2(width, height) / 2f, previewImageTexture.width, previewImageTexture.height, -degree) - new Vector2Int(width, height) / 2;
            previewImageTexture = RotateAndCrop(previewImageTexture, degree, previewImageTexture.width, previewImageTexture.height);
            latestPhoto = new Texture2D(width, height, previewImageTexture.format, false, false);
            Graphics.CopyTexture(previewImageTexture, 0, 0, newXY.x, newXY.y, width, height, latestPhoto, 0, 0, 0,
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
                    var nodePoint = node.ToVector2();
                    var curFaceCropInAspectNormFrameSpace = new Rect(
                        nodePoint.x - trackedBodyNode.leftMarginInInches * ppi,
                        nodePoint.y - trackedBodyNode.bottomMarginInInches * ppi,
                        (trackedBodyNode.leftMarginInInches + trackedBodyNode.rightMarginInInches) * ppi,
                        (trackedBodyNode.bottomMarginInInches + trackedBodyNode.topMarginInInches) * ppi);

                    if (trackedBodyNode.nodeRotation == null || trackedBodyNode.nodeRotation.nodeIndex1 == trackedBodyNode.nodeRotation.nodeIndex2)
                    {
                        trackedBodyNode.rotationVector = Vector3.zero;
                    }
                    else
                    {
                        var nodePoint1 = pose.GetNode(trackedBodyNode.nodeRotation.nodeIndex1).ToVector2();
                        var nodePoint2 = pose.GetNode(trackedBodyNode.nodeRotation.nodeIndex2).ToVector2();
                        var rotationVec = nodePoint2 - nodePoint1;
                        trackedBodyNode.rotationVector = new Vector3(0, 0, Vector2.SignedAngle(Vector2.up, rotationVec));
                    }

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

        //Drop Texture with rotation
        public Texture2D RotateAndCrop(Texture2D source, float angleDegrees, int targetWidth, int targetHeight)
        {
            var result = new Texture2D(targetWidth, targetHeight);
            var angleRad = angleDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(angleRad);
            var sin = Mathf.Sin(angleRad);

            // Center of the target image
            var targetCenter = new Vector2(targetWidth * 0.5f, targetHeight * 0.5f);
            // Center of the source image (in normalized 0-1 UV space)
            var sourceCenter = new Vector2(0.5f, 0.5f);

            for (var y = 0; y < targetHeight; y++)
            {
                for (var x = 0; x < targetWidth; x++)
                {
                    // 1. Shift to center
                    var tx = (x - targetCenter.x);
                    var ty = (y - targetCenter.y);

                    // 2. Rotate
                    var rx = tx * cos - ty * sin;
                    var ry = tx * sin + ty * cos;

                    // 3. Convert back to UV space (0 to 1)
                    // Divide by source dimensions if you want the crop to scale with original size
                    var u = (rx / source.width) + sourceCenter.x;
                    var v = (ry / source.height) + sourceCenter.y;

                    // 4. Sample and Set
                    result.SetPixel(x, y, source.GetPixelBilinear(u, v));
                }
            }
            result.Apply(); //
            return result;
        }

        //Rotate a point in a rect
        public Vector2Int GetRotatedPoint(Vector2 point, float width, float height, float angleDegrees)
        {
            var center = new Vector2(width * 0.5f, height * 0.5f);

            // Create a rotation quaternion around the Z axis
            var rotation = Quaternion.Euler(0, 0, angleDegrees);

            // Shift point to center, rotate it, and shift it back
            var shiftedPoint = point - center;
            Vector2 rotatedPoint = rotation * shiftedPoint;

            var result = rotatedPoint + center;

            return new Vector2Int(Mathf.FloorToInt(result.x), Mathf.FloorToInt(result.y));
        }
    }
}
