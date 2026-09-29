#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using DG.Tweening;
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
        [Tooltip("The nested GameplayHud prefab instance (UI-Views module), wired by FlowViewPrefabsBuilder.")]
        [SerializeField] GameplayHud hud = null!;
        [SerializeField] GameSession session = null!;
        [SerializeField] TimeScaleController timeScale = null!;
        [Tooltip("Parent for the per-player shot input instances.")]
        [SerializeField] Transform inputRoot = null!;
        [SerializeField] GameplayPip pipPrefab = null!;

        [Header("Look")]
        [SerializeField] UiTheme theme = null!;
        [Tooltip("Opaque full-screen cover shown on Present (calibration ends on the same colour): the board is built "
            + "under it and it fades once the first overlay (stage intro or reward) is on top.")]
        [SerializeField] CanvasGroup curtain = null!;

        readonly GameplayPauseGate pauseGate = new();
        GameplayViewContext context = null!;
        // View.Manager is cleared when the view is dismissed; the pause events and overlays need it until OnDestroy.
        ViewManager manager = null!;
        GameplayPip pip = null!;
        Func<bool> managerInTransition = null!;
        Func<bool> overlaysBlocked = null!;
        bool hudRevealed;
        bool worldRevealed;
        // Overlay currently covering this view (the PiP overlay canvas would draw above it); null when on top.
        ViewIdentifier? coveringOverlay;
        bool runStarted;

        /// <summary>
        /// The run, how GameSession.RunAsync ended (Abandoned = Save &amp; Quit / cancelled / a faulted run loop, never
        /// written into the run) and whether the finished run set a new best stage.
        /// </summary>
        public event Action<RunState, RunOutcome, bool>? RunEnded;

        public RunState Run => context.run;
        public bool IsPaused => pauseGate.IsPaused;
        public Transform InputRoot => inputRoot;

        public override ViewIdentifier Identifier => ViewIdentifier.Gameplay;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "gameplay";

        #region Life Cycle

        protected override void Awake()
        {
            base.Awake();
            // RunFlow instantiates this view before it pops the views above the title: nothing shows until Initialize
            // raises the opaque curtain.
            canvas.enabled = false;
        }

        public override async UniTask Present(bool animate = true)
        {
            canvas.enabled = true;
            await base.Present(animate);
        }

        public override async UniTask EnterBackground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            coveringOverlay = childViewIdentifier;
            UpdatePip();
            await base.EnterBackground(childViewIdentifier, animate);
        }

        public override async UniTask EnterForeground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            coveringOverlay = null;
            UpdatePip();
            await base.EnterForeground(childViewIdentifier, animate);
        }

        public void Initialize(GameplayViewContext ctx)
        {
            context = ctx;
            manager = ctx.viewManager;
            managerInTransition = () => manager.IsInTransition;
            overlaysBlocked = () => pauseGate.BlocksOverlays || manager.IsInTransition;
            manager.PauseViewResumeClicked += HandlePauseResume;
            manager.PauseViewHomeClicked += HandlePauseHome;

            var detection = ctx.camera.Detection;
            // Its own Screen Space Overlay canvas (TDD D5), so it lives as a scene root and dies with this view.
            pip = Instantiate(pipPrefab);
            pip.Initialize(ctx.run.numPlayers, detection.PlayAreaController, detection.BodyPoseDetectionManager);
            pip.SetActivePlayer(ctx.run.numPlayers > 1 ? ctx.run.activePlayerIndex : -1);
            pip.SetVisible(false);

            hud.Initialize(ctx.config.Balls, ctx.config.Pacing);
            // Revealed after the first stage intro band (or by the first turn banner).
            hud.SetRevealed(false, false);

            // Calibration has faded to the curtain colour: cover the screen from now on (the canvas renders on top
            // until the push gives it its camera), so its pop and the board build never show.
            curtain.alpha = 1f;
            canvas.enabled = true;

            ctx.board.Initialize(ctx.config, ctx.rules, ctx.layout, ctx.display, worldLabelLayer);
            session.Initialize(new GameSessionContext
            {
                config = ctx.config,
                rules = ctx.rules,
                run = ctx.run,
                isContinue = ctx.isContinue,
                board = ctx.board,
                hud = new GameplayHudRelay(hud, pip),
                flowHost = this,
                inputs = ctx.inputs,
                persistence = ctx.persistence,
                analytics = ctx.analytics,
                timeScale = timeScale,
                display = ctx.display,
            });
        }

        public override void ViewDidBecomeTopView(bool afterPush)
        {
            base.ViewDidBecomeTopView(afterPush);
            if (!runStarted)
            {
                runStarted = true;
                RunAsync(destroyCancellationToken).Forget();
                return;
            }

            if (!pauseGate.ShouldResumeOnTop()) return;
            HandlePauseResume();
        }

        void OnDestroy()
        {
            manager.PauseViewResumeClicked -= HandlePauseResume;
            manager.PauseViewHomeClicked -= HandlePauseHome;
            // A scene root of its own: on scene unload it can be destroyed before this view.
            if (pip != null)
            {
                Destroy(pip.gameObject);
            }

            context.board.Clear();
        }

        #endregion

        #region Run

        async UniTaskVoid RunAsync(CancellationToken ct)
        {
            // Leave the push continuation before the session pushes its first overlay.
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            var outcome = RunOutcome.Abandoned;
            try
            {
                var run = session.RunAsync(ct);
                // Subscribe replays the current value, so a run that starts while the platform is stopped opens on
                // the pause view. Only a true pauses: the player resumes through the pause view.
                CherryIntegrationManager.Instance.PreferGameStopped.Subscribe(HandlePreferGameStopped, ct);
                outcome = await run;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // A faulted run loop still hands the flow back: the coordinator returns to the title and the last
                // turn-boundary save stays on disk.
                Debug.LogException(e);
            }

            pauseGate.EndRun();
            RunEnded?.Invoke(context.run, outcome, session.NewRecord);
        }

        public void NotifyRunEnded(RunState run)
        {
            pauseGate.EndRun();
            pip.SetActivePlayer(-1);
        }

        #endregion

        #region Reveal

        /// <summary>Fades the opening curtain out (once): the first overlay is on top, so the built board shows under it.</summary>
        void RevealWorld()
        {
            if (worldRevealed) return;
            worldRevealed = true;
            curtain.DOFade(0f, theme.CurtainFadeOutDuration).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>HUD columns and the PiP feed together: hidden while the stage intro band is up.</summary>
        void SetHudRevealed(bool show)
        {
            hudRevealed = show;
            hud.SetRevealed(show, true);
            UpdatePip();
        }

        // The feed shows with the HUD, except under an overlay other than tracking lost (where it helps the player
        // step back into view).
        void UpdatePip()
        {
            var covered = coveringOverlay != null && coveringOverlay != ViewIdentifier.TrackingLost;
            var visible = hudRevealed && !covered;
            pip.SetVisible(visible, visible ? theme.HudRevealDuration : theme.DismissDuration);
        }

        #endregion

        #region Pause

        public override void OnBackButton() => RequestPause();

        /// <summary>Top-level Back and Escape; the platform pause goes through HandlePreferGameStopped.</summary>
        public void RequestPause()
        {
            if (!IsActive) return;
            BeginPause();
        }

        void HandlePreferGameStopped(bool stopped)
        {
            if (!stopped) return;
            BeginPause();
        }

        // Also runs while an overlay is on top or closing: the pause view is pushed once the ViewManager is idle, and
        // GameSession.RequestPause owns the pause analytics.
        void BeginPause()
        {
            if (!pauseGate.TryBegin()) return;
            RevealWorld();
            session.RequestPause(true);
            context.camera.Pause();
            manager.AnnouncePaused();
            PushPauseOverlayAsync(destroyCancellationToken).Forget();
        }

        void HandlePauseResume()
        {
            if (!pauseGate.TryResume()) return;
            session.RequestPause(false);
            context.camera.UnPause();
        }

        void HandlePauseHome()
        {
            if (!pauseGate.IsPaused) return;
            // Save & Quit: the last turn-boundary save stays on disk, RunAsync completes with Abandoned and the
            // coordinator unwinds to the title.
            session.RequestSaveAndQuit();
        }

        #endregion
    }
}
