#nullable enable

using Nex.Platform;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Earth Seal gameplay events over AnalyticsManager.TrackEvent. player_index is the player the event belongs to:
    /// the holder for a seal, the last holder for a drop or a breakthrough, -1 when no player was involved.
    /// </summary>
    public static class EarthSealAnalytics
    {
        static AnalyticsManager Analytics => AnalyticsManager.Instance;

        #region Wall

        public static void CrackStart(EarthSealWave wave, int tile, int liveCracks)
        {
            var props = Tile(wave, tile, -1);
            props["live_cracks"] = liveCracks;
            Analytics.TrackEvent("crack_start", props);
        }

        public static void SealComplete(EarthSealWave wave, int tile, int holderMask, int cursorCount, float seconds)
        {
            var props = Tile(wave, tile, LowestPlayer(holderMask));
            props["p1_held"] = (holderMask & 1) != 0;
            props["p2_held"] = (holderMask & 2) != 0;
            props["cursors"] = cursorCount;
            props["two_hands"] = cursorCount >= 2;
            props["seconds"] = Round(seconds);
            Analytics.TrackEvent("seal_complete", props);
        }

        /// <param name="atBreakthrough">True when the monster broke through a partly filled seal, false when it drained away.</param>
        public static void SealDrop(EarthSealWave wave, int tile, int lastHolder, float peakFill, bool atBreakthrough)
        {
            var props = Tile(wave, tile, lastHolder);
            props["peak_fill"] = Round(peakFill);
            props["reason"] = atBreakthrough ? "breakthrough" : "drained";
            Analytics.TrackEvent("seal_drop", props);
        }

        public static void Breakthrough(EarthSealWave wave, int tile, int lastHolder, int heartsLeft, float fill)
        {
            var props = Tile(wave, tile, lastHolder);
            props["hearts_left"] = heartsLeft;
            props["fill"] = Round(fill);
            Analytics.TrackEvent("breakthrough", props);
        }

        #endregion

        #region Helpers

        static float Round(float value) => Mathf.Round(value * 100f) / 100f;

        static int LowestPlayer(int mask) => (mask & 1) != 0 ? 0 : (mask & 2) != 0 ? 1 : -1;

        static GameAnalyticsProperties Tile(EarthSealWave wave, int tile, int playerIndex)
        {
            return new GameAnalyticsProperties { ["wave"] = (int)wave + 1, ["tile"] = tile, ["player_index"] = playerIndex };
        }

        #endregion
    }
}
