#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.KeyboardNavigation;
using Nex.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>One result screen for both mini-games: a title, the per-player lines, Retry (through setup) or Menu.</summary>
    public class NinjagoResultView : SimpleCanvasView
    {
        [Header("Title")]
        [SerializeField] NexLocalizedString title = null!;
        [Header("Lines Container")]
        [SerializeField] RectTransform linesRoot = null!;
        [Header("Line Prefab")]
        [SerializeField] ResultLine linePrefab = null!;
        [Header("Retry Button")]
        [SerializeField] Button retryButton = null!;
        [Header("Menu Button")]
        [SerializeField] Button menuButton = null!;
        [Header("Keyboard Navigation")]
        [SerializeField] GroupKeyResponder buttonsGroup = null!;

        readonly UniTaskCompletionSource<bool> choiceSource = new();

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.NinjagoResult;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "ninjago-result";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            Choose(false, "back");
        }

        #endregion

        #region Initialization

        public void Initialize(NinjagoOutcome outcome)
        {
            title.StringReference = outcome.Title;
            foreach (var line in outcome.Lines)
            {
                Instantiate(linePrefab, linesRoot).Initialize(line);
            }

            retryButton.onClick.AddListener(() => Choose(true, "retry"));
            menuButton.onClick.AddListener(() => Choose(false, "menu"));
            buttonsGroup.SetInitialActiveIndex(0);
        }

        #endregion

        #region Public API

        /// <summary>True for Retry, false for Menu.</summary>
        public UniTask<bool> WaitForChoiceAsync(CancellationToken cancellationToken)
        {
            return choiceSource.Task.AttachExternalCancellation(cancellationToken);
        }

        #endregion

        #region Unity Events

        void Choose(bool retry, string button)
        {
            if (!IsActive) return;
            if (!choiceSource.TrySetResult(retry)) return;
            NinjagoAnalytics.UiAction(AnalyticsScreenName, button);
            SfxManager.Instance.PlaySoundEffect(SfxManager.SoundEffect.UiSelect);
        }

        #endregion
    }
}
