using Cysharp.Threading.Tasks;
using DG.Tweening;
using Jazz;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class AreaPreviewFrame : PreviewFrameBase
    {
        CvDetectionManager cvDetectionManager = null!;
        PlayAreaController playAreaController = null!;

        Rect playAreaRectInNormalizedSpace;
        Rect previewRectInNormalizedSpace;

        bool isFirstFrameReceived;

        #region Public

        public void Initialize(
            CvDetectionManager aCvDetectionManager,
            PlayAreaController aPlayAreaController
        )
        {
            cvDetectionManager = aCvDetectionManager;
            playAreaController = aPlayAreaController;

            cvDetectionManager.captureCameraFrame += CvDetectionManagerOnCaptureCameraFrame;
            playAreaRectInNormalizedSpace = new Rect(0, 0, 1, 1);
            previewRectInNormalizedSpace = new Rect(0, 0, 1, 1);

            canvasGroup.alpha = 0;
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

            rawImage.texture = frameInformation.texture;
            var isMirrored = frameInformation.shouldMirror;

            UpdatePreviewRectInWorldSpaceInfoIfNeeded();

            var rawFrameAspectRatio = frameInformation.texture.width / (float)frameInformation.texture.height;
            var previewWidthRatio = previewRectInWorldSpaceAspectRatio / rawFrameAspectRatio; // If preview is 16/9 (and raw is 16/9), then widthRatio = 1.

            playAreaRectInNormalizedSpace = playAreaController.GetPlayAreaInNormalizedSpace();

            previewRectInNormalizedSpace = CenterRect(playAreaRectInNormalizedSpace, previewWidthRatio);

            rawImage.uvRect = FlipRectIfNeeded(previewRectInNormalizedSpace, isMirrored);
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
