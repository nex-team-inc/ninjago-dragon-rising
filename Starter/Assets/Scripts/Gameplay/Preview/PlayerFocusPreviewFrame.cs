using Cysharp.Threading.Tasks;
using DG.Tweening;
using Jazz;
using UnityEngine;
using UnityEngine.UI;

#nullable enable

namespace Nex
{
    public class PlayerFocusPreviewFrame : MonoBehaviour
    {
        [SerializeField] RawImage rawImage = null!;
        [SerializeField] CanvasGroup canvasGroup = null!;

        CvDetectionManager cvDetectionManager = null!;
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        PlayAreaController playAreaController = null!;

        Rect viewportFullRect;
        Rect previewRectInNormalizedSpace;
        Rect previewRectInWorldSpace;

        bool isFirstFrameReceived;
        int playerIndex;
        int numOfPlayers;

        #region Public

        public void Initialize(
            int aPlayerIndex,
            int aNumOfPlayers,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            PlayAreaController aPlayAreaController
        )
        {
            playerIndex = aPlayerIndex;
            numOfPlayers = aNumOfPlayers;

            cvDetectionManager = aCvDetectionManager;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
            playAreaController = aPlayAreaController;

            cvDetectionManager.captureCameraFrame += CvDetectionManagerOnCaptureCameraFrame;
            viewportFullRect = new Rect(0, 0, 1, 1);
            previewRectInNormalizedSpace = new Rect(0, 0, 1, 1);

            canvasGroup.alpha = 0;
        }

        public Rect PreviewRectInNormalizedSpace()
        {
            return previewRectInNormalizedSpace;
        }

        public Rect PreviewRectInWorldSpace()
        {
            return previewRectInWorldSpace;
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

            viewportFullRect = playAreaController.GetPlayAreaInNormalizedSpace();

            var playerCenterRatio = PlayerPositionDefinition.GetXRatioForPlayer(playerIndex, numOfPlayers);
            var playerWidthRatio = PlayerPositionDefinition.PlayerPreviewWidthRatio(numOfPlayers);

            previewRectInNormalizedSpace = PlayerRect(viewportFullRect, playerCenterRatio, playerWidthRatio);

            var rectTransform = GetComponent<RectTransform>();
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            previewRectInWorldSpace = new Rect(corners[0], corners[2] - corners[0]);

            rawImage.uvRect = FlipRectIfNeeded(previewRectInNormalizedSpace, viewportFullRect.x + viewportFullRect.width * 0.5f, isMirrored);
        }

        Rect PlayerRect(Rect fullRect, float playerRatio, float playerWidthRatio)
        {

            return new Rect(
                fullRect.x + fullRect.width * (playerRatio - playerWidthRatio * 0.5f),
                fullRect.y,
                fullRect.width * playerWidthRatio,
                fullRect.height
            );
        }

        static Rect FlipRectIfNeeded(Rect rect, float centerX, bool flip)
        {
            if (flip)
            {
                rect.x = 2 * centerX - rect.x;
                rect.width = -rect.width;
            }

            return rect;
        }

        #endregion
    }
}
