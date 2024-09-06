#nullable enable

using Jazz;
using UnityEngine;

namespace Nex
{
    public class DetectionManager : MonoBehaviour
    {
        [SerializeField] CvDetectionManager cvDetectionManager = null!;
        [SerializeField] BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        [SerializeField] PlayersManager playersManager = null!;
        [SerializeField] PreviewsManager previewsManager = null!;
        [SerializeField] SetupStateManager setupStateManager = null!;
        [SerializeField] SetupUI setupUI = null!;
        [SerializeField] PlayAreaController playAreaController = null!;

        #region Life Cycle

        public void Initialize(int numOfPlayers)
        {
            ConfigMdk(numOfPlayers);

            playAreaController.Initialize(numOfPlayers, new PlayAreaController.Config(), cvDetectionManager, bodyPoseDetectionManager);
            playersManager.Initialize(numOfPlayers, bodyPoseDetectionManager);
            previewsManager.Initialize(numOfPlayers, cvDetectionManager, bodyPoseDetectionManager, playAreaController);
            setupStateManager.Initialize(numOfPlayers, cvDetectionManager, bodyPoseDetectionManager, playAreaController);
            setupUI.Initialize(numOfPlayers, setupStateManager);

            setupStateManager.SetTrackingEnabled(true);
        }

        void ConfigMdk(int numOfPlayers)
        {
            cvDetectionManager.numOfPlayers = numOfPlayers;
        }

        #endregion
    }
}
