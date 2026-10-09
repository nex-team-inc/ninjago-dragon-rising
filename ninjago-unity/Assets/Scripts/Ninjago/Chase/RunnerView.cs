#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>Chest Chase on one shared screen: the world feed, segment HUD, hints and the camera preview.</summary>
    public class RunnerView : SimpleCanvasView
    {
        [Header("World Feed")]
        [SerializeField] RawImage feed = null!;
        [Header("World Prefab")]
        [SerializeField] RunnerWorld worldPrefab = null!;
        [Header("World Origin")]
        [Tooltip("Far from the GameScene main camera's view so only the world camera draws the track.")]
        [SerializeField] Vector3 worldOrigin = new(2000f, 0f, 0f);
        [Header("Vehicle Label")]
        [SerializeField] NexLocalizedString vehicleLabel = null!;
        [Header("Car Name")]
        [SerializeField] LocalizedString carName = new();
        [Header("Skycraft Name")]
        [SerializeField] LocalizedString skycraftName = new();
        [Header("Time Left")]
        [SerializeField] TMP_Text timeLeft = null!;
        [Header("Dodges Label")]
        [SerializeField] NexLocalizedString dodgesLabel = null!;
        [Header("Hits Label")]
        [SerializeField] NexLocalizedString hitsLabel = null!;
        [Header("Lean To Steer Hint")]
        [SerializeField] GameObject leanHint = null!;
        [Header("Whole Chest Hint")]
        [SerializeField] GameObject wholeChestHint = null!;
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;

        readonly UniTaskCompletionSource quitSource = new();
        RunnerWorld world = null!;
        RenderTexture texture = null!;
        ChaseConfig config = null!;
        int shownSeconds = -1;

        public RunnerWorld World => world;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.Runner;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "runner";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            quitSource.TrySetResult();
        }

        #endregion

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> players, ChaseConfig aConfig, DetectionManager detection, int requestedPlayers)
        {
            config = aConfig;
            texture = new RenderTexture(Screen.width, Screen.height, 24) { name = "RunnerWorld" };
            feed.texture = texture;
            world = Instantiate(worldPrefab, worldOrigin, Quaternion.identity);
            world.VehicleShown += HandleVehicleShown;
            world.CountsChanged += HandleCountsChanged;
            world.Initialize(players, config, texture);
            HandleVehicleShown(world.CurrentVehicle);
            HandleCountsChanged();
            wholeChestHint.SetActive(false);

            var indicatorPlayers = new List<int>(players.Count);
            foreach (var player in players) indicatorPlayers.Add(player.PlayerIndex);
            pipFrame.Initialize(detection.PlayAreaController);
            pipIndicators.Initialize(requestedPlayers, indicatorPlayers, pipFrame, detection.BodyPoseDetectionManager);
        }

        void OnDestroy()
        {
            if (world != null) Destroy(world.gameObject);
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }

        #endregion

        #region Public API

        /// <summary>Plays both segments to the end; false when the player backed out.</summary>
        public async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            ShowForAsync(leanHint, config.IntroSeconds, runCancellation.Token).Forget();
            var winner = await UniTask.WhenAny(world.RunAsync(runCancellation.Token), quitSource.Task);
            if (winner == 0) return true;
            runCancellation.Cancel();
            return false;
        }

        #endregion

        #region HUD

        void Update()
        {
            var seconds = Mathf.CeilToInt(world.TimeLeft);
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            timeLeft.SetText("{0}", seconds);
        }

        void HandleVehicleShown(VehicleType type)
        {
            vehicleLabel.StringReference = type == VehicleType.Car ? carName : skycraftName;
            HandleCountsChanged();
            if (type == VehicleType.Skycraft) ShowForAsync(wholeChestHint, config.WholeChestHintSeconds, destroyCancellationToken).Forget();
        }

        void HandleCountsChanged()
        {
            dodgesLabel.SetSmartStringArgument("count", world.GetDodges(world.CurrentVehicle));
            hitsLabel.SetSmartStringArgument("count", world.GetHits(world.CurrentVehicle));
        }

        static async UniTaskVoid ShowForAsync(GameObject hint, float seconds, CancellationToken cancellationToken)
        {
            hint.SetActive(true);
            await UniTask.Delay((int)(seconds * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
            hint.SetActive(false);
        }

        #endregion
    }
}
