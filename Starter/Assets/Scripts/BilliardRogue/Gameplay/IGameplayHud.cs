#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>HUD surface the GameplayView implements; GameSession only pushes values, never reads UI.</summary>
    public interface IGameplayHud
    {
        void SetHp(int cur, int max);
        /// <summary>The bag every shot launches as a volley; nextIndex = balls of the current volley already launched; extraBalls = bonus shots this turn.</summary>
        void SetBallQueue(IReadOnlyList<BallInstance> balls, int nextIndex, int extraBalls);
        void SetBallsRemaining(int remaining, int total);
        void SetStage(int actIndex, int stageInAct, bool isBoss);
        void SetActivePlayer(int playerIndex, int numPlayers);
        void SetBossHp(bool visible, int hp, int maxHp, EnemyType type);
        void SetFastForward(bool on);
        /// <summary>Power pickup armed: the next fired ball deals double damage (RunState.powerPickupArmed, LocKeys.Hud.PowerArmed).</summary>
        void SetPowerArmed(bool armed);
        void ShowTurnBanner(int turn);
        void ShowShooterBanner(int playerIndex);
        /// <summary>"Enemies incoming!" while a spawn batch pops in (GDD v2 §5); the next turn banner replaces it.</summary>
        void ShowIncomingBanner();
        void SetTrackingWarning(int playerIndex, bool lost);
        /// <summary>Hype meter (GDD v2 §3): hype01 in 0..1, tier 0..3.</summary>
        void SetHype(float hype01, int tier);
    }
}
