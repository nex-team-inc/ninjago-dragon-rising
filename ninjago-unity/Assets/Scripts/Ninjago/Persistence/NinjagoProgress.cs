#nullable enable

using UnityEngine.Scripting;

namespace Nex.Ninjago
{
    /// <summary>Saved by PlayerDataManager (Easy Save). Public fields only; append new fields, never rename.</summary>
    [Preserve]
    public class NinjagoProgress
    {
        public GameModeType lastMiniGame = GameModeType.None;

        public int fightRuns;
        public int fightBestSlips;
        public int fightBestFightBacks;
        public int fightBestHearts;

        public int chaseRuns;
        public int chaseBestCarDodges;
        public int chaseBestSkyDodges;
        // -1 until the first finished chase.
        public int chaseFewestCarHits = -1;
        public int chaseFewestSkyHits = -1;

        public int stoneKickRuns;
        public int stoneKickBestSlashes;
        public int stoneKickBestFullReturns;
        public int stoneKickBestStones;
        // Fastest average seconds from KICK to the kick; -1 until the first kick.
        public float stoneKickBestKickTime = -1f;

        public int earthSealRuns;
        public int earthSealBestSeals;
        public int earthSealBestHearts;
        public int earthSealBestTwoHandSeals;
        // -1 until the first finished wall.
        public int earthSealFewestBreakthroughs = -1;
    }
}
