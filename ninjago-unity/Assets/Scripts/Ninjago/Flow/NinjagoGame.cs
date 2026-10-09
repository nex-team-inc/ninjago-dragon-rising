#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Shared flow of every Ninjago mini-game: setup view (body calibration) → gameplay view → result view, and Retry
    /// goes back through setup. One detection engine and PlayerBody per player for the chosen player count.
    /// </summary>
    public abstract class NinjagoGame : BaseGame
    {
        [Header("Detection Engine")]
        [Tooltip("OnePlayerDetectionEngine variant with hidden nodes, so nothing renders in the 3D worlds.")]
        [SerializeField] OnePlayerDetectionEngine detectionEnginePrefab = null!;
        [Header("Detection Engines Root")]
        [SerializeField] Transform enginesRoot = null!;
        [Header("Setup View")]
        [SerializeField] NinjagoSetupView setupViewPrefab = null!;
        [Header("Result View")]
        [SerializeField] NinjagoResultView resultViewPrefab = null!;
        [Header("Players Config")]
        [SerializeField] NinjagoPlayersConfig playersConfig = null!;

        readonly List<PlayerBody> bodies = new();

        protected DetectionManager Detection { get; private set; } = null!;
        protected ViewManager Views { get; private set; } = null!;
        protected IReadOnlyList<PlayerBody> Bodies => bodies;
        protected abstract GameModeType Mode { get; }
        /// <summary>Hand cursors the setup view confirms before gameplay; null for games without them.</summary>
        protected virtual HandCursorTracker? SetupCursors => null;

        public override int NumOfPlayers => Mathf.Clamp(PlayerDataManager.Instance.PlayerPreference.numPlayers, 1, SimulatedBody.MaxPlayers);

        #region Initialization

        public override void Initialize(DetectionManager detectionManager, ViewManager viewManager)
        {
            Detection = detectionManager;
            Views = viewManager;
            for (var playerIndex = 0; playerIndex < NumOfPlayers; playerIndex++)
            {
                var engine = Instantiate(detectionEnginePrefab, enginesRoot);
                engine.Initialize(playerIndex, detectionManager.BodyPoseDetectionManager);
                bodies.Add(new PlayerBody(playerIndex, playersConfig.GetPlayerColor(playerIndex), engine));
            }

            // The Editor stops ticking unfocused otherwise; webcam playtests often run with another window focused.
            if (Application.isEditor) Application.runInBackground = true;
        }

        #endregion

        #region Public API

        public override async UniTask RunAsync(CancellationToken cancellationToken)
        {
            PlayerDataManager.Instance.ScopedNinjagoProgressUpdate(progress => progress.lastMiniGame = Mode);
            NinjagoAnalytics.MiniGameStart(Mode, NumOfPlayers);
            var firstRound = true;
            while (true)
            {
                var setupView = Instantiate(setupViewPrefab);
                setupView.Initialize(bodies, Detection, playersConfig, SetupCursors);
                if (firstRound)
                {
                    await Views.PushView(setupView);
                }
                else
                {
                    await Views.ReplaceView(setupView);
                }

                firstRound = false;
                var activePlayers = await setupView.RunAsync(cancellationToken);
                var outcome = activePlayers == null ? null : await PlayAsync(activePlayers, cancellationToken);
                if (outcome == null)
                {
                    NinjagoAnalytics.MiniGameEnd(Mode, "abandon", activePlayers?.Count ?? 0, new GameAnalyticsProperties());
                    break;
                }

                NinjagoAnalytics.MiniGameEnd(Mode, outcome.AnalyticsResult, activePlayers!.Count, outcome.AnalyticsDetails);
                var resultView = Instantiate(resultViewPrefab);
                resultView.Initialize(outcome);
                await Views.ReplaceView(resultView);
                if (!await resultView.WaitForChoiceAsync(cancellationToken)) break;
            }

            await BgmManager.Instance.FadeOut();
        }

        #endregion

        #region Gameplay

        /// <summary>Replaces the setup view with this game's view and plays it; null when the player backed out.</summary>
        protected abstract UniTask<NinjagoOutcome?> PlayAsync(IReadOnlyList<PlayerBody> activePlayers, CancellationToken cancellationToken);

        #endregion
    }
}
