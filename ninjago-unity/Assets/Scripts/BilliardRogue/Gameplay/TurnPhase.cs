#nullable enable

namespace Nex.BilliardRogue
{
    /// <summary>States of the run loop (TDD §7); GameSession.Phase exposes the current one.</summary>
    public enum TurnPhase
    {
        NotStarted = 0,
        StageIntro = 1,
        PlayerTurn = 2,
        TrackingLost = 3,
        EnemyPhase = 4,
        StageClear = 5,
        Reward = 6,
        Defeat = 7,
        Victory = 8,
        Finished = 9,
    }
}
