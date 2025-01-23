#nullable enable

using System;
using TMPro;
using UnityEngine;

namespace Nex
{
    public class SetupWarningMessage : MonoBehaviour
    {
        [SerializeField] TMP_Text warningText = null!;

        int playerIndex;

        public void Initialize(
            int aPlayerIndex,
            SetupStateManager setupStateManager
            )
        {
            playerIndex = aPlayerIndex;

            setupStateManager.PlayerTrackerUpdated += SetupStateManagerOnPlayerTrackerUpdated;
        }

        void SetupStateManagerOnPlayerTrackerUpdated((int playerIndex, SetupSummary setupSummary) updatedItem)
        {
            if (playerIndex != updatedItem.playerIndex)
            {
                return;
            }

            var setupSummary = updatedItem.setupSummary;
            warningText.text = SetupWarning(setupSummary.currentSetupIssue);
        }

        string SetupWarning(SetupIssueType issue)
        {
            return issue switch
            {
                SetupIssueType.None => "",
                SetupIssueType.NoPose => "No player",
                SetupIssueType.ChestTooHigh => "Step back",
                SetupIssueType.ChestTooLow => "Step back",
                SetupIssueType.ChestTooLeft => "Move to center",
                SetupIssueType.ChestTooRight => $"Move to center",
                SetupIssueType.TooFar => $"Move closer",
                SetupIssueType.TooClose => $"Step back",
                SetupIssueType.TooFarInPlayArea => $"Move closer",
                SetupIssueType.TooCloseInPlayArea => $"Step back",
                SetupIssueType.NotAtCenter => $"Move to center",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
