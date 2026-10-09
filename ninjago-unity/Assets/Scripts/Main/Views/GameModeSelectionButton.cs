#nullable enable

using System;
using Nex.KeyboardNavigation;
using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex
{
    public class GameModeSelectionButton : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] Button button = null!;
        [SerializeField] ButtonKeyResponder keyResponder = null!;

        [Header("Label")]
        [SerializeField] NexLocalizedString label = null!;

        public event Action<GameModeType>? Clicked;

        public KeyResponder KeyResponder => keyResponder;

        GameModeType mode;

        #region Initialization

        public void Initialize(GameModeType aMode, LocalizedString displayName)
        {
            mode = aMode;
            label.StringReference = displayName;
            button.onClick.AddListener(HandleClick);
        }

        #endregion

        #region Unity Events

        void HandleClick()
        {
            Clicked?.Invoke(mode);
        }

        #endregion
    }
}
