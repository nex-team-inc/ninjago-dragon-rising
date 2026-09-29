#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Hype meter (GDD v2 §3): a bar that follows Hype 0..1 with tier ticks at the tier thresholds and a callout
    /// ("POWER ×1.4!", "×1.9!!", "MAX!!!") that punches in with a stinger each time a higher tier is reached. Dimmed
    /// while Hype is 0 (no ball in flight). Set may be called every frame; unscaled-time animation.
    /// </summary>
    public sealed class HypeMeterWidget : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [SerializeField] CanvasGroup group = null!;
        [Tooltip("Horizontal filled image.")]
        [SerializeField] Image fill = null!;
        [Tooltip("Tier 1..3 ticks on the bar; lit once their tier is reached.")]
        [SerializeField] Image[] ticks = null!;
        [SerializeField] RectTransform callout = null!;
        [SerializeField] TextLabel calloutLabel = null!;

        float target;
        float shown;
        float alpha;
        int tier;
        Tween? punch;

        void Awake()
        {
            fill.fillAmount = 0f;
            alpha = theme.HypeIdleAlpha;
            group.alpha = alpha;
            Apply(0);
        }

        /// <summary>hype01 in 0..1, tier 0..3.</summary>
        public void Set(float hype01, int newTier)
        {
            target = Mathf.Clamp01(hype01);
            newTier = Mathf.Clamp(newTier, 0, 3);
            if (newTier == tier) return;
            var rising = newTier > tier;
            Apply(newTier);
            if (!rising) return;
            UiTheme.PlaySfx(theme.HypeTierSfx(newTier));
            punch?.Kill(true);
            callout.localScale = Vector3.one;
            punch = callout.DOPunchScale(Vector3.one * theme.HypeTierPunch, 0.35f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
        }

        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var value = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-14f * dt));
            if (Mathf.Abs(value - shown) > 0.001f)
            {
                shown = value;
                fill.fillAmount = value;
            }

            var wantAlpha = target > 0.001f || tier > 0 ? 1f : theme.HypeIdleAlpha;
            var nextAlpha = Mathf.MoveTowards(alpha, wantAlpha, dt * 4f);
            if (!Mathf.Approximately(nextAlpha, alpha))
            {
                alpha = nextAlpha;
                group.alpha = alpha;
            }

            if (tier < 3) return;
            // MAX: the bar flickers between the tier colour and white.
            var flicker = Mathf.PingPong(Time.unscaledTime * 8f, 1f);
            fill.color = Color.Lerp(theme.HypeTierColor(3), Color.white, flicker * 0.6f);
        }

        void Apply(int newTier)
        {
            tier = newTier;
            var color = theme.HypeTierColor(tier);
            fill.color = color;

            for (var i = 0; i < ticks.Length; i++)
            {
                ticks[i].color = i < tier ? theme.Accent : theme.Disabled;
            }

            var hasCallout = tier > 0;
            calloutLabel.gameObject.SetActive(hasCallout);
            if (!hasCallout) return;
            calloutLabel.SetKey(LocKeys.Hud.HypeTiers[tier]);
            calloutLabel.Color = color;
        }
    }
}
