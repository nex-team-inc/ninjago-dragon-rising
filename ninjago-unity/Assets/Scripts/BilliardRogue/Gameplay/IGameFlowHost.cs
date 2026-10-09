#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>Overlay flow the coordinator/GameplayView provides to GameSession (stage intro, reward, tracking lost, end).</summary>
    public interface IGameFlowHost
    {
        UniTask ShowStageIntroAsync(int actIndex, int stageInAct, bool isBoss, CancellationToken ct);
        /// <summary>Returns the chosen option index (0..options.Count-1).</summary>
        UniTask<int> ChooseRewardAsync(IReadOnlyList<RewardOption> options, RunState run, CancellationToken ct);
        /// <summary>Shows the tracking-lost overlay until that player is tracked again.</summary>
        UniTask ShowTrackingLostAsync(int playerIndex, CancellationToken ct);
        void NotifyRunEnded(RunState run);
    }
}
