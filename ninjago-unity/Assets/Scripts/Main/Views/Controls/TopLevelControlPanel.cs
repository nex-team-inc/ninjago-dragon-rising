using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nex.KeyboardNavigation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#nullable enable

namespace Nex
{
    // This class contains all the logic for all the buttons / things that the user can click while navigating
    // the song selection screens.
    public abstract class TopLevelControlPanel : MonoBehaviour
    {
        #region Configuration
        [Flags]
        public enum ControlConfig
        {
            None = 0,
            Back = 1 << 0,
            Exit = 1 << 1,
            Help = 1 << 2,

            DebugSettings = 1 << 31,
        }

        public enum ButtonKind
        {
            Back = 0,
            Exit = 1,
            Help = 2,

            DebugSettings = 31,
        }

        public event UnityAction<ButtonKind>? OnButton;

        #endregion

        [Serializable]
        protected struct DismissableButton
        {
            public DismissableControl control;
            public Button button;
            public KeyResponder keyResponder;
        }

        #region Instance Variables

        // Whether this panel is interactable. This is not interactable if it is during a transition.

        public virtual bool Interactable { get; set; }

        #endregion

        #region Controls

        [SerializeField] ControlConfig supportedControls;
        protected ControlConfig currControlConfig = ControlConfig.None;

        const float standardDuration = 0.5f;
        public async UniTask ConfigureControls(ControlConfig newControlConfig, float duration, bool animate = true)
        {
            newControlConfig &= supportedControls;  // Filter out unsupported controls.
            var extraDelay = animate ? Mathf.Max(duration - standardDuration) : 0f;
            var changed = currControlConfig ^ newControlConfig;
            if (changed == ControlConfig.None) return;  // There is nothing to do here.
            // So the two configs are different.
            // We need to figure out which one to hide and which one to show.
            var allAnimations = new List<UniTask>();
            AddAnimationIfNeeded(changed, ControlConfig.Back, extraDelay, animate, allAnimations);
            AddAnimationIfNeeded(changed, ControlConfig.Exit, extraDelay, animate, allAnimations);
            AddAnimationIfNeeded(changed, ControlConfig.Help, extraDelay, animate, allAnimations);
            AddAnimationIfNeeded(changed, ControlConfig.DebugSettings, extraDelay, animate, allAnimations);

            await UniTask.WhenAll(allAnimations);
            currControlConfig ^= changed;
        }

        void AddAnimationIfNeeded(ControlConfig changed, ControlConfig controlIdentifier, float extraDelay, bool animate, ICollection<UniTask> collection)
        {
            if (!changed.HasFlag(controlIdentifier)) return;
            var currShowing = currControlConfig.HasFlag(controlIdentifier);
            var widget = GetDismissableControl(controlIdentifier);
            if (widget == null) return;  // This control is not defined.
            collection.Add(currShowing ? widget.Dismiss(animate) : widget.Present(extraDelay, animate));
        }

        protected abstract DismissableControl? GetDismissableControl(ControlConfig controlIdentifier);

        protected abstract KeyResponder? GetKeyResponder(ControlConfig control);

        public KeyResponder? GetActiveKeyResponder(ControlConfig control)
        {
            return !currControlConfig.HasFlag(control) ? null : GetKeyResponder(control);
        }

        #endregion

        #region Events

        protected void InvokeButtonHandler(ButtonKind kind)
        {
            OnButton?.Invoke(kind);
        }

        #endregion
    }
}
