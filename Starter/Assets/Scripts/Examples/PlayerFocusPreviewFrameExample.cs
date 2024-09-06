using Jazz;
using UnityEngine;

namespace Nex
{
    public class PlayerFocusPreviewFrameExample : MonoBehaviour
    {
        [SerializeField] PlayerFocusPreviewFrame previewFrame = null!;
        [SerializeField] BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        [SerializeField] CvDetectionManager cvDetectionManager = null!;
        [SerializeField] PlayAreaController playAreaController = null!;
        [SerializeField] int numOfPlayers = 1;

        void Start()
        {
            cvDetectionManager.numOfPlayers = numOfPlayers;
            playAreaController.Initialize(numOfPlayers, new PlayAreaController.Config(), cvDetectionManager, bodyPoseDetectionManager);
            previewFrame.Initialize(0, numOfPlayers, cvDetectionManager, bodyPoseDetectionManager, playAreaController);
        }
    }
}
