#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>A setup ring in one player's color that fills while that player's cursor rests inside it.</summary>
    public class HandCursorTarget : MonoBehaviour
    {
        [Header("Rect")]
        [SerializeField] RectTransform rect = null!;
        [Header("Ring")]
        [SerializeField] Image ring = null!;
        [Header("Radial Fill")]
        [SerializeField] Image fill = null!;
        [Header("Player Tag")]
        [SerializeField] PlayerTagLabel playerTag = null!;
        [Header("Done Mark")]
        [SerializeField] GameObject doneMark = null!;

        #region Initialization

        public void Initialize(int playerIndex, Color color, Vector2 localPosition)
        {
            rect.anchoredPosition = localPosition;
            ring.color = color;
            fill.color = color;
            playerTag.Initialize(playerIndex, color);
            SetFill(0f);
            doneMark.SetActive(false);
        }

        #endregion

        #region Public API

        public bool Contains(Vector2 localPosition) => (localPosition - rect.anchoredPosition).magnitude <= rect.rect.width * 0.5f;

        public void SetFill(float fill01)
        {
            fill.fillAmount = fill01;
        }

        public void ShowDone()
        {
            SetFill(1f);
            doneMark.SetActive(true);
        }

        #endregion
    }
}
