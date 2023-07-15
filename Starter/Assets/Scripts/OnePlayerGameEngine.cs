using System;
using Jazz;
using UnityEngine;

namespace Starter
{
    public class OnePlayerGameEngine : MonoBehaviour
    {
        [SerializeField] OnePlayerDetectionEngine detectionEngine;
        [SerializeField] RectTransform referenceTransform;
        [SerializeField] PreviewFrame previewFrame;

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
