#nullable enable

using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Owns the view flow of Main.unity (TDD §9): the Title menu and its Settings / Exit, and hands runs to RunFlow
    /// (Calibration → Gameplay → Summary → Title). Views raise events; the coordinator performs the transitions and
    /// guards them against IsInTransition. Shot inputs come from PlayerShotInputFactory and the DebugHooks flow
    /// commands (TDD D7) from CoordinatorDebugHooks; the menu view factories live in the Views partial.
    /// </summary>
    public sealed partial class BilliardRogueCoordinator : MonoBehaviour
    {
        [Header("Framework")]
        [SerializeField] ViewManager viewManager = null!;
        [SerializeField] BilliardRogueConfig config = null!;
        [SerializeField] CameraSession cameraSession = null!;

        [Header("Flow views")]
        [SerializeField] CalibrationView calibrationViewPrefab = null!;
        [SerializeField] GameplayView gameplayViewPrefab = null!;

        [Header("Detection")]
        [SerializeField] DetectionManager detectionManagerPrefab = null!;
        [Tooltip("Hidden-node engine variant (Input module); the starter engine is the fallback.")]
        [SerializeField] OnePlayerDetectionEngine detectionEnginePrefab = null!;
        [SerializeField] Transform detectionRoot = null!;

        [Header("World (scene instances, wired by MainSceneBuilder)")]
        [SerializeField] BoardPresenter boardPresenter = null!;
        [SerializeField] ArenaLayout arenaLayout = null!;
        [SerializeField] WorldCameraRig worldCameraRig = null!;
        [SerializeField] ActEnvironmentController actEnvironment = null!;
        [Tooltip("The view manager's RootCamera: the UI camera the world display canvas renders on.")]
        [SerializeField] Camera rootCamera = null!;

        [Header("Input (Input module prefab, wired by FlowPrefabsBuilder)")]
        [Tooltip("One instance per player: PawShotInput + DebugShotInput + AutoAimBot + ShotInputRouter.")]
        [SerializeField] GameObject? playerShotInputPrefab;

        [Header("Debug (wired by FlowPrefabsBuilder)")]
        [Tooltip("Control lab readout (DebugSettings.showControlReadout, off by default); instantiated in Editor / development / ENABLE_DEBUG_SETTINGS builds only.")]
        [SerializeField] ControlReadoutOverlay? controlReadoutPrefab;

        RunPersistence persistence = null!;
        GameRules rules = null!;
        RunFlow runFlow = null!;
        CoordinatorDebugHooks debugHooks = null!;
        bool prepared;
        UniTaskCompletionSource? preparationSource;

        #region Life Cycle

        void OnEnable()
        {
            if (prepared) return;
            prepared = true;
            preparationSource?.TrySetResult();
            preparationSource = null;
        }

        void OnDestroy()
        {
            viewManager.SecretCodeEntered -= HandleSecretCodeEntered;
            CoordinatorDebugHooks.Unregister();
        }

        #endregion

        #region Public Methods

        /// <summary>Completes once this component is enabled (the scene activates the flow objects after the initializer).</summary>
        public async UniTask WaitUntilEnabledAsync()
        {
            if (prepared) return;
            preparationSource = new UniTaskCompletionSource();
            await preparationSource.Task;
        }

        public async UniTask StartMainAsync()
        {
            rules = RulesFactory.Build(config);
            persistence = new RunPersistence(PlayerDataManager.Instance, rules);
            cameraSession.Initialize(detectionManagerPrefab, detectionEnginePrefab, detectionRoot);
            // The world display sits behind every view, so the rig and the Title look exist before the first push.
            worldCameraRig.Initialize(rootCamera);
            worldCameraRig.SetPose(config.Arena.CameraPosition, config.Arena.CameraPitchDeg, config.Arena.CameraFov);
            actEnvironment.Initialize(config.Arena);
            actEnvironment.ApplyTitle(config.Acts[0], instant: true);
            var readout = CreateControlReadout();
            var shotInputs = new PlayerShotInputFactory(playerShotInputPrefab, cameraSession, config.Control, rules,
                worldCameraRig.WorldCamera, arenaLayout, ActiveRun, readout != null ? readout.Track : null);
            runFlow = new RunFlow(new RunFlowContext
            {
                viewManager = viewManager,
                config = config,
                rules = rules,
                persistence = persistence,
                camera = cameraSession,
                pipPreviewInterval = () => config.Visual.PipPreviewInterval(worldCameraRig.Tier),
                calibrationViewPrefab = calibrationViewPrefab,
                gameplayViewPrefab = gameplayViewPrefab,
                board = boardPresenter,
                layout = arenaLayout,
                display = worldCameraRig.Display,
                environment = actEnvironment,
                calibrationShotInput = shotInputs.CreateCalibrationShotInput,
                shotInputs = shotInputs.CreateShotInputs,
                summaryView = CreateSummaryView,
                runEnded = RegisterDebugHooks,
                returnedToTitle = HandleReturnedToTitle,
                lifetime = destroyCancellationToken,
            })
            {
                LastNumPlayers = Mathf.Clamp(PlayerDataManager.Instance.PlayerPreference.numPlayers, 1, 2),
            };
            debugHooks = new CoordinatorDebugHooks(runFlow, persistence, cameraSession, viewManager);
            RegisterDebugHooks();
            // Raised only in debug builds, where the view manager binds the secret code to Debug Settings.
            viewManager.SecretCodeEntered += HandleSecretCodeEntered;

            var pending = PendingFlowState.Take(PlayerDataManager.Instance.appViewState);
            if (pending == null)
            {
                await ShowTitleAsync(animate: true);
                return;
            }

            using (viewManager.CreateTransaction())
            {
                await ShowTitleAsync(animate: false);
            }

            await runFlow.BeginCalibrationAsync(pending.NumPlayers, pending.IsContinue);
        }

        #endregion

        #region Helpers

        // The gameplay module replaces the State hook while a run is alive: RunFlow hands it back at run end.
        void RegisterDebugHooks()
        {
            debugHooks.Register();
        }

        static void HandleSecretCodeEntered(View view)
        {
            RunAnalytics.SecretCode(view.AnalyticsScreenName);
        }

        /// <summary>The control lab readout, following every shot input the factory builds; null in release builds.</summary>
        ControlReadoutOverlay? CreateControlReadout()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            if (controlReadoutPrefab == null) return null;
            var overlay = Instantiate(controlReadoutPrefab, transform);
            overlay.Initialize(IsControlReadoutShown);
            return overlay;
#else
            return null;
#endif
        }

#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
        // The calibration test strike and gameplay (also under the tracking-lost overlay); hidden under pause,
        // rewards, stage intros and Debug Settings.
        bool IsControlReadoutShown()
        {
            if (!PlayerDataManager.Instance.DebugSettings.showControlReadout) return false;
            return viewManager.TopViewIdentifier is View.ViewIdentifier.Calibration or View.ViewIdentifier.Gameplay
                or View.ViewIdentifier.TrackingLost;
        }
#endif

        /// <summary>The run being played, for the bot; null during calibration and on the title.</summary>
        RunState? ActiveRun()
        {
            var gameplay = runFlow.ActiveGameplay;
            return gameplay != null ? gameplay.Run : null;
        }

        #endregion

        #region Title

        async UniTask ShowTitleAsync(bool animate)
        {
            PlayTitleBgm();
            await viewManager.PushView(CreateTitleView(persistence.Load(), persistence.MetaProgress), animate);
        }

        void HandleReturnedToTitle()
        {
            RefreshTitle();
            PlayTitleBgm();
            actEnvironment.ApplyTitle(config.Acts[0]);
        }

        void HandleNewRunRequested()
        {
            if (viewManager.IsInTransition) return;
            viewManager.PushView(CreatePlayerModeView()).Forget();
        }

        // PlayerModeView already saved the choice into PlayerPreference.numPlayers.
        void HandlePlayersChosen(int numPlayers)
        {
            runFlow.BeginCalibrationAsync(Mathf.Clamp(numPlayers, 1, 2), isContinue: false).Forget();
        }

        void HandleContinueRequested()
        {
            var run = persistence.Load();
            if (run == null)
            {
                RefreshTitle();
                return;
            }

            runFlow.BeginCalibrationAsync(run.numPlayers, isContinue: true).Forget();
        }

        void HandleSettingsRequested()
        {
            if (viewManager.IsInTransition) return;
            viewManager.PushView(CreateSettingsView()).Forget();
        }

        void HandlePlayAgainRequested()
        {
            runFlow.PlayAgainAsync().Forget();
        }

        void HandleTitleRequested()
        {
            runFlow.ReturnToTitleAsync().Forget();
        }

        static void HandleExitRequested()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Nex.Platform.DeviceActionDelegate.Instance.ExitGame();
#endif
        }

        void PlayTitleBgm()
        {
            var bgm = BgmManager.Instance;
            if (bgm.Current == BgmManager.BgmType.Title) return;
            bgm.CrossFadeTo(BgmManager.BgmType.Title, cancellationToken: destroyCancellationToken).Forget();
        }

        #endregion
    }
}
