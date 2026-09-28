#nullable enable

using System;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Registration points for driving the game from the Editor/CLI (`unity command eval
    /// 'return Nex.BilliardRogue.DebugHooks.Shoot(70f);'`) and from the DebugSettings panel. Flow and gameplay
    /// register handlers while they are alive and clear them on teardown. Every wrapper returns a status string.
    /// </summary>
    public static class DebugHooks
    {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>(numPlayers, seed; seed 0 = random) → accepted.</summary>
        public static Func<int, int, bool>? StartNewRunHandler;
        public static Func<bool>? ContinueRunHandler;
        public static Func<bool>? SkipCalibrationHandler;
        public static Func<bool, bool>? SetBotHandler;
        /// <summary>Reward card index 0..2.</summary>
        public static Func<int, bool>? ChooseRewardHandler;
        /// <summary>Aim angle in degrees from +x (90 = straight up); fires the next ball.</summary>
        public static Func<float, bool>? ShootHandler;
        /// <summary>Stage number 0..11.</summary>
        public static Func<int, bool>? GotoStageHandler;
        public static Func<bool>? KillAllHandler;
        public static Func<bool>? ClearStageHandler;
        public static Func<bool>? AddEveryBallHandler;
        /// <summary>Human-readable summary of the flow/run state.</summary>
        public static Func<string>? StateHandler;

        public static string StartNewRun(int players, int seed) => Report(nameof(StartNewRun), StartNewRunHandler?.Invoke(players, seed));
        public static string ContinueRun() => Report(nameof(ContinueRun), ContinueRunHandler?.Invoke());
        public static string SkipCalibration() => Report(nameof(SkipCalibration), SkipCalibrationHandler?.Invoke());
        public static string SetBot(bool enabled) => Report(nameof(SetBot), SetBotHandler?.Invoke(enabled));
        public static string ChooseReward(int index) => Report(nameof(ChooseReward), ChooseRewardHandler?.Invoke(index));
        public static string Shoot(float angleDeg) => Report(nameof(Shoot), ShootHandler?.Invoke(angleDeg));
        public static string GotoStage(int stageNumber) => Report(nameof(GotoStage), GotoStageHandler?.Invoke(stageNumber));
        public static string KillAll() => Report(nameof(KillAll), KillAllHandler?.Invoke());
        public static string ClearStage() => Report(nameof(ClearStage), ClearStageHandler?.Invoke());
        public static string AddEveryBall() => Report(nameof(AddEveryBall), AddEveryBallHandler?.Invoke());
        public static string State() => StateHandler?.Invoke() ?? NotRegistered(nameof(State));

        /// <summary>Drops every handler (call when the owning objects are destroyed).</summary>
        public static void Clear()
        {
            StartNewRunHandler = null;
            ContinueRunHandler = null;
            SkipCalibrationHandler = null;
            SetBotHandler = null;
            ChooseRewardHandler = null;
            ShootHandler = null;
            GotoStageHandler = null;
            KillAllHandler = null;
            ClearStageHandler = null;
            AddEveryBallHandler = null;
            StateHandler = null;
        }

        static string Report(string name, bool? accepted)
        {
            if (accepted == null) return NotRegistered(name);
            return accepted.Value ? $"{name}: ok" : $"{name}: rejected";
        }

        static string NotRegistered(string name) => $"{name}: not registered";
#endif
    }
}
