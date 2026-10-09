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

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            // Its own LocalizedString instance: smart arguments live on the instance and would leak between labels.
            var template = label.StringReference;
            label.StringReference = new LocalizedString(template.TableReference, template.TableEntryReference);
            label.SetSmartStringArgument("player", playerIndex + 1);
            text.color = color;
        }

        #endregion
    }
}
