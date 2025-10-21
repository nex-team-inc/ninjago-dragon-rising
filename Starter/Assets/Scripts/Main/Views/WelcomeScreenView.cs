#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nex
{
    public class WelcomeScreenView : SimpleCanvasView
    {
        #region Variables

        [SerializeField] Button startARGameExampleButton = null!;
        [SerializeField] Button startNonARGameExampleButton = null!;
        [SerializeField] Button exitButton = null!;
        public event Action? OnStartARGameExampleButton;
        public event Action? OnStartNonARGameExampleButton;
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
            startARGameExampleButton.onClick.AddListener(HandleStartARGameExampleButton);
            startNonARGameExampleButton.onClick.AddListener(HandleStartNonARGameExampleButton);
            exitButton.onClick.AddListener(HandleExitButton);
        }

        #endregion

        #region Unity Events

        void HandleStartARGameExampleButton()
        {
            if (!IsActive) return;
            OnStartARGameExampleButton?.Invoke();
        }

        void HandleStartNonARGameExampleButton()
        {
            if (!IsActive) return;
            OnStartNonARGameExampleButton?.Invoke();
        }

        void HandleExitButton()
        {
            if (!IsActive) return;
            OnExitButton?.Invoke();
        }

        #endregion
    }
}
