#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Nex.BilliardRogue.Simulation;
using Nex.KeyboardNavigation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Three reward cards (left/right + Enter). ChooseAsync fills the cards, waits until the view is on top, reveals
    /// them one by one, waits for a pick, plays the pick animation, pops itself once it is on top again and returns
    /// the chosen index.
    /// Usage: Instantiate → Initialize → PushView (don't await first) → await ChooseAsync.
    /// </summary>
    public sealed class RewardView : RogueView
    {
        [Header("Cards")]
        [Tooltip("Exactly three slots, direct children of cardsGroup.")]
        [SerializeField] RewardCard[] cards = null!;
        [SerializeField] GroupKeyResponder cardsGroup = null!;

        PacingConfig pacing = null!;
        UniTaskCompletionSource<int>? choice;
        int shownCount;
        bool interactable;

        public override ViewIdentifier Identifier => ViewIdentifier.Reward;
        public override TopLevelControlPanel.ControlConfig Controls => TopLevelControlPanel.ControlConfig.None;
        public override string AnalyticsScreenName => "reward";

        #region Life Cycle

        protected override void Awake()
        {
            base.Awake();
            for (var i = 0; i < cards.Length; i++)
            {
                var index = i;
                cards[i].Button.onClick.AddListener(() => HandleChoose(index));
                // Hidden until ChooseAsync reveals them, even when the view is pushed before ChooseAsync fills them.
                cards[i].BodyGroup.alpha = 0f;
            }
        }

        #endregion

        #region Public Methods

        public void Initialize(PacingConfig pacing)
        {
            this.pacing = pacing;
        }

        public async UniTask<int> ChooseAsync(IReadOnlyList<RewardOption> options, BallCatalog balls, RunState run,
            CancellationToken ct = default)
        {
            shownCount = Mathf.Min(options.Count, cards.Length);
            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                card.gameObject.SetActive(i < shownCount);
                if (i >= shownCount) continue;
                card.Show(options[i], balls, run, theme);
                card.BodyGroup.alpha = 0f;
            }

            cardsGroup.SetInitialActiveIndex(shownCount == 3 ? 1 : 0);
            choice = new UniTaskCompletionSource<int>();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken);
            await UniTask.WaitUntil(() => IsActive, cancellationToken: linked.Token);
            await RevealAsync(linked.Token);
            interactable = true;
            var index = await choice.Task.AttachExternalCancellation(linked.Token);
            await PickAsync(index, linked.Token);
            // A pause pushed during the pick animation: pop once we are the top view again (PopSelf pops the top).
            await UniTask.WaitUntil(() => IsActive, cancellationToken: linked.Token);
            await PopSelf();
            return index;
        }

        /// <summary>Motion/CLI hover: moves the keyboard highlight to a card.</summary>
        public void Hover(int index)
        {
            if (!IsActive) return;
            if (index < 0) return;
            if (index >= shownCount) return;
            cardsGroup.NavigateTo(cards[index].Responder);
        }

        /// <summary>Debug/CLI pick (DebugHooks.ChooseReward). False while the cards are not selectable yet.</summary>
        public bool TryChoose(int index)
        {
            if (!IsActive || !interactable || index < 0 || index >= shownCount)
            {
                return false;
            }

            HandleChoose(index);
            return true;
        }

        #endregion

        #region Helpers

        void HandleChoose(int index)
        {
            if (!IsActive) return;
            if (!interactable) return;
            if (index >= shownCount) return;
            interactable = false;
            TrackButton("card", index);
            choice?.TrySetResult(index);
        }

        UniTask RevealAsync(CancellationToken ct) => RevealSequence().ToUniTask(TweenCancelBehaviour.Complete, ct);

        UniTask PickAsync(int index, CancellationToken ct)
        {
            UiTheme.PlaySfx(theme.RewardPickSfx);
            return PickSequence(index).ToUniTask(TweenCancelBehaviour.Complete, ct);
        }

        Sequence RevealSequence()
        {
            var duration = theme.RewardRevealDuration;
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            for (var i = 0; i < shownCount; i++)
            {
                var body = cards[i].Body;
                var group = cards[i].BodyGroup;
                body.localScale = new Vector3(0f, 1f, 1f);
                var at = i * pacing.RewardRevealStagger;
                sequence.InsertCallback(at, () => UiTheme.PlaySfx(theme.RewardRevealSfx));
                sequence.Insert(at, body.DOScaleX(1f, duration).SetEase(Ease.OutBack));
                sequence.Insert(at, group.DOFade(1f, duration * 0.5f));
            }

            return sequence;
        }

        Sequence PickSequence(int index)
        {
            var duration = pacing.RewardPickDuration;
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            for (var i = 0; i < shownCount; i++)
            {
                var card = cards[i];
                if (i == index)
                {
                    sequence.Join(card.Body.DOPunchScale(Vector3.one * theme.RewardPickPunch, duration, 6, 0.6f));
                }
                else
                {
                    sequence.Join(card.BodyGroup.DOFade(0.25f, duration * 0.5f));
                    sequence.Join(card.Body.DOScale(0.9f, duration * 0.5f).SetEase(Ease.InQuad));
                }
            }

            return sequence;
        }

        #endregion
    }
}
