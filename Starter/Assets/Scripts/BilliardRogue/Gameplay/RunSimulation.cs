#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// One run's simulation services bound to its RunState. The only place that turns run.rngState into a SimRandom,
    /// so every phase resumes the same stream after a save. Player-turn events go to Events (drained every frame);
    /// enemy-phase events go to PhaseEvents, which BoardPresenter reads asynchronously during playback.
    /// </summary>
    public sealed class RunSimulation
    {
        const int EventCapacity = 512;

        readonly GameRules rules;
        readonly RunState run;
        readonly BoardOps ops;
        readonly BallSimulator balls;
        readonly EnemyPhaseResolver resolver;
        readonly RunFactory factory = new();
        readonly RewardGenerator rewards = new();
        readonly List<SimEvent> events = new(EventCapacity);
        readonly List<SimEvent> phaseEvents = new(EventCapacity);
        readonly List<EnemyState> enemySnapshot = new();

        public RunSimulation(GameRules aRules, RunState aRun)
        {
            rules = aRules;
            run = aRun;
            ops = new BoardOps(rules);
            balls = new BallSimulator(rules, ops);
            resolver = new EnemyPhaseResolver(rules, ops);
        }

        public GameRules Rules => rules;
        public RunState Run => run;
        public BallSimulator Balls => balls;
        public List<SimEvent> Events => events;
        public List<SimEvent> PhaseEvents => phaseEvents;
        public int ActiveBalls => balls.ActiveCount;
        public ActRules Act => rules.acts[run.actIndex];
        public bool IsStageCleared => factory.IsStageCleared(run);

        #region Stages

        /// <summary>Generates and spawns the current stage; the spawn events land in Events (BoardPresenter.Rebuild covers them).</summary>
        public void BeginStage() => factory.BeginStage(rules, run, Rng(), events);

        public void CompleteStage() => factory.CompleteStage(rules, run, events);

        public bool AdvanceToNextStage() => factory.AdvanceToNextStage(rules, run);

        /// <summary>Points the run at stage number 0..StageCount-1 without generating it (debug GotoStage, forceStartStage); call BeginStage next.</summary>
        public void SetStage(int stageNumber)
        {
            var remaining = stageNumber;
            for (var act = 0; act < rules.acts.Length; act++)
            {
                var stagesInAct = rules.acts[act].normalStages + 1;
                if (remaining < stagesInAct || act == rules.acts.Length - 1)
                {
                    run.actIndex = act;
                    run.stageInAct = Mathf.Min(remaining, stagesInAct - 1);
                    break;
                }

                remaining -= stagesInAct;
            }

            run.stageNumber = stageNumber;
            run.turnInStage = 0;
            run.outcome = RunOutcome.None;
            run.awaitingReward = false;
            run.pendingRewards.Clear();
        }

        #endregion

        #region Turns

        public void ResolveEnemyPhase()
        {
            phaseEvents.Clear();
            resolver.Resolve(run, Rng(), phaseEvents);
        }

        public void RollRewards(int highestUnlockTier, List<RewardOption> output) => rewards.Roll(rules, run, highestUnlockTier, Rng(), output);

        public void ApplyReward(RewardOption option) => rewards.Apply(rules, run, option);

        #endregion

        #region Balls

        public void Launch(BallInstance ball, Vector2 origin, Vector2 direction, bool powerShot, int shooterIndex)
        {
            balls.Launch(run, ball, origin, direction, powerShot, shooterIndex, events);
        }

        public void Step(float dt) => balls.Step(run, dt, events);

        public int PredictPath(Vector2 origin, Vector2 direction, float maxLength, int maxBounces, Vector2[] pointsOut)
        {
            return balls.PredictPath(run, origin, direction, maxLength, maxBounces, pointsOut);
        }

        public void ClearBalls() => balls.Clear();

        #endregion

        #region Board

        public bool IsBoss(EnemyType type) => rules.enemies[(int)type].isBoss;

        public EnemyState? FindBoss()
        {
            var enemies = run.board.enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                if (IsBoss(enemies[i].type)) return enemies[i];
            }

            return null;
        }

        /// <summary>Enemies other than Bone Walls.</summary>
        public int LivingEnemyCount()
        {
            var count = 0;
            var enemies = run.board.enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].type != EnemyType.BoneWall) count++;
            }

            return count;
        }

        /// <summary>Deals every enemy's remaining HP through BoardOps (debug KillAll); the events land in Events like player-turn hits.</summary>
        public void KillAllEnemies()
        {
            enemySnapshot.Clear();
            enemySnapshot.AddRange(run.board.enemies);
            var source = new DamageSource { ballType = BallType.Basic };
            for (var i = 0; i < enemySnapshot.Count; i++)
            {
                var enemy = enemySnapshot[i];
                if (enemy.hp <= 0) continue;
                ops.DamageEnemy(run, enemy, enemy.hp, source, events);
            }
        }

        /// <summary>Marks every planned wave as spawned so a normal stage counts as cleared once the board is empty (debug ClearStage).</summary>
        public void ExhaustWaves() => run.nextWaveIndex = run.stage.waves.Count;

        #endregion

        SimRandom Rng() => new(run.rngState);
    }
}
