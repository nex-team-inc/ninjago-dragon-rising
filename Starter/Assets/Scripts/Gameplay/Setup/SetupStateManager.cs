using System.Collections.Generic;
using Jazz;
using UnityEngine;
using UnityEngine.Events;

#nullable enable

namespace Nex
{
    public class SetupStateManager : MonoBehaviour
    {
        [SerializeField] OnePlayerSetupStateTracker onePlayerSetupStateTrackerPrefab = null!;

        CvDetectionManager cvDetectionManager = null!;
        BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        PlayAreaController playAreaController = null!;
        readonly List<OnePlayerSetupStateTracker> playerTrackers = new();
        readonly List<PlayerSetupState> playerStates = new();

        public event UnityAction<(int playerIndex, SetupSummary setupSummary)>? PlayerTrackerUpdated;

        int numOfPlayers;

        #region Public

        public void Initialize(
            int aNumOfPlayers,
            CvDetectionManager aCvDetectionManager,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            PlayAreaController aPlayAreaController
        )
        {
            numOfPlayers = aNumOfPlayers;
            cvDetectionManager = aCvDetectionManager;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
            playAreaController = aPlayAreaController;
        }

        public void SetTrackingEnabled(bool shouldTrack)
        {
            var isTracking = playerTrackers.Count > 0;
            switch (shouldTrack)
            {
                case true when !isTracking:
                    CreateTrackers();
                    break;
                case false when isTracking:
                    ClearTrackers();
                    break;
            }
        }

        public void ResetSetupStates()
        {
            ClearTrackers();
            CreateTrackers();
        }

        #endregion

        #region Trackers

        void ClearTrackers()
        {
            for (var playerIndex = 0; playerIndex < playerTrackers.Count; playerIndex++)
            {
                var tracker = playerTrackers[playerIndex];
                tracker.SetIsTracking(false);

                // Send out a dummy update so that the outside can update UI and cursor visibility accordingly.
                TrackerOnUpdated(playerIndex, SetupSummary.CreateDummy());
                Destroy(tracker.gameObject);
            }
            playerTrackers.Clear();
        }

        void CreateTrackers()
        {
            for (var playerIndex = 0; playerIndex < numOfPlayers; playerIndex++)
            {
                var tracker = Instantiate(onePlayerSetupStateTrackerPrefab, transform);
                tracker.Initialize(playerIndex, bodyPoseDetectionManager, playAreaController);
                playerTrackers.Add(tracker);

                playerStates.Add(new PlayerSetupState
                {
                    setupStateType = default
                });

                var playerIndexCopy = playerIndex;
                tracker.Updated += summary => TrackerOnUpdated(playerIndexCopy, summary);

                tracker.SetIsTracking(true);
            }
        }

        void HandlePlayerStatesChange()
        {
            ReconfigureDetection();
        }

        void ReconfigureDetection()
        {
            var playingStateCount = playerStates.ReadyPlayerCount();

            var isAnyInPlaying = playingStateCount >= 1;
            var isAllInPlaying = playingStateCount == 2;

            // This should be changed according to the game's need.
            playAreaController.SetPlayAreaLocked(isAllInPlaying);
            DewarpLocked = isAnyInPlaying;
            TrackingConsistencyEnabled = isAllInPlaying;
        }

        #region Configs

        bool DewarpLocked
        {
            get => CvDetectionManager.dewarpController.continuousAutoTiltMode == ContinuousAutoTiltMode.Off;
            set
            {
                if (DewarpLocked != value)
                {
                    var autoTiltValue = value
                        ? ContinuousAutoTiltMode.Off
                        : ContinuousAutoTiltMode.Recovery;
                    CvDetectionManager.dewarpController.continuousAutoTiltMode = autoTiltValue;
                    cvDetectionManager.dynamicDewarpConfig.continuousAutoTiltMode = autoTiltValue;

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

        void TrackerOnUpdated(int playerIndex, SetupSummary summary)
        {
            if (playerStates[playerIndex].setupStateType != summary.setupStateType)
            {
                playerStates[playerIndex].setupStateType = summary.setupStateType;

                HandlePlayerStatesChange();
            }

            PlayerTrackerUpdated?.Invoke((playerIndex, summary));
        }

        #endregion
    }
}
