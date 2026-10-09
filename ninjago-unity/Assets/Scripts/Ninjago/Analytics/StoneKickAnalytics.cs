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

        /// <param name="slashNumber">1 for the slash that starts the frenzy, then counting up within the throw.</param>
        public static void SlashHit(int playerIndex, int throwIndex, int slashNumber, float speedInchesPerSecond, float secondsAirborne)
        {
            var props = Throw(playerIndex, throwIndex);
            props["slash"] = slashNumber;
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

        public static void KickAccepted(int playerIndex, int throwIndex, int stones, float secondsIntoPrompt)
        {
            var props = Throw(playerIndex, throwIndex);
            props["stones"] = stones;
            props["seconds_into_prompt"] = Round(secondsIntoPrompt);
            Analytics.TrackEvent("kick_accepted", props);
        }

        /// <param name="reason">"cooldown" (double count) or "no_prompt" (no stones hanging, or already kicked).</param>
        public static void KickRejected(int playerIndex, int throwIndex, string reason)
        {
            var props = Throw(playerIndex, throwIndex);
            props["reason"] = reason;
            Analytics.TrackEvent("kick_rejected", props);
        }

        /// <summary>The kick came in time and every stone went back to the boss.</summary>
        public static void FullReturn(int playerIndex, int throwIndex, int stones, float kickSeconds)
        {
            var props = Throw(playerIndex, throwIndex);
            props["stones"] = stones;
            props["kick_seconds"] = Round(kickSeconds);
            Analytics.TrackEvent("full_return", props);
        }

        /// <summary>No kick before the prompt closed; every stone dropped.</summary>
        public static void IncompleteReturn(int playerIndex, int throwIndex, int stones)
        {
            var props = Throw(playerIndex, throwIndex);
            props["stones"] = stones;
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
