using Cysharp.Threading.Tasks;
using DG.Tweening;
using Jazz;
using NaughtyAttributes;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class AreaPreviewFrame : PreviewFrameBase, IPreviewTextureHandler
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

        public Rect GetPreviewRegion()
        {
            return previewRectInNormalizedSpace;
        }

        public void Initialize(
            CvDetectionManager aCvDetectionManager,
            BasePlayAreaController aPlayAreaController
        )
        {
            cvDetectionManager = aCvDetectionManager;
            playAreaController = aPlayAreaController;

            CvDetectionManager.previewController.AddPreviewTextureHandler(this);
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
            CvDetectionManager.previewController.RemovePreviewTextureHandler(this);
        }

        #endregion

        #region Event

        public void OnTextureUpdated(Texture2D newTexture, Rect newUV)
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

            SetTexture(newTexture);


            UpdatePreviewRectInWorldSpaceInfoIfNeeded();

            var rawFrameAspectRatio = CvDetectionManager.previewController.PreviewWidth / (float)CvDetectionManager.previewController.PreviewHeight;
            var previewWidthRatio = previewRectInWorldSpaceAspectRatio / rawFrameAspectRatio; // If preview is 16/9 (and raw is 16/9), then widthRatio = 1.

            playAreaRectInNormalizedSpace = playAreaController.GetPlayAreaInNormalizedSpace();

            previewRectInNormalizedSpace = CenterRect(playAreaRectInNormalizedSpace, previewWidthRatio);

            var rect = previewRectInNormalizedSpace;

            if (enableSmoothing && Time.timeSinceLevelLoad - startTime > enableSmoothingAfterPeriod)
            {
                var smoothedNewRect = new Rect(
                    Mathf.Lerp(rawImage.uvRect.x, rect.x, smoothFactor),
                    Mathf.Lerp(rawImage.uvRect.y, rect.y, smoothFactor),
                    Mathf.Lerp(rawImage.uvRect.width, rect.width, smoothFactor),
                    Mathf.Lerp(rawImage.uvRect.height, rect.height, smoothFactor)
                );
                rawImage.uvRect = smoothedNewRect;
            }
            else
            {
                rawImage.uvRect = rect;
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
