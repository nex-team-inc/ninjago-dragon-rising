using Jazz;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex
{
    public class ARGameExample : MonoBehaviour
    {
        [SerializeField] AreaPreviewFrame previewFrame = null!;
        [SerializeField] BasePlayAreaController playAreaController = null!;
        [SerializeField] BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        [SerializeField] CvDetectionManager cvDetectionManager = null!;
        [SerializeField] int numOfPlayers = 1;

        [SerializeField] GameObject playersContainer = null!;
        [SerializeField] OnePlayerPreviewPoseEngine onePlayerPreviewPoseEnginePrefab = null!;
        [SerializeField] ExampleARPlayer playerPrefab = null!;

        void Start()
        {
            if (Application.isEditor)
            {
                Application.runInBackground = true;
            }

            cvDetectionManager.numOfPlayers = numOfPlayers;
            playAreaController.Initialize(numOfPlayers, cvDetectionManager, bodyPoseDetectionManager);
            previewFrame.Initialize(cvDetectionManager, playAreaController);

            for (var playerIndex = 0; playerIndex < numOfPlayers; playerIndex++)
            {
                var onePlayerPreviewPoseEngine = Instantiate(onePlayerPreviewPoseEnginePrefab, playersContainer.transform);
                onePlayerPreviewPoseEngine.Initialize(playerIndex, bodyPoseDetectionManager, previewFrame);

                var player = Instantiate(playerPrefab, playersContainer.transform);
                player.Initialize(playerIndex, onePlayerPreviewPoseEngine);
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SceneManager.LoadScene(GameConfigsManager.Instance.MainScene);
            }
        }
    }
}
