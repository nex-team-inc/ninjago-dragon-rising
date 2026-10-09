#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nex
{
    public class PressButtonToWinView : SimpleCanvasView
    {
        [Header("Win Button")]
        [SerializeField] Button winButton = null!;

        public event Action? WinPressed;
        public event Action? QuitRequested;

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.PressButtonToWin;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Back;
        public override string AnalyticsScreenName => "press-button-to-win";

        public override void OnBackButton()
        {
            if (!IsActive) return;
            QuitRequested?.Invoke();
        }

        #endregion

        #region Initialization

        public void Initialize()
        {
            winButton.onClick.AddListener(HandleWinButton);
        }

        #endregion

        #region Unity Events

        void HandleWinButton()
        {
            if (!IsActive) return;
            WinPressed?.Invoke();
        }

        #endregion
    }
}
