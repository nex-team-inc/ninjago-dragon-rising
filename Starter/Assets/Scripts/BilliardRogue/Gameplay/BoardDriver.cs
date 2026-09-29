#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The session's view of BoardPresenter; every call is a no-op in headless smoke runs (no presenter). BatchIncoming runs
    /// whenever a spawn batch starts popping in (stage start and enemy phase, GDD v2 §5).
    /// </summary>
    public sealed class BoardDriver
    {
        readonly BoardPresenter presenter;
        readonly bool enabled;

        public Action? BatchIncoming { get; set; }

        public BoardDriver(BoardPresenter? aPresenter)
        {
            enabled = aPresenter != null;
            presenter = aPresenter!;
        }

        public void Rebuild(RunState run)
        {
            if (enabled) presenter.Rebuild(run);
        }

        /// <summary>flightCombo: the flight's enemy hits before these events (HypeController.ComboHits), for the COMBO floats.</summary>
        public void Consume(List<SimEvent> events, RunState run, int flightCombo = 0)
        {
            if (enabled) presenter.Consume(events, run, flightCombo);
        }

        public void UpdateBalls(BallSimulator sim)
        {
            if (enabled) presenter.UpdateBalls(sim);
        }

        public UniTask PlayEnemyPhaseAsync(List<SimEvent> events, RunState run, CancellationToken ct)
        {
            return enabled ? presenter.PlayEnemyPhaseAsync(events, run, BatchIncoming, ct) : UniTask.CompletedTask;
        }

        /// <summary>Stage start: rebuilds the board with the first batch (BeginStage's events) hidden for its pop-in.</summary>
        public void RebuildForPopIn(RunState run, List<SimEvent> spawnEvents)
        {
            if (enabled) presenter.RebuildForPopIn(run, spawnEvents);
        }

        public UniTask PlayBatchSpawnAsync(List<SimEvent> spawnEvents, RunState run, CancellationToken ct)
        {
            return enabled ? presenter.PlayBatchSpawnAsync(spawnEvents, run, BatchIncoming, ct) : UniTask.CompletedTask;
        }

        public void SetAim(int shooterIndex, float launchX01, Vector2 direction, int predictedCount, Vector2[] predictedPoints, bool visible)
        {
            if (enabled) presenter.SetAim(shooterIndex, launchX01, direction, predictedCount, predictedPoints, visible);
        }

        public void SetPlayers(int numPlayers)
        {
            if (enabled) presenter.SetPlayers(numPlayers);
        }

        /// <summary>Hype 0..1 for the presenter's juice scaling (GDD v2 §3).</summary>
        public void SetHype(float hype01)
        {
            if (enabled) presenter.SetHype(hype01);
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
