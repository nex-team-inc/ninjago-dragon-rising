#nullable enable

using System;
using TMPro;
using UnityEngine;

namespace Nex
{
    public class OnePlayerSetupUI : MonoBehaviour
    {
        [SerializeField] TMP_Text setupStateText = null!;

        int playerIndex;
        SetupStateManager setupStateManager = null!;

        #region Public

        public void Initialize(
            int aPlayerIndex,
            SetupStateManager aSetupStateManager
        )
        {
            playerIndex = aPlayerIndex;
            setupStateManager = aSetupStateManager;
            setupStateManager.PlayerTrackerUpdated += SetupStateManagerOnPlayerTrackerUpdated;
        }

        #endregion

        #region Life Cycle

        void OnDestroy()
        {
            setupStateManager.PlayerTrackerUpdated -= SetupStateManagerOnPlayerTrackerUpdated;
        }

        #endregion


        #region Tracker

        void SetupStateManagerOnPlayerTrackerUpdated((int playerIndex, SetupSummary setupSummary) updatedItem)
        {
            if (playerIndex != updatedItem.playerIndex)
            {
                return;
            }

            var setupSummary = updatedItem.setupSummary;
            string setupText;
            switch (setupSummary.setupStateType)
            {
                case SetupStateType.Preparing:
                {
                    setupText = "Preparing...";
                    break;
                }
                case SetupStateType.WaitingForGoodPlayerPosition:
                {
                    setupText = setupSummary.currentSetupIssue switch
                    {
                        SetupIssueType.None => $"Good Position: {setupSummary.goodPositionProgress:P0}",
                        SetupIssueType.NoPose => $"No Player",
                        SetupIssueType.ChestTooHigh => $"Step back",
                        SetupIssueType.ChestTooLow => $"Low position",
                        SetupIssueType.ChestTooLeft => $"Move to center",
                        SetupIssueType.ChestTooRight => $"Move to center",
                        SetupIssueType.TooFar => $"Move closer",
                        SetupIssueType.TooClose => $"Step back",
                        SetupIssueType.TooFarInPlayArea => $"Move closer",
                        SetupIssueType.TooCloseInPlayArea => $"Step back",
                        SetupIssueType.NotAtCenter => $"Move to center",
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    break;
                }
                case SetupStateType.WaitingForRaisingHand:
                {
                    setupText = $"Raise Hand: {setupSummary.raiseHandProgress:P0}";
                    break;
                }
                case SetupStateType.Playing:
                {
                    var text = "Ready\n";
                    text += setupSummary.currentSetupIssue switch
                    {
                        SetupIssueType.None => $"",
                        SetupIssueType.NoPose => $"(No Player)",
                        SetupIssueType.ChestTooHigh => $"(Step back)",
                        SetupIssueType.ChestTooLow => $"(Low position)",
                        SetupIssueType.ChestTooLeft => $"(Move to center)",
                        SetupIssueType.ChestTooRight => $"(Move to center)",
                        SetupIssueType.TooFar => $"(Move closer)",
                        SetupIssueType.TooClose => $"(Step back)",
                        SetupIssueType.TooFarInPlayArea => $"(Move closer)",
                        SetupIssueType.TooCloseInPlayArea => $"(Step back)",
                        SetupIssueType.NotAtCenter => $"(Move to center)",
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    setupText = text;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            setupStateText.text = setupText;
        }

        #endregion
    }
}
