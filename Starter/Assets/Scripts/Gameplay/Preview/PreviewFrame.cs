using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Jazz;
using UnityEngine;
using UnityEngine.UI;

#nullable enable

namespace Nex
{
    public class PreviewFrame : MonoBehaviour
    {
        public enum DisplayMode
        {
            Full = 0,
            LeftHalf = 1,
            RightHalf = 2,
            CenterHalf = 3
        }

        [SerializeField] RawImage rawImage = null!;
        [SerializeField] CanvasGroup canvasGroup = null!;

        CvDetectionManager cvDetectionManager = null!;
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;

        Rect viewportFullRect;
        bool isViewportLocked;
        bool isFirstFrameReceived;
        DisplayMode displayMode;

        #region Public

        public void Initialize(
            DisplayMode aDisplayMode,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager
            )
        {
            displayMode = aDisplayMode;

            cvDetectionManager = aCvDetectionManager;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;

            cvDetectionManager.captureCameraFrame += CvDetectionManagerOnCaptureCameraFrame;
            viewportFullRect = new Rect(0, 0, 1, 1);

            canvasGroup.alpha = 0;
        }

        public void SetViewportLocked(bool shouldLock)
        {
            isViewportLocked = shouldLock;

            var autoTiltValue = shouldLock
                ? ContinuousAutoTiltMode.Off
                : ContinuousAutoTiltMode.Recovery;
            CvDetectionManager.dewarpController.continuousAutoTiltMode = autoTiltValue;
            cvDetectionManager.dynamicDewarpConfig.continuousAutoTiltMode = autoTiltValue;
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

            rawImage.rectTransform.localScale = GameObjectUtils.LocalScaleForMirror(rawImage.rectTransform.localScale, frameInformation.shouldMirror);
            rawImage.texture = frameInformation.texture;
            var isMirrored = frameInformation.shouldMirror;

            if (!isViewportLocked)
            {
                // Change view port.
                if (bodyPoseDetectionManager.latestBodyPoseDetection != null)
                {
                    // Now we use
                    var lastBodyDetection = bodyPoseDetectionManager.latestBodyPoseDetection;
                    var rawFrameSize = lastBodyDetection.frameSize;
                    var processFrameCrop = lastBodyDetection.GetProcessFrameTransformInfo().processFrameCrop;

                    viewportFullRect = new Rect(
                        processFrameCrop.x / rawFrameSize.x,
                        processFrameCrop.y / rawFrameSize.y,
                        processFrameCrop.width / rawFrameSize.x,
                        processFrameCrop.height / rawFrameSize.y);
                }
                else
                {
                    viewportFullRect = new Rect(0, 0, 1, 1);
                }
            }

            var centerHalfRect = new Rect(
                viewportFullRect.x + viewportFullRect.width * 0.25f,
                viewportFullRect.y,
                viewportFullRect.width * 0.5f,
                viewportFullRect.height);

            var leftHalfRatioRect = new Rect(
                viewportFullRect.x,
                viewportFullRect.y,
                viewportFullRect.width * 0.5f,
                viewportFullRect.height);

            var rightHalfRatioRect = new Rect(
                viewportFullRect.x + viewportFullRect.width * 0.5f,
                viewportFullRect.y,
                viewportFullRect.width * 0.5f,
                viewportFullRect.height);

            rawImage.uvRect = displayMode switch
            {
                // ReSharper disable once UnreachableSwitchArmDueToIntegerAnalysis
                DisplayMode.Full => viewportFullRect,
                DisplayMode.LeftHalf => isMirrored ? rightHalfRatioRect : leftHalfRatioRect,
                DisplayMode.RightHalf => isMirrored ? leftHalfRatioRect : rightHalfRatioRect,
                // ReSharper disable once UnreachableSwitchArmDueToIntegerAnalysis
                DisplayMode.CenterHalf => centerHalfRect,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        #endregion
    }
}
