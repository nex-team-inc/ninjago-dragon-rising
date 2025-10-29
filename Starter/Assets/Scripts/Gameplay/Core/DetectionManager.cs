#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Jazz;
using UnityEngine;

namespace Nex
{
    public class DetectionManager : MonoBehaviour
    {
        [SerializeField] CvDetectionManager cvDetectionManager = null!;
        [SerializeField] BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        [SerializeField] SetupStateManager setupStateManager = null!;
        [SerializeField] BasePlayAreaController playAreaController = null!;

        int numOfPlayers;

        public CvDetectionManager CvDetectionManager => cvDetectionManager;
        public BodyPoseDetectionManager BodyPoseDetectionManager => bodyPoseDetectionManager;
        public SetupStateManager SetupStateManager => setupStateManager;
        public BasePlayAreaController PlayAreaController => playAreaController;

        #region Life Cycle

        public void Initialize(int aNumOfPlayers)
        {
            numOfPlayers = aNumOfPlayers;
            ConfigMdk();

            playAreaController.Initialize(numOfPlayers, cvDetectionManager, bodyPoseDetectionManager);
            setupStateManager.Initialize(numOfPlayers, bodyPoseDetectionManager, playAreaController);
        }

        public void ConfigForSetup()
        {
            playAreaController.SetPlayAreaLocked(false);
            DewarpLocked = false;
            TrackingConsistencyEnabled = false;
            setupStateManager.SetTrackingEnabled(true);
            setupStateManager.SetSetupDetectorMode(SetupDetectorMode.Base);
        }

        public void ConfigForGameplay(bool shouldLockPlayerAreaAndDewarp = true)
        {
            playAreaController.SetPlayAreaLocked(shouldLockPlayerAreaAndDewarp);
            DewarpLocked = shouldLockPlayerAreaAndDewarp;
            TrackingConsistencyEnabled = true;
            setupStateManager.SetSetupDetectorMode(SetupDetectorMode.Gameplay);
        }

        #endregion

        #region Configs

        bool DewarpLocked
        {
            get => CvDetectionManager.dewarpAutoTiltController.continuousAutoTiltMode == ContinuousAutoTiltMode.Off;
            set
            {
                if (DewarpLocked != value)
                {
                    var autoTiltValue = value
                        ? ContinuousAutoTiltMode.Off
                        : ContinuousAutoTiltMode.Recovery;
                    CvDetectionManager.dewarpAutoTiltController.continuousAutoTiltMode = autoTiltValue;

                    Debug.Log($"Dewarp changed: {(value ? "Locked" : "Unlocked")}");
                }
            }
        }

        bool TrackingConsistencyEnabled
        {
            get => bodyPoseDetectionManager.trackingConfig.enableConsistency;
            set
            {
                if (TrackingConsistencyEnabled != value)
                {
                    bodyPoseDetectionManager.trackingConfig.enableConsistency = value;

                    Debug.Log($"Tracking consistency changed: {value}");
                }
            }
        }

        #endregion

        #region Helper

        void ConfigMdk()
        {
            var playerPositions = new List<Vector2>();
            for (var playerIndex = 0; playerIndex < numOfPlayers; playerIndex++)
            {
                playerPositions.Add(new Vector2(PlayerPositionDefinition.GetXRatioForPlayer(playerIndex, numOfPlayers), 0.5f));
            }

            cvDetectionManager.numOfPlayers = numOfPlayers;
            cvDetectionManager.playerPositions = playerPositions;
        }

        #endregion

        #region First Detection Ready

        UniTaskCompletionSource? firstDetectionSource;

        public UniTask WaitForFirstDetection()
        {
            firstDetectionSource = new UniTaskCompletionSource();
            bodyPoseDetectionManager.processed.captureAspectNormalizedDetection += HandleFirstDetection;
            return firstDetectionSource.Task;
        }

        void HandleFirstDetection(BodyPoseDetectionResult _)
        {
            bodyPoseDetectionManager.processed.captureAspectNormalizedDetection -= HandleFirstDetection;
            firstDetectionSource?.TrySetResult();
        }

        #endregion

        #region Pause Detection

        public void StopDetection()
        {
            bodyPoseDetectionManager.shouldDetect = false;
            cvDetectionManager.StopRunning();
        }

        public void PauseDetection()
        {
            bodyPoseDetectionManager.shouldDetect = false;
            cvDetectionManager.GetFrameProvider().TurnOffPreviewTexture();
        }

        public void UnPauseDetection()
        {
            bodyPoseDetectionManager.shouldDetect = true;
            cvDetectionManager.GetFrameProvider().TurnOnPreviewTexture();
        }

        #endregion
    }
}
