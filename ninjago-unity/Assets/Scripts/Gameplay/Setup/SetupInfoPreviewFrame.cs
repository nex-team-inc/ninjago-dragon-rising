using System.Collections.Generic;
using Jazz;
using UnityEngine;

namespace Nex
{
    public class SetupInfoPreviewFrame : MonoBehaviour
    {
        [SerializeField] PlayerSetupPreviewFrame previewFrame = null!;
        [SerializeField] SetupWarningMessage warningMessage = null!;
        [SerializeField] PlayerIndicatorsManager playerIndicatorsManager = null!;

        public void Initialize(
            int playerIndex,
            int numOfPlayers,
            BodyPoseDetectionManager bodyPoseDetectionManager,
            BasePlayAreaController playAreaController,
            SetupStateManager setupStateManager
        )
        {
            previewFrame.Initialize(playerIndex, numOfPlayers, bodyPoseDetectionManager, playAreaController);
            warningMessage.Initialize(playerIndex, setupStateManager);
            playerIndicatorsManager.Initialize(numOfPlayers, new List<int> {playerIndex}, previewFrame, bodyPoseDetectionManager);
        }
    }
}
