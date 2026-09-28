#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The run state machine (TDD §7): StageIntro → PlayerTurn → EnemyPhase → (StageClear → Reward) | Defeat |
    /// Victory, plus TrackingLost inside a player turn. It is polled: GameSession.Tick advances it every frame and
    /// asynchronous steps (flow host overlays, BoardPresenter playback) are stored and checked for completion, so
    /// the machine never depends on a player loop and edit-mode smoke runs can tick it synchronously. Saves at stage
    /// start and turn start here, reward roll and pick in RewardFlow; analytics at every boundary.
    /// </summary>
    public sealed class TurnController
    {
        readonly SessionServices services;
        readonly PlayerTurnLoop loop;
        readonly EnemyPhaseRunner enemyPhase;
        readonly RewardFlow reward;
        UniTask pendingStep;
        UniTask<int> pendingChoice;
        CancellationToken ct;
        bool stageJustSaved;
        bool lowHp;
        int pendingStage = -1;

        public TurnController(SessionServices aServices)
        {
            services = aServices;
            loop = new PlayerTurnLoop(aServices);
            enemyPhase = new EnemyPhaseRunner(aServices);
            reward = new RewardFlow(aServices);
        }

        public TurnPhase Phase { get; private set; }

        /// <summary>None until Phase == Finished.</summary>
        public RunOutcome Result { get; private set; }

        public bool NewRecord { get; private set; }

        public PlayerTurnLoop Loop => loop;

        /// <summary>A save now would equal the turn-start snapshot: player turn, nothing fired, no balls in flight.</summary>
        public bool IsStable => Phase == TurnPhase.PlayerTurn && services.Sequencer.NextIndex == 0 && services.Sim.ActiveBalls == 0;

        #region Public Methods

        public void Start(bool isContinue, CancellationToken aCt)
        {
            ct = aCt;
            var run = services.Run;
            services.Analytics.SessionStart(run.numPlayers, isContinue, run.runId, run.seed);
            if (isContinue)
            {
                services.Board.Rebuild(run);
                if (run.awaitingReward) EnterReward();
                else EnterStageIntro();
                return;
            }

            ApplyDebugStart();
            BeginNewStage(true);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (services.Pause.MenuPaused) return;
            switch (Phase)
            {
                case TurnPhase.StageIntro:
                    if (StepDone()) EnterPlayerTurn();
                    break;
                case TurnPhase.PlayerTurn:
                    TickPlayerTurn(unscaledDeltaTime);
                    break;
                case TurnPhase.TrackingLost:
                    if (StepDone()) ResumeFromTrackingLost();
                    break;
                case TurnPhase.EnemyPhase:
                    if (StepDone()) EndEnemyPhase();
                    break;
                case TurnPhase.StageClear:
                    if (StepDone()) EndStageClear();
                    break;
                case TurnPhase.Reward:
                    if (ChoiceDone(out var index)) EndReward(index);
                    break;
                case TurnPhase.Defeat:
                    if (StepDone()) Finish(RunOutcome.Defeat);
                    break;
                case TurnPhase.Victory:
                    if (StepDone()) Finish(RunOutcome.Victory);
                    break;
            }
        }

        /// <summary>Save &amp; Quit or cancellation: the last turn-boundary save stays, meta progress is untouched.</summary>
        public void Abandon()
        {
            if (Phase == TurnPhase.Finished) return;
            var run = services.Run;
            services.Sim.ClearBalls();
            services.Pause.SetHold(false);
            services.TimeScale.ResetEffects();
            services.Analytics.RunEnd(RunOutcome.Abandoned, run.stageNumber, run.stats.turns, run.stats.playSeconds, run.stats.kills);
            services.Analytics.SessionStop(RunOutcome.Abandoned);
            Result = RunOutcome.Abandoned;
            Phase = TurnPhase.Finished;
        }

        /// <summary>Debug GotoStage: applied at the next player-turn frame with no balls in flight.</summary>
        public void RequestStage(int stageNumber) => pendingStage = stageNumber;

        #endregion

        #region Player Turn

        void TickPlayerTurn(float unscaledDeltaTime)
        {
            if (pendingStage >= 0 && services.Sim.ActiveBalls == 0)
            {
                JumpToStage();
                return;
            }

            loop.Tick(unscaledDeltaTime);
            if (loop.TrackingLostPlayer >= 0)
            {
                EnterTrackingLost(loop.TrackingLostPlayer);
                return;
            }

            if (loop.IsTurnDone) EnterEnemyPhase();
        }

        void EnterPlayerTurn()
        {
            var run = services.Run;
            if (!stageJustSaved) Save();
            stageJustSaved = false;
            var turn = run.turnInStage + 1;
            loop.BeginTurn();
            services.Board.SetActiveShooter(run.activePlayerIndex, run.numPlayers);
            services.Hud.RefreshAll();
            services.Hud.ShowTurnBanner(turn);
            services.Audio.Sfx(SfxManager.SoundEffect.TurnStart);
            WarnLowHp(true);
            services.Analytics.TurnStart(run.stageNumber, turn, services.Sequencer.Total, run.playerHp);
            Phase = TurnPhase.PlayerTurn;
        }

        void EnterEnemyPhase()
        {
            loop.EndTurn();
            if (services.Sim.IsStageCleared)
            {
                EnterStageClear();
                return;
            }

            pendingStep = enemyPhase.Begin(ct);
            Phase = TurnPhase.EnemyPhase;
        }

        void EndEnemyPhase()
        {
            if (enemyPhase.End())
            {
                EnterDefeat();
                return;
            }

            WarnLowHp(false);
            if (services.Sim.IsStageCleared) EnterStageClear();
            else EnterPlayerTurn();
        }

        #endregion

        #region Stages

        void BeginNewStage(bool firstStage)
        {
            var run = services.Run;
            var events = services.Sim.Events;
            events.Clear();
            services.Sim.BeginStage();
            // Rebuild recreates every view from the state, so the spawn events are not replayed.
            events.Clear();
            services.Board.Rebuild(run);
            if (firstStage) services.Persistence.BeginRun(run);
            else Save();
            stageJustSaved = true;
            services.Analytics.StageStart(run.actIndex, run.stageInAct, run.stage.isBoss);
            if (run.stage.isBoss) services.Analytics.BossSpawn(services.Sim.Act.bossType);
            EnterStageIntro();
        }

        void EnterStageIntro()
        {
            var run = services.Run;
            services.Hud.SetStage();
            services.Hud.RefreshAll();
            services.Audio.PlayMusic(run.stage.isBoss ? BgmManager.BgmType.Boss : services.Config.Acts[run.actIndex].BattleBgm);
            pendingStep = IntroAsync();
            Phase = TurnPhase.StageIntro;
        }

        async UniTask IntroAsync()
        {
            var run = services.Run;
            await services.FlowHost.ShowStageIntroAsync(run.actIndex, run.stageInAct, run.stage.isBoss, ct);
            if (!run.stage.isBoss) return;
            await services.Board.PlayBossIntroAsync(services.Sim.Act.bossType, ct);
        }

        void EnterStageClear()
        {
            var run = services.Run;
            var events = services.Sim.Events;
            events.Clear();
            services.Sim.CompleteStage();
            services.Board.Consume(events, run);
            events.Clear();
            services.Hud.RefreshHp();
            services.Hud.RefreshBoss();
            services.Analytics.StageClear(run.actIndex, run.stageInAct, run.turnInStage, run.playerHp);
            pendingStep = services.Board.PlayStageClearAsync(ct);
            Phase = TurnPhase.StageClear;
        }

        void EndStageClear()
        {
            if (services.Run.awaitingReward) EnterReward();
            else Advance();
        }

        void Advance()
        {
            if (services.Sim.AdvanceToNextStage()) BeginNewStage(false);
            else EnterVictory();
        }

        void JumpToStage()
        {
            var stage = pendingStage;
            pendingStage = -1;
            services.Sim.ClearBalls();
            loop.EndTurn();
            services.Sim.SetStage(stage);
            BeginNewStage(false);
        }

        #endregion

        #region Reward

        void EnterReward()
        {
            pendingChoice = reward.Offer(ct);
            Phase = TurnPhase.Reward;
        }

        void EndReward(int index)
        {
            reward.Apply(index);
            Advance();
        }

        #endregion

        #region Tracking Lost

        void EnterTrackingLost(int player)
        {
            services.Pause.SetHold(true);
            pendingStep = services.FlowHost.ShowTrackingLostAsync(player, ct);
            Phase = TurnPhase.TrackingLost;
        }

        void ResumeFromTrackingLost()
        {
            var player = loop.TrackingLostPlayer;
            services.Analytics.TrackingLost(player, loop.TrackingLostSeconds);
            loop.ClearTrackingLost();
            services.Inputs[player].ResetStrike();
            services.Hud.SetTrackingWarning(player, false);
            services.Pause.SetHold(false);
            Phase = TurnPhase.PlayerTurn;
        }

        #endregion

        #region Run End

        // The presenter's sequences play the Defeat / Victory / StageClear / BossAppear stingers in sync with their visuals.
        void EnterDefeat()
        {
            pendingStep = services.Board.PlayDefeatAsync(ct);
            Phase = TurnPhase.Defeat;
        }

        void EnterVictory()
        {
            pendingStep = services.Board.PlayVictoryAsync(ct);
            Phase = TurnPhase.Victory;
        }

        void Finish(RunOutcome outcome)
        {
            var run = services.Run;
            run.outcome = outcome;
            services.TimeScale.ResetEffects();
            services.Analytics.RunEnd(outcome, run.stageNumber, run.stats.turns, run.stats.playSeconds, run.stats.kills);
            services.Analytics.SessionStop(outcome);
            NewRecord = services.Persistence.CompleteRun(run);
            Result = outcome;
            Phase = TurnPhase.Finished;
            services.FlowHost.NotifyRunEnded(run);
        }

        #endregion

        #region Helpers

        void Save() => services.Persistence.SaveTurnBoundary(services.Run);

        // Low HP: repeat the warning at every turn start while low, otherwise only when damage crosses the line.
        void WarnLowHp(bool repeat)
        {
            var run = services.Run;
            var low = run.playerHp <= run.playerMaxHp * services.Pacing.LowHpFraction;
            if (low && (repeat || !lowHp)) services.Audio.Sfx(SfxManager.SoundEffect.LowHpWarning);
            lowHp = low;
        }

        bool StepDone()
        {
            if (!pendingStep.Status.IsCompleted()) return false;
            var done = pendingStep;
            pendingStep = UniTask.CompletedTask;
            done.GetAwaiter().GetResult();
            return true;
        }

        bool ChoiceDone(out int index)
        {
            index = 0;
            if (!pendingChoice.Status.IsCompleted()) return false;
            var done = pendingChoice;
            pendingChoice = UniTask.FromResult(0);
            index = done.GetAwaiter().GetResult();
            return true;
        }

        // Nothing has consumed the RNG before the first BeginStage, so reseeding here keeps the run deterministic.
        void ApplyDebugStart()
        {
            var run = services.Run;
            var fixedSeed = services.Debug.fixedSeed;
            if (fixedSeed != 0)
            {
                run.seed = fixedSeed;
                run.rngState = SimRandom.SeedToState(fixedSeed);
            }

            var forcedStage = services.Debug.forceStartStage;
            if (forcedStage > 0 && forcedStage < SimConstants.StageCount) services.Sim.SetStage(forcedStage);
        }

        #endregion
    }
}
