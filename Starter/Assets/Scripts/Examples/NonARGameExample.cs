using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex
{
    public class NonARGameExample : MonoBehaviour
    {
        [SerializeField] DetectionManager detectionManager = null!;
        [SerializeField] PlayersManager playersManager = null!;
        [SerializeField] int numOfPlayers = 1;

        void Start()
        {
            StartAsync().Forget();
        }

        async UniTask StartAsync()
        {
            detectionManager.Initialize(numOfPlayers);
            playersManager.Initialize(numOfPlayers, detectionManager.BodyPoseDetectionManager);
            playersManager.gameObject.SetActive(false);
            await ScreenBlockerManager.Instance.Hide();

            await RunSetup();
            await RunGame();
        }

        #region Setup

        async UniTask RunSetup()
        {
            detectionManager.ConfigForSetup();
            await detectionManager.PreviewsManager.MoveIn(true);

            detectionManager.PreviewsManager.SetPromptText("Move into the frame");
            await detectionManager.SetupStateManager.WaitForGoodPlayerPosition();

            detectionManager.PreviewsManager.SetPromptText("Raise your hand to start");
            detectionManager.SetupStateManager.SetAllowPassingRaisingHandState(true);
            await detectionManager.SetupStateManager.WaitForRaiseHand();

            await detectionManager.PreviewsManager.MoveOut(true);
        }

        #endregion

        #region Game

        async UniTask RunGame()
        {
            detectionManager.ConfigForGameplay();
            playersManager.gameObject.SetActive(true);
        }

        #endregion
    }
}
