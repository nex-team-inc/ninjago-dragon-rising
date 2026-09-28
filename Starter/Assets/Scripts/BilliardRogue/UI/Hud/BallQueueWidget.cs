#nullable enable

using System.Collections.Generic;
using DG.Tweening;
using Nex.BilliardRogue.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Ball bag in firing order (grid of fixed slots): fired balls dimmed, the next one on the active frame with a
    /// pulse and a NEXT marker, levels as small digits. Only changed slots are touched (no per-frame work or GC).
    /// </summary>
    public sealed class BallQueueWidget : MonoBehaviour
    {
        enum SlotState
        {
            Hidden,
            Fired,
            Next,
            Waiting,
        }

        [SerializeField] UiTheme theme = null!;
        [Tooltip("Fixed slots (bag cap); extra bag entries are not shown.")]
        [SerializeField] Image[] frames = null!;
        [SerializeField] Image[] icons = null!;
        [SerializeField] TextLabel[] levels = null!;
        [SerializeField] CanvasGroup[] groups = null!;
        [Tooltip("NEXT marker moved onto the next slot.")]
        [SerializeField] RectTransform nextMarker = null!;

        SlotState[] states = null!;
        BallType[] types = null!;
        int[] shownLevels = null!;
        BallCatalog balls = null!;
        Tween? pulse;
        int pulsedSlot = -1;

        #region Public Methods

        public void Initialize(BallCatalog balls)
        {
            this.balls = balls;
            var count = frames.Length;
            states = new SlotState[count];
            types = new BallType[count];
            shownLevels = new int[count];
            for (var i = 0; i < count; i++)
            {
                shownLevels[i] = -1;
                frames[i].gameObject.SetActive(false);
            }

            nextMarker.gameObject.SetActive(false);
        }

        public void Set(IReadOnlyList<BallInstance> bag, int nextIndex)
        {
            var next = -1;
            for (var i = 0; i < frames.Length; i++)
            {
                if (i >= bag.Count)
                {
                    SetState(i, SlotState.Hidden);
                    continue;
                }

                var ball = bag[i];
                if (types[i] != ball.type || shownLevels[i] != ball.level)
                {
                    types[i] = ball.type;
                    shownLevels[i] = ball.level;
                    icons[i].sprite = balls.Get(ball.type).Icon;
                    levels[i].gameObject.SetActive(ball.level > 1);
                    levels[i].SetNumber(ball.level);
                }

                var state = i < nextIndex ? SlotState.Fired : i == nextIndex ? SlotState.Next : SlotState.Waiting;
                if (state == SlotState.Next) next = i;
                SetState(i, state);
            }

            UpdateNext(next);
        }

        #endregion

        #region Helpers

        void SetState(int i, SlotState state)
        {
            if (states[i] == state) return;
            states[i] = state;
            frames[i].gameObject.SetActive(state != SlotState.Hidden);
            if (state == SlotState.Hidden) return;
            frames[i].sprite = state == SlotState.Next ? theme.SlotActive : theme.Slot;
            groups[i].alpha = state == SlotState.Fired ? 0.3f : 1f;
        }

        void UpdateNext(int slot)
        {
            if (slot == pulsedSlot) return;
            pulse?.Kill();
            if (pulsedSlot >= 0) frames[pulsedSlot].transform.localScale = Vector3.one;
            pulsedSlot = slot;
            nextMarker.gameObject.SetActive(slot >= 0);
            if (slot < 0) return;
            var target = frames[slot].rectTransform;
            nextMarker.SetParent(target, false);
            pulse = target.DOScale(theme.ChipPulseScale, theme.ChipPulseDuration).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
        }

        #endregion
    }
}
