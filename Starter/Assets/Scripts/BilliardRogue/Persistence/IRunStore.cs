#nullable enable

using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Run and meta storage behind RunPersistence: PlayerDataManager (Easy Save) in the game, an in-memory
    /// JSON store in editor smoke runs. Implementations hand out copies, never the live RunState.
    /// </summary>
    public interface IRunStore
    {
        RunState? LoadRun();
        void SaveRun(RunState run);
        void ClearRun();
        MetaProgressData MetaProgress { get; }
        void SaveMetaProgress();
    }
}
