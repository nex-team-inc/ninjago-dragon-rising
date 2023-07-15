using Jazz;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class OnePlayerGameEngine : MonoBehaviour
    {
        [SerializeField] OnePlayerDetectionEngine detectionEngine = null!;
        [SerializeField] RectTransform referenceTransform = null!;
        [SerializeField] PreviewFrame previewFrame = null!;

        int playerIndex;
        int numOfPlayers;

        #region Public

        public void Initialize(
            int aPlayerIndex,
            int aNumOfPlayers,
            CvDetectionManager cvDetectionManager,
            BodyPoseDetectionManager bodyPoseDetectionManager
        )
        {
            playerIndex = aPlayerIndex;
            numOfPlayers = aNumOfPlayers;

            detectionEngine.Initialize(playerIndex, numOfPlayers, bodyPoseDetectionManager, referenceTransform);
            detectionEngine.gameObject.SetActive(true);

            previewFrame.Initialize(playerIndex, numOfPlayers, cvDetectionManager);
            previewFrame.gameObject.SetActive(true);
        }

        #endregion
    }
}
