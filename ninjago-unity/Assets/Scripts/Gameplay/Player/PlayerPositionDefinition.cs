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
                    0 => 0.3f,
                    1 => 0.7f,
                    _ => throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, null)
                },
                3 => playerIndex switch
                {
                    0 => 0.25f,
                    1 => 0.5f,
                    2 => 0.75f,
                    _ => throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, null)
                },
                4 => playerIndex switch
                {
                    0 => 0.2f,
                    1 => 0.4f,
                    2 => 0.6f,
                    3 => 0.8f,
                    _ => throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, null)
                },
                _ => throw new ArgumentOutOfRangeException(nameof(numOfPlayers), numOfPlayers, null)
            };
        }
    }
}
