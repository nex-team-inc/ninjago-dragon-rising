#nullable enable

using Nex.Dev;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Keyboard and mouse driver for SimulatedBody. Writes only on input changes so CLI-set values persist: the mouse
    /// moves player 1's right-hand cursor, I/J/K/L move player 2's, and the storm keys also pulse that player's knee.
    /// </summary>
    public class SimulatedBodyKeyboard : MonoBehaviour
    {
        [Header("Simulated Lean")]
        [Tooltip("Body inches a held lean key moves the simulated chest sideways.")]
        [SerializeField] float leanInches = 8f;
        [Header("Simulated Rise")]
        [Tooltip("Body inches a held rise/drop key moves the simulated chest up or down.")]
        [SerializeField] float riseInches = 6f;
        [Header("Player 2 Cursor Speed")]
        [Tooltip("Screen widths per second a held I/J/K/L key moves player 2's simulated right-hand cursor.")]
        [SerializeField] float playerTwoCursorSpeed = 1.2f;

        static readonly KeyCode[][] keysByPlayer =
        {
            new[] { KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S, KeyCode.Space },
            new[] { KeyCode.J, KeyCode.L, KeyCode.I, KeyCode.K, KeyCode.RightShift },
        };

        Vector3 lastMousePosition;

        void Update()
        {
            if (!SimulatedBody.IsEnabled) return;
            for (var playerIndex = 0; playerIndex < keysByPlayer.Length; playerIndex++)
            {
                var keys = keysByPlayer[playerIndex];
                if (DebugInput.GetKeyDown(keys[4])) SimulatedBody.PulseKnee(playerIndex);
                if (!AnyKeyChanged(keys)) continue;
                var x = (DebugInput.GetKey(keys[1]) ? leanInches : 0f) - (DebugInput.GetKey(keys[0]) ? leanInches : 0f);
                var y = (DebugInput.GetKey(keys[2]) ? riseInches : 0f) - (DebugInput.GetKey(keys[3]) ? riseInches : 0f);
                SimulatedBody.Set(playerIndex, x, y, DebugInput.GetKey(keys[4]));
            }

            DriveMouseCursor();
            DrivePlayerTwoCursor();
        }

        void DriveMouseCursor()
        {
            var mouse = DebugInput.MousePosition;
            if (mouse == lastMousePosition) return;
            lastMousePosition = mouse;
            SimulatedBody.SetHand(0, 1, mouse.x / Screen.width, mouse.y / Screen.height);
        }

        void DrivePlayerTwoCursor()
        {
            var keys = keysByPlayer[1];
            var direction = new Vector2(
                (DebugInput.GetKey(keys[1]) ? 1f : 0f) - (DebugInput.GetKey(keys[0]) ? 1f : 0f),
                (DebugInput.GetKey(keys[2]) ? 1f : 0f) - (DebugInput.GetKey(keys[3]) ? 1f : 0f));
            if (direction == Vector2.zero) return;
            var current = SimulatedBody.TryGetHand(1, 1, out var position) ? position : new Vector2(0.75f, 0.5f);
            var step = direction * (playerTwoCursorSpeed * Time.unscaledDeltaTime);
            step.y *= (float)Screen.width / Screen.height;
            var next = current + step;
            SimulatedBody.SetHand(1, 1, Mathf.Clamp01(next.x), Mathf.Clamp01(next.y));
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
