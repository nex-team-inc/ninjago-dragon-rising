#nullable enable

using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>The production IRunStore: PlayerDataManager stays the only Easy Save user.</summary>
    public sealed class PlayerDataRunStore : IRunStore
    {
        readonly PlayerDataManager playerData;

        public PlayerDataRunStore(PlayerDataManager aPlayerData)
        {
            playerData = aPlayerData;
        }

        public RunState? LoadRun() => playerData.LoadRun();

        public void SaveRun(RunState run) => playerData.SaveRun(run);

        public void ClearRun() => playerData.ClearRun();

        public MetaProgressData MetaProgress => playerData.MetaProgress;

        public void SaveMetaProgress() => playerData.SaveMetaProgress();
    }
}
