#nullable enable

using Nex.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>"P1" / "P2" in the player's color, through the localized smart string with a {player} argument.</summary>
    public class PlayerTagLabel : MonoBehaviour
    {
        [Header("Label")]
        [SerializeField] NexLocalizedString label = null!;
        [Header("Label Text")]
        [SerializeField] TMP_Text text = null!;
        [Header("Tag Text")]
        [Tooltip("Smart string with {player}.")]
        [SerializeField] LocalizedString tagText = new();

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            label.StringReference = LocalizedStrings.WithArguments(tagText, ("player", playerIndex + 1));
            text.color = color;
        }

        #endregion
    }
}
