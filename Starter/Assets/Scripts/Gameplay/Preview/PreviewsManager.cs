#nullable enable

using Jazz;
using UnityEngine;
using UnityEngine.Serialization;

namespace Nex
{
    public class PreviewsManager : MonoBehaviour
    {
        [SerializeField] PreviewFrame previewFramePrefab = null!;
        [SerializeField] GameObject fullFramePreviewContainer = null!;
        [SerializeField] GameObject p1PreviewContainer = null!;
        [SerializeField] GameObject p2PreviewContainer = null!;

        #region Public

        public void Initialize(
            int aNumOfPlayers,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            PlayAreaController playAreaController
        )
        {
            // This only works for showing preview for individual players.
            for (var playerIndex = 0; playerIndex < aNumOfPlayers; playerIndex++)
            {
                var previewContainer = GetPreviewContainer(playerIndex, aNumOfPlayers);
                previewContainer.SetActive(true);

                var previewFrame = Instantiate(previewFramePrefab, previewContainer.transform);
                previewFrame.Initialize(playerIndex, aNumOfPlayers, aCvDetectionManager, aBodyPoseDetectionManager, playAreaController);
            }
        }

        #endregion

        #region Config

        GameObject GetPreviewContainer(int playerIndex, int numOfPlayers)
        {
            return numOfPlayers == 1 ? fullFramePreviewContainer :
                playerIndex == 0 ? p1PreviewContainer : p2PreviewContainer;
        }

        #endregion
    }
}
