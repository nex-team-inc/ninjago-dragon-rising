#nullable enable

using Cysharp.Threading.Tasks;

namespace Nex.BilliardRogue
{
    // DebugHooks flow commands (TDD D7), driven from the Editor CLI:
    // unity command eval 'return Nex.BilliardRogue.DebugHooks.StartNewRun(1, 42);'
    // The gameplay module replaces the State hook while a run is alive; RunFlow hands it back at run end.
    public sealed partial class BilliardRogueCoordinator
    {
        void RegisterDebugHooks()
        {
            DebugHooks.StartNewRunHandler = HandleDebugStartNewRun;
            DebugHooks.ContinueRunHandler = HandleDebugContinueRun;
            DebugHooks.SkipCalibrationHandler = HandleDebugSkipCalibration;
            DebugHooks.ChooseRewardHandler = HandleDebugChooseReward;
            DebugHooks.StateHandler = DescribeState;
        }

        void UnregisterDebugHooks()
        {
            DebugHooks.StartNewRunHandler = null;
            DebugHooks.ContinueRunHandler = null;
            DebugHooks.SkipCalibrationHandler = null;
            DebugHooks.ChooseRewardHandler = null;
            DebugHooks.StateHandler = null;
        }

        bool HandleDebugStartNewRun(int players, int seed)
        {
            if (players is < 1 or > 2 || runFlow.IsBusy) return false;
            runFlow.NextSeed = seed;
            DebugStartAsync(players, isContinue: false).Forget();
            return true;
        }

        bool HandleDebugContinueRun()
        {
            if (runFlow.IsBusy) return false;
            var run = persistence.Load();
            if (run == null) return false;
            DebugStartAsync(run.numPlayers, isContinue: true).Forget();
            return true;
        }

        async UniTask DebugStartAsync(int numPlayers, bool isContinue)
        {
            // Unwind Settings / PlayerMode / Summary first so Calibration sits on the Title like a menu-driven start.
            await runFlow.ReturnToTitleAsync();
            await runFlow.BeginCalibrationAsync(numPlayers, isContinue);
        }

        bool HandleDebugSkipCalibration()
        {
            var calibration = runFlow.ActiveCalibration;
            if (calibration == null) return false;
            calibration.Skip();
            return true;
        }

        bool HandleDebugChooseReward(int index)
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
    }
}
