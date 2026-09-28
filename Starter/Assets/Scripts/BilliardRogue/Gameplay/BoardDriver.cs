#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>The session's view of BoardPresenter; every call is a no-op in headless smoke runs (no presenter).</summary>
    public sealed class BoardDriver
    {
        readonly BoardPresenter presenter;
        readonly bool enabled;

        public BoardDriver(BoardPresenter? aPresenter)
        {
            enabled = aPresenter != null;
            presenter = aPresenter!;
        }

        public void Rebuild(RunState run)
        {
            if (enabled) presenter.Rebuild(run);
        }

        public void Consume(List<SimEvent> events, RunState run)
        {
            if (enabled) presenter.Consume(events, run);
        }

        public void UpdateBalls(BallSimulator sim)
        {
            if (enabled) presenter.UpdateBalls(sim);
        }

        public UniTask PlayEnemyPhaseAsync(List<SimEvent> events, RunState run, CancellationToken ct)
        {
            return enabled ? presenter.PlayEnemyPhaseAsync(events, run, ct) : UniTask.CompletedTask;
        }

        public void SetAim(int shooterIndex, float launchX01, Vector2 direction, int predictedCount, Vector2[] predictedPoints, bool visible)
        {
            if (enabled) presenter.SetAim(shooterIndex, launchX01, direction, predictedCount, predictedPoints, visible);
        }

        public void SetActiveShooter(int playerIndex, int numPlayers)
        {
            if (enabled) presenter.SetActiveShooter(playerIndex, numPlayers);
        }

        public void PlayStrike(int shooterIndex, bool power)
        {
            if (enabled) presenter.PlayStrike(shooterIndex, power);
        }

        public UniTask PlayStageClearAsync(CancellationToken ct) => enabled ? presenter.PlayStageClearAsync(ct) : UniTask.CompletedTask;

        public UniTask PlayBossIntroAsync(EnemyType boss, CancellationToken ct) => enabled ? presenter.PlayBossIntroAsync(boss, ct) : UniTask.CompletedTask;

        public UniTask PlayDefeatAsync(CancellationToken ct) => enabled ? presenter.PlayDefeatAsync(ct) : UniTask.CompletedTask;

        public UniTask PlayVictoryAsync(CancellationToken ct) => enabled ? presenter.PlayVictoryAsync(ct) : UniTask.CompletedTask;

        public void Clear()
        {
            if (enabled) presenter.Clear();
        }
    }
}
