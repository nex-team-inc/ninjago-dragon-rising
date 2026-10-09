#nullable enable

namespace Nex.BilliardRogue
{
    /// <summary>
    /// TDD D1 fallback: the calibration step to resume after Main.unity reloads for a player-count change. Stored in
    /// PlayerDataManager.appViewState.NextViewState (in memory, survives the scene load) and consumed by
    /// BilliardRogueCoordinator.StartMain.
    /// </summary>
    public sealed class PendingFlowState : AbstractViewState
    {
        public PendingFlowState(int aNumPlayers, bool aIsContinue)
        {
            NumPlayers = aNumPlayers;
            IsContinue = aIsContinue;
        }

        public override View.ViewIdentifier ViewIdentifier => View.ViewIdentifier.Calibration;

        public int NumPlayers { get; }
        public bool IsContinue { get; }

        /// <summary>Removes and returns the pending step, or null when none is stored.</summary>
        public static PendingFlowState? Take(PlayerDataManager.AppViewState state)
        {
            if (!state.HasValidNextViewStateOrClear(View.ViewIdentifier.Calibration))
            {
                return null;
            }

            var pending = (PendingFlowState)state.NextViewState;
            state.NextViewState = null;
            return pending;
        }
    }
}
