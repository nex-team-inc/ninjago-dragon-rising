using System;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using Nex.Utils;
using UnityEngine;

namespace Nex
{
    public class DismissableControl : MonoBehaviour
    {
        // This is for calling in the widget.
        [SerializeField] MMFeedbacks entryAnimator;

        void Awake()
        {
            // By default, all dismissable controls are in the dismissed state.
            gameObject.SetActive(false);
        }

        public async UniTask Present(float delay, bool animate = true)
        {
            gameObject.SetActive(true);
            if (animate && delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(delay));
            await entryAnimator.PlayAsUniTask(animate);
        }

        public async UniTask Dismiss(bool animate = true)
        {
            await entryAnimator.PlayAsUniTask(animate, reverted: true);
            gameObject.SetActive(false);
        }
    }
}
