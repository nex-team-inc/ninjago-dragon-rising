#nullable enable

using Nex.KeyboardNavigation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>ButtonKeyResponder with the Billiard Rogue focus look (frame swap, pulse, paw cursor) and move sound.</summary>
    [AddComponentMenu("Nex/Billiard Rogue/UI Button Key Responder")]
    public sealed class UiButtonKeyResponder : ButtonKeyResponder
    {
        [SerializeField] FocusHighlight highlight = null!;

        public override bool EnableHighlighting
        {
            set
            {
                base.EnableHighlighting = value;
                highlight.Refresh(this);
            }
        }

        public override bool Activate(IKeyboardNavigationContext? context, bool isFocused)
        {
            var activated = base.Activate(context, isFocused);
            highlight.Refresh(this);
            return activated;
        }

        public override void Deactivate(IKeyboardNavigationContext? context)
        {
            base.Deactivate(context);
            highlight.SetFocused(false);
        }

        public override void LoseFocus()
        {
            base.LoseFocus();
            highlight.Refresh(this);
        }

        protected override RectTransform? OnFocusRequested(NavigationKey key = NavigationKey.None)
        {
            var result = base.OnFocusRequested(key);
            highlight.NotifyFocusRequested(key);
            // Focused flips after this returns; the highlight follows the request itself.
            highlight.SetFocused(Activated && EnableHighlighting);
            return result;
        }
    }
}
