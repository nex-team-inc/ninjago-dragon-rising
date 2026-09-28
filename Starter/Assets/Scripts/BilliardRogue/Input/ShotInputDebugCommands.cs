#nullable enable

#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The input half of DebugHooks (TDD D7): SetBot(bool) toggles DebugSettings.autoAimBot for this session (not
    /// saved), which every ShotInputRouter reads live. Registered at startup so `SetBot(true)` before `StartNewRun`
    /// also lets the bot pass the calibration test strike.
    /// </summary>
    public static class ShotInputDebugCommands
    {
        static readonly Func<bool, bool> setBot = SetBot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            DebugHooks.SetBotHandler = setBot;
        }

        static bool SetBot(bool enabled)
        {
            var data = PlayerDataManager.Instance;
            if (data == null) return false;
            data.DebugSettings.autoAimBot = enabled;
            return true;
        }
    }
}
#endif
