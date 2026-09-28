#nullable enable

#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Text;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The gameplay half of DebugHooks (TDD D7): Shoot, GotoStage, KillAll, ClearStage, AddEveryBall and State,
    /// registered while a GameSession runs and removed on teardown without touching the flow's handlers.
    /// ChooseReward stays with the reward view.
    /// </summary>
    public sealed class GameplayDebugCommands
    {
        readonly SessionServices services;
        readonly TurnController turns;
        readonly StringBuilder summary = new();
        readonly Func<float, bool> shoot;
        readonly Func<int, bool> gotoStage;
        readonly Func<bool> killAll;
        readonly Func<bool> clearStage;
        readonly Func<bool> addEveryBall;
        readonly Func<string> state;

        public GameplayDebugCommands(SessionServices aServices, TurnController aTurns)
        {
            services = aServices;
            turns = aTurns;
            shoot = Shoot;
            gotoStage = GotoStage;
            killAll = KillAll;
            clearStage = ClearStage;
            addEveryBall = AddEveryBall;
            state = State;
        }

        #region Registration

        public void Register()
        {
            DebugHooks.ShootHandler = shoot;
            DebugHooks.GotoStageHandler = gotoStage;
            DebugHooks.KillAllHandler = killAll;
            DebugHooks.ClearStageHandler = clearStage;
            DebugHooks.AddEveryBallHandler = addEveryBall;
            DebugHooks.StateHandler = state;
        }

        public void Unregister()
        {
            if (ReferenceEquals(DebugHooks.ShootHandler, shoot)) DebugHooks.ShootHandler = null;
            if (ReferenceEquals(DebugHooks.GotoStageHandler, gotoStage)) DebugHooks.GotoStageHandler = null;
            if (ReferenceEquals(DebugHooks.KillAllHandler, killAll)) DebugHooks.KillAllHandler = null;
            if (ReferenceEquals(DebugHooks.ClearStageHandler, clearStage)) DebugHooks.ClearStageHandler = null;
            if (ReferenceEquals(DebugHooks.AddEveryBallHandler, addEveryBall)) DebugHooks.AddEveryBallHandler = null;
            if (ReferenceEquals(DebugHooks.StateHandler, state)) DebugHooks.StateHandler = null;
        }

        #endregion

        #region Commands

        bool Shoot(float angleDeg) => turns.Phase == TurnPhase.PlayerTurn && turns.Loop.ForceShoot(angleDeg);

        bool GotoStage(int stageNumber)
        {
            if (stageNumber < 0 || stageNumber >= SimConstants.StageCount || turns.Phase == TurnPhase.Finished) return false;
            turns.RequestStage(stageNumber);
            return true;
        }

        bool KillAll()
        {
            if (turns.Phase != TurnPhase.PlayerTurn) return false;
            services.Sim.KillAllEnemies();
            return true;
        }

        bool ClearStage()
        {
            if (turns.Phase != TurnPhase.PlayerTurn) return false;
            services.Sim.ExhaustWaves();
            services.Sim.KillAllEnemies();
            return true;
        }

        bool AddEveryBall()
        {
            var bag = services.Run.bag;
            var cap = services.Rules.balance.bagCap;
            for (var t = 0; t < SimConstants.BallTypeCount && bag.Count < cap; t++)
            {
                var type = (BallType)t;
                if (Contains(bag, type)) continue;
                bag.Add(new BallInstance { type = type, level = 1 });
            }

            services.Hud.RefreshQueue();
            return true;
        }

        string State()
        {
            var run = services.Run;
            summary.Clear();
            summary.Append("phase=").Append(turns.Phase)
                .Append(" act=").Append(run.actIndex + 1).Append(" stage=").Append(run.stageInAct + 1)
                .Append(" stageNumber=").Append(run.stageNumber).Append(run.stage.isBoss ? " boss" : "")
                .Append(" turn=").Append(run.turnInStage + 1)
                .Append(" hp=").Append(run.playerHp).Append('/').Append(run.playerMaxHp)
                .Append(" shooter=P").Append(run.activePlayerIndex + 1)
                .Append(" balls=").Append(services.Sequencer.Remaining).Append('/').Append(services.Sequencer.Total)
                .Append(" inFlight=").Append(services.Sim.ActiveBalls)
                .Append(" enemies=").Append(run.board.enemies.Count)
                .Append(" wavesLeft=").Append(run.stage.waves.Count - run.nextWaveIndex)
                .Append(" outcome=").Append(run.outcome)
                .Append(" timeScale=").Append(services.TimeScale.GameplayTimeScale)
                .Append(" bag=");
            for (var i = 0; i < run.bag.Count; i++)
            {
                if (i > 0) summary.Append(',');
                summary.Append(run.bag[i].type).Append('L').Append(run.bag[i].level);
            }

            return summary.ToString();
        }

        static bool Contains(System.Collections.Generic.List<BallInstance> bag, BallType type)
        {
            for (var i = 0; i < bag.Count; i++)
            {
                if (bag[i].type == type) return true;
            }

            return false;
        }

        #endregion
    }
}
#endif
