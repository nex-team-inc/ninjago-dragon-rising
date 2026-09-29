#nullable enable

using System;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Registration points for driving the game from the Editor/CLI (`unity command eval
    /// 'return Nex.BilliardRogue.DebugHooks.Shoot(70f);'`) and from the DebugSettings panel. Flow and gameplay
    /// register handlers while they are alive and clear them on teardown. Every wrapper returns a status string.
    /// Registration compiles in every build; only Editor, development and ENABLE_DEBUG_SETTINGS builds invoke
    /// the handlers, release wrappers answer "disabled".
    /// </summary>
    public static class DebugHooks
    {
        #region Handlers

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
        /// <summary>hype01 (0..1; negative = off) → accepted. Overrides the body-motion Hype while balls fly.</summary>
        public static Func<float, bool>? SetHypeHandler;
        /// <summary>Human-readable summary of the flow/run state.</summary>
        public static Func<string>? StateHandler;

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
            SetHypeHandler = null;
            StateHandler = null;
        }

        #endregion

        #region Wrappers

        public static string StartNewRun(int players, int seed) => Run(nameof(StartNewRun), StartNewRunHandler, players, seed);
        public static string ContinueRun() => Run(nameof(ContinueRun), ContinueRunHandler);
        public static string SkipCalibration() => Run(nameof(SkipCalibration), SkipCalibrationHandler);
        public static string SetBot(bool enabled) => Run(nameof(SetBot), SetBotHandler, enabled);
        public static string ChooseReward(int index) => Run(nameof(ChooseReward), ChooseRewardHandler, index);
        public static string Shoot(float angleDeg) => Run(nameof(Shoot), ShootHandler, angleDeg);
        public static string GotoStage(int stageNumber) => Run(nameof(GotoStage), GotoStageHandler, stageNumber);
        public static string KillAll() => Run(nameof(KillAll), KillAllHandler);
        public static string ClearStage() => Run(nameof(ClearStage), ClearStageHandler);
        public static string AddEveryBall() => Run(nameof(AddEveryBall), AddEveryBallHandler);
        /// <summary>Forces Hype to hype01 while balls fly (-1 = back to body motion / DebugSettings.forceHype).</summary>
        public static string SetHype(float hype01) => Run(nameof(SetHype), SetHypeHandler, hype01);

        public static string State()
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            return StateHandler?.Invoke() ?? NotRegistered(nameof(State));
#else
            return Disabled(nameof(State));
#endif
        }

        #endregion

        #region Helpers

        static string Run(string name, Func<bool>? handler)
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            return Report(name, handler?.Invoke());
#else
            return Disabled(name);
#endif
        }

        static string Run<T>(string name, Func<T, bool>? handler, T arg)
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            return Report(name, handler?.Invoke(arg));
#else
            return Disabled(name);
#endif
        }

        static string Run<T1, T2>(string name, Func<T1, T2, bool>? handler, T1 arg1, T2 arg2)
        {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            return Report(name, handler?.Invoke(arg1, arg2));
#else
            return Disabled(name);
#endif
        }

#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
        static string Report(string name, bool? accepted)
        {
            if (accepted == null)
            {
                return NotRegistered(name);
            }

            return accepted.Value ? $"{name}: ok" : $"{name}: rejected";
        }

        static string NotRegistered(string name) => $"{name}: not registered";
#else
        static string Disabled(string name) => $"{name}: disabled in release builds";
#endif

        #endregion
    }
}
