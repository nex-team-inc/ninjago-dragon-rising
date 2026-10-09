#nullable enable

using Nex.Dev;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Keyboard driver for SimulatedBody. Writes only on key changes so CLI-set values persist.</summary>
    public class SimulatedBodyKeyboard : MonoBehaviour
    {
        [Header("Simulated Lean")]
        [Tooltip("Body inches a held lean key moves the simulated chest sideways.")]
        [SerializeField] float leanInches = 8f;
        [Header("Simulated Rise")]
        [Tooltip("Body inches a held rise/drop key moves the simulated chest up or down.")]
        [SerializeField] float riseInches = 6f;

        static readonly KeyCode[][] keysByPlayer =
        {
            new[] { KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S, KeyCode.Space },
            new[] { KeyCode.J, KeyCode.L, KeyCode.I, KeyCode.K, KeyCode.RightShift },
        };

        void Update()
        {
            if (!SimulatedBody.IsEnabled) return;
            for (var playerIndex = 0; playerIndex < keysByPlayer.Length; playerIndex++)
            {
                var keys = keysByPlayer[playerIndex];
                if (!AnyKeyChanged(keys)) continue;
                var x = (DebugInput.GetKey(keys[1]) ? leanInches : 0f) - (DebugInput.GetKey(keys[0]) ? leanInches : 0f);
                var y = (DebugInput.GetKey(keys[2]) ? riseInches : 0f) - (DebugInput.GetKey(keys[3]) ? riseInches : 0f);
                SimulatedBody.Set(playerIndex, x, y, DebugInput.GetKey(keys[4]));
            }
        }

        static bool AnyKeyChanged(KeyCode[] keys)
        {
            foreach (var key in keys)
            {
                if (DebugInput.GetKeyDown(key) || DebugInput.GetKeyUp(key)) return true;
            }

            return false;
        }
    }
}
