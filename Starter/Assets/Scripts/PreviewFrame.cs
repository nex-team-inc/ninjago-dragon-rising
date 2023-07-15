using Jazz;
using UnityEngine;
using UnityEngine.UI;

namespace Starter
{
    public class PreviewFrame : MonoBehaviour
    {
        [SerializeField] RawImage rawImage;

        CvDetectionManager cvDetectionManager;

        int playerIndex;
        int numOfPlayers;

        #region Public

        public void Initialize(
            int aPlayerIndex,
            int aNumOfPlayers,
            CvDetectionManager aCvDetectionManager
            )
        {
            cvDetectionManager = aCvDetectionManager;
            playerIndex = aPlayerIndex;
            numOfPlayers = aNumOfPlayers;

            cvDetectionManager.captureCameraFrame += CvDetectionManagerOnCaptureCameraFrame;
        }

        #endregion

        #region Event

        void CvDetectionManagerOnCaptureCameraFrame(FrameInformation frameInformation)
        {
            rawImage.rectTransform.localScale = GameObjectUtils.LocalScaleForMirror(rawImage.rectTransform.localScale, frameInformation.shouldMirror);
            rawImage.texture = frameInformation.texture;
            var isMirrored = frameInformation.shouldMirror;

            var playerRatio = 1f / numOfPlayers;
            rawImage.uvRect = new Rect((isMirrored ? numOfPlayers - 1 - playerIndex : playerIndex) * playerRatio, 0, playerRatio, 1);
        }

        #endregion
    }
}
