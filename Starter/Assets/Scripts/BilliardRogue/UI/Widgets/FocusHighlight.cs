#nullable enable

using DG.Tweening;
using Nex.KeyboardNavigation;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Remote/keyboard focus look of one selectable: focused frame sprite (or a glow object), a scale pulse and the
    /// bobbing paw cursor. Driven by the element's key responder; all tweens run on unscaled time (pause-safe).
    /// </summary>
    public sealed class FocusHighlight : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [Tooltip("Pulsed while focused (usually the selectable itself).")]
        [SerializeField] Transform pulseTarget = null!;
        [Tooltip("Frame whose sprite switches to the focused sprite; optional.")]
        [SerializeField] Image? frame;
        [SerializeField] Sprite? focusedSprite;
        [Tooltip("Shown only while focused (glow frame behind cards); optional.")]
        [SerializeField] GameObject? glow;
        [Tooltip("Paw cursor next to the element; bobs horizontally while focused. Optional.")]
        [SerializeField] RectTransform? cursor;
        [Tooltip("Graphics tinted with the theme accent while focused (labels, arrows); optional.")]
        [SerializeField] Graphic[] accentGraphics = System.Array.Empty<Graphic>();
        [Tooltip("Share of the theme focus scale applied here (wide rows pulse less).")]
        [SerializeField, Range(0f, 1f)] float scaleWeight = 1f;

        Sprite? normalSprite;
        Color[] accentBaseColors = System.Array.Empty<Color>();
        Vector3 baseScale;
        Vector2 cursorBase;
        bool focused;
        Tween? pulse;
        Tween? bob;

        #region Life Cycle

        void Awake()
        {
            baseScale = pulseTarget.localScale;
            if (frame != null) normalSprite = frame.sprite;
            if (cursor != null) cursorBase = cursor.anchoredPosition;
            accentBaseColors = new Color[accentGraphics.Length];
            for (var i = 0; i < accentGraphics.Length; i++)
            {
                accentBaseColors[i] = accentGraphics[i].color;
            }

            ApplyStatic(false);
        }

        void OnDisable()
        {
            SetFocused(false);
        }

        #endregion

        #region Public Methods

        /// <summary>Re-evaluates the look from the responder state (focused, activated, highlighting enabled).</summary>
        public void Refresh(KeyResponder responder)
        {
            SetFocused(responder.Focused && responder.Activated && responder.EnableHighlighting);
        }

        /// <summary>Plays the move sound when focus arrived through a navigation key (not on activation).</summary>
        public void NotifyFocusRequested(KeyResponder.NavigationKey key)
        {
            if (key == KeyResponder.NavigationKey.None) return;
            UiTheme.PlaySfx(theme.MoveSfx);
        }

        public void SetFocused(bool value)
        {
            if (focused == value) return;
            focused = value;
            ApplyStatic(value);
            StopTweens();
            if (!value || !isActiveAndEnabled) return;

            var grow = (theme.FocusScale - 1f) * scaleWeight;
            pulseTarget.localScale = baseScale * (1f + grow);
            pulse = pulseTarget.DOScale(baseScale * (1f + grow * 0.4f), theme.FocusPulseDuration)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
            if (cursor == null) return;
            bob = cursor.DOAnchorPosX(cursorBase.x - theme.CursorBobDistance, theme.CursorBobDuration)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }

        #endregion

        #region Helpers

        void ApplyStatic(bool on)
        {
            if (frame != null && focusedSprite != null) frame.sprite = on ? focusedSprite : normalSprite;
            if (glow != null) glow.SetActive(on);
            if (cursor != null) cursor.gameObject.SetActive(on);
            for (var i = 0; i < accentGraphics.Length; i++)
            {
                accentGraphics[i].color = on ? theme.Accent : accentBaseColors[i];
            }
        }

        void StopTweens()
        {
            pulse?.Kill();
            bob?.Kill();
            pulse = null;
            bob = null;
            pulseTarget.localScale = baseScale;
            if (cursor != null) cursor.anchoredPosition = cursorBase;
        }

        #endregion
    }
}
