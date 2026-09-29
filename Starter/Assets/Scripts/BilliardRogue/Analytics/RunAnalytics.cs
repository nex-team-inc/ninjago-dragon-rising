#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;
using Nex.Platform;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Thin wrapper over AnalyticsManager for the run taxonomy (TDD §12). Events are snake_case, ids are enum
    /// names, times are seconds. UI/setting helpers are static so views can call them without a session.
    /// One shot_fired + one shot_result per shot at most; never log per bounce or per hit.
    /// </summary>
    public sealed class RunAnalytics
    {
        public const string ContentName = "billiard_rogue";

        float runStartTime;

        /// <summary>False once the singleton is gone (scene teardown reaches it before the session's last events).</summary>
        static bool TryGetManager(out AnalyticsManager manager)
        {
            manager = AnalyticsManager.Instance;
            return manager != null;
        }

        static void Track(string eventName, GameAnalyticsProperties props)
        {
            if (TryGetManager(out var manager)) manager.TrackEvent(eventName, props);
        }

        #region Static (views, settings, setup)

        public static void UiAction(string screen, string button, int index = -1, string input = "remote")
        {
            Track("ui_button_click", new GameAnalyticsProperties
            {
                ["screen"] = screen, ["button"] = button, ["index"] = index, ["input"] = input,
            });
        }

        public static void UiBack(string screen)
        {
            Track("ui_back", new GameAnalyticsProperties { ["screen"] = screen });
        }

        public static void SettingChanged(string name, string value)
        {
            Track("settings_changed", new GameAnalyticsProperties { ["setting"] = name, ["new_value"] = value });
        }

        public static void SettingChanged(string name, float value)
        {
            Track("settings_changed", new GameAnalyticsProperties { ["setting"] = name, ["new_value"] = value });
        }

        public static void Setup(string step, float seconds)
        {
            Track("setup_step", new GameAnalyticsProperties { ["step"] = step, ["elapsed_s"] = Round1(seconds) });
        }

        public static void SetupComplete(int numPlayers, float seconds)
        {
            Track("setup_complete", new GameAnalyticsProperties { ["num_players"] = numPlayers, ["duration_s"] = Round1(seconds) });
        }

        public static void SecretCode(string screen)
        {
            Track("secret_code_entered", new GameAnalyticsProperties { ["code"] = "konami", ["screen"] = screen });
        }

        #endregion

        #region Session

        /// <summary>TrackGameStart + PLAY-scope session props. runId is RunState.runId so a continued run's sessions join up.</summary>
        public void SessionStart(int numPlayers, bool isContinue, string runId, int seed = 0)
        {
            runStartTime = Time.realtimeSinceStartup;
            if (!TryGetManager(out var manager)) return;
            manager.TrackGameStart(ContentName, numPlayers, isContinue ? "continue" : "new", new GameAnalyticsProperties { ["seed"] = seed, ["run_id"] = runId });
            // PLAY-scope props must be registered after TrackGameStart (start resets them).
            var analytics = GameAnalytics.Instance;
            analytics.RegisterSessionProperties("run_id", runId, GameAnalytics.SessionPropertiesScope.PLAY);
            analytics.RegisterSessionProperties("num_players", numPlayers, GameAnalytics.SessionPropertiesScope.PLAY);
            analytics.RegisterSessionProperties("is_continue", isContinue, GameAnalytics.SessionPropertiesScope.PLAY);
        }

        public void SessionStop(RunOutcome outcome)
        {
            if (TryGetManager(out var manager)) manager.TrackGameStop(new GameAnalyticsProperties { ["result"] = outcome.ToString() });
        }

        public void Pause()
        {
            if (TryGetManager(out var manager)) manager.TrackPause();
        }

        public void Resume()
        {
            if (TryGetManager(out var manager)) manager.TrackResume();
        }

        public float SecondsSinceRunStart => Time.realtimeSinceStartup - runStartTime;

        #endregion

        #region Turns & shots

        public void TurnStart(int stage, int turn, int balls, int hp)
        {
            Track("turn_start", new GameAnalyticsProperties
            {
                ["stage"] = stage, ["turn"] = turn, ["balls_in_hand"] = balls, ["hp"] = hp,
            });
        }

        public void ShotFired(BallType ballType, int level, float angleDeg, float power, int shooter)
        {
            Track("shot_fired", new GameAnalyticsProperties
            {
                ["ball_type"] = ballType.ToString(), ["ball_level"] = level, ["angle_deg"] = Round1(angleDeg),
                ["power"] = power, ["player_index"] = shooter,
            });
        }

        public void ShotResult(BallType ballType, int hits, int damage, int bounces, int kills, int combo)
        {
            Track("ball_result", new GameAnalyticsProperties
            {
                ["ball_type"] = ballType.ToString(), ["hits"] = hits, ["damage"] = damage, ["bounces"] = bounces,
                ["kills"] = kills, ["max_combo"] = combo,
            });
        }

        public void TurnEnd(int stage, int turn, int enemiesAdvanced, int damageTaken, int hp)
        {
            Track("turn_end", new GameAnalyticsProperties
            {
                ["stage"] = stage, ["turn"] = turn, ["enemies_advanced"] = enemiesAdvanced,
                ["damage_taken"] = damageTaken, ["hp"] = hp,
            });
        }

        #endregion

        #region Stages, rewards, bosses, run end

        public void StageStart(int act, int stage, bool isBoss)
        {
            Track("stage_start", new GameAnalyticsProperties
            {
                ["act"] = act, ["stage"] = stage, ["stage_kind"] = isBoss ? "boss" : "normal",
            });
        }

        public void StageClear(int act, int stage, int turns, int hp)
        {
            Track("stage_clear", new GameAnalyticsProperties
            {
                ["act"] = act, ["stage"] = stage, ["turns"] = turns, ["hp"] = hp,
            });
        }

        public void RewardOffered(IReadOnlyList<RewardOption> options)
        {
            Track("reward_offered", new GameAnalyticsProperties { ["options"] = Describe(options) });
        }

        public void RewardChosen(RewardOption option, int index)
        {
            Track("reward_chosen", new GameAnalyticsProperties { ["choice"] = Describe(option), ["index"] = index });
        }

        public void BossSpawn(EnemyType type)
        {
            Track("boss_spawn", new GameAnalyticsProperties { ["boss_id"] = type.ToString() });
        }

        public void BossDefeated(EnemyType type, int turns)
        {
            Track("boss_defeated", new GameAnalyticsProperties { ["boss_id"] = type.ToString(), ["turns"] = turns });
        }

        /// <summary>Sent before SessionStop.</summary>
        public void RunEnd(RunOutcome outcome, int stageNumber, int turns, float seconds, int kills)
        {
            Track("run_end", new GameAnalyticsProperties
            {
                ["result"] = outcome.ToString(), ["stage"] = stageNumber, ["turns_total"] = turns,
                ["duration_s"] = Round1(seconds), ["kills_total"] = kills,
            });
        }

        public void TrackingLost(int player, float seconds)
        {
            Track("tracking_lost", new GameAnalyticsProperties { ["player_index"] = player, ["lost_s"] = Round1(seconds) });
        }

        #endregion

        #region Helpers

        static float Round1(float value) => Mathf.Round(value * 10f) / 10f;

        static string[] Describe(IReadOnlyList<RewardOption> options)
        {
            var result = new string[options.Count];
            for (var i = 0; i < options.Count; i++)
            {
                result[i] = Describe(options[i]);
            }

            return result;
        }

        static string Describe(RewardOption option)
        {
            return option.kind switch
            {
                RewardKind.NewBall => "new_" + option.ballType,
                RewardKind.UpgradeBall => "up_" + option.ballType + "_" + option.amount,
                _ => option.kind.ToString(),
            };
        }

        #endregion
    }
}
