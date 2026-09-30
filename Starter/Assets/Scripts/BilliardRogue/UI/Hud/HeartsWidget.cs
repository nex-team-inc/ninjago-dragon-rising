#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Player HP as a row of hearts (GDD v2 §22): max hearts shown, the current ones lit and the lost ones dimmed.
    /// Shakes and pops a heart on damage, and the last heart pulses at 1 HP. No panel behind it (bottom-left of the HUD).
    /// Set ignores unchanged values, so GameSession may push every frame.
    /// </summary>
    public sealed class HeartsWidget : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [SerializeField] RectTransform shakeTarget = null!;
        [Tooltip("Left to right; as many as the max HP the game uses are shown, the rest stay hidden.")]
        [SerializeField] Image[] hearts = System.Array.Empty<Image>();

        static readonly Color Lost = new(1f, 1f, 1f, 0.22f);

        int current = -1;
        int maximum = -1;
        Vector2 shakeBase;
        Tween? shakeTween;
        Tween? pulseTween;

        void Awake()
        {
            shakeBase = shakeTarget.anchoredPosition;
        }

        public void Set(int hp, int maxHp)
        {
            if (hp == current && maxHp == maximum) return;
            var damaged = current >= 0 && hp < current;
            current = hp;
            maximum = maxHp;

            for (var i = 0; i < hearts.Length; i++)
            {
                var shown = i < maxHp;
                hearts[i].gameObject.SetActive(shown);
                if (shown) hearts[i].color = i < hp ? Color.white : Lost;
            }

            pulseTween?.Kill();
            var lowIndex = hp - 1;
            if (hp > 0 && hp <= 1 && lowIndex < hearts.Length)
            {
                var t = hearts[lowIndex].transform;
                t.localScale = Vector3.one;
                pulseTween = t.DOScale(1.18f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                    .SetUpdate(true).SetLink(gameObject);
            }

            if (!damaged) return;
            shakeTween?.Kill();
            shakeTarget.anchoredPosition = shakeBase;
            shakeTween = shakeTarget.DOShakeAnchorPos(0.35f, new Vector2(14f, 8f), 22, 90f, false)
                .SetUpdate(true).SetLink(gameObject).OnComplete(() => shakeTarget.anchoredPosition = shakeBase);
            if (hp >= 0 && hp < hearts.Length + 1 && hp < maxHp + 1)
            {
                var hit = Mathf.Clamp(hp, 0, hearts.Length - 1);
                hearts[hit].transform.DOPunchScale(Vector3.one * 0.4f, 0.3f, 8, 0.6f).SetUpdate(true).SetLink(gameObject);
            }
        }
    }
}
