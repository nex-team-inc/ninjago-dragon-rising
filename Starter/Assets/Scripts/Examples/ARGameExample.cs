using Jazz;
using UnityEngine;

namespace Nex
{
    public class ARGameExample : MonoBehaviour
    {
        [SerializeField] AreaPreviewFrame previewFrame = null!;
        [SerializeField] PlayAreaController playAreaController = null!;
        [SerializeField] BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        [SerializeField] CvDetectionManager cvDetectionManager = null!;
        [SerializeField] OnePlayerPreviewPoseEngine onePlayerPreviewPoseEnginePrefab = null!;
        [SerializeField] int numOfPlayers = 1;

        void Start()
        {
            cvDetectionManager.numOfPlayers = numOfPlayers;
            playAreaController.Initialize(numOfPlayers, cvDetectionManager, bodyPoseDetectionManager);
            previewFrame.Initialize(cvDetectionManager, playAreaController);

            for (var playerIndex = 0; playerIndex < numOfPlayers; playerIndex++)
            {
                var onePlayerPreviewPoseEngine = Instantiate(onePlayerPreviewPoseEnginePrefab, transform);
                onePlayerPreviewPoseEngine.Initialize(playerIndex, numOfPlayers, bodyPoseDetectionManager, previewFrame);
            }
        }
    }
}
