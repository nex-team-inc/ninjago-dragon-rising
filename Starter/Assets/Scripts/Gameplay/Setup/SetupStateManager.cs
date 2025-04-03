using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Jazz;
using UnityEngine;
using UnityEngine.Events;

#nullable enable

namespace Nex
{
    public class SetupStateManager : MonoBehaviour
    {
        [SerializeField] OnePlayerSetupStateTracker onePlayerSetupStateTrackerPrefab = null!;

        BodyPoseDetectionManager bodyPoseDetectionManager = null!;
        BasePlayAreaController playAreaController = null!;
        readonly List<OnePlayerSetupStateTracker> playerTrackers = new();
        readonly List<PlayerSetupState> playerStates = new();

        public bool AllPlayersAreInGoodPosition =>
            playerStates.All(x => x.setupStateType > SetupStateType.WaitingForGoodPlayerPosition);

        public event UnityAction<(int playerIndex, SetupSummary setupSummary)>? PlayerTrackerUpdated;

        int numOfPlayers;

        #region Public

        public void Initialize(
            int aNumOfPlayers,
            BodyPoseDetectionManager aBodyPoseDetectionManager,
            BasePlayAreaController aPlayAreaController
        )
        {
            numOfPlayers = aNumOfPlayers;
            bodyPoseDetectionManager = aBodyPoseDetectionManager;
            playAreaController = aPlayAreaController;

            SetAllowPassingRaisingHandState(false);
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

        public void SetAllowPassingRaisingHandState(bool value)
        {
            foreach (var tracker in playerTrackers)
            {
                tracker.SetAllowPassingRaisingHandState(value);
            }
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                SetAllowPassingRaisingHandState(true);
                ResolveGoodPlayerPosition();
                ResolveRaiseHand();
            }
        }

        #endregion

        #region Trackers

        void ClearTrackers()
        {
            foreach (var tracker in playerTrackers)
            {
                tracker.SetIsTracking(false);
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

        void TrackerOnUpdated(int playerIndex, SetupSummary summary)
        {
            if (playerStates[playerIndex].setupStateType != summary.setupStateType)
            {
                playerStates[playerIndex].setupStateType = summary.setupStateType;
                HandlePlayerStatesChange();
            }

            PlayerTrackerUpdated?.Invoke((playerIndex, summary));
        }

        void HandlePlayerStatesChange()
        {
            AnnounceGoodPlayerPositionIfNeeded();
            AnnounceRaiseHandIfNeeded();
        }

        #endregion

        #region UniTask API

        UniTaskCompletionSource? goodPlayerPositionSource;
        UniTaskCompletionSource? raiseHandSource;

        public UniTask WaitForGoodPlayerPosition()
        {
            goodPlayerPositionSource ??= new UniTaskCompletionSource();
            var task = goodPlayerPositionSource.Task;

            AnnounceGoodPlayerPositionIfNeeded();

            return task;
        }

        void AnnounceGoodPlayerPositionIfNeeded()
        {
            if (AllPlayersAreInGoodPosition)
            {
                ResolveGoodPlayerPosition();
            }
        }

        void ResolveGoodPlayerPosition()
        {
            if (goodPlayerPositionSource != null)
            {
                goodPlayerPositionSource.TrySetResult();
                goodPlayerPositionSource = null;
            }
        }

        public UniTask WaitForRaiseHand()
        {
            raiseHandSource ??= new UniTaskCompletionSource();
            var task = raiseHandSource.Task;

            AnnounceRaiseHandIfNeeded();

            return task;
        }

        void AnnounceRaiseHandIfNeeded()
        {
            if (playerStates.All(x => x.setupStateType > SetupStateType.WaitingForRaisingHand))
            {
                ResolveRaiseHand();
            }
        }

        void ResolveRaiseHand()
        {
            if (raiseHandSource != null)
            {
                raiseHandSource.TrySetResult();
                raiseHandSource = null;
            }
        }

        #endregion
    }
}
