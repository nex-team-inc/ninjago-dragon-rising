#nullable enable

using System;
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
    /// Scripted shooter: stands under the lowest enemy and aims at its centre (straight or via one wall bounce when
    /// the direct line is too flat), striking whenever asked. Deterministic for a seed.
    /// </summary>
    public sealed class SmokeShotInput : IShotInput
    {
        readonly RunState run;
        readonly ArenaRules arena;
        readonly System.Random random;

        public SmokeShotInput(RunState aRun, ArenaRules aArena, int seed)
        {
            run = aRun;
            arena = aArena;
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
            EnemyState? target = null;
            foreach (var enemy in run.board.enemies)
            {
                if (enemy.type == EnemyType.BoneWall) continue;
                if (target == null || enemy.row + enemy.height > target.row + target.height) target = enemy;
            }

            if (target == null)
            {
                LaunchX01 = 0.5f;
                AimDirection = Vector2.up;
                return;
            }

            var centre = ArenaGeometry.FootprintCenter(arena, target.col, target.row, target.width, target.height);
            var jitter = (float)(random.NextDouble() - 0.5) * 0.6f;
            LaunchX01 = Mathf.Clamp01((centre.x + jitter) / arena.columns);
            var origin = ArenaGeometry.LaunchOrigin(arena, LaunchX01);
            AimDirection = ArenaGeometry.ClampAim(arena, (centre - origin).normalized);
        }
    }
}
