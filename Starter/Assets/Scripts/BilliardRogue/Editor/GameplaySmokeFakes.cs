#nullable enable

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>In-memory IRunStore: JSON round trip on every save, like PlayerDataManager's file, without touching the real save.</summary>
    public sealed class MemoryRunStore : IRunStore
    {
        string? json;

        public int Saves { get; private set; }
        public int MetaSaves { get; private set; }
        public MetaProgressData MetaProgress { get; } = new();

        public RunState? LoadRun() => json == null ? null : JsonUtility.FromJson<RunState>(json);

        public void SaveRun(RunState run)
        {
            json = JsonUtility.ToJson(run);
            Saves++;
        }

        public void ClearRun() => json = null;

        public void SaveMetaProgress() => MetaSaves++;
    }

    /// <summary>Records HUD pushes so the smoke report can show the session drove the HUD.</summary>
    public sealed class SmokeHud : IGameplayHud
    {
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public int BallsRemaining { get; private set; }
        public int BallsTotal { get; private set; }
        public int QueueUpdates { get; private set; }
        public int TurnBanners { get; private set; }
        public int ShooterBanners { get; private set; }
        public bool BossVisible { get; private set; }
        public bool FastForward { get; private set; }
        public int FastForwardOns { get; private set; }

        public void SetHp(int cur, int max)
        {
            Hp = cur;
            MaxHp = max;
        }

        public void SetBallQueue(IReadOnlyList<BallInstance> bag, int nextIndex, int extraBalls) => QueueUpdates++;

        public void SetBallsRemaining(int remaining, int total)
        {
            BallsRemaining = remaining;
            BallsTotal = total;
        }

        public void SetStage(int actIndex, int stageInAct, bool isBoss)
        {
        }

        public void SetActivePlayer(int playerIndex, int numPlayers)
        {
        }

        public void SetBossHp(bool visible, int hp, int maxHp, EnemyType type) => BossVisible = visible;

        public void SetFastForward(bool on)
        {
            FastForward = on;
            if (on) FastForwardOns++;
        }

        public void SetPowerArmed(bool armed)
        {
        }

        public void ShowTurnBanner(int turn) => TurnBanners++;

        public void ShowShooterBanner(int playerIndex) => ShooterBanners++;

        public void SetTrackingWarning(int playerIndex, bool lost)
        {
        }
    }

    /// <summary>Overlays complete at once; rewards pick the first card (a new ball whenever offered).</summary>
    public sealed class SmokeFlowHost : IGameFlowHost
    {
        public int StageIntros { get; private set; }
        public int BossIntros { get; private set; }
        public int RewardsChosen { get; private set; }
        public int TrackingLostShown { get; private set; }
        public RunOutcome? Ended { get; private set; }
        public string LastReward { get; private set; } = "";

        public UniTask ShowStageIntroAsync(int actIndex, int stageInAct, bool isBoss, CancellationToken ct)
        {
            StageIntros++;
            if (isBoss) BossIntros++;
            return UniTask.CompletedTask;
        }

        public UniTask<int> ChooseRewardAsync(IReadOnlyList<RewardOption> options, RunState run, CancellationToken ct)
        {
            RewardsChosen++;
            var index = 0;
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].kind != RewardKind.NewBall) continue;
                index = i;
                break;
            }

            LastReward = options[index].kind + ":" + options[index].ballType;
            return UniTask.FromResult(index);
        }

        public UniTask ShowTrackingLostAsync(int playerIndex, CancellationToken ct)
        {
            TrackingLostShown++;
            return UniTask.CompletedTask;
        }

        public void NotifyRunEnded(RunState run) => Ended = run.outcome;
    }

    /// <summary>
    /// Scripted shooter: scores launch positions × angles by the enemy contacts of BallSimulator.PredictPath (lower
    /// rows weigh more) on its own read-only simulator, re-aims once per shot or board change, and strikes whenever
    /// asked. Deterministic for a seed.
    /// </summary>
    public sealed class SmokeShotInput : IShotInput
    {
        const int LaunchSamples = 7;
        const int AngleSamples = 31;
        const float PathLength = 36f;
        const int PathBounces = 8;
        const float ContactSlack = 0.06f;

        readonly RunState run;
        readonly GameRules rules;
        readonly BallSimulator predictor;
        readonly Vector2[] points = new Vector2[64];
        readonly System.Random random;
        int aimedShots = -1;
        int aimedEnemies = -1;

        public SmokeShotInput(RunState aRun, GameRules aRules, int seed)
        {
            run = aRun;
            rules = aRules;
            predictor = new BallSimulator(rules, new BoardOps(rules));
            random = new System.Random(seed);
        }

        public bool IsTracking => true;
        public float LaunchX01 { get; private set; } = 0.5f;
        public Vector2 AimDirection { get; private set; } = Vector2.up;

        public bool TryConsumeStrike(out StrikeInfo strike)
        {
            Retarget();
            strike = new StrikeInfo { direction = AimDirection, power01 = 0.6f, isPowerShot = random.NextDouble() < 0.1 };
            return true;
        }

        public void ResetStrike()
        {
        }

        void Retarget()
        {
            var enemies = run.board.enemies.Count;
            if (aimedShots == run.stats.shots && aimedEnemies == enemies) return;
            aimedShots = run.stats.shots;
            aimedEnemies = enemies;
            var arena = rules.arena;
            var bestScore = -1f;
            var bestLaunch = 0.5f;
            var bestDirection = Vector2.up;
            var minAngle = arena.minAimAngleDeg + 1f;
            var maxAngle = 180f - arena.minAimAngleDeg - 1f;
            for (var l = 0; l < LaunchSamples; l++)
            {
                var launch = (l + 0.5f) / LaunchSamples;
                var origin = ArenaGeometry.LaunchOrigin(arena, launch);
                for (var a = 0; a < AngleSamples; a++)
                {
                    var angle = Mathf.Lerp(minAngle, maxAngle, a / (AngleSamples - 1f)) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var score = Score(origin, direction) + (float)random.NextDouble() * 0.01f;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    bestLaunch = launch;
                    bestDirection = direction;
                }
            }

            LaunchX01 = bestLaunch;
            AimDirection = bestDirection;
        }

        // Every predicted polyline vertex inside an enemy footprint is one contact; deeper rows count more.
        float Score(Vector2 origin, Vector2 direction)
        {
            var count = predictor.PredictPath(run, origin, direction, PathLength, PathBounces, points);
            var arena = rules.arena;
            var score = 0f;
            for (var i = 1; i < count; i++)
            {
                var p = points[i];
                foreach (var enemy in run.board.enemies)
                {
                    var inset = rules.enemies[(int)enemy.type].isBoss ? arena.bossInset : arena.enemyInset;
                    var rect = ArenaGeometry.FootprintRect(arena, enemy.col, enemy.row, enemy.width, enemy.height, inset);
                    var slack = arena.ballRadius + ContactSlack;
                    if (p.x < rect.xMin - slack || p.x > rect.xMax + slack || p.y < rect.yMin - slack || p.y > rect.yMax + slack) continue;
                    score += 1f + 0.15f * (enemy.row + enemy.height - 1);
                    break;
                }
            }

            return score;
        }
    }
}
