#nullable enable

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

            // turn on native zoom before we start detection
            GlobalOptions.shared.enableNativeZoom = true;
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

        #region Preview Texture Rate

        /// <summary>SetPreviewTextureInterval value that stops the preview texture refreshes; detection keeps running.</summary>
        public const float PreviewTextureOff = -1f;

        // 0 = the MDK default: the camera refreshes the preview textures on every camera frame.
        float previewInterval;
        float nextPreviewTime;

        /// <summary>
        /// Seconds between camera preview texture refreshes: 0 = every camera frame (the MDK default), PreviewTextureOff
        /// = none. A refresh uploads the whole camera frame into two textures on the render thread (about 9 ms on the
        /// Nex Playground), so a view that shows only a small feed can ask for fewer. PauseDetection still wins.
        /// </summary>
        public void SetPreviewTextureInterval(float seconds)
        {
            previewInterval = seconds;
            nextPreviewTime = 0f;
            if (seconds != 0f) return;
            if (TryGetCamera(out var provider, out var nexCamera))
            {
                nexCamera.isRenderPreview = provider.IsPreviewTextureOn();
            }
        }

        void Update()
        {
            if (previewInterval == 0f) return;
            if (!TryGetCamera(out var provider, out var nexCamera)) return;
            // NexCamera uploads at the end of a frame that has isRenderPreview set and announces the texture at the end
            // of the next frame, so one frame on gives exactly one upload and one announcement.
            var due = previewInterval > 0f && provider.IsPreviewTextureOn() && Time.unscaledTime >= nextPreviewTime;
            nexCamera.isRenderPreview = due;
            if (due) nextPreviewTime = Time.unscaledTime + previewInterval;
        }

        bool TryGetCamera([NotNullWhen(true)] out CameraFrameProvider? provider, [NotNullWhen(true)] out NexCamera? nexCamera)
        {
            provider = cvDetectionManager.GetFrameProvider() as CameraFrameProvider;
            nexCamera = provider != null ? provider.GetCamera() : null;
            return provider != null && nexCamera != null;
        }

        #endregion
    }
}
