#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Camera setup before either mini-game: every player stands in frame and holds still, and that player's chest
    /// origin is stored. In two-player mode a player who never appears is dropped after the solo-fallback wait.
    /// </summary>
    public class NinjagoSetupView : SimpleCanvasView
    {
        [Header("Camera Previews Prefab")]
        [Tooltip("Instantiated as its own root: its canvas is a screen-space overlay and does not size as a nested canvas.")]
        [SerializeField] PreviewsManager previewsManagerPrefab = null!;
        [Header("Player Status Rows")]
        [SerializeField] SetupPlayerStatus[] playerStatuses = null!;
        [Header("Prompt: Stand In Frame")]
        [SerializeField] LocalizedString standPrompt = new();
        [Header("Prompt: Hold Still")]
        [SerializeField] LocalizedString holdPrompt = new();
        [Header("Prompt: Ready")]
        [SerializeField] LocalizedString readyPrompt = new();
        [Header("Prompt: Starting Solo")]
        [SerializeField] LocalizedString soloPrompt = new();

        struct Hold
        {
            public bool active;
            public float startTime;
            public Vector2 anchor;
            public Vector2 sum;
            public int count;
        }

        IReadOnlyList<PlayerBody> bodies = null!;
        DetectionManager detection = null!;
        PreviewsManager previewsManager = null!;
        NinjagoPlayersConfig config = null!;
        bool[] inGoodPosition = null!;
        Hold[] holds = null!;
        LocalizedString? shownPrompt;
        bool quitRequested;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.NinjagoSetup;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "ninjago-setup";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            quitRequested = true;
        }

        // The previews are a screen-space overlay, which would draw over any view pushed on top (Debug Settings).
        public override async UniTask EnterBackground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            previewsManager.gameObject.SetActive(false);
            await base.EnterBackground(childViewIdentifier, animate);
        }

        public override async UniTask EnterForeground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            await base.EnterForeground(childViewIdentifier, animate);
            previewsManager.gameObject.SetActive(true);
        }

        #endregion

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> aBodies, DetectionManager aDetection, NinjagoPlayersConfig aConfig)
        {
            bodies = aBodies;
            detection = aDetection;
            config = aConfig;
            inGoodPosition = new bool[bodies.Count];
            holds = new Hold[bodies.Count];
            previewsManager = Instantiate(previewsManagerPrefab);
            for (var i = 0; i < playerStatuses.Length; i++)
            {
                var used = i < bodies.Count;
                playerStatuses[i].gameObject.SetActive(used);
                if (used) playerStatuses[i].Initialize(i, bodies[i].Color);
            }
        }

        void OnDestroy()
        {
            if (previewsManager != null) Destroy(previewsManager.gameObject);
        }

        #endregion

        #region Public API

        /// <summary>Calibrates the players; returns the ones with a chest origin, or null when the player backed out.</summary>
        public async UniTask<List<PlayerBody>?> RunAsync(CancellationToken cancellationToken)
        {
            var setupStateManager = detection.SetupStateManager;
            detection.ConfigForSetup();
            previewsManager.Initialize(bodies.Count, detection.BodyPoseDetectionManager, detection.PlayAreaController, setupStateManager);
            setupStateManager.PlayerTrackerUpdated += HandlePlayerTrackerUpdated;
            foreach (var body in bodies) body.ClearOrigin();
            try
            {
                await previewsManager.MoveIn(true);
                var startTime = Time.unscaledTime;
                var calibrated = await CalibrateAsync(cancellationToken);
                if (calibrated == null) return null;

                ShowPrompt(calibrated.Count < bodies.Count ? soloPrompt : readyPrompt);
                await UniTask.Delay((int)(config.ReadyHoldSeconds * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
                await previewsManager.MoveOut(true);
                detection.ConfigForGameplay();
                NinjagoAnalytics.SetupComplete(bodies.Count, calibrated.Count, Time.unscaledTime - startTime);
                return calibrated;
            }
            finally
            {
                setupStateManager.PlayerTrackerUpdated -= HandlePlayerTrackerUpdated;
            }
        }

        #endregion

        #region Calibration

        async UniTask<List<PlayerBody>?> CalibrateAsync(CancellationToken cancellationToken)
        {
            var startTime = Time.unscaledTime;
            float? firstReadyTime = null;
            while (!quitRequested)
            {
                var now = Time.unscaledTime;
                var readyCount = 0;
                var anyHolding = false;
                for (var i = 0; i < bodies.Count; i++)
                {
                    if (!bodies[i].IsCalibrated && UpdateHold(i, now))
                    {
                        firstReadyTime ??= now;
                        NinjagoAnalytics.PlayerCalibrated(i, now - startTime);
                        SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
                    }

                    if (bodies[i].IsCalibrated) readyCount++;
                    anyHolding |= holds[i].active;
                }

                var soloTimeUp = firstReadyTime.HasValue && now - firstReadyTime.Value >= config.SoloFallbackSeconds;
                if (readyCount == bodies.Count || soloTimeUp) return CollectCalibrated();
                ShowPrompt(anyHolding ? holdPrompt : standPrompt);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            return null;
        }

        // Returns true on the frame this player's origin is stored.
        bool UpdateHold(int playerIndex, float now)
        {
            var body = bodies[playerIndex];
            ref var hold = ref holds[playerIndex];
            if (SimulatedBody.IsEnabled)
            {
                body.SetOrigin(body.TryGetChest(out var simulatedChest) ? simulatedChest : Vector2.zero);
                playerStatuses[playerIndex].Show(SetupPlayerStatus.State.Ready);
                return true;
            }

            if (!inGoodPosition[playerIndex] || !body.TryGetChest(out var chest))
            {
                hold.active = false;
                playerStatuses[playerIndex].Show(SetupPlayerStatus.State.Waiting);
                return false;
            }

            if (!hold.active || Vector2.Distance(chest, hold.anchor) / body.RawPpi > config.CalibrationStillnessInches)
            {
                hold = new Hold { active = true, startTime = now, anchor = chest };
            }

            hold.sum += chest;
            hold.count++;
            playerStatuses[playerIndex].Show(SetupPlayerStatus.State.Holding);
            if (now - hold.startTime < config.CalibrationSeconds) return false;

            body.SetOrigin(hold.sum / hold.count);
            hold.active = false;
            playerStatuses[playerIndex].Show(SetupPlayerStatus.State.Ready);
            return true;
        }

        List<PlayerBody> CollectCalibrated()
        {
            var calibrated = new List<PlayerBody>(bodies.Count);
            foreach (var body in bodies)
            {
                if (body.IsCalibrated) calibrated.Add(body);
            }

            return calibrated;
        }

        void HandlePlayerTrackerUpdated((int playerIndex, SetupSummary setupSummary) update)
        {
            if (update.playerIndex >= inGoodPosition.Length) return;
            var state = update.setupSummary.setupStateType;
            inGoodPosition[update.playerIndex] = state is SetupStateType.WaitingForRaisingHand or SetupStateType.Playing;
        }

        void ShowPrompt(LocalizedString prompt)
        {
            if (shownPrompt == prompt) return;
            shownPrompt = prompt;
            previewsManager.SetPromptText(prompt.GetLocalizedString());
        }

        #endregion
    }
}
