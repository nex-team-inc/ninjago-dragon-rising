#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Nex.Dev;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Nex.BilliardRogue
{
    /// <summary>Builds the paw input used for the live test strike; null when no input source is available.</summary>
    public delegate IShotInput? CalibrationShotInputFactory(int playerIndex, OnePlayerDetectionEngine engine, Transform parent);

    /// <summary>
    /// Camera setup step: starts the camera session, runs the starter setup (move into frame → raise hand) with the
    /// PreviewsManager, then the pose tutorial and a live test strike per player. Back cancels (the view pops
    /// itself), DebugSettings.skipCalibration / DebugHooks.SkipCalibration / the S key skip steps in debug builds.
    /// </summary>
    public sealed class CalibrationView : SimpleCanvasView
    {
        public enum Step { Idle, Starting, MoveIn, RaiseHand, Tutorial, TestStrike, Ready }

        const int MaxPlayers = 2;

        [Header("Setup previews")]
        [SerializeField] PreviewsManager previewsManager = null!;

        [Header("Tutorial")]
        [SerializeField] CanvasGroup tutorialGroup = null!;
        [SerializeField] CalibrationTutorialIllustration illustration = null!;
        [SerializeField] TMP_Text promptLabel = null!;
        [SerializeField] TMP_Text hintLabel = null!;
        [Tooltip("Per-player status (2P only), index = player index.")]
        [SerializeField] TMP_Text[] playerStatusLabels = null!;
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

        CameraSession camera = null!;
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
        Strings strings = null!;

        public Step CurrentStep { get; private set; }

        public override ViewIdentifier Identifier => ViewIdentifier.Calibration;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "calibration";

        #region Life Cycle

        public void Initialize(CameraSession aCamera, int aNumPlayers, bool aTutorialSeen, CalibrationShotInputFactory aShotInputFactory)
        {
            camera = aCamera;
            numPlayers = aNumPlayers;
            tutorialSeen = aTutorialSeen;
            shotInputFactory = aShotInputFactory;
            tutorialGroup.alpha = 0f;
            hintLabel.gameObject.SetActive(false);
            promptLabel.text = "";
            for (var i = 0; i < playerStatusLabels.Length; i++)
            {
                playerStatusLabels[i].gameObject.SetActive(numPlayers > 1 && i < numPlayers);
            }
        }

        void Update()
        {
            if (!IsActive) return;
            if (CurrentStep is not (Step.Tutorial or Step.TestStrike)) return;
            if (DebugInput.GetKeyDown(KeyCode.S)) stepSkip.TrySetResult();
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
            skipAll = PlayerDataManager.Instance.DebugSettings.skipCalibration;
            strings = await Strings.LoadAsync(numPlayers, ct);

            BeginStep(Step.Starting);
            promptLabel.text = strings.starting;
            await camera.StartAsync(numPlayers, ct);
            promptLabel.text = "";
            var detection = camera.Detection;
            var setup = detection.SetupStateManager;

            if (!skipAll)
            {
                previewsManager.Initialize(numPlayers, detection.BodyPoseDetectionManager, detection.PlayAreaController, setup);
                await previewsManager.MoveIn(true).AttachExternalCancellation(ct);

                BeginStep(Step.MoveIn);
                previewsManager.SetPromptText(strings.moveIn);
                await WaitOrSkipAsync(setup.WaitForGoodPlayerPosition(), ct);
                EndStep("move_in");

                BeginStep(Step.RaiseHand);
                previewsManager.SetPromptText(strings.raiseHand);
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
        }

        async UniTask TutorialAsync(CancellationToken ct)
        {
            BeginStep(Step.Tutorial);
            promptLabel.text = strings.poseTutorial;
            hintLabel.text = PlayerDataManager.Instance.PlayerPreference.leftHandedCue ? strings.leftHandedHint : strings.tutorialHint;
            hintLabel.gameObject.SetActive(true);
            // Discarded on purpose: the tween is awaitable through UniTask's DOTween support but runs fire-and-forget.
            _ = tutorialGroup.DOFade(1f, 0.3f).SetUpdate(true).SetLink(gameObject);
            illustration.Play();
            var seconds = tutorialSeen ? tutorialSecondsSeen : tutorialSeconds;
            await WaitOrSkipAsync(UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, cancellationToken: ct), ct);
            EndStep("tutorial");
        }

        async UniTask TestStrikeAsync(CancellationToken ct)
        {
            BeginStep(Step.TestStrike);
            promptLabel.text = strings.testStrike;
            var hasInput = true;
            for (var i = 0; i < numPlayers; i++)
            {
                playerReady[i] = false;
                inputs[i] = shotInputFactory(i, camera.GetEngine(i), inputRoot);
                if (inputs[i] == null) hasInput = false;
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
            illustration.Stop();
            promptLabel.text = numPlayers > 1 ? strings.allReady : strings.ready;
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
                    if (playerReady[i] || !inputs[i]!.TryConsumeStrike(out _)) continue;
                    playerReady[i] = true;
                    readyCount++;
                    SetPlayerStatus(i, true);
                    promptLabel.text = strings.strikeSuccess;
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
        }

        void EndStep(string analyticsStep)
        {
            RunAnalytics.Setup(analyticsStep, Time.realtimeSinceStartup - stepStartTime);
        }

        void SetPlayerStatus(int playerIndex, bool ready)
        {
            if (playerIndex >= playerStatusLabels.Length) return;
            playerStatusLabels[playerIndex].text = ready ? strings.playerReady[playerIndex] : strings.waiting[playerIndex];
        }

        /// <summary>Localized copy fetched once per calibration (labels change every step, so no per-label components).</summary>
        sealed class Strings
        {
            public string starting = "";
            public string moveIn = "";
            public string raiseHand = "";
            public string poseTutorial = "";
            public string tutorialHint = "";
            public string leftHandedHint = "";
            public string testStrike = "";
            public string strikeSuccess = "";
            public string ready = "";
            public string allReady = "";
            public readonly string[] playerReady = new string[MaxPlayers];
            public readonly string[] waiting = new string[MaxPlayers];

            public static async UniTask<Strings> LoadAsync(int numPlayers, CancellationToken ct)
            {
                await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: ct);
                var strings = new Strings
                {
                    starting = await GetAsync(LocKeys.Calibration.Starting, ct),
                    moveIn = await GetAsync(LocKeys.Calibration.MoveIn, ct),
                    raiseHand = await GetAsync(LocKeys.Calibration.RaiseHand, ct),
                    poseTutorial = await GetAsync(LocKeys.Calibration.PoseTutorial, ct),
                    tutorialHint = await GetAsync(LocKeys.Calibration.TutorialHint, ct),
                    leftHandedHint = await GetAsync(LocKeys.Calibration.LeftHandedHint, ct),
                    testStrike = await GetAsync(LocKeys.Calibration.TestStrike, ct),
                    strikeSuccess = await GetAsync(LocKeys.Calibration.StrikeSuccess, ct),
                    ready = await GetAsync(LocKeys.Calibration.Ready, ct),
                    allReady = await GetAsync(LocKeys.Calibration.AllReady, ct),
                };
                for (var i = 0; i < numPlayers && i < MaxPlayers; i++)
                {
                    IList<object> playerNumber = new object[] { i + 1 };
                    strings.playerReady[i] = await GetAsync(LocKeys.Calibration.PlayerReady, playerNumber, ct);
                    strings.waiting[i] = await GetAsync(LocKeys.Calibration.Waiting, playerNumber, ct);
                }

                return strings;
            }

            static UniTask<string> GetAsync(string key, CancellationToken ct)
            {
                return LocalizationSettings.StringDatabase.GetLocalizedStringAsync(LocKeys.Table, key).ToUniTask(cancellationToken: ct);
            }

            static UniTask<string> GetAsync(string key, IList<object> arguments, CancellationToken ct)
            {
                return LocalizationSettings.StringDatabase.GetLocalizedStringAsync(LocKeys.Table, key, arguments).ToUniTask(cancellationToken: ct);
            }
        }

        #endregion
    }
}
