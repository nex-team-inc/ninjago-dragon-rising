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
    /// GDD v2 §5 pop-in: a spawn batch's enemies and pickups appear from nowhere at their cells, one every
    /// PacingConfig.BatchSpawnStagger in spawn order (SimEvent.value2): a portal flash and a dust puff at the cell, the view
    /// scales in from 0 with an overshoot (EnemyView / PickupView pop) and a pop SFX that climbs in pitch. The batch opens
    /// with the portal whoosh and the "Enemies incoming!" callback. Every other event of the range plays through
    /// BoardEventPlayer unchanged (caster spawns keep their dust puff). Used by the enemy phase (step 4) and by the stage
    /// start, where the board is rebuilt first and the batch views are hidden until the intro is over.
    /// </summary>
    public sealed class BatchSpawnPlayer
    {
        const float PortalScale = 0.7f;
        const float PopPitchStep = 0.035f;
        const float PopPitchMax = 1.45f;

        readonly BoardViews views;
        readonly BoardEventPlayer player;

        public BatchSpawnPlayer(BoardViews aViews, BoardEventPlayer aPlayer)
        {
            views = aViews;
            player = aPlayer;
        }

        #region Public Methods

        /// <summary>Releases the batch views a Rebuild created from the state, so PlayAsync can pop them in.</summary>
        public void Hide(List<SimEvent> events)
        {
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.kind == SimEventKind.EnemySpawned && ev.flag) views.RemoveEnemy(ev.targetId, false);
                else if (ev.kind == SimEventKind.PickupSpawned) views.RemovePickup(ev.targetId, false);
            }
        }

        /// <summary>
        /// Plays the events of one enemy-phase step (step &lt; 0 = every event). Returns true when a batch popped in
        /// (the caller then waits PacingConfig.WaveSpawnDuration for the last pops to land).
        /// </summary>
        public async UniTask<bool> PlayAsync(List<SimEvent> events, RunState run, int step, float stagger, Action? onBatch, CancellationToken ct)
        {
            var batch = false;
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.kind == SimEventKind.BatchSpawned && ev.value2 > 0 && (step < 0 || ev.step == step)) batch = true;
            }

            if (batch)
            {
                onBatch?.Invoke();
                player.PlaySfx(SfxManager.SoundEffect.Portal, 0.85f, 0.9f);
            }

            var popped = 0;
            for (var i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (step >= 0 && ev.step != step) continue;
                switch (ev.kind)
                {
                    case SimEventKind.EnemySpawned when ev.flag:
                    case SimEventKind.PickupSpawned:
                        if (popped > 0 && stagger > 0f) await UniTask.Delay(TimeSpan.FromSeconds(stagger), cancellationToken: ct);
                        Pop(ev, run, popped++);
                        break;
                    case SimEventKind.BatchSpawned:
                        views.SyncPickups(run);
                        break;
                    default:
                        player.Play(ev, run);
                        break;
                }
            }

            return batch;
        }

        #endregion

        #region Helpers

        void Pop(in SimEvent ev, RunState run, int index)
        {
            Vector3 feet;
            if (ev.kind == SimEventKind.PickupSpawned)
            {
                var pickup = BoardViews.FindPickup(run, ev.targetId);
                if (pickup == null) return;
                var view = views.SpawnPickup(pickup, true);
                feet = view.Position;
                player.PlayVfx(VfxManager.VisualEffect.PickupSparkle, view.Center, 0.8f);
            }
            else
            {
                var enemy = BoardViews.FindEnemy(run, ev.targetId);
                if (enemy == null) return;
                feet = views.SpawnEnemy(enemy, true).Position;
            }

            player.PlayVfx(VfxManager.VisualEffect.PortalFlash, feet, PortalScale);
            player.PlayVfx(VfxManager.VisualEffect.DustPuff, feet);
            player.PlaySfx(SfxManager.SoundEffect.Split, Mathf.Min(PopPitchMax, 0.95f + PopPitchStep * index), 0.6f);
        }

        #endregion
    }
}
