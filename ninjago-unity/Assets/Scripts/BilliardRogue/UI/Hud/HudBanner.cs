#nullable enable

using DG.Tweening;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Transient ribbon ("Turn 3 — shoot!", "P2's shot!") that slides down, holds, and slides away (unscaled).</summary>
    public sealed class HudBanner : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [SerializeField] RectTransform banner = null!;
        [SerializeField] CanvasGroup group = null!;
        [SerializeField] TextLabel label = null!;

        Vector2 shownPosition;
        Tween? sequence;

        public TextLabel Label => label;

        // Authored inactive: Awake runs on the first Show.
        void Awake()
        {
            shownPosition = banner.anchoredPosition;
            group.alpha = 0f;
        }

        /// <summary>Label must be set just before (label.SetKey); holdSeconds excludes the slide in/out.</summary>
        public void Show(float holdSeconds)
        {
            gameObject.SetActive(true);
            sequence?.Kill();
            var hidden = shownPosition + new Vector2(0f, theme.BannerSlideDistance);
            var slide = theme.BannerSlideDuration;
            banner.anchoredPosition = hidden;
            group.alpha = 0f;
            sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(banner.DOAnchorPos(shownPosition, slide).SetEase(Ease.OutBack))
                .Join(group.DOFade(1f, slide * 0.6f))
                .AppendInterval(holdSeconds)
                .Append(banner.DOAnchorPos(hidden, slide).SetEase(Ease.InQuad))
                .Join(group.DOFade(0f, slide))
                .OnComplete(Hide);
        }

        void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
