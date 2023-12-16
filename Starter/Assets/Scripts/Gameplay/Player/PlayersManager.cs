using System.Collections.Generic;
using Jazz;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class PlayersManager : MonoBehaviour
    {
        [SerializeField] CvDetectionManager cvDetectionManager = null!;
        [SerializeField] BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        [SerializeField] OnePlayerManager onePlayerManagerPrefab = null!;
        [SerializeField] PreviewFrame previewFrame = null!;

        readonly List<OnePlayerManager> playerManagers = new();

        int numOfPlayers = 1;

        #region Life Cycle

        void Awake()
        {
            Initialize();
        }

        void Initialize()
        {
            ConfigMDK();

            for (var playerIndex = 0; playerIndex < numOfPlayers; playerIndex++)
            {
                var playerManager = Instantiate(onePlayerManagerPrefab, transform);
                playerManager.Initialize(playerIndex, numOfPlayers, bodyPoseDetectionManager);
                playerManagers.Add(playerManager);
            }

            // NOTE: we need to have a UI manager eventually to handle the preview. This is just an example.
            previewFrame.Initialize(0, numOfPlayers, cvDetectionManager, bodyPoseDetectionManager);
        }

        #endregion

        #region MDK

        void ConfigMDK()
        {
            cvDetectionManager.numOfPlayers = numOfPlayers;
            CvDetectionManager.gameViewportController.SetUseDetectionViewportForPlayerTracking(true);
        }

        #endregion
    }
}
