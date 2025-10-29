using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex
{
    public class NonARGameExample : MonoBehaviour
    {
        [SerializeField] DetectionManager detectionManager = null!;
        [SerializeField] PreviewsManager previewsManager = null!;
        [SerializeField] PlayersManager playersManager = null!;
        [SerializeField] int numOfPlayers = 1;

        void Start()
        {
            if (Application.isEditor)
            {
                Application.runInBackground = true;
            }

            StartAsync().Forget();
        }

        async UniTask StartAsync()
        {
            detectionManager.Initialize(numOfPlayers);
            previewsManager.Initialize(numOfPlayers, detectionManager.CvDetectionManager, detectionManager.BodyPoseDetectionManager, detectionManager.PlayAreaController, detectionManager.SetupStateManager);
            playersManager.Initialize(numOfPlayers, detectionManager.BodyPoseDetectionManager);
            playersManager.gameObject.SetActive(false);
            await ScreenBlockerManager.Instance.Hide();

            await RunSetup();
            RunGame();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SceneManager.LoadScene(GameConfigsManager.Instance.MainScene);
            }
        }

        #region Setup

        async UniTask RunSetup()
        {
            detectionManager.ConfigForSetup();
            await previewsManager.MoveIn(true);

            previewsManager.SetPromptText("Move into the frame");
            await detectionManager.SetupStateManager.WaitForGoodPlayerPosition();

            previewsManager.SetPromptText("Raise your hand to start");
            detectionManager.SetupStateManager.SetAllowPassingRaisingHandState(true);
            await detectionManager.SetupStateManager.WaitForRaiseHand();

            await previewsManager.MoveOut(true);
        }

        #endregion

        #region Game

        void RunGame()
        {
            detectionManager.ConfigForGameplay();
            playersManager.gameObject.SetActive(true);
        }

        #endregion
    }
}
