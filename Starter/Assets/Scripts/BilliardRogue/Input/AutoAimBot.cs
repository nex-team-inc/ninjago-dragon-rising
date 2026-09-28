#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using Nex.Utils;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Auto-aim shot input for automated playtests and debugging (DebugSettings.autoAimBot, DebugHooks.SetBot). It
    /// behaves like a patient player: think (BotShotPlanner searches while the think delay runs), walk the cat and
    /// sweep the cue to the chosen shot, then strike and wait until the strike is consumed. A board change (shot
    /// fired, enemies died or moved, new turn or stage) restarts the thinking. Without a run (calibration) it strikes
    /// straight up from the centre. Deterministic per player index. ShotInputRouter enables it only while selected.
    /// </summary>
    public sealed class AutoAimBot : MonoBehaviour, IShotInput
    {
        const float MinStrikePower = 0.4f;
        const int SeedSalt = 7919;

        enum Phase
        {
            Thinking = 0,
            Aiming = 1,
            Ready = 2,
        }

        ControlConfig config = null!;
        ArenaRules arena = null!;
        BotShotPlanner planner = null!;
        Func<RunState?> runProvider = null!;
        System.Random random = null!;
        Phase phase;
        float phaseStart;
        float thinkSeconds;
        float fromLaunch;
        float targetLaunch;
        Vector2 fromAim;
        Vector2 targetAim;
        long boardSignature;
        bool hasPendingStrike;
        StrikeInfo pendingStrike;

        public int PlayerIndex { get; private set; }
        public bool IsTracking => true;
        public float LaunchX01 { get; private set; } = 0.5f;
        public Vector2 AimDirection { get; private set; } = Vector2.up;

        #region Life Cycle

        /// <summary>run returns the current RunState, or null outside gameplay.</summary>
        public void Initialize(int playerIndex, GameRules rules, Func<RunState?> run, ControlConfig aConfig)
        {
            PlayerIndex = playerIndex;
            config = aConfig;
            arena = rules.arena;
            runProvider = run;
            planner = new BotShotPlanner(rules, config);
            random = new System.Random(SeedSalt * (playerIndex + 1));
            BeginThinking(runProvider());
        }

        void Update()
        {
            var run = runProvider();
            var signature = Signature(run);
            if (signature != boardSignature)
            {
                BeginThinking(run);
                return;
            }

            var now = Time.unscaledTime;
            switch (phase)
            {
                case Phase.Thinking:
                    if (run != null && planner.IsSearching) planner.Step(run, config.BotCandidatesPerFrame);
                    if (now - phaseStart < thinkSeconds || (run != null && planner.IsSearching)) return;
                    BeginAiming(run != null, now);
                    return;
                case Phase.Aiming:
                    UpdateAiming(now);
                    return;
                case Phase.Ready:
                default:
                    return;
            }
        }

        #endregion

        #region IShotInput

        public bool TryConsumeStrike(out StrikeInfo strike)
        {
            strike = pendingStrike;
            if (!hasPendingStrike) return false;

            hasPendingStrike = false;
            BeginThinking(runProvider());
            return true;
        }

        public void ResetStrike()
        {
            hasPendingStrike = false;
            BeginThinking(runProvider());
        }

        #endregion

        #region Behaviour

        void BeginThinking(RunState? run)
        {
            phase = Phase.Thinking;
            phaseStart = Time.unscaledTime;
            thinkSeconds = config.BotThinkSeconds + (float)random.NextDouble() * config.BotThinkJitterSeconds;
            boardSignature = Signature(run);
            hasPendingStrike = false;
            planner.Begin(LaunchX01);
        }

        void BeginAiming(bool planned, float now)
        {
            fromLaunch = LaunchX01;
            fromAim = AimDirection;
            targetLaunch = planned ? planner.BestLaunch01 : 0.5f;
            var angle = planned ? Mathf.Atan2(planner.BestDirection.y, planner.BestDirection.x) * Mathf.Rad2Deg : 90f;
            angle += ((float)random.NextDouble() * 2f - 1f) * config.BotAimJitterDeg;
            targetAim = ArenaGeometry.ClampAim(arena, Vector2Utils.PolarDeg(angle));
            phase = Phase.Aiming;
            phaseStart = now;
        }

        void UpdateAiming(float now)
        {
            var sweep = config.BotAimSweepSeconds;
            var t = sweep > 0f ? Mathf.Clamp01((now - phaseStart) / sweep) : 1f;
            var eased = Mathf.SmoothStep(0f, 1f, t);
            LaunchX01 = Mathf.Lerp(fromLaunch, targetLaunch, eased);
            AimDirection = ArenaGeometry.ClampAim(arena, Vector2.Lerp(fromAim, targetAim, eased));
            if (t < 1f) return;

            var power = random.NextDouble() < config.BotPowerShotChance;
            pendingStrike = new StrikeInfo
            {
                direction = AimDirection,
                power01 = power ? 1f : Mathf.Lerp(MinStrikePower, 1f, (float)random.NextDouble()),
                isPowerShot = power,
            };
            hasPendingStrike = true;
            phase = Phase.Ready;
        }

        /// <summary>Cheap board-change fingerprint: shots fired, turn, stage, active shooter and enemy count.</summary>
        static long Signature(RunState? run)
        {
            if (run == null) return -1;
            return run.stats.shots
                   + ((long)run.turnInStage << 20)
                   + ((long)run.stageNumber << 32)
                   + ((long)run.activePlayerIndex << 40)
                   + ((long)run.board.enemies.Count << 44);
        }

        #endregion
    }
}
