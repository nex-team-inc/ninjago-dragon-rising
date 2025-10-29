using Cysharp.Threading.Tasks;
using DG.Tweening;
using Jazz;
using NaughtyAttributes;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class AreaPreviewFrame : PreviewFrameBase
    {
        [SerializeField] bool enableSmoothing;
        // 0 = No Update, 1 = No Smoothing
        [ShowIf("enableSmoothing"), Range(0, 1), SerializeField] float smoothFactor = 0.1f;
        [ShowIf("enableSmoothing"), SerializeField] float enableSmoothingAfterPeriod = 1f;

        CvDetectionManager cvDetectionManager = null!;
        BasePlayAreaController playAreaController = null!;

        Rect playAreaRectInNormalizedSpace;
        Rect previewRectInNormalizedSpace;

        bool isFirstFrameReceived;
        float startTime;

        #region Public

        public void Initialize(
            CvDetectionManager aCvDetectionManager,
            BasePlayAreaController aPlayAreaController
        )
        {
            cvDetectionManager = aCvDetectionManager;
            playAreaController = aPlayAreaController;

            cvDetectionManager.captureCameraFrame += CvDetectionManagerOnCaptureCameraFrame;
            playAreaRectInNormalizedSpace = new Rect(0, 0, 1, 1);
            previewRectInNormalizedSpace = new Rect(0, 0, 1, 1);

            canvasGroup.alpha = 0;

            startTime = Time.timeSinceLevelLoad;
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

            if (rawImage == null)
            {
                return;
            }

            SetTexture(frameInformation.texture);
            var isMirrored = frameInformation.shouldMirror;

            UpdatePreviewRectInWorldSpaceInfoIfNeeded();

            var rawFrameAspectRatio = frameInformation.texture.width / (float)frameInformation.texture.height;
            var previewWidthRatio = previewRectInWorldSpaceAspectRatio / rawFrameAspectRatio; // If preview is 16/9 (and raw is 16/9), then widthRatio = 1.

            playAreaRectInNormalizedSpace = playAreaController.GetPlayAreaInNormalizedSpace();

            previewRectInNormalizedSpace = CenterRect(playAreaRectInNormalizedSpace, previewWidthRatio);

            var newRect = FlipRectIfNeeded(previewRectInNormalizedSpace, isMirrored);

            if (enableSmoothing && Time.timeSinceLevelLoad - startTime > enableSmoothingAfterPeriod)
            {
                var smoothedNewRect = new Rect(
                    Mathf.Lerp(rawImage.uvRect.x, newRect.x, smoothFactor),
                    Mathf.Lerp(rawImage.uvRect.y, newRect.y, smoothFactor),
                    Mathf.Lerp(rawImage.uvRect.width, newRect.width, smoothFactor),
                    Mathf.Lerp(rawImage.uvRect.height, newRect.height, smoothFactor)
                );
                rawImage.uvRect = smoothedNewRect;
            }
            else
            {
                rawImage.uvRect = newRect;
            }
        }

        void SetTexture(Texture texture)
        {
            if (rawImage != null && isFirstFrameReceived)
            {
                rawImage.texture = texture;
            }
        }

        Rect CenterRect(Rect fullRect, float previewWidthRatio)
        {
            return new Rect(
                fullRect.x + fullRect.width * (0.5f - previewWidthRatio * 0.5f),
                fullRect.y,
                fullRect.width * previewWidthRatio,
                fullRect.height
            );
        }


        #endregion
    }
}
