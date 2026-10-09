#nullable enable

using System;
using Nex.Localization;
using Nex.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Nex
{
    public class WelcomeScreenView : SimpleCanvasView
    {
        #region Variables

        [SerializeField] Button startARGameButton = null!;
        [SerializeField] Button startNonARGameButton = null!;
        [SerializeField] Button exitButton = null!;

        [SerializeField] NexLocalizedString versionLabel = null!;
        public event Action? OnStartARGameButton;
        public event Action? OnStartNonARGameButton;
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
            startARGameButton.onClick.AddListener(HandleStartARGameButton);
            startNonARGameButton.onClick.AddListener(HandleStartNonARGameButton);
            exitButton.onClick.AddListener(HandleExitButton);

            versionLabel.StringReference.GetLocalizedString();
            versionLabel.SetSmartStringArgument("0", AppInfo.Instance.VersionDisplayString);
        }

        #endregion

        #region Unity Events

        void HandleStartARGameButton()
        {
            if (!IsActive) return;
            OnStartARGameButton?.Invoke();
        }

        void HandleStartNonARGameButton()
        {
            if (!IsActive) return;
            OnStartNonARGameButton?.Invoke();
        }

        void HandleExitButton()
        {
            if (!IsActive) return;
            OnExitButton?.Invoke();
        }

        #endregion
    }
}
