#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// Stone Kick on screen: one lane per active player, each camera rendering into its own half (or the full screen
    /// alone), that player's HUD and cursor region on it, the cursors above everything, and the camera preview.
    /// </summary>
    public class StoneKickView : SimpleCanvasView
    {
        [Header("Solo Feed")]
        [SerializeField] RawImage soloFeed = null!;
        [Header("Left Feed")]
        [SerializeField] RawImage leftFeed = null!;
        [Header("Right Feed")]
        [SerializeField] RawImage rightFeed = null!;
        [Header("Split Divider")]
        [SerializeField] GameObject splitDivider = null!;
        [Header("HUD Prefab")]
        [SerializeField] StoneKickHud hudPrefab = null!;
        [Header("Lane Prefab")]
        [SerializeField] StoneKickLane lanePrefab = null!;
        [Header("Lane Origin")]
        [Tooltip("Far from the GameScene main camera and the other mini-games' worlds.")]
        [SerializeField] Vector3 laneOrigin = new(3000f, 0f, 0f);
        [Header("Lane Spacing")]
        [SerializeField] Vector3 laneSpacing = new(200f, 0f, 0f);
        [Header("Cursor Layer")]
        [SerializeField] HandCursorLayer cursorLayer = null!;
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;
        [Header("Get Ready Banner")]
        [SerializeField] GameObject getReadyBanner = null!;

        readonly List<StoneKickLane> lanes = new();
        readonly List<RenderTexture> textures = new();
        readonly UniTaskCompletionSource quitSource = new();
        HandCursorTracker cursors = null!;
        float introSeconds;

        public IReadOnlyList<StoneKickLane> Lanes => lanes;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.StoneKick;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "stone-kick";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            quitSource.TrySetResult();
        }

        #endregion

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> players, StoneKickConfig config, HandCursorTracker aCursors, DetectionManager detection,
            int requestedPlayers)
        {
            cursors = aCursors;
            introSeconds = config.IntroSeconds;
            var split = players.Count > 1;
            soloFeed.gameObject.SetActive(!split);
            leftFeed.gameObject.SetActive(split);
            rightFeed.gameObject.SetActive(split);
            splitDivider.SetActive(split);

            var indicatorPlayers = new List<int>(players.Count);
            for (var slot = 0; slot < players.Count; slot++)
            {
                var player = players[slot];
                var feed = split ? slot == 0 ? leftFeed : rightFeed : soloFeed;
                var region = SplitScreen.Region(slot, players.Count);
                cursors.SetRegion(player.PlayerIndex, region);
                var texture = new RenderTexture(split ? Screen.width / 2 : Screen.width, Screen.height, 24) { name = $"StoneKickLane_P{player.PlayerIndex + 1}" };
                textures.Add(texture);
                feed.texture = texture;
                var lane = Instantiate(lanePrefab, laneOrigin + laneSpacing * player.PlayerIndex, Quaternion.identity);
                lane.Initialize(player, config, cursors, Instantiate(hudPrefab, feed.rectTransform), texture, region);
                lanes.Add(lane);
                indicatorPlayers.Add(player.PlayerIndex);
            }

            cursorLayer.Initialize(cursors, players);
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
            cursors.SetDetecting(true);
            HideGetReadyAsync(laneCancellation.Token).Forget();
            var laneTasks = new UniTask[lanes.Count];
            for (var i = 0; i < lanes.Count; i++)
            {
                laneTasks[i] = lanes[i].RunAsync(laneCancellation.Token);
            }

            var winner = await UniTask.WhenAny(UniTask.WhenAll(laneTasks), quitSource.Task);
            cursors.SetDetecting(false);
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
