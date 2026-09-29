#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The HUD surface GameSession talks to: forwards every call to the GameplayHud nested in the GameplayView prefab
    /// (UI-Views module) and mirrors the active shooter into the PiP indicators.
    /// </summary>
    public sealed class GameplayHudRelay : IGameplayHud
    {
        readonly IGameplayHud inner;
        readonly GameplayPip pip;

        public GameplayHudRelay(IGameplayHud aInner, GameplayPip aPip)
        {
            inner = aInner;
            pip = aPip;
        }

        public void SetHp(int cur, int max) => inner.SetHp(cur, max);

        public void SetBallQueue(IReadOnlyList<BallInstance> bag, int nextIndex, int extraBalls) => inner.SetBallQueue(bag, nextIndex, extraBalls);

        public void SetBallsRemaining(int remaining, int total) => inner.SetBallsRemaining(remaining, total);

        public void SetStage(int actIndex, int stageInAct, bool isBoss) => inner.SetStage(actIndex, stageInAct, isBoss);

        public void SetActivePlayer(int playerIndex, int numPlayers)
        {
            pip.SetActivePlayer(numPlayers > 1 ? playerIndex : -1);
            inner.SetActivePlayer(playerIndex, numPlayers);
        }

        public void SetBossHp(bool visible, int hp, int maxHp, EnemyType type) => inner.SetBossHp(visible, hp, maxHp, type);

        public void SetFastForward(bool on) => inner.SetFastForward(on);

        public void SetPowerArmed(bool armed) => inner.SetPowerArmed(armed);

        public void ShowTurnBanner(int turn) => inner.ShowTurnBanner(turn);

        public void ShowShooterBanner(int playerIndex) => inner.ShowShooterBanner(playerIndex);

        public void SetTrackingWarning(int playerIndex, bool lost) => inner.SetTrackingWarning(playerIndex, lost);
    }
}
