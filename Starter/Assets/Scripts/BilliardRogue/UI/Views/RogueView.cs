#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Base of the Billiard Rogue views: unscaled DOTween present/dismiss (fade + panel pop), fade-out while another
    /// view covers it, and the shared button feedback (analytics + UI sound). Present/Dismiss never throw, so a view
    /// destroyed mid-transition cannot deadlock the ViewManager.
    /// </summary>
    public abstract class RogueView : SimpleCanvasView
    {
        [Header("Look")]
        [SerializeField] protected UiTheme theme = null!;
        [Tooltip("Everything visible; faded on present/dismiss and while covered.")]
        [SerializeField] protected CanvasGroup content = null!;
        [Tooltip("Main panel popped in on present; optional.")]
        [SerializeField] RectTransform? popTarget;
        [Tooltip("Fade out while another view is on top (full-screen menus); overlays under overlays keep this on too.")]
        [SerializeField] bool hideWhenCovered = true;

        Tween? transition;

        public override float PresentDuration => theme.PresentDuration;
        public override float DismissDuration => theme.DismissDuration;
        public override float BackgroundDuration => hideWhenCovered ? theme.BackgroundDuration : 0f;
        public override float ForegroundDuration => hideWhenCovered ? theme.BackgroundDuration : 0f;

        #region Life Cycle

        protected override void Awake()
        {
            base.Awake();
            content.alpha = 0f;
        }

        #endregion

        #region Transitions

        public override async UniTask Present(bool animate = true)
        {
            Kill();
            content.interactable = true;
            if (popTarget != null) popTarget.localScale = Vector3.one * theme.PresentScaleFrom;
            if (!animate)
            {
                content.alpha = 1f;
                if (popTarget != null) popTarget.localScale = Vector3.one;
                return;
            }

            await Play(Transition(1f, 1f, theme.PresentDuration, Ease.OutQuad, Ease.OutBack));
        }

        public override async UniTask Dismiss(bool animate = true)
        {
            Kill();
            content.interactable = false;
            if (!animate)
            {
                content.alpha = 0f;
                return;
            }

            await Play(Transition(0f, theme.PresentScaleFrom, theme.DismissDuration, Ease.InQuad, Ease.InQuad));
        }

        public override async UniTask EnterBackground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            if (hideWhenCovered) await FadeContent(0f, animate);
            await base.EnterBackground(childViewIdentifier, animate);
        }

        public override async UniTask EnterForeground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            await base.EnterForeground(childViewIdentifier, animate);
            if (hideWhenCovered) await FadeContent(1f, animate);
        }

        /// <summary>Completes when the view is destroyed (after its pop/replace); throws only when ct is cancelled.</summary>
        public async UniTask WaitClosedAsync(CancellationToken ct)
        {
            await UniTask.WhenAny(UniTask.WaitUntilCanceled(destroyCancellationToken), UniTask.WaitUntilCanceled(ct));
            ct.ThrowIfCancellationRequested();
        }

        #endregion

        #region Feedback

        /// <summary>Button pressed: analytics ui_button_click + select sound. Call after the IsActive guard.</summary>
        protected void TrackButton(string button, int index = -1)
        {
            RunAnalytics.UiAction(AnalyticsScreenName, button, index);
            UiTheme.PlaySfx(theme.SelectSfx);
        }

        /// <summary>Back handled by this view (the top-level Back button already played its sound).</summary>
        protected void TrackBack()
        {
            RunAnalytics.UiBack(AnalyticsScreenName);
        }

        #endregion

        #region Helpers

        async UniTask FadeContent(float alpha, bool animate)
        {
            Kill();
            content.interactable = alpha > 0f;
            if (!animate)
            {
                content.alpha = alpha;
                return;
            }

            await Play(content.DOFade(alpha, theme.BackgroundDuration).SetUpdate(true).SetLink(gameObject));
        }

        Sequence Transition(float alpha, float scale, float duration, Ease fadeEase, Ease scaleEase)
        {
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            sequence.Join(content.DOFade(alpha, duration).SetEase(fadeEase));
            if (popTarget != null) sequence.Join(popTarget.DOScale(scale, duration).SetEase(scaleEase));
            return sequence;
        }

        /// <summary>Awaits a tween without ever throwing (a destroyed view completes it).</summary>
        async UniTask Play(Tween tween)
        {
            transition = tween;
            await tween.ToUniTask(TweenCancelBehaviour.Complete, destroyCancellationToken).SuppressCancellationThrow();
        }

        void Kill()
        {
            transition?.Kill(true);
            transition = null;
        }

        #endregion
    }
}
