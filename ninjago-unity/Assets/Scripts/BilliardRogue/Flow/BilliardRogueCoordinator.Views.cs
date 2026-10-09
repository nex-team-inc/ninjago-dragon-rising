#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    // Menu views (UI-Views module): the coordinator instantiates them, feeds their data and owns every transition
    // their events ask for.
    public sealed partial class BilliardRogueCoordinator
    {
        [Header("UI views (UI-Views module prefabs, wired by FlowPrefabsBuilder)")]
        [SerializeField] TitleView titleViewPrefab = null!;
        [SerializeField] PlayerModeView playerModeViewPrefab = null!;
        [SerializeField] SummaryView summaryViewPrefab = null!;
        [SerializeField] SettingsView settingsViewPrefab = null!;

        TitleView? titleView;

        TitleView CreateTitleView(RunState? save, MetaProgressData meta)
        {
            var view = Instantiate(titleViewPrefab);
            view.SetContinueInfo(save);
            view.SetBest(meta);
            view.ContinueRequested += HandleContinueRequested;
            view.NewRunRequested += HandleNewRunRequested;
            view.SettingsRequested += HandleSettingsRequested;
            view.ExitRequested += HandleExitRequested;
            titleView = view;
            return view;
        }

        /// <summary>Continue / best record after a run ended or a save was dropped.</summary>
        void RefreshTitle()
        {
            if (titleView == null) return;
            titleView.SetContinueInfo(persistence.Load());
            titleView.SetBest(persistence.MetaProgress);
        }

        PlayerModeView CreatePlayerModeView()
        {
            var view = Instantiate(playerModeViewPrefab);
            view.PlayersChosen += HandlePlayersChosen;
            return view;
        }

        SummaryView CreateSummaryView(RunState run, MetaProgressData meta, bool newRecord, int unlockTierBefore)
        {
            var view = Instantiate(summaryViewPrefab);
            view.Initialize(config.Balls);
            view.Show(run, meta, newRecord, unlockTierBefore);
            view.PlayAgainRequested += HandlePlayAgainRequested;
            view.TitleRequested += HandleTitleRequested;
            return view;
        }

        SettingsView CreateSettingsView()
        {
            return Instantiate(settingsViewPrefab);
        }
    }
}
