#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using Nex.KeyboardNavigation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Title menu over the idle diorama: Continue (only with a run save, shows act/stage/HP), New Run, Settings, best
    /// record. Escape / top-level Exit raises ExitRequested. The coordinator owns the flow through the events.
    /// </summary>
    public sealed class TitleView : RogueView
    {
        [Header("Menu")]
        [SerializeField] UnityEngine.UI.Button continueButton = null!;
        [SerializeField] UnityEngine.UI.Button newRunButton = null!;
        [SerializeField] UnityEngine.UI.Button settingsButton = null!;
        [Tooltip("'Act 1 · Stage 2 · 24 HP' under the Continue label.")]
        [SerializeField] TextLabel continueInfo = null!;
        [Tooltip("Menu focus group: a focused Continue that disappears (the run just ended) would leave nothing focused.")]
        [SerializeField] GroupKeyResponder menuGroup = null!;
        [SerializeField] KeyResponder continueResponder = null!;
        [SerializeField] KeyResponder newRunResponder = null!;

        [Header("Record")]
        [SerializeField] TextLabel bestLabel = null!;
        [SerializeField] TextLabel runsLabel = null!;

        public event Action? ContinueRequested;
        public event Action? NewRunRequested;
        public event Action? SettingsRequested;
        public event Action? ExitRequested;

        public override ViewIdentifier Identifier => ViewIdentifier.Title;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Exit;
        public override string AnalyticsScreenName => "title";

        #region Life Cycle

        protected override void Awake()
        {
            base.Awake();
            continueButton.onClick.AddListener(HandleContinue);
            newRunButton.onClick.AddListener(HandleNewRun);
            settingsButton.onClick.AddListener(HandleSettings);
        }

        #endregion

        #region Public Methods

        /// <summary>Call before the view is pushed (and again whenever the save changes). Null hides Continue.</summary>
        public void SetContinueInfo(RunState? run)
        {
            continueButton.gameObject.SetActive(run != null);
            menuGroup.NavigateTo(run != null ? continueResponder : newRunResponder);
            if (run == null) return;
            continueInfo.SetKey(LocKeys.Title.ContinueInfo, run.actIndex + 1, run.stageInAct + 1, run.playerHp);
        }

        /// <summary>
        /// Record panel (it sizes to its lines): nothing played → "No runs yet"; runs started but none finished
        /// (the best is recorded at run end) → "Best run: none yet" + the runs counter; otherwise the best + counter.
        /// </summary>
        public void SetBest(MetaProgressData meta)
        {
            var anyRun = meta.runsStarted > 0;
            runsLabel.gameObject.SetActive(anyRun);
            if (anyRun)
            {
                runsLabel.SetKey(LocKeys.Title.Runs, meta.runsStarted, meta.runsWon);
            }

            if (meta.bestStageNumber >= SimConstants.StageCount)
            {
                bestLabel.SetKey(LocKeys.Title.BestVictory);
            }
            else if (meta.bestStageNumber >= 0)
            {
                bestLabel.SetKey(LocKeys.Title.Best, meta.bestStageNumber / SimConstants.StagesPerAct + 1,
                    meta.bestStageNumber % SimConstants.StagesPerAct + 1);
            }
            else
            {
                bestLabel.SetKey(anyRun ? LocKeys.Title.NoRecordYet : LocKeys.Title.NoBest);
            }

            bestLabel.Color = meta.bestStageNumber >= 0 ? theme.Accent : theme.TextMuted;
        }

        public override void OnBackButton()
        {
            // Title is the first real view: popping it would expose the EmptyView root.
        }

        public override bool OnControlButton(TopLevelControlPanel.ButtonKind buttonKind)
        {
            if (!IsActive)
            {
                return false;
            }

            if (buttonKind != TopLevelControlPanel.ButtonKind.Exit)
            {
                return false;
            }

            TrackButton("exit");
            ExitRequested?.Invoke();
            return true;
        }

        #endregion

        #region Helpers

        void HandleContinue()
        {
            if (!IsActive) return;
            TrackButton("continue");
            ContinueRequested?.Invoke();
        }

        void HandleNewRun()
        {
            if (!IsActive) return;
            TrackButton("new_run");
            NewRunRequested?.Invoke();
        }

        void HandleSettings()
        {
            if (!IsActive) return;
            TrackButton("settings");
            SettingsRequested?.Invoke();
        }

        #endregion
    }
}
