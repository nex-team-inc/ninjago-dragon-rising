#nullable enable

using System;
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
    // updates on every detection tick.
    public class SetupWarningMessage : MonoBehaviour
    {
        [SerializeField] TMP_Text warningText = null!;

        static readonly int issueCount = Enum.GetValues(typeof(SetupIssueType)).Length;

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
                var key = KeyFor((SetupIssueType)i);
                var entry = key.Length == 0 || table == null ? null : table.GetEntry(key);
                warningByIssue[i] = entry?.GetLocalizedString() ?? "";
            }

            ApplyCurrent();
        }

        void ApplyCurrent()
        {
            warningText.text = warningByIssue[(int)currentIssue];
        }

        #endregion

        #region Helpers

        static string KeyFor(SetupIssueType issue)
        {
            return issue switch
            {
                SetupIssueType.None => "",
                SetupIssueType.NoPose => LocKeys.Setup.NoPlayer,
                SetupIssueType.ChestTooHigh => LocKeys.Setup.StepBack,
                SetupIssueType.ChestTooLow => LocKeys.Setup.StepBack,
                SetupIssueType.ChestTooLeft => LocKeys.Setup.MoveToCenter,
                SetupIssueType.ChestTooRight => LocKeys.Setup.MoveToCenter,
                SetupIssueType.TooFar => LocKeys.Setup.MoveCloser,
                SetupIssueType.TooClose => LocKeys.Setup.StepBack,
                SetupIssueType.TooFarInPlayArea => LocKeys.Setup.MoveCloser,
                SetupIssueType.TooCloseInPlayArea => LocKeys.Setup.StepBack,
                SetupIssueType.NotAtCenter => LocKeys.Setup.MoveToCenter,
                _ => throw new ArgumentOutOfRangeException(nameof(issue), issue, null)
            };
        }

        #endregion
    }
}
