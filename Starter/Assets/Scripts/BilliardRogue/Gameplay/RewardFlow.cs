#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The between-stage reward: rolls the three cards (or re-displays run.pendingRewards after a continue),
    /// applies the debug forceRewardBall / unlockAllBalls settings, saves before and after the pick so a killed app
    /// resumes at the choice, and logs reward_offered / reward_chosen.
    /// </summary>
    public sealed class RewardFlow
    {
        readonly SessionServices services;
        readonly List<RewardOption> rolled = new(3);

        public RewardFlow(SessionServices aServices)
        {
            services = aServices;
        }

        #region Public Methods

        /// <summary>Prepares run.pendingRewards, saves, and returns the flow host's choice task.</summary>
        public UniTask<int> Offer(CancellationToken ct)
        {
            var run = services.Run;
            services.Audio.PlayMusic(BgmManager.BgmType.Reward);
            if (run.pendingRewards.Count == 0)
            {
                var tier = services.Debug.unlockAllBalls ? int.MaxValue : services.Persistence.MetaProgress.highestUnlockTier;
                services.Sim.RollRewards(tier, rolled);
                ForceRewardBall(rolled);
                run.pendingRewards.Clear();
                run.pendingRewards.AddRange(rolled);
                run.awaitingReward = true;
            }

            services.Persistence.SaveTurnBoundary(run);
            services.Analytics.RewardOffered(run.pendingRewards);
            return services.FlowHost.ChooseRewardAsync(run.pendingRewards, run, ct);
        }

        /// <summary>Applies the chosen card (clears the pending list), refreshes the HUD and saves.</summary>
        public void Apply(int index)
        {
            var run = services.Run;
            var options = run.pendingRewards;
            if (options.Count > 0)
            {
                index = Mathf.Clamp(index, 0, options.Count - 1);
                var option = options[index];
                services.Sim.ApplyReward(option);
                services.Analytics.RewardChosen(option, index);
            }

            run.awaitingReward = false;
            services.Hud.RefreshHp();
            services.Hud.RefreshQueue();
            services.Persistence.SaveTurnBoundary(run);
        }

        #endregion

        #region Helpers

        void ForceRewardBall(List<RewardOption> options)
        {
            var forced = services.Debug.forceRewardBall;
            if (forced < 0 || forced >= SimConstants.BallTypeCount || options.Count == 0) return;
            if (services.Run.bag.Count >= services.Rules.balance.bagCap) return;
            options[0] = new RewardOption { kind = RewardKind.NewBall, ballType = (BallType)forced };
        }

        #endregion
    }
}
