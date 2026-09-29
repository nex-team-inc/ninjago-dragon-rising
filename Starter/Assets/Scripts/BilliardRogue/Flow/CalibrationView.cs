#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Nex.Dev;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Builds the paw input used for the live test strike; null when no input source is available.</summary>
    public delegate IShotInput? CalibrationShotInputFactory(int playerIndex, OnePlayerDetectionEngine engine, Transform parent);

    /// <summary>
    /// Camera setup step: starts the camera session, runs the starter setup (move into frame → raise hand) with the
    /// PreviewsManager on the camera panel, then the pose tutorial and a live test strike per player (player cards on
    /// the same panel). The controls card (animated illustration + left paw / right paw / strike rows) stays up the
    /// whole time; numbered pips show the step and the prompt says what to do. On success the view fades to the
    /// curtain colour GameplayView opens on. Back cancels (the view pops itself); DebugSettings.skipCalibration /
    /// DebugHooks.SkipCalibration / the S key skip steps in debug builds.
    /// </summary>
    public sealed class CalibrationView : SimpleCanvasView
    {
        public enum Step { Idle, Starting, MoveIn, RaiseHand, Tutorial, TestStrike, Ready }

        const int MaxPlayers = 2;

        [Header("Setup previews")]
        [SerializeField] PreviewsManager previewsManager = null!;

        [Header("Controls card")]
        [SerializeField] CalibrationTutorialIllustration illustration = null!;
        [Tooltip("Shown instead of the paw rows' footnote when the left-handed cue is on.")]
        [SerializeField] TextLabel hintLabel = null!;

        [Header("Camera panel")]
        [Tooltip("Camera glyph on an empty screen while the camera starts; the setup previews replace it.")]
        [SerializeField] CanvasGroup placeholderGroup = null!;
        [Tooltip("'Stand in front of the camera…' line under the setup previews.")]
        [SerializeField] CanvasGroup setupTipGroup = null!;
        [Tooltip("Player cards for the pose tutorial and the test strike.")]
        [SerializeField] CanvasGroup playersGroup = null!;
        [Tooltip("Index = player index; cards beyond the player count are hidden.")]
        [SerializeField] CalibrationPlayerCard[] playerCards = null!;
        [Tooltip("Card centres for 1 player (index 0) and 2 players (indices 1, 2).")]
        [SerializeField] Vector2[] cardPositions = { new(0f, 0f), new(-232f, 0f), new(232f, 0f) };

        [Header("Progress")]
        [SerializeField] CalibrationStepPips stepPips = null!;
        [SerializeField] TextLabel promptLabel = null!;
        [SerializeField] UiTheme theme = null!;
        [Tooltip("Full-screen cover faded in on success (GameplayView opens on the same colour).")]
        [SerializeField] CanvasGroup curtain = null!;
        [SerializeField] Transform inputRoot = null!;

        [Header("Timing")]
        [Tooltip("How long the pose tutorial stays up before the test strike (first run).")]
        [SerializeField, Range(0.5f, 10f)] float tutorialSeconds = 3f;
        [Tooltip("Tutorial hold once the meta progress says the tutorial was seen.")]
        [SerializeField, Range(0f, 10f)] float tutorialSecondsSeen = 1.2f;
        [Tooltip("Pause on 'Ready!' before gameplay starts.")]
        [SerializeField, Range(0f, 5f)] float readySeconds = 1f;
        [Tooltip("Without a paw input source the test strike auto-passes after this delay.")]
        [SerializeField, Range(0f, 5f)] float autoPassSeconds = 0.5f;

        CameraSession cameraSession = null!;
        CalibrationShotInputFactory shotInputFactory = null!;
        int numPlayers;
        bool tutorialSeen;
        readonly IShotInput?[] inputs = new IShotInput?[MaxPlayers];
        readonly bool[] playerReady = new bool[MaxPlayers];
        readonly UniTaskCompletionSource<bool> result = new();
        UniTaskCompletionSource stepSkip = new();
        CancellationTokenSource? flowCts;
        bool skipAll;
        float stepStartTime;
        float flowStartTime;

        public Step CurrentStep { get; private set; }

        public override ViewIdentifier Identifier => ViewIdentifier.Calibration;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "calibration";

        #region Life Cycle

        public void Initialize(CameraSession aCamera, int aNumPlayers, bool aTutorialSeen, CalibrationShotInputFactory aShotInputFactory)
        {
            cameraSession = aCamera;
            numPlayers = aNumPlayers;
            tutorialSeen = aTutorialSeen;
            shotInputFactory = aShotInputFactory;
            curtain.alpha = 0f;
            playersGroup.alpha = 0f;
            placeholderGroup.alpha = 1f;
            setupTipGroup.alpha = 1f;
            hintLabel.gameObject.SetActive(true);
            for (var i = 0; i < playerCards.Length; i++)
            {
                var shown = i < numPlayers;
                playerCards[i].gameObject.SetActive(shown);
                if (!shown) continue;
                ((RectTransform)playerCards[i].transform).anchoredPosition = cardPositions[numPlayers > 1 ? i + 1 : 0];
                playerCards[i].Set(i, false);
            }

            stepPips.Set(-1);
            promptLabel.SetKey(LocKeys.Calibration.Starting);
        }

        public override void ViewDidBecomeTopView(bool afterPush)
        {
            base.ViewDidBecomeTopView(afterPush);
            illustration.Play();
        }

        void Update()
        {
            if (!IsActive) return;
            if (CurrentStep is not (Step.Tutorial or Step.TestStrike)) return;
            if (DebugInput.GetKeyDown(KeyCode.S))
            {
                stepSkip.TrySetResult();
            }
        }

        void OnDestroy()
        {
            result.TrySetResult(false);
            flowCts?.Cancel();
            flowCts?.Dispose();
        }

        #endregion

        #region Public Methods

        /// <summary>Runs every step; true when the players are ready, false after Back (the view has popped itself).</summary>
        public UniTask<bool> RunAsync(CancellationToken ct)
        {
            if (CurrentStep == Step.Idle && !result.Task.Status.IsCompleted())
            {
                FlowAsync(ct).Forget();
            }

            return result.Task;
        }

        /// <summary>Debug: passes the current and every remaining step (the camera is still configured for gameplay).</summary>
        public void Skip()
        {
            skipAll = true;
            stepSkip.TrySetResult();
        }

        public override void OnBackButton()
        {
            if (!IsActive) return;
            RunAnalytics.UiBack(AnalyticsScreenName);
            result.TrySetResult(false);
            flowCts?.Cancel();
            PopSelf().Forget();
        }

        #endregion

        #region Flow

        async UniTaskVoid FlowAsync(CancellationToken externalCt)
        {
            flowCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt, destroyCancellationToken);
            try
            {
                await RunStepsAsync(flowCts.Token);
                result.TrySetResult(true);
            }
            catch (OperationCanceledException)
            {
                result.TrySetResult(false);
            }
        }

        async UniTask RunStepsAsync(CancellationToken ct)
        {
            flowStartTime = Time.realtimeSinceStartup;
            // |=: DebugHooks.SkipCalibration may already have run during the push animation.
            skipAll |= PlayerDataManager.Instance.DebugSettings.skipCalibration;

            BeginStep(Step.Starting);
            await cameraSession.StartAsync(numPlayers, ct);
            var detection = cameraSession.Detection;
            var setup = detection.SetupStateManager;

            if (!skipAll)
            {
                previewsManager.Initialize(numPlayers, detection.BodyPoseDetectionManager, detection.PlayAreaController, setup);
                _ = placeholderGroup.DOFade(0f, 0.3f).SetUpdate(true).SetLink(gameObject);
                await previewsManager.MoveIn(true).AttachExternalCancellation(ct);

                BeginStep(Step.MoveIn);
                promptLabel.SetKey(LocKeys.Calibration.MoveIn);
                await WaitOrSkipAsync(setup.WaitForGoodPlayerPosition(), ct);
                EndStep("move_in");

                BeginStep(Step.RaiseHand);
                promptLabel.SetKey(LocKeys.Calibration.RaiseHand);
                setup.SetAllowPassingRaisingHandState(true);
                await WaitOrSkipAsync(setup.WaitForRaiseHand(), ct);
                EndStep("raise_hand");

                await previewsManager.MoveOut(true).AttachExternalCancellation(ct);
            }

            detection.ConfigForGameplay();

            if (!skipAll)
            {
                await TutorialAsync(ct);
                await TestStrikeAsync(ct);
            }

            RunAnalytics.SetupComplete(numPlayers, Time.realtimeSinceStartup - flowStartTime);
            await curtain.DOFade(1f, theme.CurtainFadeInDuration).SetUpdate(true).SetLink(gameObject).ToUniTask(cancellationToken: ct);
        }

        async UniTask TutorialAsync(CancellationToken ct)
        {
            BeginStep(Step.Tutorial);
            promptLabel.SetKey(LocKeys.Calibration.PoseTutorial);
            // Discarded on purpose: the tweens are awaitable through UniTask's DOTween support but run fire-and-forget.
            _ = placeholderGroup.DOFade(0f, 0.3f).SetUpdate(true).SetLink(gameObject);
            _ = setupTipGroup.DOFade(0f, 0.3f).SetUpdate(true).SetLink(gameObject);
            _ = playersGroup.DOFade(1f, 0.3f).SetUpdate(true).SetLink(gameObject);
            var seconds = tutorialSeen ? tutorialSecondsSeen : tutorialSeconds;
            await WaitOrSkipAsync(UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, cancellationToken: ct), ct);
            EndStep("tutorial");
        }

        async UniTask TestStrikeAsync(CancellationToken ct)
        {
            BeginStep(Step.TestStrike);
            promptLabel.SetKey(LocKeys.Calibration.TestStrike);
            var hasInput = true;
            for (var i = 0; i < numPlayers; i++)
            {
                playerReady[i] = false;
                inputs[i] = shotInputFactory(i, cameraSession.GetEngine(i), inputRoot);
                if (inputs[i] == null)
                {
                    hasInput = false;
                }

                SetPlayerStatus(i, false);
            }

            if (hasInput)
            {
                await WaitForStrikesAsync(ct);
            }
            else
            {
                Debug.LogWarning("[CalibrationView] No paw input source available; the test strike auto-passes.");
                await WaitOrSkipAsync(UniTask.Delay(TimeSpan.FromSeconds(autoPassSeconds), DelayType.UnscaledDeltaTime, cancellationToken: ct), ct);
            }

            EndStep("test_strike");

            BeginStep(Step.Ready);
            for (var i = 0; i < numPlayers; i++)
            {
                SetPlayerStatus(i, true);
            }

            promptLabel.SetKey(numPlayers > 1 ? LocKeys.Calibration.AllReady : LocKeys.Calibration.Ready);
            await UniTask.Delay(TimeSpan.FromSeconds(readySeconds), DelayType.UnscaledDeltaTime, cancellationToken: ct);
        }

        async UniTask WaitForStrikesAsync(CancellationToken ct)
        {
            stepSkip = new UniTaskCompletionSource();
            var readyCount = 0;
            while (readyCount < numPlayers && !skipAll && !stepSkip.Task.Status.IsCompleted())
            {
                for (var i = 0; i < numPlayers; i++)
                {
                    if (playerReady[i]) continue;
                    var input = inputs[i]!;
                    SetPlayerTracked(i, input.IsTracking);
                    if (!input.TryConsumeStrike(out _)) continue;
                    playerReady[i] = true;
                    readyCount++;
                    SetPlayerStatus(i, true);
                    promptLabel.SetKey(LocKeys.Calibration.StrikeSuccess);
                    illustration.Flash();
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        async UniTask WaitOrSkipAsync(UniTask wait, CancellationToken ct)
        {
            if (skipAll) return;
            stepSkip = new UniTaskCompletionSource();
            await UniTask.WhenAny(wait.AttachExternalCancellation(ct), stepSkip.Task.AttachExternalCancellation(ct));
        }

        #endregion

        #region Helpers

        void BeginStep(Step step)
        {
            CurrentStep = step;
            stepStartTime = Time.realtimeSinceStartup;
            // Pips count the four player-facing steps; Starting shows none, Ready shows all done.
            stepPips.Set(step switch
            {
                Step.MoveIn => 0,
                Step.RaiseHand => 1,
                Step.Tutorial => 2,
                Step.TestStrike => 3,
                Step.Ready => 4,
                _ => -1,
            });
        }

        void EndStep(string analyticsStep)
        {
            RunAnalytics.Setup(analyticsStep, Time.realtimeSinceStartup - stepStartTime);
        }

        void SetPlayerStatus(int playerIndex, bool ready)
        {
            if (playerIndex >= playerCards.Length) return;
            playerCards[playerIndex].Set(playerIndex, ready);
        }

        void SetPlayerTracked(int playerIndex, bool tracked)
        {
            if (playerIndex >= playerCards.Length) return;
            playerCards[playerIndex].SetTracked(playerIndex, tracked);
        }

        #endregion
    }
}
