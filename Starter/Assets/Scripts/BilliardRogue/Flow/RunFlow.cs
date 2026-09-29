#nullable enable

using System;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// One run from the menu's point of view: Calibration (camera session) → Gameplay (transaction, Title stays the
    /// root) → Summary (ReplaceView) → back to Title, plus Save &amp; Quit / Play Again. Every transition waits for
    /// the ViewManager to be idle. The GameSession applies the run to the meta progress itself; this only reads
    /// newRecord from the view.
    /// </summary>
    public sealed class RunFlow
    {
        readonly RunFlowContext ctx;
        readonly RunFactory runFactory = new();
        readonly Func<bool> viewManagerInTransition;
        int unlockTierAtRunStart;

        public RunFlow(RunFlowContext aCtx)
        {
            ctx = aCtx;
            viewManagerInTransition = () => ctx.viewManager.IsInTransition;
        }

        public CalibrationView? ActiveCalibration { get; private set; }
        public GameplayView? ActiveGameplay { get; private set; }
        public bool IsBusy => ActiveCalibration != null || ActiveGameplay != null || ctx.viewManager.IsInTransition;

        /// <summary>Player count of the last started run (Play Again reuses it).</summary>
        public int LastNumPlayers { get; set; } = 1;

        /// <summary>Seed for the next new run; 0 = DebugSettings.fixedSeed or random.</summary>
        public int NextSeed { get; set; }

        #region Calibration → Gameplay

        /// <summary>Pushes Calibration on the current top (Title or PlayerMode) and continues into gameplay when it succeeds.</summary>
        public async UniTask BeginCalibrationAsync(int numPlayers, bool isContinue)
        {
            if (IsBusy) return;
            LastNumPlayers = numPlayers;
            var camera = ctx.camera;
            if (camera.IsRunning && camera.NumPlayers != numPlayers && camera.ReloadSceneOnPlayerCountChange)
            {
                await ReloadMainSceneAsync(numPlayers, isContinue);
                return;
            }

            var calibration = UnityEngine.Object.Instantiate(ctx.calibrationViewPrefab);
            calibration.Initialize(camera, numPlayers, ctx.persistence.MetaProgress.tutorialSeen, ctx.calibrationShotInput);
            ActiveCalibration = calibration;
            await ctx.viewManager.PushView(calibration);
            var ready = await calibration.RunAsync(ctx.lifetime);
            ActiveCalibration = null;
            if (!ready)
            {
                // Back: the view popped itself; PlayerMode / Title is on top again.
                camera.Stop();
                return;
            }

            ctx.persistence.MarkTutorialSeen();
            await EnterGameplayAsync(isContinue);
        }

        async UniTask EnterGameplayAsync(bool isContinue)
        {
            var run = isContinue ? ctx.persistence.Load() : null;
            if (run == null)
            {
                // A vanished save turns Continue into a fresh run with the calibrated player count.
                run = CreateNewRun(ctx.camera.NumPlayers);
                isContinue = false;
            }

            var gameplay = UnityEngine.Object.Instantiate(ctx.gameplayViewPrefab);
            gameplay.Initialize(new GameplayViewContext
            {
                viewManager = ctx.viewManager,
                config = ctx.config,
                rules = ctx.rules,
                run = run,
                isContinue = isContinue,
                camera = ctx.camera,
                board = ctx.board,
                layout = ctx.layout,
                display = ctx.display,
                environment = ctx.environment,
                inputs = ctx.shotInputs(run.numPlayers, gameplay.InputRoot),
                persistence = ctx.persistence,
                analytics = new RunAnalytics(),
            });
            gameplay.RunEnded += HandleRunEnded;
            ActiveGameplay = gameplay;
            // "Unlocked by this run" for the summary: the tier before this run's boss kills moved it.
            unlockTierAtRunStart = ctx.persistence.MetaProgress.highestUnlockTier;

            // Title stays the root below Gameplay: PlayerMode and Calibration go without activating in between, and
            // the views they reveal stay faded out (KeepHidden) so neither PlayerMode nor the title flashes. The
            // whole swap happens under the curtain: calibration faded to it, GameplayView raised the same colour at
            // Initialize and fades it out once the stage intro band is on top.
            var covered = UnityEngine.Object.FindObjectsByType<RogueView>(FindObjectsSortMode.None);
            foreach (var view in covered) view.KeepHidden = true;
            try
            {
                using (ctx.viewManager.CreateTransaction())
                {
                    while (ctx.viewManager.TopViewIdentifier != View.ViewIdentifier.Title)
                    {
                        await ctx.viewManager.PopView(animate: false);
                    }

                    await ctx.viewManager.PushView(gameplay, animate: false);
                }
            }
            finally
            {
                foreach (var view in covered)
                {
                    if (view != null) view.KeepHidden = false;
                }
            }
        }

        RunState CreateNewRun(int numPlayers)
        {
            var seed = NextSeed != 0 ? NextSeed : PlayerDataManager.Instance.DebugSettings.fixedSeed;
            if (seed == 0) seed = UnityEngine.Random.Range(1, int.MaxValue);
            NextSeed = 0;
            // The session's TurnController calls persistence.BeginRun at its first stage (runsStarted + first save);
            // doing it here as well counted every new run twice on the title.
            return runFactory.NewRun(ctx.rules, seed, numPlayers);
        }

        #endregion

        #region Run end

        void HandleRunEnded(RunState run, RunOutcome outcome, bool newRecord)
        {
            FinishRunAsync(run, outcome, newRecord).Forget();
        }

        async UniTask FinishRunAsync(RunState run, RunOutcome outcome, bool newRecord)
        {
            ActiveGameplay = null;
            ctx.runEnded();
            await WaitForIdleAsync();
            ctx.camera.Stop();

            if (outcome == RunOutcome.Abandoned)
            {
                await ReturnToTitleAsync();
                return;
            }

            BgmManager.Instance.CrossFadeTo(BgmManager.BgmType.Reward, cancellationToken: ctx.lifetime).Forget();
            var summary = ctx.summaryView(run, ctx.persistence.MetaProgress, newRecord, unlockTierAtRunStart);
            using (ctx.viewManager.CreateTransaction())
            {
                while (ctx.viewManager.TopViewIdentifier != View.ViewIdentifier.Gameplay)
                {
                    await ctx.viewManager.PopView(animate: false);
                }

                await ctx.viewManager.ReplaceView(summary);
            }
        }

        public async UniTask PlayAgainAsync()
        {
            await ReturnToTitleAsync();
            await BeginCalibrationAsync(LastNumPlayers, isContinue: false);
        }

        /// <summary>Pops everything above the Title (overlays instantly, the last full-screen view animated).</summary>
        public async UniTask ReturnToTitleAsync()
        {
            await WaitForIdleAsync();
            using (ctx.viewManager.CreateTransaction())
            {
                while (ctx.viewManager.TopViewIdentifier is not (View.ViewIdentifier.Title or View.ViewIdentifier.Empty))
                {
                    var top = ctx.viewManager.TopViewIdentifier;
                    var animate = top is View.ViewIdentifier.Gameplay or View.ViewIdentifier.Summary;
                    await ctx.viewManager.PopView(animate);
                }
            }

            ctx.returnedToTitle();
        }

        #endregion

        #region Helpers

        public UniTask WaitForIdleAsync()
        {
            return UniTask.WaitWhile(viewManagerInTransition, cancellationToken: ctx.lifetime);
        }

        /// <summary>TDD D1 fallback: remember the step, cover the screen and reload the scene for a new player count.</summary>
        async UniTask ReloadMainSceneAsync(int numPlayers, bool isContinue)
        {
            PlayerDataManager.Instance.appViewState.NextViewState = new PendingFlowState(numPlayers, isContinue);
            ctx.camera.Stop();
            await ScreenBlockerManager.Instance.Show();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        #endregion
    }
}
