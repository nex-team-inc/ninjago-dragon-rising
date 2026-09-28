#nullable enable

using System;
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

        static AnalyticsManager Manager => AnalyticsManager.Instance;

        float runStartTime;

        #region Static (views, settings, setup)

        public static void UiAction(string screen, string button, int index = -1, string input = "remote")
        {
            Manager.TrackEvent("ui_button_click", new GameAnalyticsProperties
            {
                ["screen"] = screen, ["button"] = button, ["index"] = index, ["input"] = input,
            });
        }

        public static void UiBack(string screen)
        {
            Manager.TrackEvent("ui_back", new GameAnalyticsProperties { ["screen"] = screen });
        }

        public static void SettingChanged(string name, string value)
        {
            Manager.TrackEvent("settings_changed", new GameAnalyticsProperties { ["setting"] = name, ["new_value"] = value });
        }

        public static void SettingChanged(string name, float value)
        {
            Manager.TrackEvent("settings_changed", new GameAnalyticsProperties { ["setting"] = name, ["new_value"] = value });
        }

        public static void Setup(string step, float seconds)
        {
            Manager.TrackEvent("setup_step", new GameAnalyticsProperties { ["step"] = step, ["elapsed_s"] = Round1(seconds) });
        }

        public static void SetupComplete(int numPlayers, float seconds)
        {
            Manager.TrackEvent("setup_complete", new GameAnalyticsProperties { ["num_players"] = numPlayers, ["duration_s"] = Round1(seconds) });
        }

        public static void SecretCode(string screen)
        {
            Manager.TrackEvent("secret_code_entered", new GameAnalyticsProperties { ["code"] = "konami", ["screen"] = screen });
        }

        #endregion

        #region Session

        /// <summary>TrackGameStart + PLAY-scope session props; seed is optional so TDD §12 callers keep compiling.</summary>
        public void SessionStart(int numPlayers, bool isContinue, int seed = 0)
        {
            runStartTime = Time.realtimeSinceStartup;
            Manager.TrackGameStart(ContentName, numPlayers, isContinue ? "continue" : "new", new GameAnalyticsProperties { ["seed"] = seed });
            // PLAY-scope props must be registered after TrackGameStart (start resets them).
            var analytics = GameAnalytics.Instance;
            analytics.RegisterSessionProperties("run_id", Guid.NewGuid().ToString("N"), GameAnalytics.SessionPropertiesScope.PLAY);
            analytics.RegisterSessionProperties("num_players", numPlayers, GameAnalytics.SessionPropertiesScope.PLAY);
            analytics.RegisterSessionProperties("is_continue", isContinue, GameAnalytics.SessionPropertiesScope.PLAY);
        }

        public void SessionStop(RunOutcome outcome)
        {
            Manager.TrackGameStop(new GameAnalyticsProperties { ["result"] = outcome.ToString() });
        }

        public void Pause() => Manager.TrackPause();

        public void Resume() => Manager.TrackResume();

        public float SecondsSinceRunStart => Time.realtimeSinceStartup - runStartTime;

        #endregion

        #region Turns & shots

        public void TurnStart(int stage, int turn, int balls, int hp)
        {
            Manager.TrackEvent("turn_start", new GameAnalyticsProperties
            {
                ["stage"] = stage, ["turn"] = turn, ["balls_in_hand"] = balls, ["hp"] = hp,
            });
        }

        public void ShotFired(BallType ballType, int level, float angleDeg, float power, int shooter)
        {
            Manager.TrackEvent("shot_fired", new GameAnalyticsProperties
            {
                ["ball_type"] = ballType.ToString(), ["ball_level"] = level, ["angle_deg"] = Round1(angleDeg),
                ["power"] = power, ["player_index"] = shooter,
            });
        }

        public void ShotResult(BallType ballType, int hits, int damage, int bounces, int kills, int combo)
        {
            Manager.TrackEvent("ball_result", new GameAnalyticsProperties
            {
                ["ball_type"] = ballType.ToString(), ["hits"] = hits, ["damage"] = damage, ["bounces"] = bounces,
                ["kills"] = kills, ["max_combo"] = combo,
            });
        }

        public void TurnEnd(int stage, int turn, int enemiesAdvanced, int damageTaken, int hp)
        {
            Manager.TrackEvent("turn_end", new GameAnalyticsProperties
            {
                ["stage"] = stage, ["turn"] = turn, ["enemies_advanced"] = enemiesAdvanced,
                ["damage_taken"] = damageTaken, ["hp"] = hp,
            });
        }

        #endregion

        #region Stages, rewards, bosses, run end

        public void StageStart(int act, int stage, bool isBoss)
        {
            Manager.TrackEvent("stage_start", new GameAnalyticsProperties
            {
                ["act"] = act, ["stage"] = stage, ["stage_kind"] = isBoss ? "boss" : "normal",
            });
        }

        public void StageClear(int act, int stage, int turns, int hp)
        {
            Manager.TrackEvent("stage_clear", new GameAnalyticsProperties
            {
                ["act"] = act, ["stage"] = stage, ["turns"] = turns, ["hp"] = hp,
            });
        }

        public void RewardOffered(IReadOnlyList<RewardOption> options)
        {
            Manager.TrackEvent("reward_offered", new GameAnalyticsProperties { ["options"] = Describe(options) });
        }

        public void RewardChosen(RewardOption option, int index)
        {
            Manager.TrackEvent("reward_chosen", new GameAnalyticsProperties { ["choice"] = Describe(option), ["index"] = index });
        }

        public void BossSpawn(EnemyType type)
        {
            Manager.TrackEvent("boss_spawn", new GameAnalyticsProperties { ["boss_id"] = type.ToString() });
        }

        public void BossDefeated(EnemyType type, int turns)
        {
            Manager.TrackEvent("boss_defeated", new GameAnalyticsProperties { ["boss_id"] = type.ToString(), ["turns"] = turns });
        }

        /// <summary>Sent before SessionStop.</summary>
        public void RunEnd(RunOutcome outcome, int stageNumber, int turns, float seconds, int kills)
        {
            Manager.TrackEvent("run_end", new GameAnalyticsProperties
            {
                ["result"] = outcome.ToString(), ["stage"] = stageNumber, ["turns_total"] = turns,
                ["duration_s"] = Round1(seconds), ["kills_total"] = kills,
            });
        }

        public void TrackingLost(int player, float seconds)
        {
            Manager.TrackEvent("tracking_lost", new GameAnalyticsProperties { ["player_index"] = player, ["lost_s"] = Round1(seconds) });
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
