#nullable enable

using System.Collections.Generic;
using System.Linq;
using Jazz;
using UnityEngine;

namespace Nex
{
    public class PlayerIndicatorsManager : MonoBehaviour
    {
        [SerializeField] PreviewFramePlayerIndicator playerIndicatorPrefab = null!;
        [SerializeField] float playerIndicatorSizeRatioToPreviewHeight = 0.15f;

        // ReSharper disable once NotAccessedField.Local
        int numOfPlayers;
        List<int> playerIndexList = null!;
        PreviewFrameBase previewFrame = null!;
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;

        public void Initialize(
            int aNumOfPlayers,
            List<int> aPlayerIndexList,
            PreviewFrameBase aPreviewFrame,
            BodyPoseDetectionManager aBodyPoseDetectionManager
        )
        {
            numOfPlayers = aNumOfPlayers;
            playerIndexList = aPlayerIndexList;
            previewFrame = aPreviewFrame;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
            InitializePlayerIndicators();
        }

        public void Initialize(
            int aNumOfPlayers,
            PreviewFrameBase aPreviewFrame,
            BodyPoseDetectionManager aBodyPoseDetectionManager
        )
        {
            List<int> indexList = Enumerable.Range(0, aNumOfPlayers).ToList();
            Initialize(
                aNumOfPlayers,
                indexList,
                aPreviewFrame,
                aBodyPoseDetectionManager);
        }

        void InitializePlayerIndicators()
        {
            foreach (var playerIndex in playerIndexList)
            {
                var indicator = Instantiate(playerIndicatorPrefab, previewFrame.transform);
                indicator.Initialize(
                    playerIndex,
                    bodyPoseDetectionManager,
                    previewFrame,
                    playerIndicatorSizeRatioToPreviewHeight);
            }
        }
    }
}
