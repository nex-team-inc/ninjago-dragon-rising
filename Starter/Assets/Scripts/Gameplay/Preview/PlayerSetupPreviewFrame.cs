using Cysharp.Threading.Tasks;
using DG.Tweening;
using Jazz;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class PlayerSetupPreviewFrame : PreviewFrameBase, IPreviewTextureHandler
    {
        CvDetectionManager cvDetectionManager = null!;
        // ReSharper disable once NotAccessedField.Local
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        BasePlayAreaController playAreaController = null!;

        Rect playAreaRectInNormalizedSpace;
        Rect previewRectInNormalizedSpace;

        Rect previewFrameRect;

        bool isFirstFrameReceived;
        int playerIndex;
        int numOfPlayers;

        #region Public

        public void Initialize(
            int aPlayerIndex,
            int aNumOfPlayers,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            BasePlayAreaController aPlayAreaController
        )
        {
            playerIndex = aPlayerIndex;
            numOfPlayers = aNumOfPlayers;

            cvDetectionManager = aCvDetectionManager;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
            playAreaController = aPlayAreaController;

            CvDetectionManager.previewController.AddPreviewTextureHandler(this);
            playAreaRectInNormalizedSpace = new Rect(0, 0, 1, 1);
            previewRectInNormalizedSpace = new Rect(0, 0, 1, 1);

            canvasGroup.alpha = 0;
        }

        public override Rect PreviewRectInNormalizedSpace()
        {
            return previewRectInNormalizedSpace;
        }

        public Rect GetPreviewRegion()
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

            rawImage.texture = newTexture;

            UpdatePreviewRectInWorldSpaceInfoIfNeeded();

            var rawFrameAspectRatio = CvDetectionManager.previewController.PreviewWidth / (float)CvDetectionManager.previewController.PreviewHeight;
            var previewWidthRatio = previewRectInWorldSpaceAspectRatio / rawFrameAspectRatio; // If preview is 16/9 (and raw is 16/9), then widthRatio = 1.

            playAreaRectInNormalizedSpace = playAreaController.GetPlayAreaInNormalizedSpace();

            var playerCenterXRatio = PlayerPositionDefinition.GetXRatioForPlayer(playerIndex, numOfPlayers);
            previewRectInNormalizedSpace = PlayerRect(playAreaRectInNormalizedSpace, playerCenterXRatio, previewWidthRatio);

            rawImage.uvRect = newUV;
        }

        Rect PlayerRect(Rect fullRect, float playerXRatio, float previewWidthRatio)
        {
            return new Rect(
                fullRect.x + fullRect.width * (playerXRatio - previewWidthRatio * 0.5f),
                fullRect.y,
                fullRect.width * previewWidthRatio,
                fullRect.height
            );
        }

        #endregion
    }
}
