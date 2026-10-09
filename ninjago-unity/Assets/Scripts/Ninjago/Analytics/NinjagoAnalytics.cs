#nullable enable

using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Typed wrappers over AnalyticsManager.TrackEvent for both mini-games. Names and keys are snake_case.</summary>
    public static class NinjagoAnalytics
    {
        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        #region Session

        public static void MiniGameStart(GameModeType mode, int requestedPlayers)
        {
            Analytics.TrackGameStart(ContentName(mode), requestedPlayers, mode.ToString());
        }

        public static void MiniGameEnd(GameModeType mode, string result, int activePlayers, GameAnalyticsProperties details)
        {
            details["minigame"] = ContentName(mode);
            details["result"] = result;
            details["active_players"] = activePlayers;
            Analytics.TrackEvent("minigame_end", details);
            Analytics.TrackGameStop(new GameAnalyticsProperties { ["result"] = result });
        }

        public static void UiAction(string screen, string button, int index = -1)
        {
            Analytics.TrackEvent("ui_button_click", new GameAnalyticsProperties
            {
                ["screen"] = screen, ["button"] = button, ["index"] = index,
            });
        }

        public static void PlayerCalibrated(int playerIndex, float seconds)
        {
            Analytics.TrackEvent("setup_player_calibrated", new GameAnalyticsProperties
            {
                ["player_index"] = playerIndex, ["seconds"] = Round(seconds),
            });
        }

        public static void SetupComplete(int requestedPlayers, int activePlayers, float seconds)
        {
            Analytics.TrackEvent("setup_complete", new GameAnalyticsProperties
            {
                ["requested_players"] = requestedPlayers, ["active_players"] = activePlayers, ["seconds"] = Round(seconds),
            });
        }

        #endregion

        #region Fight

        public static void SlipStart(int playerIndex, int sweep, SweepSide safeSide)
        {
            Analytics.TrackEvent("slip_start", Sweep(playerIndex, sweep, safeSide));
        }

        public static void SlipSuccess(int playerIndex, int sweep, SweepSide safeSide, float reactionSeconds)
        {
            var props = Sweep(playerIndex, sweep, safeSide);
            props["reaction_s"] = Round(reactionSeconds);
            Analytics.TrackEvent("slip_success", props);
        }

        public static void SlipFail(int playerIndex, int sweep, SweepSide safeSide, string reason, int heartsLeft)
        {
            var props = Sweep(playerIndex, sweep, safeSide);
            props["reason"] = reason;
            props["hearts_left"] = heartsLeft;
            Analytics.TrackEvent("slip_fail", props);
        }

        public static void CounterStart(int playerIndex, int sweep)
        {
            Analytics.TrackEvent("counter_start", new GameAnalyticsProperties
            {
                ["player_index"] = playerIndex, ["sweep"] = sweep,
            });
        }

        public static void CounterSuccess(int playerIndex, int sweep, float seconds, float travelInches, float peakSpeed)
        {
            Analytics.TrackEvent("counter_success", Counter(playerIndex, sweep, travelInches, peakSpeed, 1f, seconds));
        }

        public static void CounterWhiff(int playerIndex, int sweep, float travelInches, float peakSpeed, float fill01)
        {
            Analytics.TrackEvent("counter_whiff", Counter(playerIndex, sweep, travelInches, peakSpeed, fill01, -1f));
        }

        #endregion

        #region Chase

        public static void SegmentStart(VehicleType vehicle, int activePlayers)
        {
            Analytics.TrackEvent("segment_start", new GameAnalyticsProperties
            {
                ["vehicle"] = vehicle.ToString(), ["active_players"] = activePlayers,
            });
        }

        public static void ObstacleDodge(VehicleType vehicle, int obstacleIndex, int dominantPlayer, Vector2 playerOneSteer, Vector2 playerTwoSteer)
        {
            Analytics.TrackEvent("obstacle_dodge", Obstacle(vehicle, obstacleIndex, dominantPlayer, playerOneSteer, playerTwoSteer));
        }

        public static void ObstacleHit(VehicleType vehicle, int obstacleIndex, int dominantPlayer, Vector2 playerOneSteer, Vector2 playerTwoSteer)
        {
            Analytics.TrackEvent("obstacle_hit", Obstacle(vehicle, obstacleIndex, dominantPlayer, playerOneSteer, playerTwoSteer));
        }

        #endregion

        #region Helpers

        static string ContentName(GameModeType mode) => mode == GameModeType.SlipAndSpin ? "slip_and_spin" : "chest_chase";

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        static GameAnalyticsProperties Sweep(int playerIndex, int sweep, SweepSide safeSide)
        {
            return new GameAnalyticsProperties
            {
                ["player_index"] = playerIndex, ["sweep"] = sweep, ["safe_side"] = safeSide.ToString(),
            };
        }

        static GameAnalyticsProperties Counter(int playerIndex, int sweep, float travelInches, float peakSpeed, float fill01, float seconds)
        {
            return new GameAnalyticsProperties
            {
                ["player_index"] = playerIndex, ["sweep"] = sweep, ["travel_in"] = Round(travelInches),
                ["peak_speed_in_s"] = Round(peakSpeed), ["fill"] = Round(fill01), ["seconds"] = Round(seconds),
            };
        }

        // The vehicle is shared: player_index is whoever steered harder at that moment (-1 = nobody steering).
        static GameAnalyticsProperties Obstacle(VehicleType vehicle, int obstacleIndex, int dominantPlayer, Vector2 playerOneSteer, Vector2 playerTwoSteer)
        {
            return new GameAnalyticsProperties
            {
                ["vehicle"] = vehicle.ToString(), ["obstacle"] = obstacleIndex, ["player_index"] = dominantPlayer,
                ["p1_steer_x"] = Round(playerOneSteer.x), ["p1_steer_y"] = Round(playerOneSteer.y),
                ["p2_steer_x"] = Round(playerTwoSteer.x), ["p2_steer_y"] = Round(playerTwoSteer.y),
            };
        }

        #endregion
    }
}
