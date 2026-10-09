#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Numbered calibration steps (move in, raise hand, learn the pose, test strike): done steps get a slot frame and a
    /// green number, the current one the gold slot frame and a pulse, upcoming ones stay dim chips. Unscaled time.
    /// </summary>
    public sealed class CalibrationStepPips : MonoBehaviour
    {
        [SerializeField] UiTheme theme = null!;
        [Tooltip("Pip frames in step order.")]
        [SerializeField] Image[] pips = null!;
        [SerializeField] TextLabel[] numbers = null!;
        [Tooltip("Connectors between consecutive pips (pips.Length - 1).")]
        [SerializeField] Image[] links = null!;

        int current = int.MinValue;
        Tween? pulse;

        void Awake()
        {
            for (var i = 0; i < numbers.Length; i++)
            {
                numbers[i].SetNumber(i + 1);
            }
        }

        /// <summary>step = index of the current step; -1 = none started, pips.Length = all done.</summary>
        public void Set(int step)
        {
            if (step == current) return;
            current = step;
            pulse?.Kill();
            for (var i = 0; i < pips.Length; i++)
            {
                var done = i < step;
                var active = i == step;
                pips[i].sprite = active ? theme.SlotActive : done ? theme.Slot : theme.Chip;
                pips[i].color = done || active ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                pips[i].rectTransform.localScale = Vector3.one;
                numbers[i].Color = done ? theme.Positive : active ? theme.Accent : theme.Disabled;
            }

            for (var i = 0; i < links.Length; i++)
            {
                links[i].color = i < step ? theme.Positive : theme.Disabled;
            }

            if (step < 0) return;
            if (step >= pips.Length) return;
            pulse = pips[step].rectTransform.DOScale(theme.ChipPulseScale, theme.ChipPulseDuration).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }
    }
}
