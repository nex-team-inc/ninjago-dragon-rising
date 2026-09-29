#nullable enable

using System.Collections.Generic;
using DG.Tweening;
using Nex.BilliardRogue.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Ball bag in firing order: a large NEXT slot with the ball's name and level, then the whole turn (bag + bonus
    /// shots) as a grid of fixed slots, fired balls dimmed, the next one on the active frame with a pulse and marker.
    /// The panel grows by grid rows only as far as the turn needs (12 slots = 2 rows of 6). Only changed slots are
    /// touched (no per-frame work or GC).
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

        [Header("Panel")]
        [Tooltip("Resized to the grid rows the turn needs.")]
        [SerializeField] RectTransform panel = null!;
        [Tooltip("Panel height without any grid row (header + NEXT row + bottom padding).")]
        [SerializeField] float heightWithoutGrid = 252f;
        [Tooltip("Height one grid row adds (slot + gap).")]
        [SerializeField] float rowPitch = 68f;
        [SerializeField, Range(1, 12)] int columns = 6;

        [Header("Next ball")]
        [SerializeField] RectTransform heroRoot = null!;
        [SerializeField] Image heroFrame = null!;
        [SerializeField] Image heroIcon = null!;
        [SerializeField] TextLabel heroCaption = null!;
        [SerializeField] TextLabel heroName = null!;
        [SerializeField] TextLabel heroLevel = null!;

        [Header("Grid")]
        [Tooltip("Fixed slots (bag cap); shots beyond them are only counted.")]
        [SerializeField] Image[] frames = null!;
        [SerializeField] Image[] icons = null!;
        [SerializeField] TextLabel[] levels = null!;
        [Tooltip("'+' tag shown on bonus-shot slots (after the bag).")]
        [SerializeField] GameObject[] bonusTags = null!;
        [SerializeField] CanvasGroup[] groups = null!;
        [Tooltip("NEXT marker moved onto the next slot.")]
        [SerializeField] RectTransform nextMarker = null!;

        static readonly BallInstance bonusBall = new() { type = BallType.Basic, level = 1 };

        SlotState[] states = null!;
        BallType[] types = null!;
        int[] shownLevels = null!;
        bool[] shownBonus = null!;
        BallCatalog balls = null!;
        Tween? pulse;
        Tween? heroTween;
        int pulsedSlot = -1;
        int shownRows = -1;
        BallType? heroType;
        int heroShownLevel = -1;
        bool powerArmed;

        #region Public Methods

        public void Initialize(BallCatalog balls)
        {
            this.balls = balls;
            var count = frames.Length;
            states = new SlotState[count];
            types = new BallType[count];
            shownLevels = new int[count];
            shownBonus = new bool[count];
            for (var i = 0; i < count; i++)
            {
                shownLevels[i] = -1;
                frames[i].gameObject.SetActive(false);
                bonusTags[i].SetActive(false);
            }

            nextMarker.gameObject.SetActive(false);
            SetHero(null);
            SetRows(1);
        }

        /// <summary>bag in firing order, nextIndex = next shot (bag.Count.. = bonus shots), extraBalls = bonus Basic shots.</summary>
        public void Set(IReadOnlyList<BallInstance> bag, int nextIndex, int extraBalls)
        {
            var shots = bag.Count + extraBalls;
            var shown = Mathf.Min(shots, frames.Length);
            var next = -1;
            for (var i = 0; i < frames.Length; i++)
            {
                if (i >= shown)
                {
                    SetState(i, SlotState.Hidden);
                    continue;
                }

                var bonus = i >= bag.Count;
                var ball = bonus ? bonusBall : bag[i];
                if (types[i] != ball.type || shownLevels[i] != ball.level || shownBonus[i] != bonus)
                {
                    types[i] = ball.type;
                    shownLevels[i] = ball.level;
                    shownBonus[i] = bonus;
                    icons[i].sprite = balls.Get(ball.type).Icon;
                    levels[i].gameObject.SetActive(ball.level > 1);
                    levels[i].SetNumber(ball.level);
                    bonusTags[i].SetActive(bonus);
                }

                var state = i < nextIndex ? SlotState.Fired : i == nextIndex ? SlotState.Next : SlotState.Waiting;
                if (state == SlotState.Next)
                {
                    next = i;
                }

                SetState(i, state);
            }

            UpdateNext(next);
            SetRows(Mathf.Max(1, (shown + columns - 1) / columns));
            SetHero(nextIndex < shots ? nextIndex < bag.Count ? bag[nextIndex] : bonusBall : null);
        }

        /// <summary>Power pickup armed: the next ball is tinted until it is fired.</summary>
        public void SetPowerArmed(bool armed)
        {
            if (armed == powerArmed) return;
            powerArmed = armed;
            heroFrame.color = armed ? theme.Danger : Color.white;
            if (armed)
            {
                Punch();
            }
        }

        #endregion

        #region Helpers

        void SetHero(BallInstance? ball)
        {
            var type = ball?.type;
            var level = ball?.level ?? -1;
            if (type == heroType && level == heroShownLevel)
            {
                return;
            }

            var changed = heroType != null && type != null;
            heroType = type;
            heroShownLevel = level;
            var has = ball != null;
            heroIcon.gameObject.SetActive(has);
            heroCaption.gameObject.SetActive(has);
            heroName.gameObject.SetActive(has);
            heroLevel.gameObject.SetActive(has);
            heroFrame.sprite = has ? theme.SlotActive : theme.Slot;
            if (ball == null) return;
            heroIcon.sprite = balls.Get(ball.type).Icon;
            heroName.SetKey(LocKeys.Ball.Name(ball.type));
            heroLevel.SetKey(LocKeys.Reward.Level, ball.level);
            if (changed)
            {
                Punch();
            }
        }

        void Punch()
        {
            heroTween?.Kill(true);
            heroTween = heroRoot.DOPunchScale(Vector3.one * (theme.ChipPulseScale - 1f) * 2f, theme.PresentDuration, 6, 0.6f)
                .SetUpdate(true).SetLink(gameObject);
        }

        void SetRows(int rows)
        {
            if (rows == shownRows) return;
            shownRows = rows;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, heightWithoutGrid + rows * rowPitch);
        }

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
            if (pulsedSlot >= 0)
            {
                frames[pulsedSlot].transform.localScale = Vector3.one;
            }

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
