using System;
using System.Collections.Generic;
using Jazz;
using UnityEngine;
using UnityEngine.UI;

namespace Nex
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] CvDetectionManager cvDetectionManager;
        [SerializeField] BodyPoseDetectionManager bodyPoseDetectionManager;
        [SerializeField] List<OnePlayerGameEngine> gameEngines;

        #region Life Cycle

        void Awake()
        {
            foreach (var gameEngine in gameEngines)
            {
                gameEngine.gameObject.SetActive(false);
            }
        }

        void Start()
        {
            InitializeDetectionEngine();
        }

        #endregion

        #region Helper

        void InitializeDetectionEngine()
        {
            var numOfPlayers = gameEngines.Count;
            cvDetectionManager.numOfPlayers = numOfPlayers;

            for (var playerIndex = 0; playerIndex < numOfPlayers; playerIndex++)
            {
                var gameEngine = gameEngines[playerIndex];
                gameEngine.Initialize(playerIndex, numOfPlayers, cvDetectionManager, bodyPoseDetectionManager);
                gameEngine.gameObject.SetActive(true);
            }
        }

        #endregion
    }
}
