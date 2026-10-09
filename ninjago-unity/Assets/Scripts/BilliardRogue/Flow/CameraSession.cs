#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Owns the DetectionManager instance for a flow session (TDD D1): created before Calibration for numPlayers with
    /// one hidden-node OnePlayerDetectionEngine per player, paused with the app, disposed when returning to the title.
    /// A player-count change destroys and re-instantiates the prefab; the coordinator reloads Main.unity instead when
    /// reloadSceneOnPlayerCountChange is on.
    /// </summary>
    public sealed class CameraSession : MonoBehaviour
    {
        [Header("Player count change")]
        [Tooltip("TDD D1 fallback: reload Main.unity on a player-count change instead of re-instantiating the DetectionManager prefab.")]
        [SerializeField] bool reloadSceneOnPlayerCountChange;

        DetectionManager detectionPrefab = null!;
        OnePlayerDetectionEngine enginePrefab = null!;
        Transform root = null!;
        DetectionManager? detection;
        readonly List<OnePlayerDetectionEngine> engines = new();

        public DetectionManager Detection =>
            detection != null ? detection : throw new InvalidOperationException("CameraSession is not running.");

        /// <summary>Player count of the running session, or of the last started one after Stop (0 before the first start).</summary>
        public int NumPlayers { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public bool ReloadSceneOnPlayerCountChange => reloadSceneOnPlayerCountChange;

        /// <summary>
        /// TDD D1: a calibration for numPlayers follows a session started for another count. Every flow stops the
        /// camera before the next calibration, so this compares against the last started count, not a running one.
        /// </summary>
        public static bool IsPlayerCountChange(int lastStartedNumPlayers, int numPlayers)
        {
            return lastStartedNumPlayers != 0 && lastStartedNumPlayers != numPlayers;
        }

        #region Life Cycle

        public void Initialize(DetectionManager aDetectionPrefab, OnePlayerDetectionEngine aEnginePrefab, Transform aRoot)
        {
            detectionPrefab = aDetectionPrefab;
            enginePrefab = aEnginePrefab;
            root = aRoot;
        }

        void OnDestroy()
        {
            Stop();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Instantiates detection for numPlayers and configures it for setup. A running session with the same
        /// player count is reused (and un-paused); a different count is torn down and rebuilt.
        /// </summary>
        public async UniTask StartAsync(int numPlayers, CancellationToken ct)
        {
            if (IsRunning && NumPlayers == numPlayers)
            {
                UnPause();
                return;
            }

            if (IsRunning)
            {
                Stop();
            }

            NumPlayers = numPlayers;
            var instance = Instantiate(detectionPrefab, root);
            instance.name = detectionPrefab.name;
            instance.Initialize(numPlayers);
            for (var playerIndex = 0; playerIndex < numPlayers; playerIndex++)
            {
                var engine = Instantiate(enginePrefab, root);
                engine.name = $"{enginePrefab.name}_P{playerIndex + 1}";
                engine.Initialize(playerIndex, instance.BodyPoseDetectionManager);
                engines.Add(engine);
            }

            detection = instance;
            instance.ConfigForSetup();
            // Motion-only play never resets the TV's idle timer.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            IsRunning = true;
            IsPaused = false;

            // The MDK starts its camera in Start(): give it a frame before callers register preview handlers.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        public OnePlayerDetectionEngine GetEngine(int playerIndex)
        {
            return engines[playerIndex];
        }

        /// <summary>Stops the camera and destroys the DetectionManager and every engine.</summary>
        public void Stop()
        {
            if (!IsRunning) return;

            foreach (var engine in engines)
            {
                Destroy(engine.gameObject);
            }

            engines.Clear();

            if (detection != null)
            {
                detection.StopDetection();
                Destroy(detection.gameObject);
            }

            detection = null;
            IsRunning = false;
            IsPaused = false;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        /// <summary>App / pause-view pause: stops pose detection and the preview texture.</summary>
        public void Pause()
        {
            if (!IsRunning) return;
            if (IsPaused) return;
            IsPaused = true;
            Detection.PauseDetection();
        }

        public void UnPause()
        {
            if (!IsRunning) return;
            if (!IsPaused) return;
            IsPaused = false;
            Detection.UnPauseDetection();
        }

        #endregion
    }
}
