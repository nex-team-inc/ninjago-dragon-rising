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

        #region Life Cycle

        void Awake()
        {
            Initialize();
        }

        void Initialize()
        {
            var numOfPlayers = 1; // Here is where we can config 1P / 2P game.

            ConfigMdk(numOfPlayers);

            playersManager.Initialize(numOfPlayers, bodyPoseDetectionManager);
            previewsManager.Initialize(numOfPlayers, cvDetectionManager, bodyPoseDetectionManager);
            setupStateManager.Initialize(numOfPlayers, cvDetectionManager, bodyPoseDetectionManager);
            setupUI.Initialize(numOfPlayers, setupStateManager);

            setupStateManager.SetTrackingEnabled(true);
        }

        void ConfigMdk(int numOfPlayers)
        {
            cvDetectionManager.numOfPlayers = numOfPlayers;
            CvDetectionManager.gameViewportController.SetUseDetectionViewportForPlayerTracking(true);
        }

        #endregion
    }
}
