#nullable enable

using DG.Tweening;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Small HUD tag (fast-forward, power ready, bonus balls, tracking warning): pops in, optionally pulses.</summary>
    public sealed class HudChip : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [SerializeField] CanvasGroup group = null!;
        [SerializeField] TextLabel label = null!;
        [Tooltip("Pulse while shown (warnings, fast-forward).")]
        [SerializeField] bool pulse = true;

        bool visible;
        Tween? showTween;
        Tween? pulseTween;

        public TextLabel Label => label;

        // Authored inactive: Awake runs on the first SetVisible(true).
        void Awake()
        {
            group.alpha = 0f;
        }

        public void SetVisible(bool show)
        {
            if (visible == show) return;
            visible = show;
            showTween?.Kill();
            pulseTween?.Kill();
            var target = transform;
            if (show)
            {
                gameObject.SetActive(true);
                target.localScale = Vector3.one * theme.PresentScaleFrom;
                showTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                    .Join(group.DOFade(1f, theme.PresentDuration))
                    .Join(target.DOScale(1f, theme.PresentDuration).SetEase(Ease.OutBack))
                    .OnComplete(StartPulse);
                return;
            }

            showTween = group.DOFade(0f, theme.DismissDuration).SetUpdate(true).SetLink(gameObject).OnComplete(Hide);
        }

        void StartPulse()
        {
            if (!pulse) return;
            pulseTween = transform.DOScale(theme.ChipPulseScale, theme.ChipPulseDuration).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }

        void Hide()
        {
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }
    }
}
