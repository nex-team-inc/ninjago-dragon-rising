#nullable enable

using System.Collections.Generic;
using Jazz;
using UnityEngine;

namespace Nex
{
    public class PlayersManager : MonoBehaviour
    {
        [SerializeField] OnePlayerController onePlayerControllerPrefab = null!;

        // ReSharper disable once CollectionNeverQueried.Local
        readonly List<OnePlayerController> playerControllers = new();

        int numOfPlayers;

        #region Public

        public void Initialize(
            int aNumOfPlayers,
            BodyPoseDetectionManager aBodyPoseDetectionManager)
        {
            numOfPlayers = aNumOfPlayers;

            for (var playerIndex = 0; playerIndex < numOfPlayers; playerIndex++)
            {
                var playerController = Instantiate(onePlayerControllerPrefab, transform);
                playerController.Initialize(playerIndex, numOfPlayers, aBodyPoseDetectionManager);
                playerControllers.Add(playerController);
            }
        }

        #endregion
    }
}
