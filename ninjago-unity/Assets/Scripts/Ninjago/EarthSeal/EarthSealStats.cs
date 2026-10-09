#nullable enable

namespace Nex.Ninjago
{
    /// <summary>What one Earth Seal run produced, for the result view and the saved bests.</summary>
    public sealed class EarthSealStats
    {
        readonly float[] holdSeconds = new float[SimulatedBody.MaxPlayers];
        readonly int[] sealsHeld = new int[SimulatedBody.MaxPlayers];
        readonly int[] drops = new int[SimulatedBody.MaxPlayers];

        public int Seals { get; private set; }
        public int Breakthroughs { get; private set; }
        /// <summary>Seals completed while two or more cursors (any players) were on the tile.</summary>
        public int TwoHandSeals { get; private set; }
        public int DroppedSeals { get; private set; }
        public int WavesCleared { get; private set; }

        #region Recording

        public void AddHoldTime(int playerIndex, float seconds) => holdSeconds[playerIndex] += seconds;

        public void RecordSeal(int holderMask, int cursorCount)
        {
            Seals++;
            if (cursorCount >= 2) TwoHandSeals++;
            for (var player = 0; player < sealsHeld.Length; player++)
            {
                if ((holderMask & (1 << player)) != 0) sealsHeld[player]++;
            }
        }

        public void RecordDrop(int lastHolder)
        {
            DroppedSeals++;
            if (lastHolder >= 0) drops[lastHolder]++;
        }

        public void RecordBreakthrough() => Breakthroughs++;

        public void RecordWaveCleared() => WavesCleared++;

        #endregion

        #region Per Player

        /// <summary>Seconds this player's cursors spent on live cracks (each cursor counts).</summary>
        public float GetHoldSeconds(int playerIndex) => holdSeconds[playerIndex];

        /// <summary>Seals this player had a cursor on when they completed.</summary>
        public int GetSealsHeld(int playerIndex) => sealsHeld[playerIndex];

        /// <summary>Dropped seals whose last holder was this player.</summary>
        public int GetDrops(int playerIndex) => drops[playerIndex];

        #endregion
    }
}
