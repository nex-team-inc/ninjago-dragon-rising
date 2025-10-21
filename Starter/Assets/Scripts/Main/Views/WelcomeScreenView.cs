#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nex
{
    public class WelcomeScreenView : SimpleCanvasView
    {
        #region Variables

        [SerializeField] Button startButton = null!;
        [SerializeField] Button exitButton = null!;

        public bool shouldGreetPlayer = true;
        public event Action? OnStartButton;
        public event Action? OnExitButton;

        #endregion

        #region View Implementation

        public override ViewIdentifier Identifier => ViewIdentifier.WelcomeScreen;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.Exit;

        public override string AnalyticsScreenName => "title";

        #endregion

        #region Lifecycle

        public void Initialize()
        {
            startButton.onClick.AddListener(HandleStartButton);
            exitButton.onClick.AddListener(HandleExitButton);
        }

        #endregion

        #region Unity Events

        void HandleStartButton()
        {
            if (!IsActive) return;
            OnStartButton?.Invoke();
        }

        void HandleExitButton()
        {
            if (!IsActive) return;
            OnExitButton?.Invoke();
        }

        #endregion
    }
}
