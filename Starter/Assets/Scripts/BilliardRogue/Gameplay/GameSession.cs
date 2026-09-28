#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Lives under the GameplayView. Runs the turn state machine (StageIntro → PlayerTurn → EnemyPhase →
    /// StageClear/Reward | Defeat | Victory) over the simulation, drives BoardPresenter/HUD, saves at turn
    /// boundaries and reports analytics. Update drives everything with unscaled time (TimeScaleController is
    /// the single writer of Time.timeScale); edit-mode smoke runs call Tick directly.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        /// <summary>Raised after every RunPersistence save (stage start, turn start, reward, app pause).</summary>
        public event Action<RunState>? Saved;

        GameSessionContext context = null!;
        SessionServices services = null!;
        TurnController turns = null!;
        PauseState pause = null!;
        UniTaskCompletionSource<RunOutcome> completion = null!;
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
        GameplayDebugCommands? debugCommands;
#endif
        CancellationTokenRegistration cancelRegistration;
        bool initialized;
        bool running;
        bool quitting;

        public GameSessionContext Context => context;

        public TurnPhase Phase => turns.Phase;

        /// <summary>True between RunAsync and the run's completion.</summary>
        public bool IsRunning => running;

        /// <summary>True when the finished run set a new best stage (RunPersistence.CompleteRun); the summary shows it.</summary>
        public bool NewRecord => turns.NewRecord;

        public bool IsPaused => pause.MenuPaused;

        #region Public Methods

        public void Initialize(GameSessionContext ctx)
        {
            context = ctx;
            var pacing = ctx.config.Pacing;
            ctx.timeScale.Initialize(pacing.FastForwardScale);
            pause = new PauseState(ctx.timeScale);
            var sim = new RunSimulation(ctx.rules, ctx.run);
            var analytics = new SessionAnalytics(ctx.analytics);
            var sequencer = new ShotSequencer(ctx.run, pacing.ShotCooldown);
            var debug = ctx.debugSettings ?? (ctx.headless ? new DebugSettings() : PlayerDataManager.Instance.DebugSettings);
            services = new SessionServices(ctx, sim, new BoardDriver(ctx.headless ? null : ctx.board), new HudBinder(ctx.hud, ctx.run, sim, sequencer),
                new SessionAudio(!ctx.headless, pacing.BgmCrossfadeSeconds), analytics, pause, debug, sequencer, new ShotResultTracker(analytics));
            turns = new TurnController(services);
            ctx.persistence.Saved += HandleSaved;
            initialized = true;
        }

        /// <summary>
        /// Runs until the run ends or ct is cancelled; returns the outcome. Cancellation and Save &amp; Quit return
        /// Abandoned as the result only: never write Abandoned into RunState.outcome or save after it, because
        /// RunPersistence.Load drops any save whose outcome is not None and the player expects to continue.
        /// Only RunPersistence.Abandon() drops the save on purpose.
        /// </summary>
        public UniTask<RunOutcome> RunAsync(CancellationToken ct)
        {
            completion = new UniTaskCompletionSource<RunOutcome>();
            running = true;
            cancelRegistration = ct.Register(HandleCancelled);
            turns.Start(context.isContinue, ct);
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            debugCommands = new GameplayDebugCommands(services, turns);
            debugCommands.Register();
#endif
            if (!context.headless) BindPlatformPause();
            return completion.Task;
        }

        /// <summary>Pause/resume from the pause view or platform (freezes the sim, TimeScaleController.SetPaused).</summary>
        public void RequestPause(bool paused)
        {
            if (!initialized || pause.MenuPaused == paused) return;
            pause.SetMenuPaused(paused);
            if (paused) services.Analytics.Pause();
            else services.Analytics.Resume();
        }

        /// <summary>
        /// Save &amp; Quit from the pause view: the last turn-boundary save (RunPersistence.SaveTurnBoundary) stays on
        /// disk with outcome None and RunAsync completes with Abandoned (see RunAsync).
        /// </summary>
        public void RequestSaveAndQuit()
        {
            Abandon();
        }

        /// <summary>One frame of the run loop; Update calls it with unscaled time and edit-mode smoke runs call it directly.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!running) return;
            context.timeScale.Tick(unscaledDeltaTime);
            if (!pause.MenuPaused) context.run.stats.playSeconds += unscaledDeltaTime;
            try
            {
                turns.Tick(unscaledDeltaTime);
            }
            catch (OperationCanceledException)
            {
                Abandon();
                return;
            }
            catch (Exception e)
            {
                running = false;
                completion.TrySetException(e);
                return;
            }

            if (turns.Phase == TurnPhase.Finished) Complete(turns.Result);
        }

        #endregion

        #region Life Cycle

        void Update() => Tick(Time.unscaledDeltaTime);

        // Singletons may already be gone during app shutdown, so a quitting session only stops ticking.
        void OnApplicationQuit() => quitting = true;

        void OnDestroy()
        {
            if (!initialized) return;
            if (quitting) running = false;
            else Abandon();
            cancelRegistration.Dispose();
            context.persistence.Saved -= HandleSaved;
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            debugCommands?.Unregister();
#endif
        }

        #endregion

        #region Helpers

        void Complete(RunOutcome outcome)
        {
            running = false;
            completion.TrySetResult(outcome);
        }

        void Abandon()
        {
            if (!running) return;
            turns.Abandon();
            Complete(RunOutcome.Abandoned);
        }

        void HandleCancelled() => Abandon();

        void HandleSaved(RunState run) => Saved?.Invoke(run);

        // Subscribe emits the current value first, so a game started while stopped pauses at once. The platform
        // never resumes by itself: the flow resumes through RequestPause(false) from the pause view.
        void BindPlatformPause()
        {
            CherryIntegrationManager.Instance.PreferGameStopped.Subscribe(HandlePreferGameStopped, destroyCancellationToken);
        }

        void HandlePreferGameStopped(bool stopped)
        {
            if (!stopped || !running) return;
            if (turns.IsStable) services.Persistence.SaveTurnBoundary(context.run);
            RequestPause(true);
        }

        #endregion
    }
}
