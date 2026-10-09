#nullable enable

using System;
using Nex.KeyboardNavigation;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// One settings line: Left/Right (and Enter, which steps forward) change its value, Up/Down bubble to the list.
    /// Shows either a text value (TextLabel) or a 0..1 bar with a percentage.
    /// </summary>
    [AddComponentMenu("Nex/Billiard Rogue/Setting Row")]
    public sealed class SettingRow : KeyResponder
    {
        [SerializeField] FocusHighlight highlight = null!;
        [Tooltip("Text value (language, on/off, short/normal/long); optional when the row is a bar.")]
        [SerializeField] TextLabel? valueLabel;
        [Tooltip("Filled bar for 0..1 values; optional.")]
        [SerializeField] Image? bar;
        [SerializeField] TextLabel? percentLabel;

        /// <summary>(row, direction -1/+1). Raised on Left/Right and on Enter (+1).</summary>
        public event Action<SettingRow, int>? Stepped;

        public TextLabel? ValueLabel => valueLabel;

        #region Key Responder

        public override NavigationResult HandleNavigation(NavigationKey key)
        {
            switch (key)
            {
                case NavigationKey.Left:
                    Stepped?.Invoke(this, -1);
                    return new NavigationResult(RectTransform);
                case NavigationKey.Right:
                    Stepped?.Invoke(this, 1);
                    return new NavigationResult(RectTransform);
                default:
                    return new NavigationResult(key);
            }
        }

        public override bool HandleEnter()
        {
            Stepped?.Invoke(this, 1);
            return true;
        }

        public override bool HandleBack() => false;

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
            highlight.SetFocused(Activated && EnableHighlighting);
            return result;
        }

        #endregion

        #region Public Methods

        /// <summary>Bar rows: fill 0..1 and the rounded percentage.</summary>
        public void SetFraction(float value01)
        {
            bar!.fillAmount = value01;
            percentLabel!.SetNumber(Mathf.RoundToInt(value01 * 100f), "{0}%");
        }

        #endregion
    }
}
