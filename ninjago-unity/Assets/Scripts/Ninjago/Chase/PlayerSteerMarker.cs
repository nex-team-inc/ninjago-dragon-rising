#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>Badge over the shared vehicle: the player's tag plus an arrow showing that player's own lean.</summary>
    public class PlayerSteerMarker : MonoBehaviour
    {
        [Header("Player Tag")]
        [SerializeField] PlayerTagLabel playerTag = null!;
        [Header("Steer Arrow")]
        [Tooltip("Arrow sprite pointing right; rotated toward the steer and scaled by its strength.")]
        [SerializeField] RectTransform arrow = null!;
        [Header("Steer Arrow Image")]
        [SerializeField] Image arrowImage = null!;
        [Header("Untracked Alpha")]
        [SerializeField, Range(0f, 1f)] float untrackedAlpha = 0.25f;

        Color color;

        #region Initialization

        public void Initialize(int playerIndex, Color aColor)
        {
            color = aColor;
            playerTag.Initialize(playerIndex, aColor);
            SetSteer(Vector2.zero, false);
        }

        #endregion

        #region Public API

        public void SetSteer(Vector2 steer, bool tracked)
        {
            var strength = Mathf.Clamp01(steer.magnitude);
            arrow.gameObject.SetActive(strength > 0.05f);
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(steer.y, steer.x) * Mathf.Rad2Deg);
            arrow.localScale = new Vector3(0.4f + 0.6f * strength, 1f, 1f);
            arrowImage.color = tracked ? color : new Color(color.r, color.g, color.b, untrackedAlpha);
        }

        #endregion
    }
}
