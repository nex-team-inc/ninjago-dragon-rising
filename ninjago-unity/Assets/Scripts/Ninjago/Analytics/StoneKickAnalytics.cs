#nullable enable

using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Stone Kick gameplay events over AnalyticsManager.TrackEvent; every event carries the player index.</summary>
    public static class StoneKickAnalytics
    {
        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        #region Slash

        public static void SlashHit(int playerIndex, int throwIndex, float speedInchesPerSecond, float secondsAirborne)
        {
            var props = Throw(playerIndex, throwIndex);
            props["speed_in_s"] = Round(speedInchesPerSecond);
            props["seconds_airborne"] = Round(secondsAirborne);
            Analytics.TrackEvent("slash_hit", props);
        }

        public static void SlashMiss(int playerIndex, int throwIndex, int heartsLeft)
        {
            var props = Throw(playerIndex, throwIndex);
            props["hearts_left"] = heartsLeft;
            Analytics.TrackEvent("slash_miss", props);
        }

        #endregion

        #region Kick

        public static void KickAccepted(int playerIndex, int throwIndex, int kickNumber, float secondsIntoPrompt)
        {
            var props = Throw(playerIndex, throwIndex);
            props["kick"] = kickNumber;
            props["seconds_into_prompt"] = Round(secondsIntoPrompt);
            Analytics.TrackEvent("kick_accepted", props);
        }

        /// <param name="reason">"cooldown" (double count) or "no_prompt" (no pieces hanging).</param>
        public static void KickRejected(int playerIndex, int throwIndex, string reason)
        {
            var props = Throw(playerIndex, throwIndex);
            props["reason"] = reason;
            Analytics.TrackEvent("kick_rejected", props);
        }

        public static void FullReturn(int playerIndex, int throwIndex, int kicks, float promptSeconds, float averageGapSeconds)
        {
            var props = Throw(playerIndex, throwIndex);
            props["kicks"] = kicks;
            props["prompt_seconds"] = Round(promptSeconds);
            props["average_gap_s"] = Round(averageGapSeconds);
            Analytics.TrackEvent("full_return", props);
        }

        public static void IncompleteReturn(int playerIndex, int throwIndex, int kicks, int kicksRequired)
        {
            var props = Throw(playerIndex, throwIndex);
            props["kicks"] = kicks;
            props["kicks_required"] = kicksRequired;
            Analytics.TrackEvent("incomplete_return", props);
        }

        #endregion

        #region Helpers

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        static GameAnalyticsProperties Throw(int playerIndex, int throwIndex)
        {
            return new GameAnalyticsProperties { ["player_index"] = playerIndex, ["throw"] = throwIndex };
        }

        #endregion
    }
}
