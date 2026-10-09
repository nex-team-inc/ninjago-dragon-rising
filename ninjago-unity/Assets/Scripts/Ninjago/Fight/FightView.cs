#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// Slip and Spin on screen: one courtyard per active player, each camera rendering into its own half (or the full
    /// screen when one player plays), the HUD half on top, and the camera preview with player indicators.
    /// </summary>
    public class FightView : SimpleCanvasView
    {
        [Header("Solo Feed")]
        [SerializeField] RawImage soloFeed = null!;
        [Header("Solo HUD Slot")]
        [SerializeField] RectTransform soloHudSlot = null!;
        [Header("Left Feed")]
        [SerializeField] RawImage leftFeed = null!;
        [Header("Left HUD Slot")]
        [SerializeField] RectTransform leftHudSlot = null!;
        [Header("Right Feed")]
        [SerializeField] RawImage rightFeed = null!;
        [Header("Right HUD Slot")]
        [SerializeField] RectTransform rightHudSlot = null!;
        [Header("Split Divider")]
        [SerializeField] GameObject splitDivider = null!;
        [Header("HUD Prefab")]
        [SerializeField] FightHud hudPrefab = null!;
        [Header("Courtyard Prefab")]
        [SerializeField] FightLane lanePrefab = null!;
        [Header("Courtyard Origin")]
        [Tooltip("Far from the GameScene main camera's view so only the lane cameras draw the courtyards.")]
        [SerializeField] Vector3 laneOrigin = new(1000f, 0f, 0f);
        [Header("Courtyard Spacing")]
        [SerializeField] Vector3 laneSpacing = new(200f, 0f, 0f);
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;
        [Header("Get Ready Banner")]
        [SerializeField] GameObject getReadyBanner = null!;

        readonly List<FightLane> lanes = new();
        readonly List<RenderTexture> textures = new();
        readonly UniTaskCompletionSource quitSource = new();
        HandStormSampler storms = null!;
        float introSeconds;

        public IReadOnlyList<FightLane> Lanes => lanes;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.Fight;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "fight";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            quitSource.TrySetResult();
        }

        #endregion

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> players, FightConfig config, HandStormSampler aStorms,
            DetectionManager detection, int requestedPlayers)
        {
            storms = aStorms;
            introSeconds = config.IntroSeconds;
            var split = players.Count > 1;
            soloFeed.gameObject.SetActive(!split);
            leftFeed.gameObject.SetActive(split);
            rightFeed.gameObject.SetActive(split);
            splitDivider.SetActive(split);

            var indicatorPlayers = new List<int>(players.Count);
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var feed = split ? i == 0 ? leftFeed : rightFeed : soloFeed;
                var hudSlot = split ? i == 0 ? leftHudSlot : rightHudSlot : soloHudSlot;
                var texture = new RenderTexture(split ? Screen.width / 2 : Screen.width, Screen.height, 24)
                {
                    name = $"FightLane_P{player.PlayerIndex + 1}",
                };
                textures.Add(texture);
                feed.texture = texture;

                var lane = Instantiate(lanePrefab, laneOrigin + laneSpacing * player.PlayerIndex, Quaternion.identity);
                var firstSafeSide = player.PlayerIndex == 0 ? config.FirstSafeSide : config.FirstSafeSide.Opposite();
                lane.Initialize(player, config, storms, Instantiate(hudPrefab, hudSlot), texture, split, firstSafeSide);
                lanes.Add(lane);
                indicatorPlayers.Add(player.PlayerIndex);
            }

            pipFrame.Initialize(detection.PlayAreaController);
            pipIndicators.Initialize(requestedPlayers, indicatorPlayers, pipFrame, detection.BodyPoseDetectionManager);
        }

        void OnDestroy()
        {
            foreach (var lane in lanes)
            {
                if (lane != null) Destroy(lane.gameObject);
            }

            foreach (var texture in textures)
            {
                texture.Release();
                Destroy(texture);
            }
        }

        #endregion

        #region Public API

        /// <summary>Plays every lane to its end; false when the player backed out.</summary>
        public async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            using var laneCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            storms.SetDetecting(true);
            HideGetReadyAsync(laneCancellation.Token).Forget();
            var laneTasks = new UniTask[lanes.Count];
            for (var i = 0; i < lanes.Count; i++)
            {
                laneTasks[i] = lanes[i].RunAsync(laneCancellation.Token);
            }

            var winner = await UniTask.WhenAny(UniTask.WhenAll(laneTasks), quitSource.Task);
            storms.SetDetecting(false);
            if (winner == 0) return true;
            laneCancellation.Cancel();
            return false;
        }

        #endregion

        #region Helpers

        async UniTaskVoid HideGetReadyAsync(CancellationToken cancellationToken)
        {
            getReadyBanner.SetActive(true);
            await UniTask.Delay((int)(introSeconds * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
            getReadyBanner.SetActive(false);
        }

        #endregion
    }
}
