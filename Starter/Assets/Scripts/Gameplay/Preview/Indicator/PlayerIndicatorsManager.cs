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
        readonly List<PreviewFramePlayerIndicator> indicators = new();

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

        /// <summary>Highlights the active shooter's indicator and dims the others; -1 clears every highlight.</summary>
        public void SetActivePlayer(int activePlayerIndex)
        {
            foreach (var indicator in indicators)
            {
                indicator.SetHighlighted(indicator.PlayerIndex == activePlayerIndex);
            }
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
                indicators.Add(indicator);
            }
        }
    }
}
