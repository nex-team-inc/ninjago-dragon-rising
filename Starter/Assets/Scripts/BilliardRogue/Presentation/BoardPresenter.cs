#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

// Implemented by Presentation module.
namespace Nex.BilliardRogue
{
    /// <summary>
    /// Owns every visual on the arena (enemies, balls, field objects, pickups, cats, aim guide, labels, juice)
    /// and turns SimEvents into animation, VFX and SFX. Never re-derives geometry: everything goes through ArenaLayout.
    /// </summary>
    public sealed class BoardPresenter : MonoBehaviour
    {
        public void Initialize(BilliardRogueConfig config, GameRules rules, ArenaLayout layout, PixelWorldDisplay display, RectTransform labelLayer)
        {
        }

        /// <summary>Recreates every view from the run state (stage start, continue).</summary>
        public void Rebuild(RunState run)
        {
        }

        /// <summary>Plays the immediate feedback for a frame's worth of player-turn events (hits, bounces, numbers).</summary>
        public void Consume(List<SimEvent> events, RunState run)
        {
        }

        /// <summary>Syncs ball views with the simulator (positions, trails, spawn/despawn).</summary>
        public void UpdateBalls(BallSimulator sim)
        {
        }

        /// <summary>Animates one resolved enemy phase step by step (events carry step 0..4) with PacingConfig timings.</summary>
        public UniTask PlayEnemyPhaseAsync(List<SimEvent> events, RunState run, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        public void SetAim(int shooterIndex, float launchX01, Vector2 dir, int predictedCount, Vector2[] predictedPoints, bool visible)
        {
        }

        public void SetActiveShooter(int playerIndex, int numPlayers)
        {
        }

        public void PlayStrike(int shooterIndex, bool power)
        {
        }

        public UniTask PlayStageClearAsync(CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        public UniTask PlayBossIntroAsync(EnemyType boss, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        public UniTask PlayDefeatAsync(CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        public UniTask PlayVictoryAsync(CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>Releases every pooled view (stage transition, leaving gameplay).</summary>
        public void Clear()
        {
        }
    }
}
