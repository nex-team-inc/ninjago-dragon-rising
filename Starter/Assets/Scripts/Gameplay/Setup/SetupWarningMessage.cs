#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Nex
{
    // Shows the localized setup hint for one player. Strings are cached per locale because the tracker
    // updates on every detection tick. A key missing from the table (seeder not run, untranslated locale)
    // falls back to the English hint so the setup screen never goes blank.
    public class SetupWarningMessage : MonoBehaviour
    {
        [SerializeField] TMP_Text warningText = null!;

        static readonly int issueCount = Enum.GetValues(typeof(SetupIssueType)).Length;
        static readonly HashSet<string> reportedMissingKeys = new();

        readonly string[] warningByIssue = new string[issueCount];
        int playerIndex;
        SetupIssueType currentIssue;

        #region Life Cycle

        public void Initialize(
            int aPlayerIndex,
            SetupStateManager setupStateManager
            )
        {
            playerIndex = aPlayerIndex;
            for (var i = 0; i < warningByIssue.Length; i++)
            {
                warningByIssue[i] = "";
            }

            setupStateManager.PlayerTrackerUpdated += SetupStateManagerOnPlayerTrackerUpdated;
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            RefreshStringsAsync(destroyCancellationToken).Forget();
        }

        void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        #endregion

        #region Updates

        void SetupStateManagerOnPlayerTrackerUpdated((int playerIndex, SetupSummary setupSummary) updatedItem)
        {
            if (playerIndex != updatedItem.playerIndex)
            {
                return;
            }

            currentIssue = updatedItem.setupSummary.currentSetupIssue;
            ApplyCurrent();
        }

        void HandleLocaleChanged(Locale _)
        {
            RefreshStringsAsync(destroyCancellationToken).Forget();
        }

        async UniTask RefreshStringsAsync(CancellationToken cancellationToken)
        {
            await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: cancellationToken);
            var table = await LocalizationSettings.StringDatabase.GetTableAsync(LocKeys.Table).ToUniTask(cancellationToken: cancellationToken);
            for (var i = 0; i < warningByIssue.Length; i++)
            {
                var (key, english) = HintFor((SetupIssueType)i);
                var entry = key.Length == 0 || table == null ? null : table.GetEntry(key);
                if (entry == null && key.Length > 0 && reportedMissingKeys.Add(key))
                {
                    Debug.LogWarning($"[SetupWarningMessage] Missing localization entry '{key}', using English.");
                }

                warningByIssue[i] = entry?.GetLocalizedString() ?? english;
            }

            ApplyCurrent();
        }

        void ApplyCurrent()
        {
            warningText.text = warningByIssue[(int)currentIssue];
        }

        #endregion

        #region Helpers

        // Localization key plus the English copy from LocKeys.Setup (the fallback when the table lacks the key).
        static (string key, string english) HintFor(SetupIssueType issue)
        {
            return issue switch
            {
                SetupIssueType.None => ("", ""),
                SetupIssueType.NoPose => (LocKeys.Setup.NoPlayer, "No player"),
                SetupIssueType.ChestTooHigh => (LocKeys.Setup.StepBack, "Step back"),
                SetupIssueType.ChestTooLow => (LocKeys.Setup.StepBack, "Step back"),
                SetupIssueType.ChestTooLeft => (LocKeys.Setup.MoveToCenter, "Move to center"),
                SetupIssueType.ChestTooRight => (LocKeys.Setup.MoveToCenter, "Move to center"),
                SetupIssueType.TooFar => (LocKeys.Setup.MoveCloser, "Move closer"),
                SetupIssueType.TooClose => (LocKeys.Setup.StepBack, "Step back"),
                SetupIssueType.TooFarInPlayArea => (LocKeys.Setup.MoveCloser, "Move closer"),
                SetupIssueType.TooCloseInPlayArea => (LocKeys.Setup.StepBack, "Step back"),
                SetupIssueType.NotAtCenter => (LocKeys.Setup.MoveToCenter, "Move to center"),
                _ => throw new ArgumentOutOfRangeException(nameof(issue), issue, null)
            };
        }

        #endregion
    }
}
