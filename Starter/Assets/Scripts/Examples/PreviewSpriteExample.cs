#nullable enable

using Jazz;
using UnityEngine;

namespace Nex
{
    public class PreviewSpriteExample : MonoBehaviour
    {
        [SerializeField] BasePlayAreaController playAreaController = null!;
        [SerializeField] BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        [SerializeField] CvDetectionManager cvDetectionManager = null!;
        [SerializeField] PlayerPreviewSprite playerPreviewSpritePrefab = null!;

        [SerializeField] int numOfPlayers = 1;

        void Start()
        {
            cvDetectionManager.numOfPlayers = numOfPlayers;
            playAreaController.Initialize(numOfPlayers, cvDetectionManager, bodyPoseDetectionManager);

            for (var playerIndex = 0; playerIndex < numOfPlayers; playerIndex++)
            {
                var playerPreviewSprite = Instantiate(playerPreviewSpritePrefab, transform);
                playerPreviewSprite.Initialize(playerIndex, cvDetectionManager, bodyPoseDetectionManager);
                playerPreviewSprite.transform.localPosition = new Vector3(
                    (playerIndex - (numOfPlayers - 1) * 0.5f) * 3.0f, // Spread players horizontally
                    0,
                    0
                );
            }
        }
    }
}
