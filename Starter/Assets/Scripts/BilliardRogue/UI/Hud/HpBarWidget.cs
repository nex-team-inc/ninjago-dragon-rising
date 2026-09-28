#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>Player HP: heart, filled bar with a draining damage ghost, "24/30" numbers, shake on damage, low-HP pulse.</summary>
    public sealed class HpBarWidget : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [SerializeField] Image fill = null!;
        [Tooltip("Pale copy of the fill that drains after the real fill (shows the chunk just lost).")]
        [SerializeField] Image ghostFill = null!;
        [SerializeField] TextLabel numbers = null!;
        [SerializeField] RectTransform shakeTarget = null!;
        [SerializeField] RectTransform heart = null!;

        int current = -1;
        int maximum = -1;
        bool low;
        Vector2 shakeBase;
        Tween? fillTween;
        Tween? ghostTween;
        Tween? shakeTween;
        Tween? heartTween;
        Tween? colorTween;

        void Awake()
        {
            shakeBase = shakeTarget.anchoredPosition;
        }

        public void Set(int hp, int maxHp)
        {
            if (hp == current && maxHp == maximum) return;
            var first = current < 0;
            var damaged = !first && hp < current;
            var healed = !first && hp > current;
            current = hp;
            maximum = maxHp;
            numbers.SetNumbers("{0}/{1}", hp, maxHp);
            var fraction = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            low = hp > 0 && fraction <= theme.LowHpFraction;

            fillTween?.Kill();
            ghostTween?.Kill();
            if (first)
            {
                fill.fillAmount = fraction;
                ghostFill.fillAmount = fraction;
            }
            else
            {
                fillTween = fill.DOFillAmount(fraction, theme.BarTweenDuration).SetUpdate(true).SetLink(gameObject);
                ghostTween = ghostFill.DOFillAmount(fraction, theme.BarTweenDuration * 2f)
                    .SetDelay(damaged ? theme.BarTweenDuration : 0f).SetUpdate(true).SetLink(gameObject);
            }

            if (damaged) PlayDamage();
            if (healed) Flash(theme.Positive);
            if (!damaged && !healed) numbers.Color = low ? theme.Danger : theme.TextPrimary;
            UpdateHeartPulse();
        }

        void PlayDamage()
        {
            shakeTween?.Kill();
            shakeTarget.anchoredPosition = shakeBase;
            shakeTween = shakeTarget.DOShakeAnchorPos(theme.DamageShakeDuration, theme.DamageShakeStrength, 30, 90f, true)
                .SetUpdate(true).SetLink(gameObject).OnKill(ResetShake);
            Flash(theme.Danger);
        }

        void Flash(Color color)
        {
            colorTween?.Kill();
            numbers.Color = color;
            var settled = low ? theme.Danger : theme.TextPrimary;
            colorTween = numbers.Face.DOColor(settled, theme.DamageShakeDuration * 1.5f).SetDelay(theme.DamageShakeDuration)
                .SetUpdate(true).SetLink(gameObject);
        }

        void UpdateHeartPulse()
        {
            heartTween?.Kill();
            heart.localScale = Vector3.one;
            if (!low) return;
            heartTween = heart.DOScale(theme.ChipPulseScale * 1.08f, theme.ChipPulseDuration * 0.6f).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }

        void ResetShake()
        {
            shakeTarget.anchoredPosition = shakeBase;
        }
    }
}
