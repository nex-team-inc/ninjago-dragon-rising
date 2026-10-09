#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Debug stand-in for a tracked body (Debug Settings "Ninja: Simulated Body"), so both mini-games run without a
    /// camera. SimulatedBodyKeyboard drives it from keys; the Unity CLI can call Set directly.
    /// </summary>
    public static class SimulatedBody
    {
        public const int MaxPlayers = 2;

        static readonly Vector2[] leanInches = new Vector2[MaxPlayers];
        static readonly bool[] storming = new bool[MaxPlayers];

        public static bool IsEnabled
        {
            get
            {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
                return PlayerDataManager.Instance.DebugSettings.ninjaSimulatedBody;
#else
                return false;
#endif
            }
        }

        #region Public API

        public static Vector2 GetLeanInches(int playerIndex) => leanInches[playerIndex];

        public static bool IsStorming(int playerIndex) => storming[playerIndex];

        /// <summary>Lean in body inches from the calibrated origin (x right, y up) and whether both hands storm.</summary>
        public static void Set(int playerIndex, float leanX, float leanY, bool handsStorming)
        {
            leanInches[playerIndex] = new Vector2(leanX, leanY);
            storming[playerIndex] = handsStorming;
        }

        #endregion
    }
}
