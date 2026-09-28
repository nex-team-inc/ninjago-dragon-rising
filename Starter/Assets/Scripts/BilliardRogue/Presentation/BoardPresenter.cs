#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Owns every visual on the arena (enemies, balls, field objects, pickups, cats, aim guide, labels, juice)
    /// and turns SimEvents into animation, VFX and SFX. Never re-derives geometry: everything goes through ArenaLayout.
    /// Composition: BoardViews (pools and id registry), BoardEventPlayer (event feedback), EnemyPhasePlayer
    /// (stepped enemy phase), BoardSequencePlayer (stage clear / boss intro / defeat / victory), WorldLabelLayer (UI labels).
    /// </summary>
    public sealed class BoardPresenter : MonoBehaviour
    {
        [Header("Pools (WorldPrefabsBuilder)")]
        [Tooltip("Indexed by EnemyType.")]
        [SerializeField] EnemyViewPool[] enemyPools = System.Array.Empty<EnemyViewPool>();
        [SerializeField] BallViewPool ballPool = null!;
        [SerializeField] BallView ballPrefab = null!;
        [Tooltip("Indexed by FieldObjectType.")]
        [SerializeField] FieldObjectViewPool[] objectPools = System.Array.Empty<FieldObjectViewPool>();
        [Tooltip("Indexed by PickupType.")]
        [SerializeField] PickupViewPool[] pickupPools = System.Array.Empty<PickupViewPool>();

        [Header("Board")]
        [SerializeField] AimGuideView aimGuide = null!;
        [Tooltip("Cat_P1, Cat_P2.")]
        [SerializeField] CatView[] cats = System.Array.Empty<CatView>();
        [SerializeField] CameraShaker cameraShaker = null!;

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
        BoardSequencePlayer sequences = null!;
        BallVisitor ballVisitor = null!;
        Vector2[] fallbackPath = new Vector2[2];
        int activeShooter;
        int numPlayers = 1;

        public CameraShaker Shaker => cameraShaker;
        public WorldLabelLayer Labels => labels;
        public AimGuideView AimGuide => aimGuide;

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

            aimGuide.Initialize(layout, config.Juice);
            views = new BoardViews(config, rules, layout, enemyPools, ballPool, objectPools, pickupPools, labels, ballPrefab, worldLayer);
            var combo = new ComboPresenter(config.Juice, labels);
            eventPlayer = new BoardEventPlayer(views, labels, combo, cameraShaker, config, layout, cats);
            phasePlayer = new EnemyPhasePlayer(views, eventPlayer, labels, cameraShaker, layout, config, cats);
            sequences = new BoardSequencePlayer(views, eventPlayer, cameraShaker, config, cats);
            ballVisitor = views.OnBall;
            SetActiveShooter(0, 1);
        }

        #endregion

        #region Board state

        /// <summary>Recreates every view from the run state (stage start, continue).</summary>
        public void Rebuild(RunState run)
        {
            views.Rebuild(run);
            sequences.ResetCats();
            cameraShaker.CaptureBase();
        }

        /// <summary>Plays the immediate feedback for a frame's worth of player-turn events (hits, bounces, numbers).</summary>
        public void Consume(List<SimEvent> events, RunState run)
        {
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                eventPlayer.Play(ev, run);
            }
        }

        /// <summary>Syncs ball views with the simulator (positions, trails, spawn/despawn).</summary>
        public void UpdateBalls(BallSimulator sim)
        {
            views.BeginBallFrame();
            sim.ForEachBall(ballVisitor);
            views.EndBallFrame();
        }

        /// <summary>Animates one resolved enemy phase step by step (events carry step 0..4) with PacingConfig timings.</summary>
        public UniTask PlayEnemyPhaseAsync(List<SimEvent> events, RunState run, CancellationToken ct)
        {
            return phasePlayer.PlayAsync(events, run, config.Pacing, ct);
        }

        /// <summary>Releases every pooled view (stage transition, leaving gameplay).</summary>
        public void Clear()
        {
            views.ClearAll();
            aimGuide.SetVisible(false);
        }

        #endregion

        #region Aim & cats

        public void SetAim(int shooterIndex, float launchX01, Vector2 dir, int predictedCount, Vector2[] predictedPoints, bool visible)
        {
            var cat = cats[Mathf.Clamp(shooterIndex, 0, cats.Length - 1)];
            cat.SetLaunchX(launchX01);
            if (!visible)
            {
                aimGuide.SetVisible(false);
                return;
            }

            if (predictedCount >= 2)
            {
                aimGuide.SetPath(predictedCount, predictedPoints);
            }
            else
            {
                var origin = ArenaGeometry.LaunchOrigin(rules.arena, launchX01);
                fallbackPath[0] = origin;
                fallbackPath[1] = origin + ArenaGeometry.ClampAim(rules.arena, dir) * 3f;
                aimGuide.SetPath(2, fallbackPath);
            }

            aimGuide.SetVisible(true);
        }

        public void SetActiveShooter(int playerIndex, int aNumPlayers)
        {
            numPlayers = Mathf.Clamp(aNumPlayers, 1, cats.Length);
            activeShooter = Mathf.Clamp(playerIndex, 0, numPlayers - 1);
            for (var i = 0; i < cats.Length; i++)
            {
                var present = i < numPlayers;
                cats[i].gameObject.SetActive(present);
                if (present) cats[i].SetActive(i == activeShooter);
            }

            aimGuide.SetColor(config.Juice.PlayerColor(activeShooter));
            eventPlayer.ShooterIndex = activeShooter;
        }

        public void PlayStrike(int shooterIndex, bool power)
        {
            var cat = cats[Mathf.Clamp(shooterIndex, 0, cats.Length - 1)];
            cat.PlayStrike();
            eventPlayer.PlaySfx(SfxManager.SoundEffect.CueStrike, power ? 0.85f : 1f, 1f);
            if (power) eventPlayer.PlaySfx(SfxManager.SoundEffect.PowerShot);
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
