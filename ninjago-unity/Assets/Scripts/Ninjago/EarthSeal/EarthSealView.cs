#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// Earth Seal on one shared screen: the wall feed, the shared hearts, wave count and player tags over the wall,
    /// every player's cursors (each covers the whole wall) and the camera preview.
    /// </summary>
    public class EarthSealView : SimpleCanvasView
    {
        [Header("World Feed")]
        [SerializeField] RawImage feed = null!;
        [Header("World Prefab")]
        [SerializeField] EarthSealWorld worldPrefab = null!;
        [Header("World Origin")]
        [Tooltip("Far from the GameScene main camera and the other mini-games' worlds.")]
        [SerializeField] Vector3 worldOrigin = new(4000f, 0f, 0f);
        [Header("Hearts Row")]
        [SerializeField] RectTransform heartsRow = null!;
        [Header("Heart Prefab")]
        [SerializeField] Image heartPrefab = null!;
        [Header("Full Heart Color")]
        [SerializeField] Color fullHeartColor = new(1f, 0.25f, 0.3f);
        [Header("Empty Heart Color")]
        [SerializeField] Color emptyHeartColor = new(0.15f, 0.15f, 0.15f, 0.5f);
        [Header("Wave Label")]
        [SerializeField] NexLocalizedString waveLabel = null!;
        [Header("Wave Text")]
        [Tooltip("Smart string with {current} {total}.")]
        [SerializeField] LocalizedString waveText = new();
        [Header("Player Tags Row")]
        [Tooltip("Who is holding this wall: one tag per active player.")]
        [SerializeField] RectTransform playerTagsRow = null!;
        [Header("Player Tag Prefab")]
        [SerializeField] PlayerTagLabel playerTagPrefab = null!;
        [Header("Hold Hint")]
        [SerializeField] GameObject holdHint = null!;
        [Header("Wave Banner")]
        [SerializeField] NexLocalizedString waveBanner = null!;
        [Header("Wave Banner Text")]
        [Tooltip("Smart string with {current}.")]
        [SerializeField] LocalizedString waveBannerText = new();
        [Header("Cursor Layer")]
        [SerializeField] HandCursorLayer cursorLayer = null!;
        [Header("Camera Preview")]
        [SerializeField] AreaPreviewFrame pipFrame = null!;
        [Header("Camera Preview Indicators")]
        [SerializeField] PlayerIndicatorsManager pipIndicators = null!;

        readonly List<Image> hearts = new();
        readonly UniTaskCompletionSource quitSource = new();
        EarthSealWorld world = null!;
        RenderTexture texture = null!;
        EarthSealConfig config = null!;
        HandCursorTracker cursors = null!;

        public EarthSealWorld World => world;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.EarthSeal;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "earth-seal";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, "back");
            quitSource.TrySetResult();
        }

        #endregion

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> players, EarthSealConfig aConfig, HandCursorTracker aCursors, DetectionManager detection,
            int requestedPlayers)
        {
            config = aConfig;
            cursors = aCursors;
            texture = new RenderTexture(Screen.width, Screen.height, 24) { name = "EarthSealWall" };
            feed.texture = texture;
            world = Instantiate(worldPrefab, worldOrigin, Quaternion.identity);
            world.WaveStarted += HandleWaveStarted;
            world.HeartsChanged += SetHearts;
            world.Initialize(players, config, cursors, texture);

            for (var i = 0; i < config.Hearts; i++) hearts.Add(Instantiate(heartPrefab, heartsRow));
            SetHearts(config.Hearts);
            SetWave(1);
            holdHint.SetActive(false);
            waveBanner.gameObject.SetActive(false);

            var indicatorPlayers = new List<int>(players.Count);
            foreach (var player in players)
            {
                cursors.SetRegion(player.PlayerIndex, new Rect(0f, 0f, 1f, 1f));
                Instantiate(playerTagPrefab, playerTagsRow).Initialize(player.PlayerIndex, player.Color);
                indicatorPlayers.Add(player.PlayerIndex);
            }

            cursorLayer.Initialize(cursors, players);
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

        /// <summary>Plays every wave (or until the hearts run out); false when the player backed out.</summary>
        public async UniTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);
            cursors.SetDetecting(true);
            ShowForAsync(holdHint, config.IntroSeconds, runCancellation.Token).Forget();
            var winner = await UniTask.WhenAny(world.RunAsync(runCancellation.Token), quitSource.Task);
            cursors.SetDetecting(false);
            if (winner == 0) return true;
            runCancellation.Cancel();
            return false;
        }

        #endregion

        #region HUD

        void HandleWaveStarted(int waveIndex)
        {
            SetWave(waveIndex + 1);
            waveBanner.StringReference = LocalizedStrings.WithArguments(waveBannerText, ("current", waveIndex + 1));
            ShowForAsync(waveBanner.gameObject, config.WaveBreakSeconds, destroyCancellationToken).Forget();
        }

        void SetWave(int current)
        {
            waveLabel.StringReference = LocalizedStrings.WithArguments(waveText, ("current", current), ("total", config.WaveCount));
        }

        void SetHearts(int remaining)
        {
            for (var i = 0; i < hearts.Count; i++) hearts[i].color = i < remaining ? fullHeartColor : emptyHeartColor;
        }

        static async UniTaskVoid ShowForAsync(GameObject target, float seconds, CancellationToken cancellationToken)
        {
            target.SetActive(true);
            await UniTask.Delay((int)(seconds * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
            target.SetActive(false);
        }

        #endregion
    }
}
