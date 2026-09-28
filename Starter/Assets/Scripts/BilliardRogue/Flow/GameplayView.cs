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
    /// Hosts one run: the GameSession + TimeScaleController under this view, the nested GameplayHud, the world label
    /// layer for BoardPresenter, and the PiP camera preview (its own overlay canvas). Back = pause. Implements
    /// IGameFlowHost for the session's overlays (see GameplayView.Overlays.cs) and raises RunEnded for the coordinator.
    /// </summary>
    public sealed partial class GameplayView : SimpleCanvasView, IGameFlowHost
    {
        [Header("Hosted")]
        [Tooltip("Full-screen layer BoardPresenter fills with HP labels and damage numbers (behind the HUD).")]
        [SerializeField] RectTransform worldLabelLayer = null!;
        [SerializeField] GameSession session = null!;
        [SerializeField] TimeScaleController timeScale = null!;
        [Tooltip("Parent for the per-player shot input instances.")]
        [SerializeField] Transform inputRoot = null!;
        [SerializeField] GameplayPip pipPrefab = null!;

        GameplayViewContext context = null!;
        // View.Manager is cleared when the view is dismissed; the pause events need it until OnDestroy.
        ViewManager manager = null!;
        GameplayPip pip = null!;
        Func<bool> managerInTransition = null!;
        bool runStarted;
        bool runEnded;
        bool paused;

        /// <summary>
        /// The run, how GameSession.RunAsync ended (Abandoned = Save &amp; Quit / cancelled, never written into the
        /// run) and whether the finished run set a new best stage.
        /// </summary>
        public event Action<RunState, RunOutcome, bool>? RunEnded;

        public RunState Run => context.run;
        public bool IsPaused => paused;
        public Transform InputRoot => inputRoot;

        public override ViewIdentifier Identifier => ViewIdentifier.Gameplay;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "gameplay";

        #region Life Cycle

        protected override void Awake()
        {
            base.Awake();
            // RunFlow instantiates this view before it pops the views above the title: nothing shows until Present.
            canvas.enabled = false;
        }

        public override async UniTask Present(bool animate = true)
        {
            canvas.enabled = true;
            pip.SetVisible(true);
            await base.Present(animate);
        }

        public void Initialize(GameplayViewContext ctx)
        {
            context = ctx;
            var detection = ctx.camera.Detection;
            // Its own Screen Space Overlay canvas (TDD D5), so it lives as a scene root and dies with this view.
            pip = Instantiate(pipPrefab);
            pip.Initialize(ctx.run.numPlayers, detection.PlayAreaController, detection.BodyPoseDetectionManager);
            pip.SetActivePlayer(ctx.run.numPlayers > 1 ? ctx.run.activePlayerIndex : -1);
            pip.SetVisible(false);

            // The HUD prefab (UI-Views) is nested by FlowPrefabsBuilder; a missing one degrades to a no-op relay.
            var hudWidget = GetComponentInChildren<GameplayHud>(true);
            if (hudWidget != null) hudWidget.Initialize(ctx.config.Balls, ctx.config.Pacing);
            var hud = new GameplayHudRelay(hudWidget, pip);
            ctx.board.Initialize(ctx.config, ctx.rules, ctx.layout, ctx.display, worldLabelLayer);
            session.Initialize(new GameSessionContext
            {
                config = ctx.config,
                rules = ctx.rules,
                run = ctx.run,
                isContinue = ctx.isContinue,
                board = ctx.board,
                hud = hud,
                flowHost = this,
                inputs = ctx.inputs,
                persistence = ctx.persistence,
                analytics = ctx.analytics,
                timeScale = timeScale,
                display = ctx.display,
            });

            // Subscribe replays the current value; only a true pauses, the player resumes through the pause view.
            CherryIntegrationManager.Instance.PreferGameStopped.Subscribe(HandlePreferGameStopped, destroyCancellationToken);
        }

        public override void ViewDidBecomeTopView(bool afterPush)
        {
            base.ViewDidBecomeTopView(afterPush);
            if (!runStarted)
            {
                runStarted = true;
                manager = Manager;
                managerInTransition = () => manager.IsInTransition;
                manager.PauseViewResumeClicked += HandlePauseResume;
                manager.PauseViewHomeClicked += HandlePauseHome;
                RunAsync(destroyCancellationToken).Forget();
                return;
            }

            // Safety net: a pause view dismissed through the top-level Back without announcing a resume.
            if (paused) HandlePauseResume();
        }

        void OnDestroy()
        {
            if (manager != null)
            {
                manager.PauseViewResumeClicked -= HandlePauseResume;
                manager.PauseViewHomeClicked -= HandlePauseHome;
            }

            if (pip != null) Destroy(pip.gameObject);
            if (context != null) context.board.Clear();
        }

        #endregion

        #region Run

        async UniTaskVoid RunAsync(CancellationToken ct)
        {
            // Leave the push continuation before the session pushes its first overlay.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            var outcome = await session.RunAsync(ct);
            runEnded = true;
            RunEnded?.Invoke(context.run, outcome, session.NewRecord);
        }

        public void NotifyRunEnded(RunState run)
        {
            runEnded = true;
            pip.SetActivePlayer(-1);
        }

        #endregion

        #region Pause

        public override void OnBackButton()
        {
            RequestPause();
        }

        /// <summary>Single pause entry point: top-level Back, Escape and the platform pause.</summary>
        public void RequestPause()
        {
            if (!IsActive || paused || runEnded) return;
            BeginPause();
        }

        void HandlePreferGameStopped(bool stopped)
        {
            if (!stopped || !runStarted || paused || runEnded) return;
            BeginPause();
        }

        // GameSession.RequestPause owns the pause analytics; this only adds the camera and the overlay.
        void BeginPause()
        {
            paused = true;
            session.RequestPause(true);
            context.camera.Pause();
            manager.AnnouncePaused();
            PushPauseOverlayAsync(destroyCancellationToken).Forget();
        }

        void HandlePauseResume()
        {
            if (!paused) return;
            paused = false;
            session.RequestPause(false);
            context.camera.UnPause();
        }

        void HandlePauseHome()
        {
            if (!paused) return;
            // Save & Quit: the last turn-boundary save stays on disk, RunAsync completes with Abandoned and the
            // coordinator unwinds to the title.
            session.RequestSaveAndQuit();
        }

        #endregion
    }
}
