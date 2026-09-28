#nullable enable

using System;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Run and meta persistence on top of an IRunStore (PlayerDataManager in the game). Saves happen at stable
    /// points only (stage start, turn start, reward roll and pick, app pause at a turn start), so a continued run
    /// resumes at the start of a player turn or at the reward pick.
    /// </summary>
    public sealed class RunPersistence
    {
        readonly IRunStore store;

        /// <summary>Raised after every run save (BeginRun, SaveTurnBoundary).</summary>
        public event Action<RunState>? Saved;

        public RunPersistence(PlayerDataManager playerData) : this(new PlayerDataRunStore(playerData))
        {
        }

        public RunPersistence(IRunStore aStore)
        {
            store = aStore;
        }

        #region Run

        /// <summary>True when Load() would return a run: finished or unreadable saves do not count (and are dropped).</summary>
        public bool HasSave => Load() != null;

        public MetaProgressData MetaProgress => store.MetaProgress;

        /// <summary>The saved run, or null when there is none or it is unreadable / already finished (both are dropped).</summary>
        public RunState? Load()
        {
            var run = store.LoadRun();
            if (run == null) return null;
            if (run.outcome != RunOutcome.None)
            {
                store.ClearRun();
                return null;
            }

            return run;
        }

        /// <summary>Marks a new run in the meta progress and drops any previous save.</summary>
        public void BeginRun(RunState run)
        {
            store.ClearRun();
            store.MetaProgress.runsStarted++;
            store.SaveMetaProgress();
            store.SaveRun(run);
            Saved?.Invoke(run);
        }

        /// <summary>Snapshot at a stable point (turn start, reward, quit). Also bumps unlock tiers from boss kills.</summary>
        public void SaveTurnBoundary(RunState run)
        {
            store.SaveRun(run);
            if (UpdateUnlockTier(run)) store.SaveMetaProgress();
            Saved?.Invoke(run);
        }

        #endregion

        #region Meta Progress

        /// <summary>
        /// Applies the finished run to the meta progress (best stage, wins, kills, unlocks) and clears the save.
        /// Returns true on a new best. A victory counts as reaching SimConstants.StageCount, one past the last
        /// stage index, so it beats a defeat on the final boss and bestStageNumber >= StageCount means "cleared".
        /// </summary>
        public bool CompleteRun(RunState run)
        {
            var meta = store.MetaProgress;
            var reached = run.outcome == RunOutcome.Victory ? SimConstants.StageCount : run.stageNumber;
            var newRecord = reached > meta.bestStageNumber;
            if (newRecord) meta.bestStageNumber = reached;
            if (run.outcome == RunOutcome.Victory) meta.runsWon++;
            meta.totalKills += run.stats.kills;
            UpdateUnlockTier(run);
            store.SaveMetaProgress();
            store.ClearRun();
            return newRecord;
        }

        /// <summary>Drops the saved run without touching meta progress (Save &amp; Quit keeps the save; this is for giving up).</summary>
        public void Abandon()
        {
            store.ClearRun();
        }

        public void MarkTutorialSeen()
        {
            if (store.MetaProgress.tutorialSeen) return;
            store.MetaProgress.tutorialSeen = true;
            store.SaveMetaProgress();
        }

        // Unlock tiers follow boss kills: tier 1 after King Slime (act 1), tier 2 after Bone Lich (act 2).
        bool UpdateUnlockTier(RunState run)
        {
            var meta = store.MetaProgress;
            var tier = Math.Min(run.stats.bossesDefeated, SimConstants.ActCount);
            if (tier <= meta.highestUnlockTier) return false;
            meta.highestUnlockTier = tier;
            return true;
        }

        #endregion
    }
}
