using System;
using Nex.KeyboardNavigation;
using UnityEngine;

#nullable enable

namespace Nex
{
    public class MainTopLevelControlPanel : TopLevelControlPanel
    {
        [SerializeField] DismissableButton backButton;
        [SerializeField] DismissableButton exitButton;
        [SerializeField] DismissableButton helpButton;

        [SerializeField] DismissableButton debugSettingsButton;

        #region Controls

        protected override DismissableControl GetDismissableControl(ControlConfig controlIdentifier)
        {
            return controlIdentifier switch
            {
                ControlConfig.DebugSettings => debugSettingsButton.control,
                ControlConfig.Back => backButton.control,
                ControlConfig.Exit => exitButton.control,
                ControlConfig.Help => helpButton.control,

                ControlConfig.None => throw new ArgumentOutOfRangeException(nameof(controlIdentifier), controlIdentifier, null),
                _ => throw new ArgumentOutOfRangeException(nameof(controlIdentifier), controlIdentifier, null)
            };
        }

        protected override KeyResponder? GetKeyResponder(ControlConfig control)
        {
            return control switch
            {
                ControlConfig.None => null,
                ControlConfig.Back => backButton.keyResponder,
                ControlConfig.Exit => exitButton.keyResponder,
                ControlConfig.Help => helpButton.keyResponder,

                ControlConfig.DebugSettings => null,
                _ => throw new ArgumentOutOfRangeException(nameof(control), control, null)
            };
        }

        #endregion

        void Awake()
        {
            backButton.button.onClick.AddListener(HandleBackButton);
            exitButton.button.onClick.AddListener(HandleExitButton);
            helpButton.button.onClick.AddListener(HandleHelpButton);

            #if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
            debugSettingsButton.control.gameObject.SetActive(true);
            debugSettingsButton.button.onClick.AddListener(HandleDebugSettingsButton);
            #endif
        }

        #region Button Handling

        void HandleBackButton()
        {
            if (!Interactable) return;
            InvokeButtonHandler(ButtonKind.Back);
        }

        void HandleExitButton()
        {
            if (!Interactable) return;
            InvokeButtonHandler(ButtonKind.Exit);
        }

        void HandleHelpButton()
        {
            if (!Interactable) return;
            InvokeButtonHandler(ButtonKind.Help);
        }

        void HandleDebugSettingsButton()
        {
            if (!Interactable) return;
            InvokeButtonHandler(ButtonKind.DebugSettings);
        }

        #endregion
    }
}
