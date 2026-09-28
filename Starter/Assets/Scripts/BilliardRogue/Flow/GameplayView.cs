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

        GameplayViewContext context = null!;
        // View.Manager is cleared when the view is dismissed; the pause events need it until OnDestroy.
        ViewManager manager = null!;
        GameplayPip pip = null!;
        GameplayHud? hudWidget;
        Func<bool> managerInTransition = null!;
        bool hudRevealed;
        bool worldRevealed;
        // Overlay currently covering this view (the PiP overlay canvas would draw above it); null when on top.
        ViewIdentifier? coveringOverlay;
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
            var detection = ctx.camera.Detection;
            // Its own Screen Space Overlay canvas (TDD D5), so it lives as a scene root and dies with this view.
            pip = Instantiate(pipPrefab);
            pip.Initialize(ctx.run.numPlayers, detection.PlayAreaController, detection.BodyPoseDetectionManager);
            pip.SetActivePlayer(ctx.run.numPlayers > 1 ? ctx.run.activePlayerIndex : -1);
            pip.SetVisible(false);

            // The HUD prefab (UI-Views) is nested by FlowPrefabsBuilder; a missing one degrades to a no-op relay.
            hudWidget = GetComponentInChildren<GameplayHud>(true);
            if (hudWidget != null)
            {
                hudWidget.Initialize(ctx.config.Balls, ctx.config.Pacing);
                // Revealed after the first stage intro band (or by the first turn banner).
                hudWidget.SetRevealed(false, false);
            }

            // Calibration has faded to the curtain colour: cover the screen from now on (the canvas renders on top
            // until the push gives it its camera), so its pop and the board build never show.
            curtain.alpha = 1f;
            canvas.enabled = true;

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
            if (hudWidget != null) hudWidget.SetRevealed(show, true);
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
            RevealWorld();
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
