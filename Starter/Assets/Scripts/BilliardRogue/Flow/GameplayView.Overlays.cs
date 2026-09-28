#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    // IGameFlowHost: the session's overlays (UI-Views module prefabs) pushed on top of this view. Every push waits
    // for the ViewManager to be idle, because the session asks from its own loop while a pause may be in flight.
    public sealed partial class GameplayView
    {
        [Header("Overlays (UI-Views module prefabs, wired by FlowPrefabsBuilder)")]
        [SerializeField] StageIntroView stageIntroViewPrefab = null!;
        [SerializeField] RewardView rewardViewPrefab = null!;
        [SerializeField] TrackingLostView trackingLostViewPrefab = null!;
        [SerializeField] PauseView pauseViewPrefab = null!;

        RewardView? activeReward;

        #region IGameFlowHost

        public async UniTask ShowStageIntroAsync(int actIndex, int stageInAct, bool isBoss, CancellationToken ct)
        {
            // The act look fades in behind the intro overlay (a no-op for every stage after the act's first).
            context.environment.ApplyAct(context.config.Acts[actIndex]);
            await UniTask.WaitWhile(managerInTransition, cancellationToken: ct);
            var view = Instantiate(stageIntroViewPrefab);
            view.Initialize(context.config.Pacing, context.config.Enemies, context.config.Acts);
            view.Show(actIndex, stageInAct, isBoss);
            await manager.PushView(view);
            await view.WaitClosedAsync(ct);
        }

        public async UniTask<int> ChooseRewardAsync(IReadOnlyList<RewardOption> options, RunState run, CancellationToken ct)
        {
            await UniTask.WaitWhile(managerInTransition, cancellationToken: ct);
            var view = Instantiate(rewardViewPrefab);
            view.Initialize(context.config.Pacing);
            activeReward = view;
            try
            {
                // ChooseAsync waits for the view to be on top and pops it after the pick.
                var push = manager.PushView(view);
                var index = await view.ChooseAsync(options, context.config.Balls, run, ct);
                await push;
                return index;
            }
            finally
            {
                activeReward = null;
            }
        }

        public async UniTask ShowTrackingLostAsync(int playerIndex, CancellationToken ct)
        {
            await UniTask.WaitWhile(managerInTransition, cancellationToken: ct);
            var input = context.inputs[playerIndex];
            var lostAt = Time.realtimeSinceStartup;
            var view = Instantiate(trackingLostViewPrefab);
            view.Initialize(playerIndex, context.run.numPlayers, () => input.IsTracking);
            await manager.PushView(view);
            try
            {
                await view.WaitClosedAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // The run ended (or the view host died) while the player was away: let the overlay close itself.
                if (view != null) view.RequestClose();
                throw;
            }

            context.analytics.TrackingLost(playerIndex, Time.realtimeSinceStartup - lostAt);
        }

        #endregion

        #region Pause overlay

        async UniTask PushPauseOverlayAsync(CancellationToken ct)
        {
            await UniTask.WaitWhile(managerInTransition, cancellationToken: ct);
            if (!paused) return;
            await manager.PushView(Instantiate(pauseViewPrefab));
        }

        #endregion

        #region Debug

        /// <summary>DebugHooks.ChooseReward: picks a card while the reward view is selectable.</summary>
        public bool ChooseRewardForDebug(int index)
        {
            return activeReward != null && activeReward.TryChoose(index);
        }

        #endregion
    }
}
