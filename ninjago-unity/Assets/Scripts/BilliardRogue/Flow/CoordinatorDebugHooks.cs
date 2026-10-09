#nullable enable

using Cysharp.Threading.Tasks;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// DebugHooks flow commands (TDD D7), driven from the Editor CLI:
    /// <c>unity command eval 'return Nex.BilliardRogue.DebugHooks.StartNewRun(1, 42);'</c>.
    /// The gameplay module replaces the State hook while a run is alive; the coordinator registers again at run end.
    /// </summary>
    public sealed class CoordinatorDebugHooks
    {
        readonly RunFlow runFlow;
        readonly RunPersistence persistence;
        readonly CameraSession cameraSession;
        readonly ViewManager viewManager;

        public CoordinatorDebugHooks(RunFlow aRunFlow, RunPersistence aPersistence, CameraSession aCameraSession, ViewManager aViewManager)
        {
            runFlow = aRunFlow;
            persistence = aPersistence;
            cameraSession = aCameraSession;
            viewManager = aViewManager;
        }

        #region Public Methods

        public void Register()
        {
            DebugHooks.StartNewRunHandler = HandleStartNewRun;
            DebugHooks.ContinueRunHandler = HandleContinueRun;
            DebugHooks.SkipCalibrationHandler = HandleSkipCalibration;
            DebugHooks.ChooseRewardHandler = HandleChooseReward;
            DebugHooks.StateHandler = DescribeState;
        }

        public static void Unregister()
        {
            DebugHooks.StartNewRunHandler = null;
            DebugHooks.ContinueRunHandler = null;
            DebugHooks.SkipCalibrationHandler = null;
            DebugHooks.ChooseRewardHandler = null;
            DebugHooks.StateHandler = null;
        }

        #endregion

        #region Handlers

        bool HandleStartNewRun(int players, int seed)
        {
            if (players is < 1 or > 2 || runFlow.IsBusy)
            {
                return false;
            }

            runFlow.NextSeed = seed;
            StartAsync(players, isContinue: false).Forget();
            return true;
        }

        bool HandleContinueRun()
        {
            if (runFlow.IsBusy)
            {
                return false;
            }

            var run = persistence.Load();
            if (run == null)
            {
                return false;
            }

            StartAsync(run.numPlayers, isContinue: true).Forget();
            return true;
        }

        async UniTask StartAsync(int numPlayers, bool isContinue)
        {
            // Unwind Settings / PlayerMode / Summary first so Calibration sits on the Title like a menu-driven start.
            await runFlow.ReturnToTitleAsync();
            await runFlow.BeginCalibrationAsync(numPlayers, isContinue);
        }

        bool HandleSkipCalibration()
        {
            var calibration = runFlow.ActiveCalibration;
            if (calibration == null)
            {
                return false;
            }

            calibration.Skip();
            return true;
        }

        bool HandleChooseReward(int index)
        {
            var gameplay = runFlow.ActiveGameplay;
            return gameplay != null && gameplay.ChooseRewardForDebug(index);
        }

        string DescribeState()
        {
            var camera = cameraSession.IsRunning
                ? $"{cameraSession.NumPlayers}P{(cameraSession.IsPaused ? " paused" : "")}"
                : "off";
            var calibration = runFlow.ActiveCalibration;
            var gameplay = runFlow.ActiveGameplay;
            var run = gameplay != null ? gameplay.Run : null;
            var runText = run == null
                ? "none"
                : $"stage {run.stageNumber} (act {run.actIndex + 1} stage {run.stageInAct + 1}) turn {run.turnInStage} hp {run.playerHp}/{run.playerMaxHp} bag {run.bag.Count} outcome {run.outcome} players {run.numPlayers}";
            return $"top={viewManager.TopViewIdentifier} transition={viewManager.IsInTransition} camera={camera}"
                   + $" calibration={(calibration != null ? calibration.CurrentStep.ToString() : "-")}"
                   + $" gameplay={(gameplay != null ? (gameplay.IsPaused ? "paused" : "running") : "-")}"
                   + $" run={runText} save={persistence.HasSave}";
        }

        #endregion
    }
}
