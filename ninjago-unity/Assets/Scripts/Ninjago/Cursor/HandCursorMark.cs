#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>One cursor shape: a dark rim, a disc in the player's color and a white center dot.</summary>
    public class HandCursorMark : MonoBehaviour
    {
        [Header("Rect")]
        [SerializeField] RectTransform rect = null!;
        [Header("Player Color Disc")]
        [SerializeField] Image disc = null!;

        public RectTransform Rect => rect;

        #region Initialization

        public void Initialize(Color playerColor)
        {
            disc.color = playerColor;
        }

        #endregion
    }
}
