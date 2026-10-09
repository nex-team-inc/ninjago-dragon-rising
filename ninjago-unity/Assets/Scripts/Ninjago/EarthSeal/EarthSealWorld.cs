#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The shared Earth Seal wall: one grid of stone tiles facing a still camera. Every frame each active player's
    /// cursors are hit-tested against the tiles (a cursor is in at most one tile), the wall rules run, and the tiles,
    /// earth dust, sounds and stats follow. Waves run back to back until wave 4 ends or the hearts run out.
    /// </summary>
    public class EarthSealWorld : MonoBehaviour
    {
        [Header("World Camera")]
        [SerializeField] Camera worldCamera = null!;
        [Header("Tile Prefab")]
        [SerializeField] EarthSealTile tilePrefab = null!;
        [Header("Tiles Root")]
        [Tooltip("Center of the grid on the wall face.")]
        [SerializeField] Transform tilesRoot = null!;
        [Header("Grid Area")]
        [Tooltip("World size the grid fills, centered on the tiles root.")]
        [SerializeField] Vector2 gridArea = new(16.4f, 7.9f);
        [Header("Tile Gap")]
        [SerializeField] float tileGap = 0.22f;
        [Header("Effects Offset")]
        [Tooltip("Toward the camera from a tile face, so effects draw in front of it.")]
        [SerializeField] float effectsOffset = 0.6f;

        readonly SealWall wall = new();
        readonly List<EarthSealTile> tileViews = new();
        readonly List<Rect> tileRects = new();
        readonly List<int> cursorCounts = new();
        readonly List<int> holderMasks = new();
        readonly List<int> crustOwners = new();
        readonly List<ParticleSystem?> dust = new();
        readonly List<SealWall.Event> events = new();
        readonly System.Random random = new();
        IReadOnlyList<PlayerBody> players = null!;
        EarthSealConfig config = null!;
        HandCursorTracker cursors = null!;
        EarthSealWave currentWave;

        public EarthSealStats Stats { get; } = new();
        public int Hearts { get; private set; }
        public bool Finished { get; private set; }
        /// <summary>A wave's grid is up and its banner should show (wave index, 0-based).</summary>
        public event Action<int>? WaveStarted;
        public event Action<int>? HeartsChanged;

        #region Initialization

        public void Initialize(IReadOnlyList<PlayerBody> aPlayers, EarthSealConfig aConfig, HandCursorTracker aCursors, RenderTexture target)
        {
            players = aPlayers;
            config = aConfig;
            cursors = aCursors;
            worldCamera.targetTexture = target;
            Hearts = config.Hearts;
        }

        // The dust loops in VfxManager's persistent pool; leaving mid-hold must not leave it running.
        void OnDestroy()
        {
            StopAllDust();
        }

        #endregion

        #region Wave Loop

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            await WaitAsync(config.IntroSeconds, cancellationToken);
            for (var waveIndex = 0; waveIndex < config.WaveCount; waveIndex++)
            {
                currentWave = (EarthSealWave)waveIndex;
                BuildWave(config.GetWave(currentWave));
                WaveStarted?.Invoke(waveIndex);
                await WaitAsync(config.WaveBreakSeconds, cancellationToken);
                while (!wall.IsWaveOver)
                {
                    Step(Time.unscaledDeltaTime);
                    if (Hearts <= 0)
                    {
                        StopAllDust();
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, cancellationToken);
                }

                StopAllDust();
                Stats.RecordWaveCleared();
            }

            Finished = true;
        }

        void BuildWave(EarthSealConfig.WaveSettings settings)
        {
            foreach (var view in tileViews) Destroy(view.gameObject);
            tileViews.Clear();
            tileRects.Clear();
            cursorCounts.Clear();
            holderMasks.Clear();
            crustOwners.Clear();
            dust.Clear();
            wall.BeginWave(settings, random);
            var size = wall.GridSize;
            var tileSize = new Vector2((gridArea.x - tileGap * (size - 1)) / size, (gridArea.y - tileGap * (size - 1)) / size);
            var topLeft = new Vector2(-gridArea.x * 0.5f, gridArea.y * 0.5f);
            foreach (var tile in wall.Tiles)
            {
                var center = new Vector3(topLeft.x + tile.X * (tileSize.x + tileGap) + tileSize.x * 0.5f,
                    topLeft.y - tile.Y * (tileSize.y + tileGap) - tileSize.y * 0.5f, 0f);
                var view = Instantiate(tilePrefab, tilesRoot);
                view.transform.localPosition = center;
                view.Initialize(tileSize.x, tileSize.y);
                tileViews.Add(view);
                tileRects.Add(ScreenRect(view.transform.position, tileSize));
                cursorCounts.Add(0);
                holderMasks.Add(0);
                crustOwners.Add(-1);
                dust.Add(null);
            }
        }

        void Step(float deltaTime)
        {
            for (var i = 0; i < cursorCounts.Count; i++)
            {
                cursorCounts[i] = 0;
                holderMasks[i] = 0;
            }

            foreach (var player in players)
            {
                for (var hand = 0; hand < 2; hand++)
                {
                    var cursor = cursors.GetCursor(player.PlayerIndex, hand);
                    if (!cursor.IsVisible) continue;
                    var index = TileAt(cursor.ScreenPosition);
                    if (index < 0) continue;
                    cursorCounts[index]++;
                    holderMasks[index] |= 1 << player.PlayerIndex;
                    if (wall.Tiles[index].IsLive) Stats.AddHoldTime(player.PlayerIndex, deltaTime);
                }
            }

            events.Clear();
            wall.Tick(deltaTime, cursorCounts, holderMasks, config.SealDrainSeconds, events);
            foreach (var wallEvent in events) Handle(wallEvent);

            var breakthroughSeconds = config.GetWave(currentWave).breakthroughSeconds;
            for (var i = 0; i < tileViews.Count; i++)
            {
                var tile = wall.Tiles[i];
                var held = tile.IsLive && cursorCounts[i] > 0;
                // One crust per tile in the color of whoever holds it; a second holder keeps the first one's crust.
                if (held && (crustOwners[i] < 0 || (holderMasks[i] & (1 << crustOwners[i])) == 0)) crustOwners[i] = tile.LastHolder;
                var pushProgress = tile.State == SealWall.TileState.Pushing ? tile.PhaseTime / breakthroughSeconds : 0f;
                tileViews[i].Show(tile, pushProgress, ColorOf(crustOwners[i]));
                UpdateDust(i, held);
            }
        }

        void Handle(SealWall.Event wallEvent)
        {
            var index = wallEvent.tile;
            var tile = wall.Tiles[index];
            var effectPoint = tileViews[index].transform.position + Vector3.back * effectsOffset;
            switch (wallEvent.type)
            {
                case SealWall.EventType.CrackStarted:
                    EarthSealAnalytics.CrackStart(currentWave, index, wall.LiveCount);
                    break;
                case SealWall.EventType.SealCompleted:
                    Stats.RecordSeal(wallEvent.holderMask, wallEvent.cursorCount);
                    EarthSealAnalytics.SealComplete(currentWave, index, wallEvent.holderMask, wallEvent.cursorCount, tile.Age);
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.SealComplete);
                    VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.SealComplete, effectPoint);
                    break;
                case SealWall.EventType.SealDropped:
                    Stats.RecordDrop(tile.LastHolder);
                    EarthSealAnalytics.SealDrop(currentWave, index, tile.LastHolder, tile.PeakFill, wallEvent.atBreakthrough);
                    break;
                case SealWall.EventType.Breakthrough:
                    Stats.RecordBreakthrough();
                    Hearts = Mathf.Max(0, Hearts - 1);
                    HeartsChanged?.Invoke(Hearts);
                    EarthSealAnalytics.Breakthrough(currentWave, index, tile.LastHolder, Hearts, tile.Fill);
                    SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.Breakthrough);
                    VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.Breakthrough, effectPoint);
                    break;
            }
        }

        #endregion

        #region Helpers

        int TileAt(Vector2 screenPosition)
        {
            for (var i = 0; i < tileRects.Count; i++)
            {
                if (tileRects[i].Contains(screenPosition)) return i;
            }

            return -1;
        }

        // The feed fills the screen, so viewport coordinates are screen-normalized coordinates.
        Rect ScreenRect(Vector3 center, Vector2 size)
        {
            Vector2 min = worldCamera.WorldToViewportPoint(center - new Vector3(size.x, size.y, 0f) * 0.5f);
            Vector2 max = worldCamera.WorldToViewportPoint(center + new Vector3(size.x, size.y, 0f) * 0.5f);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        Color ColorOf(int playerIndex)
        {
            foreach (var player in players)
            {
                if (player.PlayerIndex == playerIndex) return player.Color;
            }

            return Color.white;
        }

        void UpdateDust(int index, bool held)
        {
            var current = dust[index];
            if (held && current == null)
            {
                dust[index] = VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.SealDust,
                    tileViews[index].transform.position + Vector3.back * effectsOffset, Quaternion.identity);
            }
            else if (!held && current != null)
            {
                current.Stop();
                dust[index] = null;
            }
        }

        void StopAllDust()
        {
            for (var i = 0; i < dust.Count; i++) UpdateDust(i, false);
        }

        static UniTask WaitAsync(float seconds, CancellationToken cancellationToken)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, cancellationToken);
        }

        #endregion
    }
}
