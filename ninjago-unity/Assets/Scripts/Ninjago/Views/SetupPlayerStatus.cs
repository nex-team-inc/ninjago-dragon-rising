#nullable enable

using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>One player's calibration state under the camera previews: "P1  Step in / Hold still / Ready!".</summary>
    public class SetupPlayerStatus : MonoBehaviour
    {
        public enum State
        {
            Waiting,
            Holding,
            Ready,
        }

        [Header("Player Tag")]
        [SerializeField] PlayerTagLabel playerTag = null!;
        [Header("Status Label")]
        [SerializeField] NexLocalizedString statusLabel = null!;
        [Header("Ready Check")]
        [SerializeField] Image readyCheck = null!;
        [Header("Waiting Text")]
        [SerializeField] LocalizedString waitingText = new();
        [Header("Holding Text")]
        [SerializeField] LocalizedString holdingText = new();
        [Header("Ready Text")]
        [SerializeField] LocalizedString readyText = new();

        State? shown;

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            playerTag.Initialize(playerIndex, color);
            readyCheck.color = color;
            Show(State.Waiting);
        }

        #endregion

        #region Public API

        public void Show(State state)
        {
            if (shown == state) return;
            shown = state;
            statusLabel.StringReference = state switch
            {
                State.Holding => holdingText,
                State.Ready => readyText,
                _ => waitingText,
            };
            readyCheck.enabled = state == State.Ready;
        }

        #endregion
    }
}
