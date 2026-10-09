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
    }
}
