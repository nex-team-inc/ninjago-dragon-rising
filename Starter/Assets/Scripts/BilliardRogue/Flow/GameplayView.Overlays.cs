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
    // for the ViewManager to be idle, because the session asks from its own loop while a pause may be in flight, and
    // session overlays also wait while paused, so the pause view always stays the top view.
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
            await UniTask.WaitWhile(overlaysBlocked, cancellationToken: ct);
            // The band covers the middle of the screen: the HUD leaves first and slides back in after it.
            SetHudRevealed(false);
            var view = Instantiate(stageIntroViewPrefab);
            view.Initialize(context.config.Pacing, context.config.Enemies, context.config.Acts);
            view.Show(actIndex, stageInAct, isBoss);
            await manager.PushView(view);
            RevealWorld();
            await view.WaitClosedAsync(ct);
            SetHudRevealed(true);
        }

        public async UniTask<int> ChooseRewardAsync(IReadOnlyList<RewardOption> options, RunState run, CancellationToken ct)
        {
            // A run continued at its reward never passed the stage intro: give it the cleared stage's act look
            // (a no-op whenever the act is already shown).
            context.environment.ApplyAct(context.config.Acts[run.actIndex]);
            await UniTask.WaitWhile(overlaysBlocked, cancellationToken: ct);
            // The three balls span the width of the screen: the HUD columns (and with them the PiP feed) leave, and the
            // stage intro that follows every reward brings them back.
            SetHudRevealed(false);
            var view = Instantiate(rewardViewPrefab);
            view.Initialize(context.config.Pacing, context.display.WorldCamera);
            // Motion pick (GDD v2 §4): in 2P the chooser alternates per reward (by stage); its paws drive the cat arms.
            var chooser = run.numPlayers > 1 ? run.stageNumber % run.numPlayers : 0;
            var router = chooser < context.inputs.Length ? context.inputs[chooser] as ShotInputRouter : null;
            view.SetPawPointer(router != null ? router.PawPointer : null, chooser, run.numPlayers);
            activeReward = view;
            try
            {
                // ChooseAsync waits for the view to be on top and pops it after the pick.
                var push = manager.PushView(view);
                RevealWorld();
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
            await UniTask.WaitWhile(overlaysBlocked, cancellationToken: ct);
            var input = context.inputs[playerIndex];
            var lostAt = Time.realtimeSinceStartup;
            var view = Instantiate(trackingLostViewPrefab);
            // Back on the overlay pauses over it, so Save & Quit stays reachable when the player never returns; after
            // Resume the tracking-lost hold keeps the sim frozen.
            view.Initialize(playerIndex, context.run.numPlayers, () => input.IsTracking, BeginPause);
            await manager.PushView(view);
            try
            {
                await view.WaitClosedAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // The run ended (or the view host died) while the player was away: let the overlay close itself.
                if (view != null)
                {
                    view.RequestClose();
                }

                throw;
            }

            // The single tracking_lost emitter (real time away). Save & Quit unwinds the overlay without cancelling
            // ct, after run_end: nothing to report then.
            if (!pauseGate.RunEnded)
            {
                context.analytics.TrackingLost(playerIndex, Time.realtimeSinceStartup - lostAt);
            }
        }

        #endregion

        #region Pause overlay

        async UniTaskVoid PushPauseOverlayAsync(CancellationToken ct)
        {
            await UniTask.WaitWhile(managerInTransition, cancellationToken: ct);
            if (!pauseGate.TryShowOverlay()) return;
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
