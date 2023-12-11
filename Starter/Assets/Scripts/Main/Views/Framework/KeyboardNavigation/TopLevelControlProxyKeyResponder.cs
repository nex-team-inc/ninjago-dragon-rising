using UnityEngine;

#nullable enable

namespace Nex.KeyboardNavigation
{
    public class TopLevelControlProxyKeyResponder : KeyResponder
    {
        [SerializeField] TopLevelControlPanel.ControlConfig control;

        KeyResponder? delegateResponder;

        public override bool EnableHighlighting
        {
            set
            {
                base.EnableHighlighting = value;
                if (delegateResponder != null)
                {
                    delegateResponder.EnableHighlighting = value;
                }
            }
        }

        public override bool Activate(IKeyboardNavigationContext context, bool isFocused)
        {
            if (!(context is ViewManagerKeyboardNavigationContext viewManagerContext)) return false;
            var controlResponder = viewManagerContext.GetTopLevelControlKeyResponder(control);
            if (controlResponder == null) return false;
            delegateResponder = controlResponder;
            delegateResponder.EnableHighlighting = EnableHighlighting;
            return delegateResponder.Activate(context, isFocused);
        }

        public override void Deactivate(IKeyboardNavigationContext context)
        {
            if (delegateResponder == null) return;
            delegateResponder.Deactivate(context);
            delegateResponder = null!;
        }

        public override void HandleEnter()
        {
            if (delegateResponder != null)
            {
                delegateResponder.HandleEnter();
            }
        }

        public override void HandleBack()
        {
            if (delegateResponder != null)
            {
                delegateResponder.HandleBack();
            }
        }

        public override NavigationResult HandleNavigation(NavigationKey key)
        {
            return delegateResponder == null ? new NavigationResult(key) : delegateResponder.HandleNavigation(key);
        }

        public override RectTransform GainFocus(NavigationKey? key)
        {
            return delegateResponder == null ? RectTransform : delegateResponder.GainFocus(key);
        }

        public override void LoseFocus()
        {
            if (delegateResponder != null)
            {
                delegateResponder.LoseFocus();
            }
        }
    }
}
