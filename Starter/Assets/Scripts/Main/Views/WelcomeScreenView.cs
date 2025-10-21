#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nex
{
    public class WelcomeScreenView : SimpleCanvasView
    {
        #region Variables

        [SerializeField] Button startArGameExampleButton = null!;
        [SerializeField] Button startNonArGameExampleButton = null!;
        [SerializeField] Button exitButton = null!;
        public event Action? OnStartArGameExampleButton;
        public event Action? OnStartNonArGameExampleButton;
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
            startArGameExampleButton.onClick.AddListener(HandleStartArGameExampleButton);
            startNonArGameExampleButton.onClick.AddListener(HandleStartNonArGameExampleButton);
            exitButton.onClick.AddListener(HandleExitButton);
        }

        #endregion

        #region Unity Events

        void HandleStartArGameExampleButton()
        {
            if (!IsActive) return;
            OnStartArGameExampleButton?.Invoke();
        }

        void HandleStartNonArGameExampleButton()
        {
            if (!IsActive) return;
            OnStartNonArGameExampleButton?.Invoke();
        }

        void HandleExitButton()
        {
            if (!IsActive) return;
            OnExitButton?.Invoke();
        }

        #endregion
    }
}
