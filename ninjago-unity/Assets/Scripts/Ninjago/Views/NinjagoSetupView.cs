#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// Camera setup before every mini-game: every player stands in frame and holds still, and that player's body
    /// origin (chest, and resting knees for kicks) is stored. Games with hand cursors then confirm each player's hands
    /// drive a cursor. In two-player mode a player who never appears is dropped after the solo-fallback wait.
    /// </summary>
    public class NinjagoSetupView : SimpleCanvasView
    {
        [Header("Camera Previews Prefab")]
        [Tooltip("Instantiated as its own root: its canvas is a screen-space overlay and does not size as a nested canvas.")]
        [SerializeField] PreviewsManager previewsManagerPrefab = null!;
        [Header("Player Status Rows")]
        [SerializeField] SetupPlayerStatus[] playerStatuses = null!;
        [Header("Hand Cursor Check")]
        [Tooltip("Only in the setup view of the hand cursor games; runs after the body calibration.")]
        [SerializeField] HandCursorCheck? handCheck;
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
            public Vector2 kneeDropSum;
            public int kneeDropCount;
        }

        IReadOnlyList<PlayerBody> bodies = null!;
        DetectionManager detection = null!;
        HandCursorTracker? cursors;
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

        /// <param name="aCursors">The game's hand cursors, or null for games without them (no hand check).</param>
        public void Initialize(IReadOnlyList<PlayerBody> aBodies, DetectionManager aDetection, NinjagoPlayersConfig aConfig, HandCursorTracker? aCursors)
        {
            bodies = aBodies;
            detection = aDetection;
            config = aConfig;
            cursors = aCursors;
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
                if (cursors != null)
                {
                    calibrated = await ConfirmHandsAsync(cursors, handCheck!, calibrated, cancellationToken);
                    if (calibrated == null) return null;
                }

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
                body.SetOrigin(body.TryGetChest(out var simulatedChest) ? simulatedChest : Vector2.zero, null);
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
            if (body.TryGetKneeDrops(out var kneeDrops))
            {
                hold.kneeDropSum += kneeDrops;
                hold.kneeDropCount++;
            }

            playerStatuses[playerIndex].Show(SetupPlayerStatus.State.Holding);
            if (now - hold.startTime < config.CalibrationSeconds) return false;

            body.SetOrigin(hold.sum / hold.count, hold.kneeDropCount > 0 ? hold.kneeDropSum / hold.kneeDropCount : null);
            hold.active = false;
            playerStatuses[playerIndex].Show(SetupPlayerStatus.State.Ready);
            return true;
        }

        // Every calibrated player fills their ring with a cursor; in two-player mode a player who never does is dropped
        // after the solo-fallback wait, like a body that never appears.
        async UniTask<List<PlayerBody>?> ConfirmHandsAsync(HandCursorTracker handCursors, HandCursorCheck check, List<PlayerBody> calibrated,
            CancellationToken cancellationToken)
        {
            handCursors.SetDetecting(true);
            check.Show(calibrated, handCursors, detection, bodies.Count);
            var confirmed = new List<PlayerBody>(calibrated.Count);
            var done = new bool[calibrated.Count];
            var startTime = Time.unscaledTime;
            float? firstConfirmTime = null;
            while (!quitRequested)
            {
                var now = Time.unscaledTime;
                for (var slot = 0; slot < calibrated.Count; slot++)
                {
                    if (done[slot] || !check.Tick(slot, Time.unscaledDeltaTime, config.HandCheckSeconds)) continue;
                    done[slot] = true;
                    confirmed.Add(calibrated[slot]);
                    firstConfirmTime ??= now;
                    NinjagoAnalytics.PlayerHandsConfirmed(calibrated[slot].PlayerIndex, now - startTime);
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
                }

                var soloTimeUp = firstConfirmTime.HasValue && now - firstConfirmTime.Value >= config.SoloFallbackSeconds;
                if (confirmed.Count == calibrated.Count || soloTimeUp)
                {
                    check.ShowReady();
                    await UniTask.Delay((int)(config.ReadyHoldSeconds * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
                    confirmed.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
                    return confirmed;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            return null;
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
