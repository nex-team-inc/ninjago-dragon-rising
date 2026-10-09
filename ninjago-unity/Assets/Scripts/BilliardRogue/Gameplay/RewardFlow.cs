#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The between-stage reward (GDD v2 §1: balls only): rolls the ball options (or re-displays run.pendingRewards
    /// after a continue; a v1 save's pending Heal / Max HP cards are re-rolled as balls), applies the debug
    /// forceRewardBall / unlockAllBalls settings, saves before and after the pick so a killed app resumes at the
    /// choice, and logs reward_offered / reward_chosen. With no option left (the whole bag maxed at bagCap) the choice
    /// is skipped.
    /// </summary>
    public sealed class RewardFlow
    {
        readonly SessionServices services;
        readonly List<RewardOption> rolled = new(3);

        public RewardFlow(SessionServices aServices)
        {
            services = aServices;
        }

        /// <summary>Prepares run.pendingRewards, saves, and returns the flow host's choice task.</summary>
        public UniTask<int> Offer(CancellationToken ct)
        {
            var run = services.Run;
            services.Audio.PlayMusic(BgmManager.BgmType.Reward);
            if (run.pendingRewards.Count == 0 || HasLegacyCard(run.pendingRewards))
            {
                // The boss kill that ends the stage unlocks balls for this very reward, not the next one.
                services.Persistence.RefreshUnlocks(run);
                var tier = services.Debug.unlockAllBalls ? int.MaxValue : services.Persistence.MetaProgress.highestUnlockTier;
                services.Sim.RollRewards(tier, rolled);
                ForceRewardBall(rolled);
                run.pendingRewards.Clear();
                run.pendingRewards.AddRange(rolled);
                run.awaitingReward = true;
            }

            services.Persistence.SaveTurnBoundary(run);
            if (run.pendingRewards.Count == 0) return UniTask.FromResult(-1);
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

        // The forced ball becomes the first option (a level-up at bagCap); a duplicate of its type is dropped.
        void ForceRewardBall(List<RewardOption> options)
        {
            var forced = services.Debug.forceRewardBall;
            if (forced < 0 || forced >= SimConstants.BallTypeCount) return;
            var type = (BallType)forced;
            if (!RewardGenerator.TryMakeBallOption(services.Rules, services.Run, type, out var option)) return;
            for (var i = options.Count - 1; i >= 0; i--)
            {
                if (options[i].ballType == type) options.RemoveAt(i);
            }

            options.Insert(0, option);
            if (options.Count > RewardGenerator.OptionCount) options.RemoveAt(options.Count - 1);
        }

        static bool HasLegacyCard(List<RewardOption> options)
        {
            for (var i = 0; i < options.Count; i++)
            {
                var kind = options[i].kind;
                if (kind != RewardKind.NewBall && kind != RewardKind.UpgradeBall) return true;
            }

            return false;
        }
    }
}
