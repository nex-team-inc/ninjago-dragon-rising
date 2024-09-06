#nullable enable

using System;

namespace Nex
{
    public static class PlayerPositionDefinition
    {
        public static float GetXRatioForPlayer(int playerIndex, int numOfPlayers)
        {
            return numOfPlayers switch
            {
                1 => 0.5f,
                2 => playerIndex switch
                {
                    0 => 0.25f,
                    1 => 0.75f,
                    _ => throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, null)
                },
                _ => throw new ArgumentOutOfRangeException(nameof(numOfPlayers), numOfPlayers, null)
            };
        }

        public static float PlayerPreviewWidthRatio(int numOfPlayers)
        {
            return numOfPlayers switch
            {
                1 => 1f,
                2 => 0.5f,
                _ => throw new ArgumentOutOfRangeException(nameof(numOfPlayers), numOfPlayers, null)
            };
        }
    }
}
