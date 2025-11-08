using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex
{
    public class NonARGameExample : MonoBehaviour
    {
        [Header("Preview")]
        [SerializeField] PreviewsManager previewsManager = null!;

        [Header("Detection & Setup")]
        [SerializeField] DetectionManager detectionManager = null!;
        [SerializeField] int numOfPlayers = 1;


        [Header("Player")]
        [SerializeField] GameObject playersContainer = null!;
        [SerializeField] OnePlayerDetectionEngine detectionEnginePrefab = null!;
        [SerializeField] ExampleNonARPlayer playerPrefab = null!;

        [Header("Player Photo")]
        [SerializeField] PlayerPhotoManager playerPhotoManager = null!;

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
            playerPhotoManager.Initialize(numOfPlayers, detectionManager.CvDetectionManager, detectionManager.BodyPoseDetectionManager);

            playersContainer.SetActive(false);
            for (var playerIndex = 0; playerIndex < numOfPlayers; playerIndex++)
            {
                var detectionEngine = Instantiate(detectionEnginePrefab, playersContainer.transform);
                detectionEngine.Initialize(playerIndex, detectionManager.BodyPoseDetectionManager);

                var player = Instantiate(playerPrefab, playersContainer.transform);
                player.Initialize(playerIndex, detectionEngine, playerPhotoManager);
            }

            await ScreenBlockerManager.Instance.Hide();

            await RunSetup();

            // Assume a photo is taken after setup
            for (var i = 0; i < numOfPlayers; i++)
            {
                playerPhotoManager.TakePhoto(i);
            }

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
            playersContainer.SetActive(true);
        }

        #endregion
    }
}
