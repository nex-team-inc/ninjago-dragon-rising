#nullable enable

using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Owns the view flow of Main.unity (TDD §9): the Title menu and its Settings / Exit, and hands runs to RunFlow
    /// (Calibration → Gameplay → Summary → Title). Views raise events; the coordinator performs the transitions and
    /// guards them against IsInTransition. Debug hooks (TDD D7) live in the Debug partial.
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

        RunPersistence persistence = null!;
        GameRules rules = null!;
        RunFlow runFlow = null!;
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
            UnregisterDebugHooks();
        }

        #endregion

        #region Public Methods

        public async UniTask Initialize()
        {
            if (prepared) return;
            preparationSource = new UniTaskCompletionSource();
            await preparationSource.Task;
        }

        public async UniTask StartMain()
        {
            persistence = new RunPersistence(PlayerDataManager.Instance);
            rules = RulesFactory.Build(config);
            cameraSession.Initialize(detectionManagerPrefab, detectionEnginePrefab, detectionRoot);
            // The world display sits behind every view, so the rig and the Title look exist before the first push.
            worldCameraRig.Initialize(rootCamera);
            worldCameraRig.SetPose(config.Arena.CameraPosition, config.Arena.CameraPitchDeg, config.Arena.CameraFov);
            actEnvironment.Initialize(config.Arena);
            actEnvironment.ApplyTitle(config.Acts[0], instant: true);
            runFlow = new RunFlow(new RunFlowContext
            {
                viewManager = viewManager,
                config = config,
                rules = rules,
                persistence = persistence,
                camera = cameraSession,
                calibrationViewPrefab = calibrationViewPrefab,
                gameplayViewPrefab = gameplayViewPrefab,
                board = boardPresenter,
                layout = arenaLayout,
                display = worldCameraRig.Display,
                environment = actEnvironment,
                calibrationShotInput = CreateCalibrationShotInput,
                shotInputs = CreateShotInputs,
                summaryView = CreateSummaryView,
                runEnded = RegisterDebugHooks,
                returnedToTitle = HandleReturnedToTitle,
                lifetime = destroyCancellationToken,
            })
            {
                LastNumPlayers = Mathf.Clamp(PlayerDataManager.Instance.PlayerPreference.numPlayers, 1, 2),
            };
            RegisterDebugHooks();

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
