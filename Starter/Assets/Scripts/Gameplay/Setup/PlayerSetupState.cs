using System.Collections.Generic;

namespace Nex
{
    public class PlayerSetupState
    {
        public SetupStateType setupStateType;
    }

    public static class PlayerSetupStateUtils
    {
        public static int ReadyPlayerCount(this List<PlayerSetupState> playerSetupStates)
        {
            var playingStateCount = 0;
            foreach (var state in playerSetupStates)
            {
                playingStateCount += state.setupStateType == SetupStateType.Playing ? 1 : 0;
            }

            return playingStateCount;
        }
    }
}
