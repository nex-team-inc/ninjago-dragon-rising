#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using Nex.Util;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Owns every visual on the arena (enemies, balls, field objects, pickups, cats, aim guide, labels, juice)
    /// and turns SimEvents into animation, VFX and SFX. Never re-derives geometry: everything goes through ArenaLayout.
    /// Composition: BoardViews (pools and id registry), BoardEventPlayer (event feedback), EnemyPhasePlayer
    /// (stepped enemy phase), BatchSpawnPlayer (GDD v2 §5 batch pop-in), BoardSequencePlayer (stage clear / boss intro /
    /// defeat / victory), WorldLabelLayer (UI labels).
    /// </summary>
    public sealed class BoardPresenter : MonoBehaviour
    {
        [Header("Pools (WorldPrefabsBuilder)")]
        [SerializeField] EnumDictionary<EnemyType, EnemyViewPool> enemyPools = new();
        [SerializeField] BallViewPool ballPool = null!;
        [SerializeField] BallView ballPrefab = null!;
        [SerializeField] EnumDictionary<FieldObjectType, FieldObjectViewPool> objectPools = new();
        [SerializeField] EnumDictionary<PickupType, PickupViewPool> pickupPools = new();

        [Header("Board")]
        [SerializeField] AimGuideView aimGuide = null!;
        [Tooltip("Cat_P1, Cat_P2.")]
        [SerializeField] CatView[] cats = System.Array.Empty<CatView>();
        [SerializeField] CameraShaker cameraShaker = null!;
        [Tooltip("Tier-3 Hype rim aura (optional).")]
        [SerializeField] HypeAuraView? hypeAura;

        [Header("Arena (scene instance, wired by MainSceneBuilder)")]
        [Tooltip("Its danger-row glow follows the enemies closest to the cat.")]
        [SerializeField] ArenaView arenaView = null!;

        [Header("Labels (instantiated under the GameplayView label layer)")]
        [SerializeField] WorldLabelLayer labelLayerPrefab = null!;
        [SerializeField] WorldLabel labelPrefab = null!;
        [SerializeField] DamageNumber damageNumberPrefab = null!;

        BilliardRogueConfig config = null!;
        GameRules rules = null!;
        ArenaLayout layout = null!;
        WorldLabelLayer labels = null!;
        BoardViews views = null!;
        BoardEventPlayer eventPlayer = null!;
        EnemyPhasePlayer phasePlayer = null!;
        BatchSpawnPlayer batchPlayer = null!;
        BoardSequencePlayer sequences = null!;
        BallVisitor ballVisitor = null!;
        HypeJuice hype = null!;
        float debugHype = -1f;
        Vector2[] fallbackPath = new Vector2[2];
        AimGuideView[] aimGuides = System.Array.Empty<AimGuideView>();
        int numPlayers = 1;

        public CameraShaker Shaker => cameraShaker;
        public WorldLabelLayer Labels => labels;
        public AimGuideView AimGuide => aimGuide;
        /// <summary>Smoothed Hype the juice currently shows, and its tier (debug / verification).</summary>
        public float HypeShown => hype != null ? hype.Current : 0f;
        public int HypeTier => hype != null ? hype.Tier : 0;

        #region Life Cycle

        public void Initialize(BilliardRogueConfig aConfig, GameRules aRules, ArenaLayout aLayout, PixelWorldDisplay display, RectTransform labelLayer)
        {
            config = aConfig;
            rules = aRules;
            layout = aLayout;
            var worldLayer = WorldLayers.Resolve();
            WorldLayers.Apply(gameObject, worldLayer);
            labels = Instantiate(labelLayerPrefab, labelLayer);
            labels.Initialize(display, config.Juice, labelPrefab, damageNumberPrefab);
            cameraShaker.Initialize(display.WorldCamera, layout.GridCenterWorld, config.Visual.RenderResolution.y);
            for (var i = 0; i < cats.Length; i++)
            {
                cats[i].Initialize(i, layout, config.Juice);
            }

            // One aim guide per player, since 2P aim together: the prefab carries P1's, the others are copies of it.
            if (aimGuides.Length != cats.Length)
            {
                aimGuides = new AimGuideView[cats.Length];
                aimGuides[0] = aimGuide;
                for (var i = 1; i < aimGuides.Length; i++)
                {
                    aimGuides[i] = Instantiate(aimGuide, aimGuide.transform.parent);
                }
            }

            for (var i = 0; i < aimGuides.Length; i++)
            {
                aimGuides[i].Initialize(layout, config.Juice);
            }

            views = new BoardViews(config, rules, layout, enemyPools, ballPool, objectPools, pickupPools, labels, ballPrefab, worldLayer);
            var combo = new ComboPresenter(config.Juice, labels);
            eventPlayer = new BoardEventPlayer(views, labels, combo, cameraShaker, config, layout, cats);
            batchPlayer = new BatchSpawnPlayer(views, eventPlayer);
            phasePlayer = new EnemyPhasePlayer(views, eventPlayer, batchPlayer, labels, cameraShaker, layout, config, cats);
            sequences = new BoardSequencePlayer(views, eventPlayer, cameraShaker, config, cats);
            ballVisitor = views.OnBall;
            if (hypeAura != null) hypeAura.Initialize(layout, config.Juice);
            hype = new HypeJuice(config.Juice, config.Hype, views, eventPlayer, cameraShaker, labels, cats, hypeAura);
            SetPlayers(1);
        }

        void Update()
        {
            if (hype == null) return;
            if (debugHype >= 0f) hype.SetTarget(debugHype);
            hype.Tick(Time.unscaledDeltaTime);
        }

        #endregion

        #region Board state

        /// <summary>Recreates every view from the run state (stage start, continue).</summary>
        public void Rebuild(RunState run)
        {
            views.Rebuild(run);
            RestoreTelegraphs(run);
            sequences.ResetCats();
            cameraShaker.CaptureBase();
            RefreshDangerLevel(run);
        }

        /// <summary>
        /// Stage start (GDD v2 §5): rebuilds from the state but keeps the first batch (spawnEvents from BeginStage) hidden,
        /// so PlayBatchSpawnAsync can pop it in after the stage intro.
        /// </summary>
        public void RebuildForPopIn(RunState run, List<SimEvent> spawnEvents)
        {
            Rebuild(run);
            batchPlayer.Hide(spawnEvents);
            RefreshDangerLevel(run);
        }

        /// <summary>Pops in the batch of spawnEvents (all steps) and waits for the last pop to land; onBatch as in PlayEnemyPhaseAsync.</summary>
        public async UniTask PlayBatchSpawnAsync(List<SimEvent> spawnEvents, RunState run, Action? onBatch, CancellationToken ct)
        {
            var pacing = config.Pacing;
            if (await batchPlayer.PlayAsync(spawnEvents, run, -1, pacing.BatchSpawnStagger, onBatch, ct))
            {
                await UniTask.Delay(TimeSpan.FromSeconds(pacing.WaveSpawnDuration), cancellationToken: ct);
            }

            RestoreTelegraphs(run);
            RefreshDangerLevel(run);
        }

        // The warning icons are set from phase events; a continued run derives them from the cadence instead.
        void RestoreTelegraphs(RunState run)
        {
            var enemies = run.board.enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                labels.SetTelegraph(enemies[i].id, EnemyPhaseResolver.PendingTelegraph(rules, enemies[i]));
            }
        }

        /// <summary>
        /// Plays the immediate feedback for a frame's worth of player-turn events (hits, bounces, numbers); flightCombo is
        /// the flight's enemy hits before these events (the POWER combo), which every EnemyHit here counts on from.
        /// </summary>
        public void Consume(List<SimEvent> events, RunState run, int flightCombo)
        {
            var enemyRemoved = false;
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                eventPlayer.Play(ev, run);
                if (ev.kind == SimEventKind.EnemyHit) eventPlayer.PlayFlightCombo(ev, ++flightCombo);
                enemyRemoved |= ev.kind == SimEventKind.EnemyKilled;
            }

            if (enemyRemoved) RefreshDangerLevel(run);
        }

        /// <summary>Syncs ball views with the simulator (positions, trails, spawn/despawn).</summary>
        public void UpdateBalls(BallSimulator sim)
        {
            views.BeginBallFrame();
            sim.ForEachBall(ballVisitor);
            views.EndBallFrame();
        }

        /// <summary>
        /// Animates one resolved enemy phase step by step (events carry step 0..4) with PacingConfig timings; onBatch runs
        /// when a spawn batch starts popping in (the HUD's "Enemies incoming!").
        /// </summary>
        public async UniTask PlayEnemyPhaseAsync(List<SimEvent> events, RunState run, Action? onBatch, CancellationToken ct)
        {
            await phasePlayer.PlayAsync(events, run, config.Pacing, onBatch, ct);
            RefreshDangerLevel(run);
        }

        /// <summary>Releases every pooled view (stage transition, leaving gameplay).</summary>
        public void Clear()
        {
            views.ClearAll();
            hype.ResetHype();
            for (var i = 0; i < aimGuides.Length; i++)
            {
                aimGuides[i].SetVisible(false);
            }

            arenaView.SetDangerLevel(0f);
        }

        // 1 = an enemy stands in the danger row, 0.35 = one row above it, 0 = nothing close to the cat.
        void RefreshDangerLevel(RunState run)
        {
            var dangerRow = ArenaGeometry.DangerRow(rules.arena);
            var level = 0f;
            var enemies = run.board.enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var bottom = enemies[i].row + enemies[i].height - 1;
                if (bottom >= dangerRow)
                {
                    level = 1f;
                    break;
                }

                if (bottom == dangerRow - 1) level = 0.35f;
            }

            arenaView.SetDangerLevel(level);
        }

        #endregion

        #region Aim & cats

        public void SetAim(int shooterIndex, float launchX01, Vector2 dir, int predictedCount, Vector2[] predictedPoints, bool visible)
        {
            var player = Mathf.Clamp(shooterIndex, 0, cats.Length - 1);
            var cat = cats[player];
            var guide = aimGuides[player];
            cat.SetLaunchX(launchX01);
            cat.SetAim(ArenaGeometry.ClampAim(rules.arena, dir), visible);
            if (!visible)
            {
                guide.SetVisible(false);
                return;
            }

            if (predictedCount >= 2)
            {
                guide.SetPath(predictedCount, predictedPoints);
            }
            else
            {
                var origin = ArenaGeometry.LaunchOrigin(rules.arena, launchX01);
                fallbackPath[0] = origin;
                fallbackPath[1] = origin + ArenaGeometry.ClampAim(rules.arena, dir) * 3f;
                guide.SetPath(2, fallbackPath);
            }

            guide.SetVisible(true);
        }

        /// <summary>Every present player shoots together: each has a cat (or cue) and an aim guide in their colour.</summary>
        public void SetPlayers(int aNumPlayers)
        {
            numPlayers = Mathf.Clamp(aNumPlayers, 1, cats.Length);
            for (var i = 0; i < cats.Length; i++)
            {
                var present = i < numPlayers;
                cats[i].gameObject.SetActive(present);
                if (present) cats[i].SetActive(true);
                aimGuides[i].SetColor(config.Juice.PlayerColor(i));
                if (!present) aimGuides[i].SetVisible(false);
            }

            eventPlayer.ShooterIndex = 0;
        }

        public void PlayStrike(int shooterIndex, bool power)
        {
            var cat = cats[Mathf.Clamp(shooterIndex, 0, cats.Length - 1)];
            cat.PlayStrike();
            eventPlayer.PlaySfx(SfxManager.SoundEffect.CueStrike, power ? 0.85f : 1f, 1f);
            if (power) eventPlayer.PlaySfx(SfxManager.SoundEffect.PowerShot);
        }

        /// <summary>
        /// Hype 0..1 (GDD v2 §3) scales the flight and hit juice: ball glow / size / trail, hit VFX and extra sparks,
        /// camera shake, damage numbers, the tier-3 rim aura and the dancing cats. Call every frame (or on change);
        /// the look follows smoothly. Hit-stop time stays with Gameplay.
        /// </summary>
        public void SetHype(float hype01)
        {
            if (hype == null || debugHype >= 0f) return;
            hype.SetTarget(hype01);
        }

        /// <summary>Debug / verification: pins the shown Hype (0..1) regardless of SetHype; a negative value releases it.</summary>
        public void SetDebugHype(float hype01)
        {
            debugHype = hype01 < 0f ? -1f : Mathf.Clamp01(hype01);
            if (hype != null && debugHype < 0f) hype.SetTarget(0f);
        }

        /// <summary>Additive: lets the world camera rig be set explicitly when the camera has no parent rig.</summary>
        public void SetCameraRig(Camera worldCamera)
        {
            cameraShaker.Initialize(worldCamera, layout.GridCenterWorld, config.Visual.RenderResolution.y);
        }

        #endregion

        #region Sequences

        public UniTask PlayStageClearAsync(CancellationToken ct) => sequences.StageClearAsync(ct);

        public UniTask PlayBossIntroAsync(EnemyType boss, CancellationToken ct) => sequences.BossIntroAsync(boss, ct);

        public UniTask PlayDefeatAsync(CancellationToken ct) => sequences.DefeatAsync(ct);

        public UniTask PlayVictoryAsync(CancellationToken ct) => sequences.VictoryAsync(ct);

        #endregion
    }
}
