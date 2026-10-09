using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using Nex.KeyboardNavigation;
using Nex.Utils;
using UnityEngine;
#pragma warning disable CS1998

namespace Nex
{
    public abstract class SimpleView : View
    {
        [SerializeField] protected MMFeedbacks entryAnimator;
        [SerializeField] protected MMFeedbacks toBackgroundAnimator;  // Can be null.

        [Header("Keyboard Navigation")]
        [SerializeField] KeyResponder keyResponder;


        public override async UniTask Present(bool animate = true)
        {
            if (entryAnimator != null)
            {
                await entryAnimator.PlayAsUniTask(animate);
            }
        }

        public override async UniTask Dismiss(bool animate = true)
        {
            if (entryAnimator != null)
            {
                await entryAnimator.PlayAsUniTask(animate, reverted: true);
            }
        }

        public override async UniTask EnterBackground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            if (toBackgroundAnimator != null)
            {
                await toBackgroundAnimator.PlayAsUniTask(animate);
            }
        }

        public override async UniTask EnterForeground(ViewIdentifier childViewIdentifier, bool animate = true)
        {
            if (toBackgroundAnimator != null)
            {
                await toBackgroundAnimator.PlayAsUniTask(animate, reverted: true);
            }
        }

        public override KeyResponder KeyResponder => keyResponder;

        public override void OnBackButton()
        {
            // Still waiting...
            if (!IsActive) return;
            PopSelf().Forget();
        }
    }
}
