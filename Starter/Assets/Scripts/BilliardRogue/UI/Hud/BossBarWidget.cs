#nullable enable

using DG.Tweening;
using Nex.BilliardRogue.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>Boss name + HP bar (numbers, draining ghost, small shake per hit); fades in/out with visibility.</summary>
    public sealed class BossBarWidget : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [SerializeField] CanvasGroup group = null!;
        [SerializeField] TextLabel nameLabel = null!;
        [SerializeField] Image fill = null!;
        [SerializeField] Image ghostFill = null!;
        [SerializeField] TextLabel numbers = null!;
        [SerializeField] RectTransform shakeTarget = null!;

        bool visible;
        int current = -1;
        int maximum = -1;
        EnemyType? shownType;
        Vector2 shakeBase;
        Tween? fadeTween;
        Tween? fillTween;
        Tween? ghostTween;
        Tween? shakeTween;

        // Authored inactive: Awake runs on the first SetVisible(true), right before the fade-in.
        void Awake()
        {
            shakeBase = shakeTarget.anchoredPosition;
            group.alpha = 0f;
        }

        public void Set(bool show, int hp, int maxHp, EnemyType type)
        {
            SetVisible(show);
            if (!show) return;
            if (shownType != type)
            {
                shownType = type;
                nameLabel.SetKey(LocKeys.Enemy.Name(type));
                current = -1;
            }

            if (hp == current && maxHp == maximum) return;
            var hit = current >= 0 && hp < current;
            var first = current < 0;
            current = hp;
            maximum = maxHp;
            numbers.SetNumbers("{0}/{1}", hp, maxHp);
            var fraction = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            fillTween?.Kill();
            ghostTween?.Kill();
            if (first)
            {
                fill.fillAmount = fraction;
                ghostFill.fillAmount = fraction;
                return;
            }

            fillTween = fill.DOFillAmount(fraction, theme.BarTweenDuration).SetUpdate(true).SetLink(gameObject);
            ghostTween = ghostFill.DOFillAmount(fraction, theme.BarTweenDuration * 2f).SetDelay(theme.BarTweenDuration)
                .SetUpdate(true).SetLink(gameObject);
            if (!hit) return;
            shakeTween?.Kill();
            shakeTarget.anchoredPosition = shakeBase;
            shakeTween = shakeTarget.DOShakeAnchorPos(theme.DamageShakeDuration * 0.6f, theme.DamageShakeStrength * 0.5f, 30, 90f, true)
                .SetUpdate(true).SetLink(gameObject).OnKill(ResetShake);
        }

        void SetVisible(bool show)
        {
            if (visible == show) return;
            visible = show;
            fadeTween?.Kill();
            if (show) gameObject.SetActive(true);
            fadeTween = group.DOFade(show ? 1f : 0f, theme.PresentDuration).SetUpdate(true).SetLink(gameObject)
                .OnComplete(show ? null : Hide);
        }

        void Hide()
        {
            gameObject.SetActive(false);
            shownType = null;
        }

        void ResetShake()
        {
            shakeTarget.anchoredPosition = shakeBase;
        }
    }
}
